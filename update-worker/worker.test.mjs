import test from 'node:test';
import assert from 'node:assert/strict';
import worker, { releaseFeed, boundedJson, latestPageFeed } from './worker.mjs';

const valid = { tag_name: 'v1.5.2', html_url: 'https://github.com/FlavorFiesta/NetAdapterManager/releases/tag/v1.5.2', draft: false, prerelease: false, body: 'notes' };
test('rejects drafts, prereleases, foreign links and invalid versions', () => {
  for (const patch of [{ draft: true }, { prerelease: true }, { html_url: 'https://evil.example/' }, { tag_name: 'v1.2.3-rc1' }])
    assert.throws(() => releaseFeed({ ...valid, ...patch }));
  assert.equal(releaseFeed(valid).tag_name, 'v1.5.2');
});
test('limits streamed response even without content-length', async () => {
  await assert.rejects(boundedJson(new Response('x'.repeat(256 * 1024 + 1))));
});
test('API quota fallback uses only the official latest stable redirect', async () => {
  const original = globalThis.fetch;
  globalThis.caches = { default: { match: async () => undefined, put: async () => {} } };
  try {
    globalThis.fetch = async url => String(url).startsWith('https://api.github.com/')
      ? new Response('limited', { status: 403 })
      : new Response(null, { status: 302, headers: { location: valid.html_url } });
    const result = await worker.fetch(new Request('https://updates.example/latest.json'), {}, { waitUntil() {} });
    assert.equal(result.status, 200); assert.equal((await result.json()).tag_name, valid.tag_name);
    for (const location of ['https://evil.example/tag/v1.5.2', valid.html_url + '?x=1', valid.html_url + '-rc1']) {
      globalThis.fetch = async () => new Response(null, { status: 302, headers: { location } });
      await assert.rejects(latestPageFeed());
    }
  } finally { globalThis.fetch = original; }
});
test('live source is fixed, cache ignores query, cached result avoids fetching', async () => {
  const original = globalThis.fetch;
  const entries = new Map(); const pending = []; let calls = 0;
  globalThis.caches = { default: { match: async r => entries.get(r.url)?.clone(), put: async (r, v) => entries.set(r.url, v) } };
  globalThis.fetch = async (url, options) => {
    calls++; assert.equal(url, 'https://api.github.com/repos/FlavorFiesta/NetAdapterManager/releases/latest');
    assert.equal(options.headers.Authorization, undefined); assert.equal(options.redirect, 'manual');
    return Response.json(valid);
  };
  try {
    const response = await worker.fetch(new Request('https://updates.example/latest.json?url=evil'), {}, { waitUntil: p => pending.push(p) });
    assert.equal(response.status, 200); assert.equal((await response.json()).tag_name, 'v1.5.2');
    await Promise.all(pending);
    const second = await worker.fetch(new Request('https://updates.example/latest.json?different=1'), {}, {});
    assert.equal(second.status, 200); assert.equal(calls, 1);
    const head = await worker.fetch(new Request('https://updates.example/latest.json', { method: 'HEAD' }), {}, {});
    assert.equal(await head.text(), '');
  } finally { globalThis.fetch = original; }
});
test('upstream errors are not cached and no old bundled snapshot is advertised', async () => {
  const original = globalThis.fetch; let puts = 0;
  globalThis.caches = { default: { match: async () => undefined, put: async () => puts++ } };
  try {
    for (const response of [new Response('redirect', { status: 302 }), new Response('limited', { status: 403 }), Response.json({ ...valid, html_url: 'https://evil.example' })]) {
      globalThis.fetch = async () => response;
      const result = await worker.fetch(new Request('https://updates.example/latest.json'), {}, { waitUntil() {} });
      assert.equal(result.status, 503); assert.equal(result.headers.get('Cache-Control'), 'no-store');
    }
    assert.equal(puts, 0);
    assert.equal((await worker.fetch(new Request('https://updates.example/nope'), {}, {})).status, 404);
    assert.equal((await worker.fetch(new Request('https://updates.example/latest.json', { method: 'POST' }), {}, {})).status, 405);
  } finally { globalThis.fetch = original; }
});

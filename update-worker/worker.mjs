const endpoint = 'https://api.github.com/repos/FlavorFiesta/NetAdapterManager/releases/latest';
const releases = 'https://github.com/FlavorFiesta/NetAdapterManager/releases/tag/';
const limit = 256 * 1024;

export function releaseFeed(value) {
  if (!value || value.draft !== false || value.prerelease !== false ||
      typeof value.tag_name !== 'string' || !/^v?\d+\.\d+\.\d+$/.test(value.tag_name) ||
      value.html_url !== releases + value.tag_name) throw new Error('Invalid stable release');
  return { tag_name: value.tag_name, html_url: value.html_url, draft: false, prerelease: false,
    body: typeof value.body === 'string' ? value.body.slice(0, 12000) : '',
    published_at: value.published_at, generated_at: new Date().toISOString() };
}

// GitHub's latest-release page resolves to the latest stable release without API quota.
export async function latestPageFeed(signal) {
  const response = await fetch('https://github.com/FlavorFiesta/NetAdapterManager/releases/latest', {
    headers: { 'User-Agent': 'NetAdapterManager-UpdateFeed' }, redirect: 'manual', signal
  });
  await response.body?.cancel();
  const location = response.headers.get('location');
  if (![301, 302, 303, 307, 308].includes(response.status) || !location?.startsWith(releases))
    throw new Error('Invalid latest release redirect');
  return releaseFeed({ tag_name: location.slice(releases.length), html_url: location,
    draft: false, prerelease: false, body: '' });
}

export async function boundedJson(response) {
  if (Number(response.headers.get('content-length')) > limit || !response.body) throw new Error('Invalid size');
  const reader = response.body.getReader();
  const chunks = []; let size = 0;
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      size += value.length;
      if (size > limit) { await reader.cancel(); throw new Error('Response too large'); }
      chunks.push(value);
    }
  } finally { reader.releaseLock(); }
  const bytes = new Uint8Array(size); let offset = 0;
  for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length; }
  return JSON.parse(new TextDecoder().decode(bytes));
}

export default {
  async fetch(request, env, ctx) {
    const url = new URL(request.url);
    if (request.method !== 'GET' && request.method !== 'HEAD')
      return new Response('Method not allowed', { status: 405, headers: { Allow: 'GET, HEAD' } });
    if (url.pathname !== '/latest.json') return new Response('NetAdapterManager update feed: /latest.json', { status: url.pathname === '/' ? 200 : 404 });
    // The upstream and cache key are fixed; never forward user URLs, cookies or authorization.
    const key = new Request(url.origin + '/latest.json');
    const cache = caches.default;
    let cached;
    try { cached = await cache.match(key); } catch { /* Cache outage must not prevent a live check. */ }
    if (cached) return request.method === 'HEAD' ? new Response(null, cached) : cached;
    try {
      const signal = AbortSignal.timeout(8000);
      const upstream = await fetch(endpoint, {
        headers: { 'User-Agent': 'NetAdapterManager-UpdateFeed', Accept: 'application/vnd.github+json', 'X-GitHub-Api-Version': '2026-03-10' },
        redirect: 'manual', signal
      });
      let feed;
      if (!upstream.ok) {
        await upstream.body?.cancel();
        if (![403, 429].includes(upstream.status)) throw new Error('GitHub unavailable');
        feed = await latestPageFeed(signal);
      } else feed = releaseFeed(await boundedJson(upstream));
      const response = Response.json(feed, { headers: { 'Cache-Control': 'public, max-age=300', 'X-Content-Type-Options': 'nosniff' } });
      ctx.waitUntil(cache.put(key, response.clone()).catch(() => {}));
      return request.method === 'HEAD' ? new Response(null, response) : response;
    } catch (error) {
      console.error('Update feed failed:', error?.name, error?.message);
      return new Response('Update feed temporarily unavailable', { status: 503, headers: { 'Cache-Control': 'no-store', 'Retry-After': '300' } });
    }
  }
};

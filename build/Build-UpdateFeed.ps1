param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/update-feed'),
    [string]$ReleaseJsonPath = ''
)
$ErrorActionPreference = 'Stop'
# Always mirror an actually published stable release, never the local build version.
if ($ReleaseJsonPath) {
    $release = Get-Content -LiteralPath $ReleaseJsonPath -Raw | ConvertFrom-Json
} else {
    $headers = @{ 'User-Agent' = 'NetAdapterManager-UpdateFeed'; 'X-GitHub-Api-Version' = '2026-03-10' }
    if ($env:GH_TOKEN) { $headers.Authorization = 'Bearer ' + $env:GH_TOKEN }
    $release = Invoke-RestMethod 'https://api.github.com/repos/FlavorFiesta/NetAdapterManager/releases/latest' -Headers $headers -TimeoutSec 30
}
if ($release.draft -ne $false -or $release.prerelease -ne $false -or
    $release.tag_name -notmatch '^v?[0-9]+\.[0-9]+\.[0-9]+$' -or
    $release.html_url -cne ('https://github.com/FlavorFiesta/NetAdapterManager/releases/tag/' + $release.tag_name)) {
    throw 'Feed must describe a published stable release of FlavorFiesta/NetAdapterManager.'
}
$body = [string]$release.body
$feed = [ordered]@{
    tag_name = $release.tag_name
    html_url = $release.html_url
    draft = $false
    prerelease = $false
    body = $body.Substring(0, [Math]::Min($body.Length, 12000))
    published_at = $release.published_at
    generated_at = [DateTimeOffset]::UtcNow.ToString('o')
}
$json = $feed | ConvertTo-Json -Depth 5
if ([Text.Encoding]::UTF8.GetByteCount($json) -gt 256KB) { throw 'Feed exceeds client size limit.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'latest.json'), $json, [Text.UTF8Encoding]::new($false))
@'
/latest.json
  Content-Type: application/json; charset=utf-8
  Cache-Control: public, max-age=300, must-revalidate
  X-Content-Type-Options: nosniff
'@ | Set-Content (Join-Path $OutputDirectory '_headers') -Encoding utf8
'<!doctype html><html lang="en"><meta charset="utf-8"><title>NetAdapterManager updates</title><h1>NetAdapterManager updates</h1><p><a href="latest.json">Stable release information</a></p><p><a href="https://github.com/FlavorFiesta/NetAdapterManager/releases">Official downloads</a></p></html>' |
    Set-Content (Join-Path $OutputDirectory 'index.html') -Encoding utf8
# Disable Pages' SPA fallback so a typo in the feed URL returns 404, not HTML 200.
'<!doctype html><title>Not found</title><h1>404</h1>' | Set-Content (Join-Path $OutputDirectory '404.html') -Encoding utf8
Write-Host "Update feed prepared: $OutputDirectory ($($release.tag_name))"

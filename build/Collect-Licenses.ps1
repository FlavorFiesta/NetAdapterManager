param([Parameter(Mandatory)][string]$PublishRoot)
$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Copy-Item -LiteralPath (Join-Path $projectRoot "LICENSE") -Destination $PublishRoot
Copy-Item -LiteralPath (Join-Path $projectRoot "THIRD-PARTY-NOTICES.md") -Destination $PublishRoot
Copy-Item -LiteralPath (Join-Path $projectRoot "licenses") -Destination $PublishRoot -Recurse

# Collect notices from the exact restored packages and bundled runtime versions.
$assets = Get-Content (Join-Path $projectRoot "obj\project.assets.json") -Raw | ConvertFrom-Json
$packageFolders = @($assets.packageFolders.PSObject.Properties.Name)
$packages = @($assets.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq "package" } | ForEach-Object { $_.Value.path })
$runtime = Get-Content (Join-Path $PublishRoot "NetAdapterManager.runtimeconfig.json") -Raw | ConvertFrom-Json
foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
    $packages += "$($framework.name.ToLowerInvariant()).runtime.win-x64/$($framework.version)"
}
foreach ($package in ($packages | Sort-Object -Unique)) {
    $packageRoot = $packageFolders | ForEach-Object { Join-Path $_ $package } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $packageRoot) { throw "Cannot locate restored package license: $package" }
    $legalFiles = @(Get-ChildItem -LiteralPath $packageRoot -File | Where-Object { $_.Name -match '^(LICENSE|THIRD-PARTY-NOTICES)(\.[^.]+)?$' })
    if (-not ($legalFiles | Where-Object { $_.Name -match '^LICENSE' })) { throw "Missing license in $package" }
    $destination = Join-Path $PublishRoot ("licenses\packages\" + $package)
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    foreach ($file in $legalFiles) { Copy-Item -LiteralPath $file.FullName -Destination $destination }
}

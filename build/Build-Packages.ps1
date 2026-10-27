param(
    [string]$Configuration = "Release",
    [string]$OutputRootOverride = ""
)
$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$mainProject = Join-Path $projectRoot "NetAdapterManager.csproj"
$version = ([xml](Get-Content -LiteralPath $mainProject -Raw)).Project.PropertyGroup.Version | Select-Object -First 1
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot "artifacts"))
$outputRoot = if ([string]::IsNullOrWhiteSpace($OutputRootOverride)) {
    Join-Path $artifactsRoot $version
} else { [IO.Path]::GetFullPath($OutputRootOverride) }

# Only descendants of artifacts may be used. Never clear the output directory.
if (-not $outputRoot.StartsWith($artifactsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Output directory must be a child of the project's artifacts directory."
}
$cursor = $outputRoot
while ($cursor -and $cursor.Length -ge $projectRoot.Length) {
    if ((Test-Path -LiteralPath $cursor) -and
        ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Output path must not contain a junction or symbolic link: $cursor"
    }
    $cursor = Split-Path -Parent $cursor
}
$iscc = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1),
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if (-not $iscc) { throw "Install Inno Setup 6.5.0+ (6.x) before packaging." }

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$stagingRoot = [IO.Path]::GetFullPath((Join-Path $artifactsRoot (".staging-" + [guid]::NewGuid().ToString("N"))))
$publishRoot = Join-Path $stagingRoot "publish"
$installerStagingRoot = Join-Path $stagingRoot "installer"
New-Item -ItemType Directory -Path $stagingRoot | Out-Null
New-Item -ItemType Directory -Path $publishRoot, $installerStagingRoot | Out-Null
try {
    & python (Join-Path $PSScriptRoot "generate_icon.py")
    if ($LASTEXITCODE -ne 0) { throw "Icon generation failed. Install build/requirements.txt." }
    & dotnet publish $mainProject -c $Configuration -r win-x64 --self-contained true `
        -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $publishRoot
    if ($LASTEXITCODE -ne 0) { throw "Main program publish failed." }
    if (-not (Test-Path -LiteralPath (Join-Path $publishRoot "NetAdapterManager.exe"))) { throw "Missing executable." }
    & (Join-Path $PSScriptRoot "Collect-Licenses.ps1") -PublishRoot $publishRoot
    $runtimeData = @(Get-ChildItem -LiteralPath $publishRoot -File -Recurse | Where-Object {
        $_.Name -match '^(presets|preset-groups|preset-library|managed-addresses|history)\.json($|\.)' -or $_.Extension -eq ".log"
    })
    if ($runtimeData.Count -gt 0) { throw "Publish output contains runtime user data." }
    $portableName = "NetAdapterManager-v$version-portable-win-x64.zip"
    $installerName = "NetAdapterManager-v$version-Setup-win-x64.exe"
    Compress-Archive -Path (Join-Path $publishRoot "*") -DestinationPath (Join-Path $stagingRoot $portableName) -CompressionLevel Optimal
    & $iscc "/DAppVersion=$version" "/DSourceRoot=$publishRoot" "/DOutputRoot=$installerStagingRoot" (Join-Path $projectRoot "Installer\NetAdapterManager.iss")
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed." }
    $packages = @(
        @{ Source = (Join-Path $stagingRoot $portableName); Name = $portableName },
        @{ Source = (Join-Path $installerStagingRoot $installerName); Name = $installerName }
    )
    # Replace only these two named deliverables, after both builds succeed.
    foreach ($package in $packages) {
        if (-not (Test-Path -LiteralPath $package.Source)) { throw "Missing package: $($package.Name)" }
        $destination = Join-Path $outputRoot $package.Name
        if ((Test-Path -LiteralPath $destination) -and
            ((Get-Item -LiteralPath $destination -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Package destination must not be a symbolic link."
        }
    }
    foreach ($package in $packages) {
        Copy-Item -LiteralPath $package.Source -Destination (Join-Path $outputRoot $package.Name) -Force
    }
}
finally {
    # The only recursive deletion is our newly created, uniquely named staging.
    $resolvedStaging = [IO.Path]::GetFullPath($stagingRoot)
    if ((Split-Path -Parent $resolvedStaging) -ne $artifactsRoot -or
        (Split-Path -Leaf $resolvedStaging) -notmatch '^\.staging-[0-9a-f]{32}$') {
        throw "Refusing to clean an unexpected staging path."
    }
    if (Test-Path -LiteralPath $resolvedStaging) {
        if ((Get-Item -LiteralPath $resolvedStaging -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Refusing to recursively delete a staging link."
        }
        Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
    }
}
Write-Host "Portable package: $(Join-Path $outputRoot $portableName)"
Write-Host "Installer package: $(Join-Path $outputRoot $installerName)"

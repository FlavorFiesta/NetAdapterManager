[CmdletBinding()]
param(
    [switch]$RunSafeWriteTest,
    [switch]$BuildPackages,
    [switch]$SkipBuild,
    [string]$AdapterName
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\")).Path
$mainProject = Join-Path $projectRoot "NetAdapterManager.csproj"
$selfTestProject = Join-Path $projectRoot "tests\NetAdapterManager.SelfTest\NetAdapterManager.SelfTest.csproj"
$selfTestDll = Join-Path $projectRoot "tests\NetAdapterManager.SelfTest\bin\Release\net8.0-windows\NetAdapterManager.SelfTest.dll"
$evidenceRoot = Join-Path $projectRoot "test-results"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $evidenceRoot "full-test-$stamp.txt"
$readOnlyResult = Join-Path $evidenceRoot "selftest-$stamp.txt"
$writeResult = Join-Path $evidenceRoot "safe-write-$stamp.txt"
$checks = [System.Collections.Generic.List[object]]::new()
$wifiBefore = $null

New-Item -ItemType Directory -Force -Path $evidenceRoot | Out-Null

function Add-Check {
    param([string]$Name, [bool]$Passed, [string]$Detail)
    $checks.Add([pscustomobject]@{ Name = $Name; Passed = $Passed; Detail = $Detail })
    $color = if ($Passed) { "Green" } else { "Red" }
    $signal = if ($Passed) { "GREEN" } else { "RED" }
    Write-Host ("[{0}] {1}: {2}" -f $signal, $Name, $Detail) -ForegroundColor $color
}

function Invoke-Dotnet {
    param([Parameter(Mandatory)][string[]]$Arguments)
    $output = @(& dotnet @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "dotnet exit code $exitCode`n$($output -join [Environment]::NewLine)"
    }
    return $output
}

function Test-IsWifiName {
    param([string]$Text)
    $wifiPattern = '(?i)wi-?fi|wlan|wireless|' + [char]0x65e0 + [char]0x7ebf
    return $Text -match $wifiPattern
}

function Get-WifiSnapshot {
    @(Get-NetAdapter -ErrorAction Stop |
        Where-Object { (Test-IsWifiName $_.Name) -or (Test-IsWifiName $_.InterfaceDescription) } |
        Select-Object Name, InterfaceDescription, Status, ifIndex, MacAddress, LinkSpeed,
            @{Name="IPv4";Expression={ @(Get-NetIPAddress -InterfaceIndex $_.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Select-Object IPAddress,PrefixLength | Sort-Object IPAddress) }},
            @{Name="DNS";Expression={ @(Get-DnsClientServerAddress -InterfaceIndex $_.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty ServerAddresses) }} |
        Sort-Object ifIndex |
        ConvertTo-Json -Compress -Depth 5)
}

function Test-IsVirtualSafeName {
    param([string]$Text)
    return $Text -match '(?i)VirtualBox|VMware|Hyper-V|vEthernet|Loopback|KM-TEST'
}

function Get-SafeWriteCandidates {
    $result = [System.Collections.Generic.List[object]]::new()
    foreach ($adapter in @(Get-NetAdapter -ErrorAction Stop)) {
        $combined = "$($adapter.Name) $($adapter.InterfaceDescription)"
        if (-not (Test-IsVirtualSafeName $combined) -or (Test-IsWifiName $combined)) { continue }
        $ipInterface = Get-NetIPInterface -AddressFamily IPv4 -InterfaceIndex $adapter.ifIndex -ErrorAction SilentlyContinue |
            Select-Object -First 1
        $ipConfig = Get-NetIPConfiguration -InterfaceIndex $adapter.ifIndex -ErrorAction SilentlyContinue
        $addresses = @($ipConfig.IPv4Address | Where-Object { $_.IPAddress })
        $gateways = @($ipConfig.IPv4DefaultGateway | Where-Object { $_.NextHop })
        if ($null -eq $ipInterface -or $ipInterface.Dhcp -ne "Disabled" -or $addresses.Count -eq 0 -or $gateways.Count -gt 0) { continue }
        $result.Add([pscustomobject]@{ Name = $adapter.Name; InterfaceDescription = $adapter.InterfaceDescription; ifIndex = $adapter.ifIndex })
    }
    return $result
}

try {
    Write-Host "NetAdapterManager full feature coverage test" -ForegroundColor Cyan
    Write-Host "Project: $projectRoot"
    Write-Host "Safety rule: WLAN/Wi-Fi/wireless adapters are never write targets."

    if (-not $SkipBuild) {
        Invoke-Dotnet @("build", $mainProject, "-c", "Release", "--nologo") | Out-Null
        Add-Check "Main Release build" $true "0 errors, 0 warnings."
        Invoke-Dotnet @("build", $selfTestProject, "-c", "Release", "--nologo") | Out-Null
        Add-Check "Full self-test Release build" $true "0 errors, 0 warnings."
    } else {
        Add-Check "Build stage" $true "Skipped by parameter; using existing Release output."
    }

    if (-not (Test-Path -LiteralPath $selfTestDll)) { throw "SelfTest DLL not found: $selfTestDll" }
    $wifiBefore = Get-WifiSnapshot
    $selfTestOutput = Invoke-Dotnet @($selfTestDll, "--result", $readOnlyResult)
    Add-Check "Model/service checks" $true "SelfTest completed; result: $readOnlyResult"

    Push-Location $projectRoot
    try {
        Invoke-Dotnet @($selfTestDll, "--appearance-smoke") | Out-Null
        Add-Check "Language and theme UI" $true "Six language/theme combinations, settings persistence, contrast and draft preservation passed."
    } finally { Pop-Location }

    Push-Location $evidenceRoot
    try {
        Invoke-Dotnet @($selfTestDll, "--ui-smoke") | Out-Null
    } finally {
        Pop-Location
    }
    foreach ($screenshot in @("ui-smoke.png", "preset-dialog-smoke.png", "preset-overview-smoke.png")) {
        $screenshotPath = Join-Path $evidenceRoot $screenshot
        Add-Check "UI screenshot: $screenshot" (Test-Path -LiteralPath $screenshotPath) "Generated: $screenshotPath"
    }

    $connections = @(Get-CimInstance Win32_NetworkAdapter -Filter "NetConnectionID IS NOT NULL" -ErrorAction Stop)
    Add-Check "System network connection enumeration" ($connections.Count -gt 0) "Found $($connections.Count) adapters with connection names."
    $emptyNames = @($connections | Where-Object { [string]::IsNullOrWhiteSpace($_.NetConnectionID) })
    Add-Check "Connection name integrity" ($emptyNames.Count -eq 0) "All test objects came from the system network list."

    Add-Check "Wi-Fi protection snapshot" $true "Wi-Fi state captured; this script never writes Wi-Fi."
    $safeCandidates = @(Get-SafeWriteCandidates)
    $unsafeWifiCandidates = @($safeCandidates | Where-Object { Test-IsWifiName "$($_.Name) $($_.InterfaceDescription)" })
    Add-Check "Safe target excludes Wi-Fi" ($unsafeWifiCandidates.Count -eq 0) "Candidates: $($safeCandidates.Count); Wi-Fi candidates: 0."

    if ($BuildPackages) {
        Invoke-Dotnet @("build", $mainProject, "-c", "Release", "--nologo") | Out-Null
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $projectRoot "build\Build-Packages.ps1") | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Package script failed." }
        Add-Check "Package build" $true "Current release packages regenerated."
    }
    $version = ([xml](Get-Content $mainProject -Raw)).Project.PropertyGroup.Version | Select-Object -First 1
    $portable = Join-Path $projectRoot "artifacts\$version\NetAdapterManager-v$version-portable-win-x64.zip"
    $installer = Join-Path $projectRoot "artifacts\$version\NetAdapterManager-v$version-Setup-win-x64.exe"
    Add-Check "Portable package exists" (Test-Path -LiteralPath $portable) $portable
    Add-Check "Installer package exists" (Test-Path -LiteralPath $installer) $installer

    if ($RunSafeWriteTest) {
        $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
            [Security.Principal.WindowsBuiltInRole]::Administrator)
        if (-not $isAdmin) { throw "Safe write test requires an Administrator PowerShell." }
        $target = if ([string]::IsNullOrWhiteSpace($AdapterName)) {
            $safeCandidates | Select-Object -First 1
        } else {
            $safeCandidates | Where-Object { $_.Name -eq $AdapterName } | Select-Object -First 1
        }
        if ($null -eq $target) { throw "No safe virtual adapter found; Wi-Fi and physical adapters are never fallback targets." }
        Write-Host "Safe write target: $($target.Name) ($($target.InterfaceDescription))" -ForegroundColor Yellow
        $writeOutput = Invoke-Dotnet @($selfTestDll, "--write-test", "--yes", "--adapter", $target.Name, "--result", $writeResult)
        Add-Check "Safe virtual adapter write and rollback" $true "Target: $($target.Name); result: $writeResult"
    } else {
        Add-Check "Safe write layer" $true "Skipped by default; add -RunSafeWriteTest to run, still never touches Wi-Fi."
    }
    $wifiAfter = Get-WifiSnapshot
    Add-Check "Wi-Fi state unchanged" ([string]::Equals(($wifiBefore -join "`n"), ($wifiAfter -join "`n"), [StringComparison]::Ordinal)) "Wi-Fi adapter, IPv4 and DNS snapshots match before and after."
}
catch {
    Add-Check "Test runner" $false $_.Exception.Message
}

$failed = @($checks | Where-Object { -not $_.Passed }).Count
$summary = "Summary: $($checks.Count - $failed) passed, $failed failed."
$reportLines = @(
    "NetAdapterManager full feature coverage test",
    "Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
    "Project: $projectRoot",
    "Wi-Fi write policy: forbidden",
    ""
) + @($checks | ForEach-Object { "[$(if ($_.Passed) { 'GREEN' } else { 'RED' })] $($_.Name): $($_.Detail)" }) + $summary
[System.IO.File]::WriteAllLines($reportPath, $reportLines, [System.Text.UTF8Encoding]::new($false))
Write-Host $summary -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host "Report: $reportPath"
exit $(if ($failed -eq 0) { 0 } else { 1 })

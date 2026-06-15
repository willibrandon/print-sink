#requires -Version 7
# Decisive test via the built-in `runas /trustlevel` (SAFER NormalUser) tool, which
# correctly sets up profile/env/desktop for a genuine medium-integrity process.
# If the capability refresh SUCCEEDS at medium where it TIMES OUT at high, the
# high-integrity (built-in-admin) context is the proven root cause.
param(
    [string] $MsixRunId = '27530511847',
    [string] $Endpoint = 'Pdf',
    [string] $OutDir = 'probe-out'
)
$ErrorActionPreference = 'Continue'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$report = [ordered]@{}
$pub = 'C:\Users\Public'

# --- Install prebuilt ARM64 package ---
$pkgDir = Join-Path $OutDir 'pkg'; New-Item -ItemType Directory -Force -Path $pkgDir | Out-Null
& gh run download $MsixRunId -n msix-ARM64 -D $pkgDir 2>&1 | Out-String | Write-Host
$msix = Get-ChildItem $pkgDir -Recurse -Filter '*.msix' | Select-Object -First 1
$cer = Get-ChildItem $pkgDir -Recurse -Filter '*.cer' | Select-Object -First 1
Import-Certificate -FilePath $cer.FullName -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
Import-Certificate -FilePath $cer.FullName -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
Add-AppxPackage -Path $msix.FullName
$pkg = Get-AppxPackage -Name 'PrintSink'; $fam = $pkg.PackageFamilyName
$diagFile = Join-Path $env:LOCALAPPDATA "Packages\$fam\LocalState\Settings\diagnostic-events.json"
$alias = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\printsink-app.exe'
foreach ($svc in 'Spooler', 'BrokerInfrastructure', 'PrintScanBrokerService', 'PrintDeviceConfigurationService', 'PrintNotify') { try { Start-Service $svc -ErrorAction Stop } catch {} }
Get-Service -Name 'PrintWorkflowUserSvc_*' -ErrorAction SilentlyContinue | ForEach-Object { try { Start-Service $_.Name } catch {} }
& $alias --disable-job-ui 2>&1 | Out-String | Write-Host
& $alias --install-virtual-printers 2>&1 | Out-String | Write-Host
Start-Sleep -Seconds 3

function Invoke-ViaRunasTrustLevel([string]$wrapperBody, [string]$resultPath) {
    Remove-Item $resultPath -ErrorAction SilentlyContinue
    $wrapper = Join-Path $pub 'psw.ps1'
    Set-Content -LiteralPath $wrapper -Value $wrapperBody -Encoding utf8
    # runas /trustlevel:0x20000 == SAFER_LEVELID_NORMALUSER (medium integrity, standard user)
    $runasOut = & cmd.exe /c 'runas /trustlevel:0x20000 "pwsh -NoProfile -ExecutionPolicy Bypass -File C:\Users\Public\psw.ps1"' 2>&1 | Out-String
    $deadline = (Get-Date).AddSeconds(200)
    while (-not (Test-Path $resultPath) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 3 }
    return [ordered]@{
        runasOutput = $runasOut.Trim()
        result = if (Test-Path $resultPath) { Get-Content $resultPath -Raw | ConvertFrom-Json } else { '<no result file>' }
    }
}

# --- CONTROL: prove runas /trustlevel yields a working medium-integrity process ---
$ctlResult = Join-Path $pub 'ctl-result.json'
$report.control = Invoke-ViaRunasTrustLevel @"
`$ErrorActionPreference='Continue'
[ordered]@{ integrity=((whoami /groups | Select-String 'Mandatory Level') | Out-String).Trim(); whoami=(whoami) } |
  ConvertTo-Json | Set-Content -LiteralPath '$ctlResult' -Encoding utf8
"@ $ctlResult

# --- Refresh at medium integrity ---
$refResult = Join-Path $pub 'ref-result.json'
Remove-Item $diagFile -ErrorAction SilentlyContinue
$report.mediumRefresh = Invoke-ViaRunasTrustLevel @"
`$ErrorActionPreference='Continue'
`$t0=Get-Date
`$p=Start-Process -FilePath '$alias' -ArgumentList @('--refresh-capabilities','--endpoint','$Endpoint') -PassThru -Wait
[ordered]@{
  integrity=((whoami /groups | Select-String 'Mandatory Level') | Out-String).Trim()
  exitCode=`$p.ExitCode
  durationSec=[math]::Round(((Get-Date)-`$t0).TotalSeconds,1)
  diagExists=(Test-Path '$diagFile')
  diag=if(Test-Path '$diagFile'){Get-Content '$diagFile' -Raw}else{'<none>'}
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath '$refResult' -Encoding utf8
"@ $refResult

try { & $alias --remove-virtual-printers 2>&1 | Out-Null } catch {}
try { Remove-AppxPackage -Package $pkg.PackageFullName -ErrorAction SilentlyContinue } catch {}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutDir 'medium-report.json') -Encoding utf8
Write-Host ($report | ConvertTo-Json -Depth 8)

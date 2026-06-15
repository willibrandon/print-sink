#requires -Version 7
# Decisive in-job-fix test: run the capability refresh as a genuinely separate,
# freshly-created NORMAL administrator (RID != 500) with a clean token, at both
# High and Limited(medium) run levels, to determine whether a normal-user context
# makes PSA extension activation work on this runner (and whether integrity or the
# built-in-admin RID-500 is the differentiator).
param(
    [string] $MsixRunId = '27530511847',
    [string] $Endpoint = 'Pdf',
    [string] $OutDir = 'probe-out'
)
$ErrorActionPreference = 'Continue'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$report = [ordered]@{}
$pub = 'C:\Users\Public'

# --- Download + trust the prebuilt signed ARM64 MSIX (machine-wide cert trust) ---
$pkgDir = Join-Path $OutDir 'pkg'; New-Item -ItemType Directory -Force -Path $pkgDir | Out-Null
& gh run download $MsixRunId -n msix-ARM64 -D $pkgDir 2>&1 | Out-String | Write-Host
$msix = (Get-ChildItem $pkgDir -Recurse -Filter '*.msix' | Select-Object -First 1).FullName
$cer = (Get-ChildItem $pkgDir -Recurse -Filter '*.cer' | Select-Object -First 1).FullName
Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
foreach ($svc in 'Spooler', 'BrokerInfrastructure', 'PrintScanBrokerService', 'PrintDeviceConfigurationService', 'PrintNotify') { try { Start-Service $svc -ErrorAction Stop } catch {} }

# --- Create a normal admin user ---
$user = 'psinkci'
$pw = 'Pr1ntS!nk-' + ([guid]::NewGuid().ToString('N').Substring(0, 10)) + 'Zz9'
$report.user = $user
$fullUser = "$env:COMPUTERNAME\$user"
$report.fullUser = $fullUser
$report.netUserAdd = (net user $user $pw /add /y 2>&1 | Out-String).Trim()
$report.netUserAddExit = $LASTEXITCODE
$report.netGroupAdd = (net localgroup Administrators $user /add 2>&1 | Out-String).Trim()
Start-Sleep -Seconds 3
$report.localUserExists = $null -ne (Get-LocalUser -Name $user -ErrorAction SilentlyContinue)
$report.resolvedSid = try { (New-Object System.Security.Principal.NTAccount($env:COMPUTERNAME, $user)).Translate([System.Security.Principal.SecurityIdentifier]).Value } catch { "err: $($_.Exception.Message)" }

function Run-AsUser([string]$runLevel, [string]$wrapperBody, [string]$resultPath) {
    Remove-Item $resultPath -ErrorAction SilentlyContinue
    $wrapper = Join-Path $pub "nu-$runLevel.ps1"
    Set-Content -LiteralPath $wrapper -Value $wrapperBody -Encoding utf8
    $taskName = "PSinkNu_$runLevel"
    $tr = "pwsh.exe -NoProfile -ExecutionPolicy Bypass -File `"$wrapper`""
    $createOut = (schtasks.exe /create /tn $taskName /tr $tr /sc ONCE /st 00:00 /ru $fullUser /rp $pw /rl $runLevel /f 2>&1 | Out-String).Trim()
    $createExit = $LASTEXITCODE
    if ($createExit -ne 0) { return [ordered]@{ error = "schtasks create exit $createExit"; output = $createOut } }
    schtasks.exe /run /tn $taskName 2>&1 | Out-Null
    $deadline = (Get-Date).AddSeconds(260)
    do { Start-Sleep -Seconds 3 } while (-not (Test-Path $resultPath) -and (Get-Date) -lt $deadline)
    $queryOut = (schtasks.exe /query /tn $taskName /fo LIST /v 2>&1 | Out-String)
    schtasks.exe /delete /tn $taskName /f 2>&1 | Out-Null
    if (Test-Path $resultPath) { return (Get-Content $resultPath -Raw | ConvertFrom-Json) }
    $lastResult = ($queryOut -split "`n" | Select-String 'Last Result' | Out-String).Trim()
    return [ordered]@{ error = 'no result file'; lastResult = $lastResult }
}

# Wrapper: register the package for THIS user, install queues, refresh, report.
function New-WrapperBody([string]$resultPath) {
    return @"
`$ErrorActionPreference='Continue'
`$integrity=((whoami /groups | Select-String 'Mandatory Level') | Out-String).Trim()
`$sid=([Security.Principal.WindowsIdentity]::GetCurrent()).User.Value
try { Add-AppxPackage -Path '$msix' -ErrorAction Stop } catch { }
`$pkg=Get-AppxPackage -Name PrintSink
`$alias=Join-Path `$env:LOCALAPPDATA 'Microsoft\WindowsApps\printsink-app.exe'
`$diag=Join-Path `$env:LOCALAPPDATA "Packages\`$(`$pkg.PackageFamilyName)\LocalState\Settings\diagnostic-events.json"
& `$alias --disable-job-ui | Out-Null
& `$alias --install-virtual-printers | Out-Null
Start-Sleep -Seconds 3
Remove-Item `$diag -ErrorAction SilentlyContinue
`$t0=Get-Date
`$p=Start-Process -FilePath `$alias -ArgumentList @('--refresh-capabilities','--endpoint','$Endpoint') -PassThru -Wait
`$res=[ordered]@{
  integrity=`$integrity; sid=`$sid; isRid500=`$sid.EndsWith('-500'); package=`$pkg.PackageFullName
  refreshExit=`$p.ExitCode; refreshSec=[math]::Round(((Get-Date)-`$t0).TotalSeconds,1)
  diagExists=(Test-Path `$diag); diag=if(Test-Path `$diag){Get-Content `$diag -Raw}else{'<none>'}
}
`$res | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath '$resultPath' -Encoding utf8
"@
}

$hiResult = Join-Path $pub 'nu-hi.json'
$report.normalAdmin_High = Run-AsUser 'Highest' (New-WrapperBody $hiResult) $hiResult
$loResult = Join-Path $pub 'nu-lo.json'
$report.normalAdmin_Limited = Run-AsUser 'Limited' (New-WrapperBody $loResult) $loResult

# --- Cleanup ---
try { net user $user /delete 2>&1 | Out-Null } catch {}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutDir 'newuser-report.json') -Encoding utf8
Write-Host ($report | ConvertTo-Json -Depth 8)

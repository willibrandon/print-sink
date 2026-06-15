#requires -Version 7
# Decisive experiment: does the PSA capability refresh fail because the trigger
# runs ELEVATED / HIGH integrity (built-in admin, UAC off on the hosted runner)?
# Runs the refresh (a) elevated/high integrity and (b) at Limited/medium integrity
# via a scheduled task in the interactive session, and compares.
param(
    [string] $MsixRunId = '27530511847',
    [string] $Endpoint = 'Pdf',
    [string] $OutDir = 'probe-out'
)
$ErrorActionPreference = 'Continue'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$report = [ordered]@{}

# --- UAC / token state ---
function Read-Val([string]$p, [string]$n) { try { (Get-ItemProperty -LiteralPath $p -Name $n -ErrorAction Stop).$n } catch { '<missing>' } }
$report.uac_EnableLUA = Read-Val 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' 'EnableLUA'
$report.uac_FilterAdministratorToken = Read-Val 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' 'FilterAdministratorToken'
$report.uac_ConsentPromptBehaviorAdmin = Read-Val 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' 'ConsentPromptBehaviorAdmin'
$report.harnessIntegrity = ((cmd /c "whoami /groups" 2>&1 | Select-String 'Mandatory Level') | Out-String).Trim()

# --- Install prebuilt ARM64 package ---
$pkgDir = Join-Path $OutDir 'pkg'; New-Item -ItemType Directory -Force -Path $pkgDir | Out-Null
& gh run download $MsixRunId -n msix-ARM64 -D $pkgDir 2>&1 | Out-String | Write-Host
$msix = Get-ChildItem $pkgDir -Recurse -Filter '*.msix' | Select-Object -First 1
$cer = Get-ChildItem $pkgDir -Recurse -Filter '*.cer' | Select-Object -First 1
Import-Certificate -FilePath $cer.FullName -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
Import-Certificate -FilePath $cer.FullName -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
Add-AppxPackage -Path $msix.FullName
$pkg = Get-AppxPackage -Name 'PrintSink'
$report.package = $pkg.PackageFullName
$fam = $pkg.PackageFamilyName
$diagFile = Join-Path $env:LOCALAPPDATA "Packages\$fam\LocalState\Settings\diagnostic-events.json"
$alias = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\printsink-app.exe'

foreach ($svc in 'Spooler', 'BrokerInfrastructure', 'PrintScanBrokerService', 'PrintDeviceConfigurationService', 'PrintNotify') { try { Start-Service $svc -ErrorAction Stop } catch {} }
Get-Service -Name 'PrintWorkflowUserSvc_*' -ErrorAction SilentlyContinue | ForEach-Object { try { Start-Service $_.Name } catch {} }

& $alias --disable-job-ui            2>&1 | Out-String | Write-Host
& $alias --install-virtual-printers  2>&1 | Out-String | Write-Host
Start-Sleep -Seconds 3

# --- Identity / RID-500 (codex H2) ---
$wid = [Security.Principal.WindowsIdentity]::GetCurrent()
$report.identity = [ordered]@{ name = $wid.Name; sid = $wid.User.Value; isRid500 = $wid.User.Value.EndsWith('-500') }

# --- PSA AUMID association on the queue's PnP device (codex H1: missing dispatch target) ---
$expectedAumid = "$fam!App"
$report.expectedAumid = $expectedAumid
$report.psaAssociation = @(
    Get-PnpDevice -Class Printer -ErrorAction SilentlyContinue |
        Where-Object { $_.FriendlyName -like 'PrintSink*' } |
        ForEach-Object {
            $psa = $null
            try { $psa = (Get-PnpDeviceProperty -InstanceId $_.InstanceId -KeyName '{A925764B-88E0-426D-AFC5-B39768BE59EB} 1' -ErrorAction Stop).Data } catch { $psa = "err: $($_.Exception.Message)" }
            [ordered]@{ friendly = $_.FriendlyName; instance = $_.InstanceId; psaAumid = $psa; matches = ($psa -eq $expectedAumid) }
        }
)

# --- WinRT activatable class registration (codex H3) ---
$report.activatableClasses = @(
    foreach ($c in 'PrintSink.Tasks.PrintSupportExtensionBackgroundTask', 'PrintSink.Tasks.VirtualPrinterBackgroundTask') {
        $found = $false; $where = $null
        foreach ($root in "HKLM:\Software\Classes\ActivatableClasses\Package\$($pkg.PackageFullName)\ActivatableClassId\$c",
            "HKCU:\Software\Classes\ActivatableClasses\Package\$($pkg.PackageFullName)\ActivatableClassId\$c") {
            if (Test-Path $root) { $found = $true; $where = $root }
        }
        [ordered]@{ class = $c; registered = $found; key = $where }
    }
)

function Invoke-RefreshElevated {
    Remove-Item -LiteralPath $diagFile -ErrorAction SilentlyContinue
    $t0 = Get-Date
    $p = Start-Process -FilePath $alias -ArgumentList @('--refresh-capabilities', '--endpoint', $Endpoint) -PassThru -Wait
    return [ordered]@{
        mode = 'elevated-high-integrity'
        exitCode = $p.ExitCode
        durationSec = [math]::Round(((Get-Date) - $t0).TotalSeconds, 1)
        diagExists = Test-Path $diagFile
        diag = if (Test-Path $diagFile) { Get-Content $diagFile -Raw } else { '<none>' }
    }
}

function Invoke-RefreshMediumViaTask {
    $resultFile = Join-Path $env:TEMP 'psink-medium-result.json'
    Remove-Item -LiteralPath $resultFile, $diagFile -ErrorAction SilentlyContinue
    $wrapper = Join-Path $env:TEMP 'psink-medium-wrapper.ps1'
    @"
`$ErrorActionPreference='Continue'
`$t0=Get-Date
`$p=Start-Process -FilePath '$alias' -ArgumentList @('--refresh-capabilities','--endpoint','$Endpoint') -PassThru -Wait
[ordered]@{
  mode='medium-integrity-scheduledtask'
  exitCode=`$p.ExitCode
  durationSec=[math]::Round(((Get-Date)-`$t0).TotalSeconds,1)
  integrity=((cmd /c "whoami /groups" 2>&1 | Select-String 'Mandatory Level') | Out-String).Trim()
  diagExists=(Test-Path '$diagFile')
  diag=if(Test-Path '$diagFile'){Get-Content '$diagFile' -Raw}else{'<none>'}
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath '$resultFile' -Encoding utf8
"@ | Set-Content -LiteralPath $wrapper -Encoding utf8

    $action = New-ScheduledTaskAction -Execute 'pwsh.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$wrapper`""
    $principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Limited
    try {
        Register-ScheduledTask -TaskName 'PSinkMediumRefresh' -Action $action -Principal $principal -Force -ErrorAction Stop | Out-Null
        Start-ScheduledTask -TaskName 'PSinkMediumRefresh'
        $deadline = (Get-Date).AddSeconds(240)
        do {
            Start-Sleep -Seconds 3
            $state = (Get-ScheduledTask -TaskName 'PSinkMediumRefresh').State
        } while ($state -ne 'Ready' -and (Get-Date) -lt $deadline)
        Start-Sleep -Seconds 2
        if (Test-Path $resultFile) { return (Get-Content $resultFile -Raw | ConvertFrom-Json) }
        return [ordered]@{ mode = 'medium-integrity-scheduledtask'; error = "no result file; final task state=$state" }
    }
    catch { return [ordered]@{ mode = 'medium-integrity-scheduledtask'; error = $_.Exception.Message } }
    finally { Unregister-ScheduledTask -TaskName 'PSinkMediumRefresh' -Confirm:$false -ErrorAction SilentlyContinue }
}

$report.elevatedRefresh = Invoke-RefreshElevated
$report.mediumRefresh = Invoke-RefreshMediumViaTask

try { & $alias --remove-virtual-printers 2>&1 | Out-Null } catch {}
try { Remove-AppxPackage -Package $pkg.PackageFullName -ErrorAction SilentlyContinue } catch {}

$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutDir 'integrity-report.json') -Encoding utf8
Write-Host ($report | ConvertTo-Json -Depth 8)

#requires -Version 7
# Run the capability refresh as a separate normal user via CreateProcessWithLogonW
# (the API behind runas) which performs a real INTERACTIVE logon -> a valid logon
# session where WinRT/AppX works (unlike a scheduled-task batch logon, which fails
# with 0x80070520). This determines whether a normal-user interactive context makes
# PSA extension activation work on this runner.
param(
    [string] $MsixRunId = '27530511847',
    [string] $Endpoint = 'Pdf',
    [string] $OutDir = 'probe-out'
)
$ErrorActionPreference = 'Continue'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$report = [ordered]@{}
$pub = 'C:\Users\Public'

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace PLogon {
  [StructLayout(LayoutKind.Sequential)] public struct SI { public uint cb; public string r1; public string desk; public string title; public uint x,y,xs,ys,xc,yc,fill,flags; public ushort show,r2; public IntPtr r3,i,o,e; }
  [StructLayout(LayoutKind.Sequential)] public struct PI { public IntPtr hProcess,hThread; public uint pid,tid; }
  public static class Native {
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    public static extern bool CreateProcessWithLogonW(string user, string domain, string password, uint logonFlags, string app, string cmd, uint creationFlags, IntPtr env, string dir, ref SI si, out PI pi);
    public static int Start(string user, string domain, string password, string app, string cmd, string dir) {
      SI si = new SI(); si.cb=(uint)Marshal.SizeOf(typeof(SI)); si.desk="winsta0\\default"; PI pi;
      // LOGON_WITH_PROFILE = 1 ; CREATE_UNICODE_ENVIRONMENT = 0x400
      if(!CreateProcessWithLogonW(user, domain, password, 1, app, cmd, 0x400, IntPtr.Zero, dir, ref si, out pi)) throw new Exception("CreateProcessWithLogonW "+Marshal.GetLastWin32Error());
      return (int)pi.pid;
    }
  }
}
'@

# --- Download + trust the signed ARM64 MSIX ---
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
net user $user $pw /add /y 2>&1 | Out-String | Write-Host
net localgroup Administrators $user /add 2>&1 | Out-String | Write-Host
Start-Sleep -Seconds 3
$report.localUserExists = $null -ne (Get-LocalUser -Name $user -ErrorAction SilentlyContinue)

$resultFile = Join-Path $pub 'cpl-result.json'
$wrapper = Join-Path $pub 'cpl-wrapper.ps1'
Remove-Item $resultFile -ErrorAction SilentlyContinue
$progressFile = Join-Path $pub 'cpl-progress.txt'
Remove-Item $progressFile -ErrorAction SilentlyContinue
@"
`$ErrorActionPreference='Continue'
function Mark(`$m){ Add-Content -LiteralPath '$progressFile' -Value "`$([DateTime]::Now.ToString('HH:mm:ss')) `$m" }
Mark 'wrapper started'
`$headless=Join-Path `$env:TEMP 'PrintSink.App.headless.log'
`$integrity=((whoami /groups | Select-String 'Mandatory Level') | Out-String).Trim()
`$sid=([Security.Principal.WindowsIdentity]::GetCurrent()).User.Value
Mark "integrity=`$integrity sid=`$sid"
`$addErr='<ok>'
try { Add-AppxPackage -Path '$msix' -ErrorAction Stop } catch { `$addErr=`$_.Exception.Message }
Mark "addpackage done err=`$addErr"
`$pkg=Get-AppxPackage -Name PrintSink
`$alias=Join-Path `$env:LOCALAPPDATA 'Microsoft\WindowsApps\printsink-app.exe'
`$diag=Join-Path `$env:LOCALAPPDATA "Packages\`$(`$pkg.PackageFamilyName)\LocalState\Settings\diagnostic-events.json"
& `$alias --disable-job-ui | Out-Null
Mark 'disable-job-ui done'
& `$alias --install-virtual-printers | Out-Null
Mark 'install-printers done'
Start-Sleep -Seconds 3
`$printers=@(Get-Printer -ErrorAction SilentlyContinue | Where-Object { `$_.Name -like 'PrintSink*' } | ForEach-Object { `$_.Name })
Mark "printers=`$(`$printers -join ',')"
Remove-Item `$diag,`$headless -ErrorAction SilentlyContinue
Mark 'refresh starting'
`$t0=Get-Date
`$p=Start-Process -FilePath `$alias -ArgumentList @('--refresh-capabilities','--endpoint','$Endpoint') -PassThru -Wait
Mark "refresh done exit=`$(`$p.ExitCode)"
[ordered]@{
  integrity=`$integrity; sid=`$sid; isRid500=`$sid.EndsWith('-500'); addPackageError=`$addErr; installedPrinters=`$printers
  refreshExit=`$p.ExitCode; refreshSec=[math]::Round(((Get-Date)-`$t0).TotalSeconds,1)
  diagExists=(Test-Path `$diag); diag=if(Test-Path `$diag){Get-Content `$diag -Raw}else{'<none>'}
  refreshLog=if(Test-Path `$headless){Get-Content `$headless -Raw}else{'<none>'}
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath '$resultFile' -Encoding utf8
Mark 'result written'
"@ | Set-Content -LiteralPath $wrapper -Encoding utf8

# Diagnostic: can CreateProcessWithLogonW run ANYTHING for this user, and at what integrity?
$whoFile = Join-Path $pub 'cpl-who.txt'
Remove-Item $whoFile -ErrorAction SilentlyContinue
try {
    [PLogon.Native]::Start($user, $env:COMPUTERNAME, $pw, 'C:\Windows\System32\cmd.exe', "cmd.exe /c whoami /groups > `"$whoFile`" 2>&1", $pub) | Out-Null
    Start-Sleep -Seconds 8
    $report.cmdDiag = if (Test-Path $whoFile) { ((Get-Content $whoFile | Select-String 'Mandatory Level') | Out-String).Trim() } else { '<cmd produced no file>' }
} catch { $report.cmdDiag = "cmd launch err: $($_.Exception.Message)" }

# Diagnostic: capture pwsh stdout/stderr via cmd redirection.
$pwshOut = Join-Path $pub 'cpl-pwsh.txt'
Remove-Item $pwshOut -ErrorAction SilentlyContinue
$cmdLine = "cmd.exe /c `"pwsh.exe -NoProfile -ExecutionPolicy Bypass -File $wrapper > $pwshOut 2>&1`""
try {
    $wpid = [PLogon.Native]::Start($user, $env:COMPUTERNAME, $pw, 'C:\Windows\System32\cmd.exe', $cmdLine, $pub)
    $report.launchedPid = $wpid
    $deadline = (Get-Date).AddSeconds(280)
    while (-not (Test-Path $resultFile) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 3 }
    $report.result = if (Test-Path $resultFile) { Get-Content $resultFile -Raw | ConvertFrom-Json } else { '<no result>' }
    $report.progress = if (Test-Path $progressFile) { (Get-Content $progressFile -Raw) } else { '<no progress>' }
    $report.pwshOutput = if (Test-Path $pwshOut) { (Get-Content $pwshOut -Raw) } else { '<no pwsh output>' }
} catch { $report.launchError = $_.Exception.Message }

try { net user $user /delete 2>&1 | Out-Null } catch {}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutDir 'cplogon-report.json') -Encoding utf8
Write-Host ($report | ConvertTo-Json -Depth 8)

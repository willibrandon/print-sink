#requires -Version 7
# Decisive test: run the PSA capability refresh at a GENUINE medium integrity
# level (explicit token-integrity lowering via SetTokenInformation +
# CreateProcessWithTokenW), since RunLevel Limited is a no-op for the RID-500
# built-in admin. If medium SUCCEEDS where high TIMES OUT, integrity is the root cause.
param(
    [string] $MsixRunId = '27530511847',
    [string] $Endpoint = 'Pdf',
    [string] $OutDir = 'probe-out'
)
$ErrorActionPreference = 'Continue'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$report = [ordered]@{}

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace PMed {
  [StructLayout(LayoutKind.Sequential)] public struct SI { public uint cb; public string r1; public string desk; public string title; public uint x,y,xs,ys,xc,yc,fill,flags; public ushort show,r2; public IntPtr r3,i,o,e; }
  [StructLayout(LayoutKind.Sequential)] public struct PI { public IntPtr hProcess,hThread; public uint pid,tid; }
  [StructLayout(LayoutKind.Sequential)] public struct SIDATTR { public IntPtr Sid; public uint Attributes; }
  [StructLayout(LayoutKind.Sequential)] public struct TML { public SIDATTR Label; }
  public static class Native {
    [DllImport("kernel32.dll")] public static extern IntPtr GetCurrentProcess();
    [DllImport("advapi32.dll", SetLastError=true)] public static extern bool OpenProcessToken(IntPtr p, uint a, out IntPtr t);
    [DllImport("advapi32.dll", SetLastError=true)] public static extern bool DuplicateTokenEx(IntPtr t, uint a, IntPtr sa, int imp, int type, out IntPtr nt);
    [DllImport("advapi32.dll", SetLastError=true)] public static extern bool SetTokenInformation(IntPtr t, int cls, ref TML info, uint len);
    [DllImport("advapi32.dll", SetLastError=true)] public static extern bool ConvertStringSidToSid(string s, out IntPtr sid);
    [DllImport("advapi32.dll", SetLastError=true)] public static extern uint GetLengthSid(IntPtr sid);
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern bool CreateProcessWithTokenW(IntPtr t, uint lf, string app, string cmd, uint cf, IntPtr env, string dir, ref SI si, out PI pi);
    public static int StartMedium(string app, string cmd, string dir) {
      IntPtr tok, dup, sid;
      if(!OpenProcessToken(GetCurrentProcess(), 0x0002|0x0008|0x0080|0x0001, out tok)) throw new Exception("OpenProcessToken "+Marshal.GetLastWin32Error());
      if(!DuplicateTokenEx(tok, 0x0002|0x0008|0x0080|0x0001, IntPtr.Zero, 2, 1, out dup)) throw new Exception("DuplicateTokenEx "+Marshal.GetLastWin32Error());
      if(!ConvertStringSidToSid("S-1-16-8192", out sid)) throw new Exception("ConvertSid "+Marshal.GetLastWin32Error()); // Medium
      TML tml = new TML(); tml.Label.Sid = sid; tml.Label.Attributes = 0x20; // SE_GROUP_INTEGRITY
      uint len = (uint)(Marshal.SizeOf(typeof(TML))) + GetLengthSid(sid);
      if(!SetTokenInformation(dup, 25, ref tml, len)) throw new Exception("SetTokenInformation "+Marshal.GetLastWin32Error());
      SI si = new SI(); si.cb=(uint)Marshal.SizeOf(typeof(SI)); si.desk="winsta0\\default"; PI pi;
      if(!CreateProcessWithTokenW(dup, 0, app, cmd, 0, IntPtr.Zero, dir, ref si, out pi)) throw new Exception("CreateProcessWithTokenW "+Marshal.GetLastWin32Error());
      return (int)pi.pid;
    }
  }
}
'@
$ErrorActionPreference = 'Continue'

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

# --- CONTROL: prove the launcher actually yields medium integrity ---
$ctl = Join-Path $env:TEMP 'med-whoami.txt'
Remove-Item $ctl -ErrorAction SilentlyContinue
try {
    $cpid = [PMed.Native]::StartMedium('C:\Windows\System32\cmd.exe', "cmd.exe /c whoami /groups > `"$ctl`"", (Get-Location).Path)
    Start-Sleep -Seconds 3
    $report.controlIntegrity = if (Test-Path $ctl) { ((Get-Content $ctl | Select-String 'Mandatory Level') | Out-String).Trim() } else { '<no output>' }
} catch { $report.controlIntegrity = "launcher error: $($_.Exception.Message)" }

# --- Refresh at MEDIUM integrity via a wrapper launched with the lowered token ---
$resultFile = Join-Path $env:TEMP 'med-refresh-result.json'
$wrapper = Join-Path $env:TEMP 'med-refresh-wrapper.ps1'
Remove-Item $resultFile, $diagFile -ErrorAction SilentlyContinue
@"
`$ErrorActionPreference='Continue'
`$t0=Get-Date
`$p=Start-Process -FilePath '$alias' -ArgumentList @('--refresh-capabilities','--endpoint','$Endpoint') -PassThru -Wait
[ordered]@{
  integrity=((cmd /c "whoami /groups" 2>&1 | Select-String 'Mandatory Level') | Out-String).Trim()
  exitCode=`$p.ExitCode
  durationSec=[math]::Round(((Get-Date)-`$t0).TotalSeconds,1)
  diagExists=(Test-Path '$diagFile')
  diag=if(Test-Path '$diagFile'){Get-Content '$diagFile' -Raw}else{'<none>'}
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath '$resultFile' -Encoding utf8
"@ | Set-Content -LiteralPath $wrapper -Encoding utf8
try {
    $wpid = [PMed.Native]::StartMedium('C:\Program Files\PowerShell\7\pwsh.exe', "pwsh.exe -NoProfile -ExecutionPolicy Bypass -File `"$wrapper`"", (Get-Location).Path)
    $deadline = (Get-Date).AddSeconds(220)
    while (-not (Test-Path $resultFile) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 3 }
    $report.mediumRefresh = if (Test-Path $resultFile) { Get-Content $resultFile -Raw | ConvertFrom-Json } else { '<no result>' }
} catch { $report.mediumRefresh = "launcher error: $($_.Exception.Message)" }

try { & $alias --remove-virtual-printers 2>&1 | Out-Null } catch {}
try { Remove-AppxPackage -Package $pkg.PackageFullName -ErrorAction SilentlyContinue } catch {}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutDir 'medium-report.json') -Encoding utf8
Write-Host ($report | ConvertTo-Json -Depth 8)

#requires -Version 7
# Canonical de-elevation: duplicate the current token, lower its integrity to
# Medium, build an environment block, and launch via CreateProcessAsUser. Then
# run the capability refresh at medium integrity and capture the packaged app's
# actual integrity level. This both PROVES the root cause and is the basis of the fix.
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
using System.Text;
namespace PMed {
  [StructLayout(LayoutKind.Sequential)] public struct SI { public uint cb; public string r1; public string desk; public string title; public uint x,y,xs,ys,xc,yc,fill,flags; public ushort show,r2; public IntPtr r3,i,o,e; }
  [StructLayout(LayoutKind.Sequential)] public struct PI { public IntPtr hProcess,hThread; public uint pid,tid; }
  [StructLayout(LayoutKind.Sequential)] public struct SIDATTR { public IntPtr Sid; public uint Attributes; }
  [StructLayout(LayoutKind.Sequential)] public struct TML { public SIDATTR Label; }
  [StructLayout(LayoutKind.Sequential)] public struct LUID { public uint lo; public int hi; }
  [StructLayout(LayoutKind.Sequential)] public struct LUID_AND_ATTR { public LUID Luid; public uint Attributes; }
  [StructLayout(LayoutKind.Sequential)] public struct TOKEN_PRIVS { public uint Count; public LUID_AND_ATTR Privilege; }
  public static class Native {
    [DllImport("kernel32.dll")] public static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError=true)] public static extern IntPtr OpenProcess(uint a, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError=true)] public static extern bool CloseHandle(IntPtr h);
    [DllImport("advapi32.dll", SetLastError=true)] public static extern bool OpenProcessToken(IntPtr p, uint a, out IntPtr t);
    [DllImport("advapi32.dll", SetLastError=true)] public static extern bool DuplicateTokenEx(IntPtr t, uint a, IntPtr sa, int imp, int type, out IntPtr nt);
    [DllImport("advapi32.dll", SetLastError=true)] public static extern bool SetTokenInformation(IntPtr t, int cls, ref TML info, uint len);
    [DllImport("advapi32.dll", SetLastError=true)] public static extern bool GetTokenInformation(IntPtr t, int cls, IntPtr info, uint len, out uint ret);
    [DllImport("advapi32.dll", SetLastError=true)] public static extern bool ConvertStringSidToSid(string s, out IntPtr sid);
    [DllImport("advapi32.dll", SetLastError=true)] public static extern uint GetLengthSid(IntPtr sid);
    [DllImport("advapi32.dll", SetLastError=true)] public static extern IntPtr GetSidSubAuthority(IntPtr sid, uint i);
    [DllImport("advapi32.dll", SetLastError=true)] public static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll", SetLastError=true)] public static extern bool LookupPrivilegeValue(string sys, string name, out LUID luid);
    [DllImport("advapi32.dll", SetLastError=true)] public static extern bool AdjustTokenPrivileges(IntPtr t, bool dis, ref TOKEN_PRIVS np, uint len, IntPtr prev, IntPtr retlen);
    [DllImport("userenv.dll", SetLastError=true)] public static extern bool CreateEnvironmentBlock(out IntPtr env, IntPtr token, bool inherit);
    [DllImport("userenv.dll", SetLastError=true)] public static extern bool DestroyEnvironmentBlock(IntPtr env);
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern bool CreateProcessAsUserW(IntPtr token, string app, string cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string dir, ref SI si, out PI pi);

    static void EnablePriv(IntPtr tok, string name) {
      LUID luid; if(!LookupPrivilegeValue(null, name, out luid)) return;
      TOKEN_PRIVS tp = new TOKEN_PRIVS(); tp.Count=1; tp.Privilege.Luid=luid; tp.Privilege.Attributes=0x2; // ENABLED
      AdjustTokenPrivileges(tok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
    }
    public static int GetIntegrityRid(int pid) {
      IntPtr p = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
      if(p==IntPtr.Zero) return -1;
      IntPtr tok;
      try {
        if(!OpenProcessToken(p, 0x0008, out tok)) return -2;
        uint need=0; GetTokenInformation(tok, 25, IntPtr.Zero, 0, out need);
        IntPtr buf=Marshal.AllocHGlobal((int)need);
        try {
          if(!GetTokenInformation(tok, 25, buf, need, out need)) return -3;
          IntPtr sid=Marshal.ReadIntPtr(buf); // TML.Label.Sid
          int count=Marshal.ReadByte(GetSidSubAuthorityCount(sid));
          IntPtr last=GetSidSubAuthority(sid,(uint)(count-1));
          return Marshal.ReadInt32(last);
        } finally { Marshal.FreeHGlobal(buf); CloseHandle(tok); }
      } finally { CloseHandle(p); }
    }
    public static int StartMedium(string app, string cmd, string dir) {
      IntPtr cur; OpenProcessToken(GetCurrentProcess(), 0x0008|0x0020, out cur); // QUERY|ADJUST_PRIVILEGES
      EnablePriv(cur, "SeAssignPrimaryTokenPrivilege"); EnablePriv(cur, "SeIncreaseQuotaPrivilege"); CloseHandle(cur);
      IntPtr tok, dup, sid, env;
      if(!OpenProcessToken(GetCurrentProcess(), 0x0002|0x0001|0x0008|0x0080|0x0100, out tok)) throw new Exception("OpenProcessToken "+Marshal.GetLastWin32Error());
      if(!DuplicateTokenEx(tok, 0x02000000, IntPtr.Zero, 2, 1, out dup)) throw new Exception("DuplicateTokenEx "+Marshal.GetLastWin32Error()); // MAXIMUM_ALLOWED, primary
      if(!ConvertStringSidToSid("S-1-16-8192", out sid)) throw new Exception("ConvertSid "+Marshal.GetLastWin32Error()); // Medium
      TML tml = new TML(); tml.Label.Sid = sid; tml.Label.Attributes = 0x20;
      uint len = (uint)Marshal.SizeOf(typeof(TML)) + GetLengthSid(sid);
      if(!SetTokenInformation(dup, 25, ref tml, len)) throw new Exception("SetTokenInformation "+Marshal.GetLastWin32Error());
      if(!CreateEnvironmentBlock(out env, dup, false)) env = IntPtr.Zero;
      SI si = new SI(); si.cb=(uint)Marshal.SizeOf(typeof(SI)); si.desk="winsta0\\default"; PI pi;
      uint flags = 0x00000400 | 0x08000000; // CREATE_UNICODE_ENVIRONMENT | CREATE_NO_WINDOW
      bool ok = CreateProcessAsUserW(dup, app, cmd, IntPtr.Zero, IntPtr.Zero, false, flags, env, dir, ref si, out pi);
      if(env!=IntPtr.Zero) DestroyEnvironmentBlock(env);
      if(!ok) throw new Exception("CreateProcessAsUser "+Marshal.GetLastWin32Error());
      return (int)pi.pid;
    }
  }
}
'@

function Get-RidName([int]$rid) { switch($rid){ 8192{'Medium'} 12288{'High'} 16384{'System'} 4096{'Low'} default{"rid=$rid"} } }

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

# --- CONTROL: prove the launcher yields a working Medium process ---
$ctl = Join-Path $env:TEMP 'ctl.txt'
Remove-Item $ctl -ErrorAction SilentlyContinue
try {
    $cpid = [PMed.Native]::StartMedium('C:\Windows\System32\whoami.exe', "whoami.exe /groups", (Get-Location).Path)
    $report.controlLaunchedPid = $cpid
    Start-Sleep -Seconds 2
    $report.controlProcIntegrity = Get-RidName ([PMed.Native]::GetIntegrityRid($cpid))
} catch { $report.controlError = $_.Exception.Message }

# --- Refresh at Medium; capture the packaged app's actual integrity ---
$resultFile = Join-Path $env:TEMP 'med-result.json'
$wrapper = Join-Path $env:TEMP 'med-wrapper.ps1'
Remove-Item $resultFile, $diagFile -ErrorAction SilentlyContinue
@"
`$ErrorActionPreference='Continue'
`$t0=Get-Date
`$p=Start-Process -FilePath '$alias' -ArgumentList @('--refresh-capabilities','--endpoint','$Endpoint') -PassThru -Wait
[ordered]@{
  wrapperIntegrity=((whoami /groups | Select-String 'Mandatory Level') | Out-String).Trim()
  exitCode=`$p.ExitCode
  durationSec=[math]::Round(((Get-Date)-`$t0).TotalSeconds,1)
  diagExists=(Test-Path '$diagFile')
  diag=if(Test-Path '$diagFile'){Get-Content '$diagFile' -Raw}else{'<none>'}
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath '$resultFile' -Encoding utf8
"@ | Set-Content -LiteralPath $wrapper -Encoding utf8
try {
    $wpid = [PMed.Native]::StartMedium('C:\Program Files\PowerShell\7\pwsh.exe', "pwsh.exe -NoProfile -ExecutionPolicy Bypass -File `"$wrapper`"", (Get-Location).Path)
    $report.wrapperLaunchedPid = $wpid
    $report.wrapperProcIntegrity = Get-RidName ([PMed.Native]::GetIntegrityRid($wpid))
    $appIntegrity = $null
    $deadline = (Get-Date).AddSeconds(200)
    while (-not (Test-Path $resultFile) -and (Get-Date) -lt $deadline) {
        if (-not $appIntegrity) {
            $ap = Get-Process -Name 'PrintSink.App' -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($ap) { $appIntegrity = Get-RidName ([PMed.Native]::GetIntegrityRid($ap.Id)) }
        }
        Start-Sleep -Milliseconds 500
    }
    $report.packagedAppIntegrity = $appIntegrity
    $report.mediumRefresh = if (Test-Path $resultFile) { Get-Content $resultFile -Raw | ConvertFrom-Json } else { '<no result>' }
} catch { $report.refreshError = $_.Exception.Message }

try { & $alias --remove-virtual-printers 2>&1 | Out-Null } catch {}
try { Remove-AppxPackage -Package $pkg.PackageFullName -ErrorAction SilentlyContinue } catch {}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutDir 'medium-report.json') -Encoding utf8
Write-Host ($report | ConvertTo-Json -Depth 8)

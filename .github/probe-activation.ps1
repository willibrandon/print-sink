#requires -Version 7
# Temporary diagnostic: reproduce the PSA capability-refresh failure on the hosted
# ARM64 runner and capture WHY the print-support extension never activates.
# Installs the prebuilt signed ARM64 MSIX, triggers --refresh-capabilities, and
# records process spawns + architecture + crash/WER/event evidence.
param(
    [string] $MsixRunId = '27530511847',   # a main CI run that uploaded msix-ARM64
    [string] $Endpoint = 'Pdf',
    [string] $OutDir = 'probe-out'
)

$ErrorActionPreference = 'Continue'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$report = [ordered]@{}

# --- Architecture of THIS process (detect x64 emulation on ARM64) ---
Add-Type -Namespace P -Name Arch -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError=true)]
public static extern bool IsWow64Process2(System.IntPtr hProcess, out ushort processMachine, out ushort nativeMachine);
[System.Runtime.InteropServices.DllImport("kernel32.dll")]
public static extern System.IntPtr GetCurrentProcess();
'@
function Get-ProcArch([System.IntPtr]$h) {
    $pm = 0; $nm = 0
    try {
        if ([P.Arch]::IsWow64Process2($h, [ref]$pm, [ref]$nm)) {
            $map = @{ 0 = 'native'; 0x8664 = 'x64-emulated'; 0xAA64 = 'ARM64'; 0x1c0 = 'ARM'; 0x14c = 'x86' }
            $proc = if ($map.ContainsKey([int]$pm)) { $map[[int]$pm] } else { ('0x{0:X}' -f $pm) }
            $nat = if ($map.ContainsKey([int]$nm)) { $map[[int]$nm] } else { ('0x{0:X}' -f $nm) }
            return "processMachine=$proc; nativeMachine=$nat"
        }
    } catch {}
    return 'unknown'
}
$report.processorArchitectureEnv = $env:PROCESSOR_ARCHITECTURE
$report.processorIdentifier = $env:PROCESSOR_IDENTIFIER
$report.thisProcessArch = Get-ProcArch ([P.Arch]::GetCurrentProcess())

# --- Download prebuilt signed ARM64 MSIX from a main CI run ---
$pkgDir = Join-Path $OutDir 'pkg'
New-Item -ItemType Directory -Force -Path $pkgDir | Out-Null
& gh run download $MsixRunId -n msix-ARM64 -D $pkgDir 2>&1 | Out-String | Write-Host
$msix = Get-ChildItem -Path $pkgDir -Recurse -Filter '*.msix' | Select-Object -First 1
$cer = Get-ChildItem -Path $pkgDir -Recurse -Filter '*.cer' | Select-Object -First 1
$report.msix = $msix.FullName
$report.cer = $cer.FullName
if (-not $msix) { $report | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $OutDir 'activation-report.json'); throw 'No MSIX downloaded.' }

# --- Trust cert + install package ---
Import-Certificate -FilePath $cer.FullName -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
Import-Certificate -FilePath $cer.FullName -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
Add-AppxPackage -Path $msix.FullName
$pkg = Get-AppxPackage -Name 'PrintSink'
$report.package = if ($pkg) { $pkg.PackageFullName } else { '<install failed>' }
$report.packageFamily = $pkg.PackageFamilyName

# --- Start print support services like the harness does ---
foreach ($svc in 'Spooler', 'BrokerInfrastructure', 'PrintScanBrokerService', 'PrintDeviceConfigurationService', 'PrintNotify') {
    try { Start-Service -Name $svc -ErrorAction Stop } catch {}
}
Get-Service -Name 'PrintWorkflowUserSvc_*' -ErrorAction SilentlyContinue | ForEach-Object { try { Start-Service -Name $_.Name } catch {} }
wevtutil.exe set-log 'Microsoft-Windows-PrintService/Operational' /enabled:true 2>&1 | Out-Null

$diagFile = Join-Path $env:LOCALAPPDATA "Packages\$($pkg.PackageFamilyName)\LocalState\Settings\diagnostic-events.json"
Remove-Item -LiteralPath $diagFile -ErrorAction SilentlyContinue

# --- Provision (these CLI commands work even when extension activation does not) ---
& printsink-app.exe --disable-job-ui            2>&1 | Out-String | Write-Host
& printsink-app.exe --install-virtual-printers  2>&1 | Out-String | Write-Host
Start-Sleep -Seconds 3

# --- Does BASIC IppPrintDevice comms work? (GetPrinterAttributes via --assert-virtual-attribute-read) ---
$attrStart = Get-Date
& printsink-app.exe --assert-virtual-attribute-read --endpoint $Endpoint 2>&1 | Out-String | Write-Host
$report.attrReadExitCode = $LASTEXITCODE
$report.attrReadDurationSec = [math]::Round(((Get-Date) - $attrStart).TotalSeconds, 1)
$attrHeadless = Join-Path $env:TEMP 'PrintSink.App.headless.log'
$report.attrReadLog = if (Test-Path $attrHeadless) { Get-Content $attrHeadless -Raw } else { '<none>' }
Remove-Item -LiteralPath $attrHeadless -ErrorAction SilentlyContinue

# --- PSA association / registration state on the PDF queue ---
$queueName = 'PrintSink - PDF'
try { $report.getPrinter = Get-Printer -Name $queueName -ErrorAction Stop | Select-Object Name, DriverName, PortName, PrinterStatus, Shared, DeviceType, RenderingMode } catch { $report.getPrinter = "err: $($_.Exception.Message)" }
try { $report.printerProperties = @(Get-PrinterProperty -PrinterName $queueName -ErrorAction SilentlyContinue | Select-Object PropertyName, Value) } catch { $report.printerProperties = "err: $($_.Exception.Message)" }
$printerRegRoot = 'HKLM:\SYSTEM\CurrentControlSet\Control\Print\Printers'
try {
    $key = Join-Path $printerRegRoot $queueName
    $report.printerRegistryValues = if (Test-Path $key) { (Get-ItemProperty -LiteralPath $key | Select-Object * -ExcludeProperty PS*) } else { '<missing>' }
    $report.printerRegistrySubkeys = if (Test-Path $key) { @(Get-ChildItem -LiteralPath $key -Recurse -ErrorAction SilentlyContinue | ForEach-Object { $_.Name }) } else { @() }
} catch { $report.printerRegistryValues = "err: $($_.Exception.Message)" }
# Spooler's record of registered Print Support Apps / print extensions
foreach ($psaKey in @(
        'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Print\Components',
        'HKLM:\SYSTEM\CurrentControlSet\Control\Print\Environments\Windows ARM64\PrintSupportApps',
        'HKLM:\SYSTEM\CurrentControlSet\Control\Print\PackageInstallation')) {
    $name = 'reg_' + ($psaKey -replace '[^A-Za-z0-9]+', '_')
    try { $report[$name] = if (Test-Path $psaKey) { @(Get-ChildItem -LiteralPath $psaKey -ErrorAction SilentlyContinue | ForEach-Object { $_.Name }) } else { '<missing>' } } catch { $report[$name] = "err: $($_.Exception.Message)" }
}

# --- Snapshot processes before the refresh ---
$before = @{}
Get-Process -ErrorAction SilentlyContinue | ForEach-Object { $before[$_.Id] = $true }

# --- Trigger the refresh; poll for NEW processes (the background host) + their arch ---
$refreshStart = Get-Date
$spawns = [System.Collections.Generic.List[object]]::new()
$seenNew = @{}
$proc = Start-Process -FilePath 'printsink-app.exe' -ArgumentList @('--refresh-capabilities', '--endpoint', $Endpoint) -PassThru
while (-not $proc.HasExited -and ((Get-Date) - $refreshStart).TotalSeconds -lt 200) {
    foreach ($p in (Get-Process -ErrorAction SilentlyContinue)) {
        if (-not $before.ContainsKey($p.Id) -and -not $seenNew.ContainsKey($p.Id)) {
            $seenNew[$p.Id] = $true
            $arch = try { Get-ProcArch $p.Handle } catch { 'n/a' }
            $spawns.Add([ordered]@{
                    t = [math]::Round(((Get-Date) - $refreshStart).TotalSeconds, 1)
                    name = $p.Name
                    id = $p.Id
                    arch = $arch
                })
        }
    }
    Start-Sleep -Milliseconds 200
}
if (-not $proc.HasExited) { $proc.Kill() }
$proc.WaitForExit()
$report.refreshExitCode = $proc.ExitCode
$report.refreshDurationSec = [math]::Round(((Get-Date) - $refreshStart).TotalSeconds, 1)
$report.newProcessesDuringRefresh = $spawns
$headless = Join-Path $env:TEMP 'PrintSink.App.headless.log'
$report.headlessLog = if (Test-Path $headless) { Get-Content $headless -Raw } else { '<none>' }
$report.diagnosticEventsExists = Test-Path $diagFile
if (Test-Path $diagFile) { $report.diagnosticEvents = Get-Content $diagFile -Raw }

# --- Capture crash / activation evidence from the refresh window ---
function Dump-Log([string]$logName, [int]$max) {
    try {
        Get-WinEvent -FilterHashtable @{ LogName = $logName; StartTime = $refreshStart.AddSeconds(-5) } -MaxEvents $max -ErrorAction Stop |
            ForEach-Object { "$($_.TimeCreated.ToString('o')) [$($_.Id)] $($_.ProviderName) $($_.LevelDisplayName): $((($_.Message) -replace '\s+', ' '))".Substring(0, [Math]::Min(400, "$($_.TimeCreated.ToString('o')) [$($_.Id)] $($_.ProviderName) $($_.LevelDisplayName): $((($_.Message) -replace '\s+', ' '))".Length)) }
    } catch { @("err: $($_.Exception.Message)") }
}
$report.log_Application = @(Dump-Log 'Application' 60)
$report.log_System = @(Dump-Log 'System' 40)
$report.log_AppModelRuntime = @(Dump-Log 'Microsoft-Windows-AppModel-Runtime/Admin' 60)
$report.log_BackgroundTask = @(Dump-Log 'Microsoft-Windows-BackgroundTaskInfrastructure/Operational' 60)
$report.log_PrintServiceOp = @(Dump-Log 'Microsoft-Windows-PrintService/Operational' 60)
$report.log_PrintServiceAdmin = @(Dump-Log 'Microsoft-Windows-PrintService/Admin' 30)

# WER reports created during/after the refresh
$werRoots = @("$env:ProgramData\Microsoft\Windows\WER\ReportQueue", "$env:ProgramData\Microsoft\Windows\WER\ReportArchive")
$report.werReports = @(
    foreach ($root in $werRoots) {
        if (Test-Path $root) {
            Get-ChildItem $root -Recurse -Filter 'Report.wer' -ErrorAction SilentlyContinue |
                Where-Object { $_.LastWriteTime -ge $refreshStart.AddMinutes(-1) } |
                ForEach-Object { ($_.FullName + "`n" + (Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue)) }
        }
    }
)

# --- Cleanup ---
try { & printsink-app.exe --remove-virtual-printers 2>&1 | Out-Null } catch {}
try { Remove-AppxPackage -Package $pkg.PackageFullName -ErrorAction SilentlyContinue } catch {}

$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutDir 'activation-report.json') -Encoding utf8
Write-Host ($report | ConvertTo-Json -Depth 8)

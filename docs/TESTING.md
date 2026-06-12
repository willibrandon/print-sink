# Testing

PrintSink uses fast automated checks for code that can run without the print broker, plus a manual end-to-end runbook for the real Windows print stack.

## Automated Gate

Run this before committing:

```powershell
dotnet build PrintSink.slnx --no-restore -p:Platform=x64
dotnet test PrintSink.slnx --no-build -p:Platform=x64
```

The build treats warnings as errors. Do not disable analyzers to pass the gate; fix the source issue.

## CLI Validation

Run the shipped validators against the package assets:

```powershell
dotnet run --project src\PrintSink.Cli -- manifest lint --manifest src\PrintSink.App\Package.appxmanifest
dotnet run --project src\PrintSink.Cli -- pdc validate --pdc src\PrintSink.App\Config\PrinterPdf.pdc.xml
dotnet run --project src\PrintSink.Cli -- pdc validate --pdc src\PrintSink.App\Config\PrinterXps.pdc.xml
dotnet run --project src\PrintSink.Cli -- pdc validate --pdc src\PrintSink.App\Config\PrinterPostScript.pdc.xml
dotnet run --project src\PrintSink.Cli -- pdc validate --pdc src\PrintSink.App\Config\PrinterCloud.pdc.xml
dotnet run --project src\PrintSink.Cli -- pdc validate --pdc src\PrintSink.App\Config\PrinterPwgRaster.pdc.xml
```

Useful fixture checks:

```powershell
dotnet run --project src\PrintSink.Cli -- ticket map --ticket <print-ticket.xml>
dotnet run --project src\PrintSink.Cli -- sink test --endpoint pdf --content-type application/oxps
dotnet run --project src\PrintSink.Cli -- tui
```

## App Startup Check

```powershell
dotnet run --project src\PrintSink.App
```

Verify that a PrintSink window opens and responds. Close it after the check if more builds will follow.

## Manual Print-Stack Runbook

Use a clean Windows 11 24H2 VM or newer. Install the signed MSIX package, then verify the queues:

```powershell
Get-Printer | Where-Object Name -like 'PrintSink*' | Select-Object Name,DriverName,PortName
```

Expected queues:

- `PrintSink - PDF`
- `PrintSink - XPS`
- `PrintSink - PostScript`
- `PrintSink - Cloud`
- `PrintSink - PWG Raster`

Run these scenarios:

1. Print from a Win32 app through the common print dialog to each file-backed queue.
2. Print from a WinRT or packaged app through the modern print dialog.
3. Print a PDF from Edge to the PDF queue and confirm PDF passthrough.
4. Print to the cloud queue and confirm no Save As target is requested.
5. Open printer preferences and confirm the settings UI is modal to the owner window.
6. Change a setting that affects capabilities and confirm the PDC refresh path runs.
7. Launch job UI, change watermark options, complete the job, and confirm the output reflects the choice.
8. Cancel from job UI and confirm no output file is written.

Output checks:

- PDF starts with `%PDF-`.
- XPS/OXPS opens in the Windows viewer or another XPS reader.
- PostScript starts with `%!PS`.
- PWG Raster output is non-empty and recognized by the chosen PWG inspection tool.
- Watermark text or image appears on rendered pages when enabled.

Record the package version, Windows build, architecture, source application, target queue, and output result for each run.

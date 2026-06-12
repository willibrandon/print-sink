# Testing

PrintSink uses fast automated checks for code that can run without the print broker, plus Windows-runner automation for the real print stack.

## Automated Gate

Run this before committing:

```powershell
.\build.ps1 -Configuration Debug -Platform x64
.\test.ps1 -Configuration Debug -Platform x64 -NoBuild
.\test-app.ps1 -Configuration Debug -Platform x64 -NoBuild
.\test-coverage.ps1 -Configuration Debug -Platform x64
```

The build treats warnings as errors. Do not disable analyzers to pass the gate; fix the source issue.
Use the build script for the full gate because `PrintSink.Xps` is a native C++/WinRT project and
requires MSBuild or Visual Studio. The test script runs the plain MSTest projects through the .NET 10
Microsoft.Testing.Platform runner; the packaged app test script uses Visual Studio's test platform
against the generated `.appxrecipe` so the WinUI test host runs with package identity. The scripts do
not call `dotnet test` on the solution file because the solution also contains the native
`PrintSink.Xps` project.

## Continuous Integration

`.github\workflows\windows-ci.yml` runs the same MSBuild/test/coverage gate on GitHub-hosted Windows runners:

- `x64` on `windows-2025-vs2026`
- `ARM64` on `windows-11-vs2026-arm`

## CLI Validation

Run the shipped validators against the package assets:

```powershell
dotnet run --project src\PrintSink.Cli -- manifest lint --manifest src\PrintSink.App\Package.appxmanifest
dotnet run --project src\PrintSink.Cli -- pdc validate --pdc src\PrintSink.App\Config\PrinterPdf.pdc.xml --pdr src\PrintSink.App\Config\PrinterPdf.pdr.xml
dotnet run --project src\PrintSink.Cli -- pdc validate --pdc src\PrintSink.App\Config\PrinterXps.pdc.xml --pdr src\PrintSink.App\Config\PrinterXps.pdr.xml
dotnet run --project src\PrintSink.Cli -- pdc validate --pdc src\PrintSink.App\Config\PrinterPostScript.pdc.xml --pdr src\PrintSink.App\Config\PrinterPostScript.pdr.xml
dotnet run --project src\PrintSink.Cli -- pdc validate --pdc src\PrintSink.App\Config\PrinterCloud.pdc.xml --pdr src\PrintSink.App\Config\PrinterCloud.pdr.xml
dotnet run --project src\PrintSink.Cli -- pdc validate --pdc src\PrintSink.App\Config\PrinterPwgRaster.pdc.xml --pdr src\PrintSink.App\Config\PrinterPwgRaster.pdr.xml
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

## Print-Stack E2E Automation

Use a Windows 11 24H2 VM or a GitHub `windows-2025` runner. Build a signed MSIX, install it, provision the queues through the app execution alias, and assert the queues through the scriptable print stack.

```powershell
tests\e2e\Invoke-PrintSinkE2E.ps1 -PackagePath <PrintSink.msix>
```

When the package is already installed:

```powershell
tests\e2e\Invoke-PrintSinkE2E.ps1 -SkipPackageInstall
```

To remove the queues after assertion:

```powershell
tests\e2e\Invoke-PrintSinkE2E.ps1 -PackagePath <PrintSink.msix> -Cleanup
```

The script validates the installed package before provisioning:

- `printsink-app.exe` app execution alias.
- all print-support foreground/background extensions.
- all five virtual-printer manifest entries.
- packaged PDC/PDR files for each queue.
- `WinRT.Host.dll`, `PrintSink.Tasks.winmd`, `PrintSink.Xps.dll`, and the registered activatable classes.

It then runs `printsink-app.exe --install-virtual-printers` and fails with `%TEMP%\PrintSink.App.headless.log` if provisioning fails. App execution aliases are verified against the signed MSIX package, not loose development registration.

The harness must assert these queues:

- `PrintSink - PDF`
- `PrintSink - XPS`
- `PrintSink - PostScript`
- `PrintSink - Cloud`
- `PrintSink - PWG Raster`

The automated E2E suite is extended as features land:

1. Print from a Win32 source through the common print path to each file-backed queue.
2. Print from a WinRT or packaged source through the modern print path.
3. Print a PDF fixture to the PDF queue and confirm PDF passthrough.
4. Print to the cloud queue and confirm no Save As target is requested.
5. Open printer preferences through automation and confirm the settings UI is modal to the owner window.
6. Change a setting that affects capabilities and assert the PDC refresh path.
7. Launch job UI, change watermark options, complete the job, and assert the output reflects the choice.
8. Cancel from job UI and assert no output file is written.

Output assertions:

- PDF starts with `%PDF-`.
- XPS/OXPS opens in the Windows viewer or another XPS reader.
- PostScript starts with `%!PS`.
- PWG Raster output is non-empty and recognized by the chosen PWG inspection tool.
- Watermark text or image appears on rendered pages when enabled.

The CI job records the package version, Windows build, architecture, source application, target queue, and output result for each run.

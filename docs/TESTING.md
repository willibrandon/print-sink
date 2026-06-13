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

`.github\workflows\windows-ci.yml` runs the same MSBuild/test/coverage gate on GitHub-hosted Windows runners, then builds a signed MSIX and runs the real print-stack E2E suite:

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
dotnet run --project src\PrintSink.Cli -- pdc validate --pdc src\PrintSink.App\Config\PrinterPclm.pdc.xml --pdr src\PrintSink.App\Config\PrinterPclm.pdr.xml
```

Useful fixture checks:

```powershell
Get-AppxPackage PrintSink | Remove-AppxPackage
$pkg = "artifacts\appxpackages\x64\PrintSink.App_1.0.0.0_x64_Debug_Test"
Import-Certificate -FilePath "$pkg\PrintSink.App_1.0.0.0_x64_Debug.cer" -CertStoreLocation Cert:\CurrentUser\TrustedPeople
Add-AppxPackage -Path "$pkg\PrintSink.App_1.0.0.0_x64_Debug.msix" -ForceApplicationShutdown -ForceUpdateFromAnyVersion
dotnet run --project src\PrintSink.Cli -- queues install
dotnet run --project src\PrintSink.Cli -- queues
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

Use a Windows 11 24H2 VM or a GitHub `windows-2025` runner. Build a signed MSIX, install it, provision the queues through `dotnet run --project src\PrintSink.Cli -- queues install` or the packaged app execution alias, and assert the queues through the scriptable print stack.

```powershell
tests\e2e\Invoke-PrintSinkE2E.ps1 -PackagePath <PrintSink.msix> -OutputDirectory artifacts\e2e\x64
```

When the package is already installed:

```powershell
tests\e2e\Invoke-PrintSinkE2E.ps1 -SkipPackageInstall
```

`-SkipPackageInstall` expects an installed MSIX package. Loose development-mode registration from `dotnet run` or F5 is rejected before provisioning because Windows can register the app while still failing virtual-printer installation.
The default run prints through all six real queues. A short STA print harness submits real Windows print jobs, UI Automation fills the Windows `Save Print Output As` dialog for file-backed queues, and the package-local diagnostics must report `Job completed` for each queue.
The harness drives the Save-As broker by setting the native filename control and accepting the dialog through window messages, so it does not rely on keyboard focus in CI.

To remove the queues after assertion:

```powershell
tests\e2e\Invoke-PrintSinkE2E.ps1 -PackagePath <PrintSink.msix> -Cleanup
```

The script validates the installed package before provisioning:

- `printsink-app.exe` app execution alias.
- all print-support foreground/background extensions.
- all six virtual-printer manifest entries.
- packaged PDC/PDR files for each queue.
- `WinRT.Host.dll`, `PrintSink.Tasks.winmd`, `PrintSink.Xps.dll`, and the registered activatable classes.

It then runs `printsink-app.exe --install-virtual-printers` and fails with `%TEMP%\PrintSink.App.headless.log` if provisioning fails. App execution aliases are verified against the signed MSIX package, not loose development registration.
Before provisioning, the harness runs `printsink-app.exe --disable-job-ui` so background print activations can complete without showing the foreground Job UI. It restores the default with `printsink-app.exe --enable-job-ui` after the assertions.

The harness must assert these queues:

- `PrintSink - PDF`
- `PrintSink - XPS`
- `PrintSink - PostScript`
- `PrintSink - Cloud`
- `PrintSink - PWG Raster`
- `PrintSink - PCLm`

The required E2E suite proves the current installed-package behavior:

1. Print from a Win32 source through the common print path to each file-backed queue.
2. Print to the cloud queue and confirm no Save As target is requested.
3. Install, list, and remove queues through `PrintSink.Cli`, and assert the reported state against
   the real Windows printer list.
4. Assert the package-local route diagnostic for every real job: source content type, target format,
   action, conversion kind, and route reason must match the expected endpoint behavior.
5. Assert the real `PrintSupportExtensionBackgroundTask` path: every queue records
   `Print ticket validated`, capability refresh records custom features, PDR update, and MXDC
   configuration, and printer selection records the adaptive-card/additional-field request.
6. Send a real source PDF through `IppPrintDevice.GetPdlPassthroughProvider`, drive the Save As
   target, and assert the output remains byte-for-byte identical while diagnostics report the PDF
   copy route.
7. Launch the packaged WinRT print-source harness, drive the real Windows print dialog to
   `PrintSink - PDF`, and assert the PDF output and route diagnostics.
8. Launch the Settings UI from the real Windows print dialog, assert it disables its owner while open,
   and assert the owner is restored when Settings closes.
9. Set package-local default text and image watermarks, call
   `IppPrintDevice.RefreshPrintDeviceCapabilities`, print real PDFs with Job UI disabled, and assert
   the outputs reflect those defaults.
10. Launch Job UI, change watermark options, complete the job, and assert the output reflects the choice.
11. Launch Job UI, cancel the job, and assert the target remains empty while package-local diagnostics record `Job canceled`.
12. Assert package shape, virtual-printer declarations, PDC/PDR assets, app execution alias, WinRT host files, and activatable classes.
13. Assert all six queues are installed through the signed package and are removed when `-Cleanup` is used.

Any implemented print-stack behavior that is not represented above must add a real E2E assertion in the
same change.

Real output assertions:

- PDF opens with PDFPig, has at least one page, and extracted text contains `foo`.
- XPS/OXPS is an OPC package, supports interleaved OXPS pieces, has at least one fixed page, and contains `foo`.
- PostScript starts with `%!PS` and declares pages.
- PWG Raster has a valid raster magic value and non-blank page body.
- PCLm opens with PDFPig and has at least one page.
- Cloud produces no Save-As output and must still report `Job completed` from the real background task.
- Route diagnostics must prove the expected copy or conversion path for the source content type.
- Extension diagnostics must prove real ticket validation for every queue, PDC/PDR refresh, MXDC image
  quality configuration, and printer-selected adaptive-card setup.
- PDF passthrough output must be byte-for-byte identical to the valid source PDF submitted through
  Windows' PDL passthrough provider.
- WinRT source printing must produce a valid PDF containing the source text through the real Windows
  print dialog.
- Settings UI activation must show the Reactor settings surface, disable the real Windows print dialog
  owner while open, and restore the owner when closed.
- Package-local default text watermark settings appear in a real PDF after a capability refresh.
- Package-local default image watermark settings add PDF image content after a capability refresh.
- Watermark text appears on rendered pages when enabled.
- Job UI cancel leaves the selected target empty and records `Job canceled` before aborting the real print flow.

The CI job records the package version, Windows build, architecture, source application, target queue, and output result for each run.

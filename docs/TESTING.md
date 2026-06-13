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
dotnet run --project src\PrintSink.Cli -- ticket map --ticket tests\fixtures\print-ticket\standard.xml
dotnet run --project src\PrintSink.Cli -- sink test --endpoint pdf --content-type application/oxps
dotnet run --project src\PrintSink.Cli -- tui
```

## App Startup Check

```powershell
dotnet run --project src\PrintSink.App
```

Verify that a PrintSink window opens and responds. Close it after the check if more builds will follow.

## Print-Stack E2E Automation

Use a Windows 11 24H2 VM or a GitHub `windows-2025` runner. Run the E2E script from elevated PowerShell 7
(`pwsh`): it installs a temporary signed extension INF for the local IPP association check. Build a signed
MSIX, install it, provision the queues through `dotnet run --project src\PrintSink.Cli -- queues install` or
the packaged app execution alias, and assert the queues through the scriptable print stack.

```powershell
tests\e2e\Invoke-PrintSinkE2E.ps1 -PackagePath <PrintSink.msix> -OutputDirectory artifacts\e2e\x64
```

When the package is already installed:

```powershell
tests\e2e\Invoke-PrintSinkE2E.ps1 -SkipPackageInstall
```

`-SkipPackageInstall` expects an installed MSIX package. Loose development-mode registration from `dotnet run` or F5 is rejected before provisioning because Windows can register the app while still failing virtual-printer installation.
The default run prints through all six real queues. A short STA print harness submits real Windows print jobs, UI Automation fills the Windows `Save Print Output As` dialog for file-backed queues, and the package-local diagnostics must report `Job completed` for each queue before the script validates the output file. The suite also prints a real text file through Notepad's `/p` entrypoint to `PrintSink - PDF`, restores the previous default printer, and validates the selected PDF so a normal desktop application path is covered.
The harness drives the Save-As broker by setting the native filename control and accepting the dialog through window messages, so it does not rely on keyboard focus in CI.

To remove the queues after assertion:

```powershell
tests\e2e\Invoke-PrintSinkE2E.ps1 -PackagePath <PrintSink.msix> -Cleanup
```

The script validates the installed package before provisioning:

- `printsink-app.exe` app execution alias.
- all print-support foreground/background extensions.
- `privateNetworkClientServer`, required for IPP workflow communication.
- multiple-instance application support for concurrent print activations.
- all six virtual-printer manifest entries and their localized DisplayName resource references.
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
3. Print a real Notepad `/p` text document to `PrintSink - PDF`, then assert the selected PDF is
   non-empty, opens with PDFPig, contains `foo`, and all queues remain installed.
4. Submit two real Win32 jobs to different file-backed queues while the first job is still active,
   then assert both outputs and overlapping route/completion diagnostics.
5. Install, list, and remove queues through `PrintSink.Cli`, and assert the reported state against
   the real Windows printer list.
6. Assert package-local route evidence for every real job: source content type, target format,
   action, conversion kind, and route reason must match the expected endpoint behavior. The standalone
   `Route resolved` event is preferred; the `Job completed` event also carries the route so completion
   evidence remains self-contained.
7. Assert the real `PrintSupportExtensionBackgroundTask` path: every queue records
   `Print ticket validated`, capability refresh records custom features, PDR update, and MXDC
   configuration, and printer selection records the adaptive-card/additional-field request.
8. Set the PDF queue's user default print ticket through `IppPrintDevice.UserDefaultPrintTicket`,
   verify the persisted copy count, and restore it before output tests continue.
9. Assert `IppPrintDevice.GetPrinterAttributes` against a real virtual queue exposes no usable
   `document-format-default` or `document-format-supported` values, matching the PSA v4 platform
   behavior for virtual printers.
10. Generate, sign, install, and remove a temporary PSA extension INF for a local IPP class-driver
   queue. Assert Windows writes the PSA AUMID device property, the local IPP helper receives real
   `GetPrinterAttributes` traffic, the real `PrintSupportExtensionBackgroundTask` validates print
   tickets for that IPP queue, and a real print job records `PrintSupportWorkflowBackgroundTask`
   start/compression-state and pass-through diagnostics. Document-output assertions are made through
   the PrintSink virtual queues.
11. Send a real source PDF through `IppPrintDevice.GetPdlPassthroughProvider`, drive the Save As
   target, and assert the output remains byte-for-byte identical while diagnostics report the PDF
   copy route.
12. Launch the packaged WinRT print-source harness, drive the real Windows print dialog to
   `PrintSink - PDF`, and assert the PDF output and route diagnostics.
13. Launch the Settings UI from the real Windows print dialog, assert it disables its owner while open,
   and assert the owner is restored when Settings closes.
14. Set package-local default text and image watermarks, call
   `IppPrintDevice.RefreshPrintDeviceCapabilities`, print real PDFs with Job UI disabled, and assert
   the outputs reflect those defaults.
15. Configure a corrupt package-local image watermark, print a real PDF job with Job UI disabled, and
    assert the background task reports `Job failed` with exception/HRESULT detail, without producing
    output or removing queues.
16. Launch Job UI, assert it receives virtual-printer PDL metadata for the real job, change watermark
    options, fill the job-password field, complete the job, assert the output reflects the watermark
    choice, assert the output does not contain the password, and assert diagnostics record the password
    metadata as not applicable to virtual file output.
17. Launch Job UI, assert it receives virtual-printer PDL metadata, cancel the job, and assert the target
    remains empty while package-local diagnostics record `Job canceled`.
18. Assert package shape, multiple-instance support, virtual-printer declarations, PDC/PDR assets,
    app execution alias, WinRT host files, and activatable classes.
19. Assert localized queue DisplayName resources are declared in the signed package and resolve to
    the expected installed queue names.
20. Assert all six queues stay installed after provisioning, extension refresh, default-ticket edits,
    every real print path, Settings UI, failed jobs, Job UI complete, and Job UI cancel.
21. Assert all six queues are removed when `-Cleanup` is used.

Any implemented print-stack behavior that is not represented above must add a real E2E assertion in the
same change. The E2E script also writes `featureEvidence` into `e2e-result.json`; that section is built
from the live assertions above and fails the run if a supported print-stack feature lacks evidence.
Tracked compatibility hooks that are not claimed as supported behavior are written separately as
`deferredFeatureEvidence` and must not be used to satisfy supported feature coverage. The current
deferred hooks are Job UI notification activation and IPP communication-error timeout recovery because
Windows does not expose deterministic triggers for those events in the supported E2E path.

Real output assertions:

- PDF opens with PDFPig, has at least one page, and extracted text contains `foo`.
- The Notepad `/p` client print must produce a non-empty PDF containing `foo`; a zero-byte target selected
  through the Save-As broker fails CI.
- XPS/OXPS is an OPC package, supports interleaved OXPS pieces, declares XPS content types,
  has at least one parseable fixed page, and contains `foo`.
- PostScript starts with `%!PS` and has resolved page count, bounding box, page trailer, trailer, and EOF markers.
- PWG Raster has a valid raster magic value, sane page header fields, and a non-blank page body.
- PCLm has PDF/PCLm header markers, opens with PDFPig, has at least one page, and contains raster image content.
- Cloud produces no Save-As output, must still report `Job completed` from the real background task,
  and must write a package-local sink artifact that validates as PDF output.
- Route diagnostics, or the route carried by `Job completed`, must prove the expected copy or conversion path for the source content type.
- Concurrent output diagnostics must prove two real jobs overlapped by comparing route and completion
  timestamps for the two activated queues.
- Extension diagnostics must prove real ticket validation for every queue, PDC/PDR refresh, MXDC image
  quality configuration, and printer-selected adaptive-card setup.
- User default print-ticket diagnostics must prove a real default copy-count update and restore through
  `IppPrintDevice.UserDefaultPrintTicket`.
- Virtual-printer IPP attribute reads must prove `GetPrinterAttributes` exposes no usable
  document-format values for the real installed virtual queue.
- IPP PSA association must prove a signed extension INF can associate the installed package AUMID
  with a real Microsoft IPP Class Driver device, trigger ticket validation for that queue, submit a
  real print job that activates workflow start and pass-through, record IPP compression state while
  leaving system rendering enabled, and produce local IPP request evidence.
- PDF passthrough output must be byte-for-byte identical to the valid source PDF submitted through
  Windows' PDL passthrough provider.
- WinRT source printing must produce a valid PDF containing the source text through the real Windows
  print dialog.
- Settings UI activation must show the Reactor settings surface, disable the real Windows print dialog
  owner while open, and restore the owner when closed.
- Package-local default text watermark settings appear in a real PDF after a capability refresh.
- Package-local default image watermark settings add PDF image content after a capability refresh.
- A corrupt image watermark causes a real background-task failure, records `Job failed` with an
  exception/HRESULT detail, and leaves the target file empty or absent.
- Job UI activation must record virtual-printer PDL metadata for the real job title, source application,
  and OXPS content type before the E2E continues or cancels the job.
- Watermark text appears on rendered pages when enabled.
- Job UI password metadata is consumed by the virtual-printer processor without exposing the secret in
  diagnostics or applying it to virtual file output.
- Job UI cancel leaves the selected target empty and records `Job canceled` before aborting the real print flow.
- Queue persistence is checked against the real Windows printer list after every major E2E step; a job
  that removes, loses, or unregisters a PrintSink queue fails CI.

The CI job records the package version, Windows build, architecture, source application, target queue,
queue snapshots, feature evidence, and output result for each run. The E2E script writes the full run
record to `e2e-result.json` in the output directory, and CI uploads that file with the generated
documents.

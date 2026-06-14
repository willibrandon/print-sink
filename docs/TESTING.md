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
Microsoft.Testing.Platform runner, including the E2E document-assertion executable's regression tests;
the packaged app test script uses Visual Studio's test platform against the generated `.appxrecipe` so
the WinUI test host runs with package identity, then removes the registered test package unless
`-KeepPackage` is used. The scripts do not call `dotnet test` on the solution file because the solution
also contains the native `PrintSink.Xps` project.

## Continuous Integration

`.github\workflows\windows-ci.yml` runs the same MSBuild/test/coverage gate on GitHub-hosted Windows runners, then calls `.\test-e2e.ps1 -BuildPackage` to build a signed MSIX and run the real print-stack E2E suite:

- `x64` on `windows-2025-vs2026`
- `ARM64` on `windows-11-vs2026-arm`

After E2E, CI runs `.\test-clean-state.ps1 -Cleanup` and fails if any `PrintSink*` package, queue, or
process was left behind. Uploaded test results, coverage, E2E outputs, and MSIX artifacts are required;
missing evidence fails the run.

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
certutil -user -addstore TrustedPeople "$pkg\PrintSink.App_1.0.0.0_x64_Debug.cer"
certutil -addstore TrustedPeople "$pkg\PrintSink.App_1.0.0.0_x64_Debug.cer"
Add-AppxPackage -Path "$pkg\PrintSink.App_1.0.0.0_x64_Debug.msix" -ForceApplicationShutdown -ForceUpdateFromAnyVersion
dotnet run --project src\PrintSink.Cli -- queues install
dotnet run --project src\PrintSink.Cli -- queues
dotnet run --project src\PrintSink.Cli -- ticket map --ticket tests\fixtures\print-ticket\standard.xml
dotnet run --project src\PrintSink.Cli -- sink test --endpoint pdf --content-type application/oxps
dotnet run --project src\PrintSink.Cli -- tui
```

The TUI exposes the same queue lifecycle and fixture sink checks as focusable actions. Hex1b headless
tests assert the rendered screen and keyboard activation path.

## App Startup Check

```powershell
dotnet run --project src\PrintSink.App
```

Verify that a PrintSink window opens and responds. Close it after the check if more builds will follow.

## Print-Stack E2E Automation

Use a Windows 11 24H2 VM or a GitHub `windows-2025` runner. Run the E2E script from elevated PowerShell 7
(`pwsh`): it installs a temporary signed extension INF for the local IPP association check. The root wrapper
reuses or creates a local code-signing certificate, builds a signed MSIX, installs it, runs the real
print-stack assertions, then removes the queues and installed package by default:

```powershell
.\test-e2e.ps1 -BuildPackage -Platform x64
```

When the signed package is already installed:

```powershell
.\test-e2e.ps1 -SkipPackageInstall
```

Pass `-KeepPackage` only when you intentionally want to inspect the installed MSIX after a run.
Pass `-KeepQueues` only when you intentionally want to inspect the installed printers after a run;
the wrapper leaves the package installed in that mode because the queues are package-backed.

The lower-level harness accepts an explicit package path when the package was built elsewhere:

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
- the preferred input format for every virtual-printer manifest entry.
- supported passthrough format declarations for every virtual-printer manifest entry.
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
   Cloud evidence must prove no Save-As output, zero file-backed bytes, a copied package-local PDF
   sink artifact, matching byte count, and extracted `foo` text.
3. Print a real Notepad `/p` text document to `PrintSink - PDF`, then assert the selected PDF is
   non-empty, opens with PDFPig, contains `foo`, and all queues remain installed.
   Save-As evidence must include exact file-backed queue names, selected output paths, byte counts,
   document validation, and a validated Notepad `/p` PDF.
4. Submit two real Win32 jobs to different file-backed queues while the first job is still active,
   then assert both outputs and overlapping route/completion diagnostics.
5. Install, list, and remove queues through `PrintSink.Cli`, and assert the reported state against
   the real Windows printer list.
6. Launch the packaged management UI, invoke the remove/install/refresh queue actions, refresh
   capabilities, set and restore default copies, toggle Job UI/headless mode, and assert the real
   Windows printer list plus package diagnostics reflect each action.
7. Assert package-local route evidence for every real job: source content type, target format,
   action, conversion kind, and route reason must match the expected endpoint behavior. The standalone
   `Route resolved` event is preferred; the `Job completed` event also carries the route so completion
   evidence remains self-contained.
   Conversion evidence must include exact converted queue names for PDF, PWG Raster, and PCLm,
   matching routes, byte counts, and document validation. XPS copy evidence must include the exact
   OXPS copy route and a validated OXPS package containing the source text.
   Preferred-input evidence must include the signed manifest preference and the observed source content type
   for each real queue.
   Passthrough evidence must include signed-package `SupportedFormat` declarations, byte-for-byte PDF
   passthrough, and observed copy routes for PDF, XPS/OXPS, and PostScript.
8. Assert the real `PrintSupportExtensionBackgroundTask` path: every queue records
   `Print ticket validated`, capability refresh records custom features, PDR update, MXDC
   configuration, contract-19 PDL passthrough-with-job-attributes enablement, and printer selection
   records Adaptive Card 1.0 plus `PageMediaType`, `PageOutputQuality`, and
   `JobCopiesAllDocuments`.
   Management UI capability-refresh evidence must be request-ordered: the extension's
   `Capabilities updated` diagnostic must be at or after the management request.
   Localized queue-name evidence must match each expected `ms-resource:` key and each resolved
   installed queue name.
9. Set the PDF queue's user default print ticket through `IppPrintDevice.UserDefaultPrintTicket`,
   verify the persisted copy count, and restore it before output tests continue.
10. Assert `IppPrintDevice.GetPrinterAttributes` against a real virtual queue exposes no usable
   `document-format-default` or `document-format-supported` values, matching the PSA v4 platform
   behavior for virtual printers.
11. Generate, sign, install, and remove a temporary PSA extension INF for local IPP class-driver
   queues. Assert Windows writes the PSA AUMID device property, the local IPP helper receives real
   `GetPrinterAttributes` traffic, a stopped/rejecting IPP probe reports `printer-state=stopped`,
   `printer-state-reasons=paused`, and `printer-is-accepting-jobs=false`, the real
   `PrintSupportExtensionBackgroundTask` validates print tickets for that IPP queue, and a real print
   job records `PrintSupportWorkflowBackgroundTask` start/compression-state. Physical
   `PdlModificationRequested` pass-through is recorded when Windows delivers it, but document-output
   assertions are made through the PrintSink virtual queues.
12. Send a real source PDF through `IppPrintDevice.GetPdlPassthroughProvider`, drive the Save As
    target, and assert the output remains byte-for-byte identical while diagnostics report the PDF
    copy route and provider-v2 state. If the live runtime can encode IPP job and operation attributes
    and accepts provider-v2 submission, the run must prove that path. If the Windows print-ticket
    converter is unusable but provider-v2 is available, the run must prove the core-mapped IPP
    fallback attributes before provider submission. If the provider reports unsupported or provider-v2
    submission is unusable, the run must record explicit v1 fallback with the runtime failure detail.
13. Launch the packaged WinRT print-source harness, drive the real Windows print dialog to
   `PrintSink - PDF`, and assert the PDF output and route diagnostics.
14. Launch the Settings UI from the real Windows print dialog, assert the owner window title,
   Settings window title, package identity, printer-selected diagnostic, absence of Reactor render
   errors, owner disabled state while open, and owner restored state when Settings closes.
15. Set package-local default text and image watermarks, call
   `IppPrintDevice.RefreshPrintDeviceCapabilities`, print real PDFs with Job UI disabled, and assert
   the outputs reflect those defaults.
16. Configure a corrupt package-local image watermark, print a real PDF job with Job UI disabled, and
    assert the background task reports `Job failed` with exception/HRESULT detail, without producing
    output or removing queues.
17. Launch Job UI, assert the packaged Job preview window and Save As dialog appear, assert it receives
    virtual-printer PDL metadata for the real job, change watermark options, fill the job-password field,
    invoke Continue, assert the output reflects the watermark choice, assert the output does not contain
    the password, assert diagnostics record the password metadata as not applicable to virtual file output,
    and assert no Reactor render error was present.
18. Launch Job UI, assert it receives virtual-printer PDL metadata, cancel the job, and assert the target
    remains empty while package-local diagnostics record `Job canceled`.
19. Assert package shape, multiple-instance support, virtual-printer declarations, PDC/PDR assets,
    app execution alias, WinRT host files, and activatable classes.
20. Assert localized queue DisplayName resources are declared in the signed package and resolve to
    the expected installed queue names.
21. Assert all six queues stay installed after provisioning, extension refresh, default-ticket edits,
    every real print path, Settings UI, failed jobs, Job UI complete, and Job UI cancel.
22. Assert all six queues are removed when `-Cleanup` is used, and write the final cleanup snapshot to
    `e2e-result.json`.

Any implemented print-stack behavior that is not represented above must add a real E2E assertion in the
same change. The E2E script also writes `featureEvidence` into `e2e-result.json`; that section is built
from the live assertions above and fails the run if a supported print-stack feature lacks evidence.
Tracked compatibility hooks that are not claimed as supported behavior are written separately as
`deferredFeatureEvidence` and must not be used to satisfy supported feature coverage. The current
deferred hooks are job notification/job-issue activation and IPP communication-error timeout recovery
because Windows does not expose deterministic triggers for those events in the supported E2E path,
plus provider-v2 PDL passthrough with job attributes when the live provider reports provider2 as
unsupported or the runtime cannot deliver the full passthrough-attribute workflow. For the
provider-v2 hook, deferred evidence must still carry the live capability-refresh,
PDL-passthrough-provider, IPP attribute source, mapped fallback attribute names when that fallback is
used, and buffer-size details when provider-v2 submission is used, plus physical-workflow diagnostics
observed during the run.

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
- Extension diagnostics must prove real ticket validation for every queue, PDC/PDR refresh with the
  applied custom feature/option names and localized resource names, the full MXDC output-quality
  mapping, and printer-selected adaptive-card setup.
- User default print-ticket diagnostics must prove a real default copy-count update and restore through
  `IppPrintDevice.UserDefaultPrintTicket`.
- Virtual-printer IPP attribute reads must prove `GetPrinterAttributes` exposes no usable
  document-format values for the real installed virtual queue.
- IPP PSA association must prove a signed extension INF can associate the installed package AUMID
  with real Microsoft IPP Class Driver devices, observe stopped/rejecting printer state from the local
  IPP device, trigger ticket validation for that queue, submit a real print job that activates
  workflow start, record IPP compression state while leaving system rendering enabled, and produce
  local IPP request evidence. Physical pass-through is optional evidence because PrintSink does not
  claim physical target-stream replacement.
- PDF passthrough output must be byte-for-byte identical to the valid source PDF submitted through
  Windows' PDL passthrough provider. Diagnostics must prove provider-v2 submission with encoded IPP
  job and operation attribute buffers when the runtime can execute it, including the attribute source
  and any core-mapped fallback attribute names, and explicit v1 fallback when provider-v2 is unsupported
  or unusable.
- WinRT source printing must produce a valid PDF containing the source text through the real Windows
  print dialog.
- Settings UI activation must show the Reactor settings surface, record the real owner and Settings
  window titles, disable the real Windows print dialog owner while open, restore the owner when closed,
  and prove no Reactor render error was present.
- Package-local default text watermark settings appear in a real PDF after a capability refresh.
- Package-local default image watermark settings add PDF image content after a capability refresh.
- Watermark feature evidence must include the default text, default image, and Job UI text watermark
  artifacts, with matching PDF routes, extracted watermark text, image-content validation, and Job UI
  PDL metadata for the per-job path.
- A corrupt image watermark causes a real background-task failure, records `Job failed` with an
  exception/HRESULT detail, and leaves the target file empty or absent.
- Job UI activation must record the real packaged Job preview window, Save As dialog observation,
  UI Automation edits, Continue action, and virtual-printer PDL metadata for the real job title,
  source application, and OXPS content type before the E2E continues or cancels the job.
- Watermark text appears on rendered pages when enabled.
- Job UI password metadata is consumed by the virtual-printer processor without exposing the secret in
  diagnostics or applying it to virtual file output.
- Job UI cancel leaves the selected target empty and records `Job canceled` before aborting the real print flow.
- Queue persistence is checked against the real Windows printer list after every major E2E step; a job
  that removes, loses, or unregisters a PrintSink queue fails CI.

The CI job records the package version, Windows build, architecture, source application, target queue,
queue snapshots, queue-persistence evidence, management UI actions and queue snapshots, cleanup
evidence, feature evidence, and output result for each run. The E2E script writes the full run record to
`e2e-result.json` in the output directory, and the root wrapper runs
`tests\e2e\Assert-PrintSinkE2EResult.ps1` against that file before CI uploads it with the generated
documents. The validator rechecks the supported/deferred feature evidence, queue persistence snapshots,
management UI evidence, cleanup state, output file byte counts, document validity, PDF passthrough byte
equality, cloud sink artifacts, failed-job empty outputs, and Job UI cancel evidence.
The root wrapper removes the installed PrintSink package after validation unless `-KeepPackage`,
`-KeepQueues`, or `-SkipPackageInstall` is used.

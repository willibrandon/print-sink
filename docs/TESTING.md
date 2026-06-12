# Testing PrintSink

## Automated tests

Run the current automated gate:

```powershell
dotnet test PrintSink.slnx
```

The current automated suite covers:

- PDL routing for OXPS, PDF, PostScript, PWG Raster, and PCLm targets
- built-in virtual endpoint catalog behavior
- file and cloud sink abstractions
- watermark settings round-trip behavior
- Print Device Capabilities custom feature injection and idempotency
- print-ticket to IPP job attribute mapping, including encrypted job-password operation attributes
- one-type-per-file source layout enforcement

## Manual end-to-end gate

The full print-stack gate will be run on a clean Windows 11 build 26100+ VM after the packaged app, background tasks, XPS component, manifest, and signing flow exist.

Planned manual validation:

1. Build and sign the MSIX package.
2. Install the package with `Add-AppxPackage`.
3. Confirm all five design queues appear with `Get-Printer`: PDF, XPS, PostScript, Cloud, and PWG Raster.
4. Print from a Win32 app, a WinRT app, and Edge to each endpoint.
5. Verify PDF, XPS/OXPS, PostScript, cloud, and PWG outputs.
6. Confirm watermark output after job UI options are saved.
7. Open print preferences and confirm settings UI owner-window modality.
8. Trigger PDC refresh and confirm custom features are reflected.
9. Cancel from job UI and confirm the workflow completes as canceled without output.

## Future CI

After the repository is published, CI should run the automated test gate on `windows-2025` for x64. ARM64 and packaged WinUI tests should be added once the native XPS and app projects are present.

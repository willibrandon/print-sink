# Build

This project is MSIX-shaped. Keep the package manifest, launch profiles, assets, and Windows App SDK settings in place even when a command-line build is enough for a local check.

## Prerequisites

- Windows 11 24H2, build 26100 or later.
- .NET SDK 10 from `global.json`.
- Visual Studio 2026 with the Windows App SDK, C++ desktop build tools, and single-project MSIX workload pieces.
- `msbuild` available on `PATH`.

## Restore

```powershell
.\build.ps1 -Target Restore
```

Central Package Management is enabled. Package versions belong in `Directory.Packages.props`, not in individual project references.

## Debug Build

```powershell
.\build.ps1
```

Use an explicit platform for WinUI, CsWinRT, and C++/WinRT projects. Local x64 is the default development target.

The script runs:

```powershell
msbuild .\PrintSink.slnx /t:Build /p:Configuration=Debug /p:Platform=x64
```

## Visual Studio

Open `PrintSink.slnx` in Visual Studio 2026 and build with `Debug|x64`.

`PrintSink.App` contains these launch profiles:

- `PrintSink.App`
- `PrintSink.App Devtools`
- `PrintSink.App (Package)`
- `PrintSink.App Devtools (Package)`

Use the package profiles when testing MSIX deployment behavior. Use the normal project profile for fast local app startup checks.

## App Launch

```powershell
dotnet run --project src\PrintSink.App
```

Success means the command registers or updates the package and opens a real PrintSink window. A launcher PID alone is not enough; verify the window appears.

When automation launches the app for verification, close the app process after the check unless the next person explicitly wants it left open.

## Headless Package Commands

Virtual-printer provisioning requires an installed MSIX package. A loose package registered by
`dotnet run --project src\PrintSink.App` or Visual Studio F5 can launch the app, but it is not the
right fixture for installing the queues.

For a local debug package, remove any loose registration, trust the test certificate, and install the
MSIX:

```powershell
Get-AppxPackage PrintSink | Remove-AppxPackage
$pkg = "artifacts\appxpackages\x64\PrintSink.App_1.0.0.0_x64_Debug_Test"
Import-Certificate -FilePath "$pkg\PrintSink.App_1.0.0.0_x64_Debug.cer" -CertStoreLocation Cert:\CurrentUser\TrustedPeople
Add-AppxPackage -Path "$pkg\PrintSink.App_1.0.0.0_x64_Debug.msix" -ForceApplicationShutdown -ForceUpdateFromAnyVersion
```

These low-level commands run through the packaged app execution alias and do not show the WinUI shell:

```powershell
printsink-app.exe --install-virtual-printers
printsink-app.exe --remove-virtual-printers
printsink-app.exe --disable-job-ui
printsink-app.exe --enable-job-ui
printsink-app.exe --set-text-watermark --endpoint Pdf --text "Draft"
printsink-app.exe --clear-watermark --endpoint Pdf
printsink-app.exe --refresh-capabilities --endpoint Pdf
```

Prefer the CLI wrapper for queue provisioning:

```powershell
dotnet run --project src\PrintSink.Cli -- queues install
dotnet run --project src\PrintSink.Cli -- queues
dotnet run --project src\PrintSink.Cli -- queues remove
```

`dotnet run --project src\PrintSink.App` launches the management UI. It is not the recommended queue provisioning entry point.

`--disable-job-ui` makes background print activations process jobs without launching the foreground Job UI. Use it for unattended E2E runs, then restore the default with `--enable-job-ui`.

The watermark and capability commands are package-identity automation hooks used by the real E2E suite.
They write the same package-local settings the UI/background tasks read and then refresh the target
queue capabilities through the Windows print API.

## CLI

```powershell
dotnet run --project src\PrintSink.Cli -- --help
dotnet run --project src\PrintSink.Cli -- queues
dotnet run --project src\PrintSink.Cli -- queues install
dotnet run --project src\PrintSink.Cli -- manifest lint --manifest src\PrintSink.App\Package.appxmanifest
```

The CLI is not a print activation entry point. It is for validation, diagnostics, and fixture-driven checks.

## Release Build

For the full MSIX path, use MSBuild or Visual Studio:

```powershell
.\build.ps1 -Configuration Release
```

Signing and deployment are package concerns. Lab installs should use a trusted test certificate. Store or production packaging must use the final publisher identity and certificate.

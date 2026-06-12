# Build

This project is MSIX-shaped. Keep the package manifest, launch profiles, assets, and Windows App SDK settings in place even when a command-line build is enough for a local check.

## Prerequisites

- Windows 11 24H2, build 26100 or later.
- .NET SDK 10 from `global.json`.
- Visual Studio 2026 with the Windows App SDK and single-project MSIX workload pieces.
- `msbuild` available on `PATH`.

The current checkpoint is managed code and can build with `dotnet`. The full design includes a native C++/WinRT XPS component; once that project is added, use Visual Studio or MSBuild for the full solution build.

## Restore

```powershell
dotnet restore PrintSink.slnx
```

Central Package Management is enabled. Package versions belong in `Directory.Packages.props`, not in individual project references.

## Debug Build

```powershell
dotnet build PrintSink.slnx --no-restore -p:Platform=x64
```

Use an explicit platform for WinUI and CsWinRT projects. Local x64 is the default development target.

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

## CLI

```powershell
dotnet run --project src\PrintSink.Cli -- --help
dotnet run --project src\PrintSink.Cli -- queues
dotnet run --project src\PrintSink.Cli -- manifest lint --manifest src\PrintSink.App\Package.appxmanifest
```

The CLI is not a print activation entry point. It is for validation, diagnostics, and fixture-driven checks.

## Release Build

For the full MSIX path, use MSBuild or Visual Studio:

```powershell
msbuild PrintSink.slnx /p:Configuration=Release /p:Platform=x64
```

Signing and deployment are package concerns. Lab installs should use a trusted test certificate. Store or production packaging must use the final publisher identity and certificate.

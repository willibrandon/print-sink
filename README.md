# PrintSink

PrintSink is a packaged Windows virtual printer built on the Print Support App v4 surface. It is a modern software printer: no legacy driver, no port monitor, and no INF.

The app installs PrintSink queues for PDF, XPS/OXPS, PostScript, cloud/custom routing, and PWG Raster. The foreground app is WinUI 3 with Microsoft.UI.Reactor. Background print activations run through CsWinRT components, while the shared routing and validation logic lives in `PrintSink.Core`.

## Requirements

- Windows 11 24H2, build 26100 or later.
- .NET SDK 10, pinned by `global.json`.
- Visual Studio 2026 with Windows App SDK and single-project MSIX tooling.
- MSBuild on `PATH` for Visual Studio-style builds.

## Build

Build the full solution:

```powershell
.\build.ps1
```

Run tests:

```powershell
.\test.ps1 -Configuration Debug -Platform x64 -NoBuild
.\test-app.ps1 -Configuration Debug -Platform x64 -NoBuild
```

`dotnet build` is useful for managed checks. The full solution uses MSBuild because `PrintSink.Xps` is a C++/WinRT project.

Run the packaged app from the project profile:

```powershell
dotnet run --project src\PrintSink.App
```

Run the CLI:

```powershell
dotnet run --project src\PrintSink.Cli -- --help
```

For unattended print-stack checks, the packaged alias can skip foreground Job UI:

```powershell
printsink-app.exe --disable-job-ui
printsink-app.exe --enable-job-ui
```

More detail lives in [docs/BUILD.md](docs/BUILD.md) and [docs/TESTING.md](docs/TESTING.md).

# PrintSink

PrintSink is a packaged Windows virtual printer built on the Print Support App v4 surface. It is a modern software printer: no legacy driver, no port monitor, and no INF.

The app installs PrintSink queues for PDF, XPS/OXPS, PostScript, cloud/custom routing, and PWG Raster. The foreground app is WinUI 3 with Microsoft.UI.Reactor. Background print activations run through CsWinRT components, while the shared routing and validation logic lives in `PrintSink.Core`.

## Requirements

- Windows 11 24H2, build 26100 or later.
- .NET SDK 10, pinned by `global.json`.
- Visual Studio 2026 with Windows App SDK and single-project MSIX tooling.
- MSBuild on `PATH` for Visual Studio-style builds.

## Build

Restore and build the current managed solution:

```powershell
dotnet restore PrintSink.slnx
dotnet build PrintSink.slnx -p:Platform=x64
```

Run tests:

```powershell
dotnet test PrintSink.slnx --no-build -p:Platform=x64
```

Run the packaged app from the project profile:

```powershell
dotnet run --project src\PrintSink.App
```

Run the CLI:

```powershell
dotnet run --project src\PrintSink.Cli -- --help
```

More detail lives in [docs/BUILD.md](docs/BUILD.md) and [docs/TESTING.md](docs/TESTING.md).

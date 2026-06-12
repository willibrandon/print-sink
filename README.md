# PrintSink

PrintSink is a Windows Print Support App prototype for software printers. It targets the PSA v4 virtual-printer model: a packaged app owns the queue, receives the job, and routes the spool data to a file, cloud sink, or custom pipeline.

No legacy print driver, INF, GPD/PPD, or port monitor is part of the design.

## Status

This is an early implementation. The core library, unit tests, package manifest, and Reactor shell are active. Live print activation and the native XPS component are still being built out.

## Build

Requirements:

- .NET 10 SDK
- Visual Studio 2026 or MSBuild 18
- Windows App SDK and single-project MSIX tooling

Build the solution:

```powershell
msbuild PrintSink.slnx /restore /p:Configuration=Debug
```

Run the core tests:

```powershell
dotnet test tests\PrintSink.Core.Tests\PrintSink.Core.Tests.csproj
```

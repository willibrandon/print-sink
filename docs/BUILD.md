# Build PrintSink

PrintSink targets Windows 11 24H2 build 26100 or later and .NET 10. The full design includes a WinUI 3 packaged app, CsWinRT background tasks, and a native C++/WinRT XPS component, so the full product build must use Visual Studio 2026 / MSBuild rather than relying only on `dotnet build`.

## Current verified build

The implemented Core slice builds and tests with the .NET SDK:

```powershell
dotnet test PrintSink.slnx
```

This verifies:

- `PrintSink.Core`
- `PrintSink.Core.Tests`
- MSTest 3.x on Microsoft.Testing.Platform
- analyzer and XML documentation gates
- the repository source-layout lint test

## Visual Studio 2026 MSBuild

On this machine Visual Studio 2026 was found at:

```powershell
C:\Program Files\Microsoft Visual Studio\18\Enterprise
```

The full-solution MSBuild path is:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" PrintSink.slnx /p:Configuration=Release /p:Platform=x64
```

If you want `msbuild` available in ordinary shells, add this directory to `PATH`:

```text
C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin
```

## Tooling notes

- `global.json` pins SDK `10.0.301` with feature roll-forward.
- `Directory.Packages.props` uses Central Package Management.
- The WinUI CLI template is not currently visible to `dotnet new list winui` in this shell. Visual Studio can still be used for WinUI work; CLI template availability will be revisited when the app project is scaffolded.
- Full native/package validation will become available after the `PrintSink.App`, `PrintSink.Tasks`, `PrintSink.Xps`, and MSIX manifest projects are added.

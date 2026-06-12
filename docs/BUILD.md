# Build PrintSink

PrintSink targets Windows 11 24H2 build 26100 or later and .NET 10. The full design includes a WinUI 3 packaged app, CsWinRT background tasks, and a native C++/WinRT XPS component, so the full product build must use Visual Studio 2026 / MSBuild rather than relying only on `dotnet build`.

## Current verified build

The current solution builds with Visual Studio 2026 MSBuild:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" PrintSink.slnx /restore /p:Configuration=Debug
```

The current automated tests run with the .NET SDK:

```powershell
dotnet test PrintSink.slnx
```

This verifies:

- `PrintSink.App` packaged WinUI build
- `PrintSink.Core`
- `PrintSink.Core.Tests`
- MSTest 3.x on Microsoft.Testing.Platform
- analyzer and XML documentation gates
- the repository source-layout lint test

The WinUI app can be launched locally with package identity:

```powershell
dotnet run --project src\PrintSink.App\PrintSink.App.csproj -p:Configuration=Debug -p:Platform=x64
```

## Visual Studio 2026 MSBuild

On this machine Visual Studio 2026 was found at:

```powershell
C:\Program Files\Microsoft Visual Studio\18\Enterprise
```

The full-solution MSBuild path is:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" PrintSink.slnx /restore /p:Configuration=Release
```

The app project can also be built directly for a specific architecture:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" src\PrintSink.App\PrintSink.App.csproj /restore /p:Configuration=Debug /p:Platform=x64
```

If you want `msbuild` available in ordinary shells, add this directory to `PATH`:

```text
C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin
```

## Tooling notes

- `global.json` pins SDK `10.0.301` with feature roll-forward.
- `Directory.Packages.props` uses Central Package Management.
- The WinUI CLI templates are installed. `dotnet new list winui` shows the WinUI Blank App, NavigationView App, TabView App, class library, and unit test templates.
- Native XPS and signing validation are still pending. `PrintSink.Tasks` and the PSA manifest entries build in the current package.

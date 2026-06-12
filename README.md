# PrintSink

PrintSink is a packaged WinUI 3 app for building a virtual printer workflow. The current baseline keeps the print-stack adapters out until the core model is stable.

## Build

```powershell
dotnet restore PrintSink.slnx
dotnet build PrintSink.slnx --no-restore
dotnet test tests\PrintSink.Core.Tests\PrintSink.Core.Tests.csproj --no-build
```

## Current Scope

- `PrintSink.App`: packaged WinUI shell built with Microsoft.UI.Reactor.
- `PrintSink.Core`: pure .NET routing and endpoint model.
- `PrintSink.Core.Tests`: unit tests and a namespace layout guard.

`PrintSink.Tasks` is intentionally absent. Add the WinRT activation project only after the core contracts and manifest shape are ready.

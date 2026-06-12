# PrintSink

PrintSink is a packaged WinUI 3 app for a modern virtual printer workflow. The current baseline keeps live print-stack activation out until the core contracts are stable.

## Build

```powershell
dotnet restore PrintSink.slnx
dotnet build PrintSink.slnx --no-restore
dotnet test tests\PrintSink.Core.Tests\PrintSink.Core.Tests.csproj --no-build
```

## Scope

- `PrintSink.App`: packaged WinUI shell built with Microsoft.UI.Reactor.
- `PrintSink.Core`: pure .NET endpoint and PDL routing model.
- `PrintSink.Core.Tests`: unit tests and namespace layout guard.

`PrintSink.Tasks` is intentionally absent until the WinRT activation surface is added deliberately.

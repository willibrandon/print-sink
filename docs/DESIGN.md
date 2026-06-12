# PrintSink Design

PrintSink starts with a small packaged WinUI app and a pure core library. The app uses Microsoft.UI.Reactor for code-first WinUI screens. The core owns endpoint definitions, PDL format routing, and sink contracts.

## Current Baseline

- `PrintSink.App` is a packaged WinUI 3 executable.
- `PrintSink.Core` contains testable logic with no live print-stack dependency.
- `PrintSink.Core.Tests` runs on MSTest and guards namespace-to-folder layout.
- `PrintSink.Tasks` is not part of this baseline.

## Namespace Policy

The root namespace is `PrintSink`.

Folders under `src/PrintSink.Core` map directly beneath that root:

- `Abstractions` -> `PrintSink.Abstractions`
- `Endpoints` -> `PrintSink.Endpoints`
- `Pdl` -> `PrintSink.Pdl`

Do not introduce `PrintSink.Core.*` namespaces in the core project.

## UI

The foreground app uses Microsoft.UI.Reactor `Component` classes instead of XAML pages or code-behind. Startup uses a top-level `Program.cs` that calls `ReactorApp.Run<PrintSinkShell>()`.

## Deferred Work

The WinRT print-support activation project, manifest print-support extensions, PDC/PDR files, and live spooler integration are deferred until the core contracts are stable.

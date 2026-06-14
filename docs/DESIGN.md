# PrintSink — Design Document

**Project:** print-sink
**Solution / assembly / root namespace:** `PrintSink`
**Author:** Brandon Williams
**Status:** Implementation — active
**Last updated:** 2026-06-13
**Target platform:** Windows 11 24H2 (build 26100) and later; Windows Server 2025+

---

## 1. Purpose and scope

PrintSink is a **Print Support Virtual Printer (Software Printer)** built on the Microsoft-endorsed
**Print Support App (PSA) v4 Virtual Printer Architecture**. It installs one or more virtual print
queues from a packaged (MSIX) application and receives, transforms, and sinks spooled print jobs —
to PDF, to XPS/OXPS, to PostScript, to a file of the user's choosing, or to a custom pipeline (cloud,
another app, etc.) — **without any third-party V3/V4 print driver and without a custom port monitor
(RedMon-style)**.

This is the only forward-looking architecture as of 2026:

- **2026-01-15** — No new printer drivers are published to Windows Update for Windows 11+ / Server 2025+.
- **2026-07-01** — Driver ranking changes to always prefer the Windows IPP inbox class driver.
- Third-party V3/V4 drivers and port monitors are on the End-of-Servicing plan.

The Virtual Printer Architecture lets an ISV implement a software printer **as an application** that
implements the features formerly carried by a V3/V4 driver. PrintSink supports every deterministic
PSA v4 virtual-printer feature Windows exposes to packaged apps. Platform hooks that Windows does not
deliver through a deterministic virtual-printer E2E path are implemented defensively and tracked as
deferred compatibility hooks until a real trigger can prove them.

### 1.1 Goals

1. Install multiple virtual print queues from one MSIX package via the
   `windows.printSupportVirtualPrinterWorkflow` contract.
2. Implement the full deterministic PSA v4 virtual-printer feature set (see the
   **Feature Completeness Matrix**, §4), with platform-trigger-only compatibility hooks tracked
   separately.
3. Be the most modern possible C#/.NET implementation: **.NET 10**, **WinUI 3 / Windows App SDK**,
   **Microsoft.UI.Reactor** for the foreground UI, **CsWinRT 2.2+** projections,
   **single-project MSIX** packaging.
4. Strict engineering standards: **one type per file**, **triple-slash XML docs on every public member**,
   nullable reference types, analyzers as errors.
5. Fully tested following Microsoft's current guidance, using the current test stack
   (**MSTest on Microsoft.Testing.Platform**, .NET 10), plus deterministic automated
   end-to-end print-stack validation on Windows runners or clean VMs.
6. Ship a companion `PrintSink.Cli` executable: command-line automation through
   `System.CommandLine`, with a Hex1b TUI for local diagnostics and operator workflows.

### 1.2 Non-goals

- No legacy V3/V4 driver, INF, GPD/PPD, or port monitor.
- No UWP-only host (UWP appears only as a reference; PrintSink ships on Windows App SDK).
- Not a physical-device IPP customization app (that is classic PSA, a sibling scenario); PrintSink is
  the **Virtual Printer** (Software Endpoint) variant, while reusing the shared PSA contracts.

---

## 2. Background and key concepts

| Term | Meaning |
| --- | --- |
| **PSA** | Print Support App — packaged app using the `Windows.Graphics.Printing.PrintSupport` / `Windows.Graphics.Printing.Workflow` APIs. |
| **Virtual Printer / Software Endpoint** | A print queue backed by a PSA app instead of a driver. Added to the system as an `IppPrintDevice` whose `IsIppPrinter` returns true so PDL passthrough reuses the PSA surface. |
| **DEH** | Windows deployment extension handler — reads the package's virtual-printer manifest declarations. PrintSink also exposes a signed-package headless provisioning command that calls `VirtualPrinterManager` for deterministic local and CI installs. |
| **PDL** | Page Description Language — the spooled document format (OXPS, PostScript, PDF, PWG Raster, PCLm). |
| **PDC** | Print Device Capabilities XML — declares printer features/options (media sizes, duplex, color, custom features). Mandatory per virtual printer. |
| **PDR** | Print Device Resources XML — localized display strings for custom PDC features. Optional. |
| **MPD / CPD** | Modern Print Dialog (WinRT printing) / Common Print Dialog (Win32 printing). |
| **Print Ticket** | Per-job settings (`WorkflowPrintTicket`) expressed in Print Schema. |
| **MXDC** | Microsoft XPS Document Converter — rasterization quality is configurable per page-output-quality. |
| **SoftwareAppMon** | Inbox port monitor for the virtual printer queue (Windows-provided; we do not author it). |
| **Broker** | The print system talks to PrintSink through a PSA broker process; our background tasks run as in-process WinRT activatable classes. |

### 2.1 The print flow for a virtual printer

```
User prints to "PrintSink — PDF"  (any Win32/WinRT app, Edge passthrough, etc.)
        │
        ▼
Windows Print System  ── renders to PreferredInputFormat (OXPS or PostScript)
        │                or passes through a SupportedFormat (e.g. PDF) unchanged
        ▼
[If OutputFileTypes set]  Save-As dialog → user picks target file → StorageFile
        │
        ▼
windows.printSupportVirtualPrinterWorkflow  background task is activated
        │   PrintWorkflowVirtualPrinterTriggerDetails → PrintWorkflowVirtualPrinterSession
        ▼
VirtualPrinterDataAvailable event
        │   args: SourceContent (PDL stream + content type), GetTargetFileAsync(),
        │         GetJobPrintTicket(), GetPdlConverter(...), UILauncher
        ├─ (optional) LaunchAndCompleteUIAsync → windows.printSupportJobUI activation
        │              → VirtualPrinterUIDataAvailable → preview / collect input
        ├─ transform: watermark (XPS-OM), convert (XpsToPdf / XpsToPwgr / XpsToPclm), or passthrough
        ├─ write to target file / custom sink (cloud, app)
        ▼
CompleteJob(PrintWorkflowSubmittedStatus.Succeeded | Canceled | Failed)
```

Alongside the job path, three **shared PSA contracts** are reused by the virtual printer:

- `windows.printSupportExtension` — background: print-ticket validation, **PDC regeneration**,
  **MXDC image-quality** configuration, printer-selected adaptive cards.
- `windows.printSupportSettingsUI` — foreground: custom print-preferences UI (reused unchanged for the
  virtual printer), with `OwnerWindowId` modality on Windows App SDK.
- `windows.printSupportJobUI` — foreground: per-job UI / preview, including the v4
  `VirtualPrinterUIDataAvailable` event.

---

## 3. High-level architecture

PrintSink is a **single MSIX package** containing one WinUI 3 executable plus a CsWinRT-hosted
background-task component and a native XPS component. The print system activates the background-task
classes in-process via `WinRT.Host.dll`. A separate `PrintSink.Cli` executable ships for developer and
operator workflows; it references the same core library but is not an OS print activation entry point.

```
┌───────────────────────────── PrintSink.msix (single-project MSIX) ─────────────────────────────┐
│                                                                                                 │
│  PrintSink.App  (WinUI 3 + Microsoft.UI.Reactor, .NET 10, packaged) ← foreground activations    │
│    • Reactor root + activation router (Launch / SettingsUI / JobUI)                             │
│    • Settings preferences UI  (printSupportSettingsUI)                                           │
│    • Job UI / preview         (printSupportJobUI, incl. VirtualPrinterUIDataAvailable)           │
│    • Management / diagnostics UI (user launch)                                                   │
│                                                                                                 │
│  PrintSink.Tasks  (.NET 10 CsWinRT component → WinRT.Host.dll)  ← background activations         │
│    • VirtualPrinterBackgroundTask   (printSupportVirtualPrinterWorkflow)                         │
│    • PrintSupportWorkflowBackgroundTask (printSupportWorkflow — physical-printer parity path)    │
│    • PrintSupportExtensionBackgroundTask (printSupportExtension — PDC / validation / image qual) │
│                                                                                                 │
│  PrintSink.Core   (.NET 10 class library, pure, fully unit-testable)                            │
│    • PDL routing, format negotiation, print-ticket↔IPP attribute logic                          │
│    • PDC/PDR mutation engine, watermark options model, settings persistence                     │
│    • Zero dependency on the live print stack (interfaces + adapters)                             │
│                                                                                                 │
│  PrintSink.Xps    (C++/WinRT component)  +  PrintSink.Xps.Projections (C# projection assembly)   │
│    • XPS Object Model watermarking (text + image), page wrapping, sequential streaming          │
│                                                                                                 │
│  PrintSink.Cli    (.NET 10 console app; System.CommandLine + Hex1b)                             │
│    • Scriptable queue/config validation commands                                                 │
│    • Terminal diagnostics dashboard for local development and support                            │
│                                                                                                 │
│  Config\*.xml (PDC/PDR per printer)   Strings\<lang>\*.resw (localization)   Assets\*            │
└─────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 3.1 Why these components

- **WinUI 3 / Windows App SDK on .NET 10** is the modern packaged-app stack and the one
  Microsoft's current PSA sample targets. It supersedes UWP for new work.
- **Microsoft.UI.Reactor** is the foreground UI model. The app is code-first WinUI: no XAML pages,
  no code-behind layer, and no duplicated view-model ceremony when a small Reactor component owns the
  screen state cleanly. The project still stays MSIX-shaped, with `Package.appxmanifest`, assets,
  launch profiles, and package identity preserved.
- **Separate `PrintSink.Tasks` CsWinRT component** is required: the OS activates the background tasks
  as WinRT activatable classes hosted by `WinRT.Host.dll`. Setting `CsWinRTComponent=true` produces the
  host + a `.winmd` whose activatable class IDs are referenced from the package manifest.
- **`PrintSink.Core`** isolates all transformation/negotiation logic behind interfaces so it is unit
  testable without the print spooler — the single most important design decision for testability
  (the live PSA event objects cannot be instantiated outside an activation).
- **`PrintSink.Xps` (C++/WinRT)** is required because the **XPS Object Model is native** and there is no
  managed XPS-OM in modern .NET (the old `System.Printing`/`System.Windows.Xps` is WPF/.NET-Framework
  only). Watermarking and page-level XPS manipulation must go through C++/WinRT, consumed from C# via a
  CsWinRT projection assembly. This is exactly how Microsoft's sample is structured.
- **`PrintSink.Cli`** keeps automation separate from the packaged foreground app. `System.CommandLine`
  owns parsing and command dispatch. Hex1b owns the terminal UI. The CLI references the Hex1b NuGet
  package and shares logic only through `PrintSink.Core`.

> **Build consequence:** because of the C++/WinRT component, the solution **must build with MSBuild /
> Visual Studio 2026**, not `dotnet build` (the CLI cannot compile `.vcxproj`). This is accepted as the
> ideal trade-off — native XPS-OM is non-negotiable for full-fidelity watermarking.

---

## 4. Feature Completeness Matrix

Rows 1-21, 23-25, and 27 are supported PrintSink capabilities. Each supported row is implemented and covered
by CI. The E2E run writes `featureEvidence` for the print-stack rows it proves. Pure model behavior is
covered by unit tests. Rows 22, 26, and 28 are tracked separately as deferred compatibility hooks until
Windows can deliver them through deterministic E2E paths, including a live provider that can encode
and accept provider-v2 passthrough attributes for row 28. The physical IPP path is limited to PSA
association and activation evidence; document output is a virtual-printer feature.

| # | Feature | Contract / API | Component | Notes |
| --- | --- | --- | --- | --- |
| 1 | Install N virtual print queues from one package | `windows.printSupportVirtualPrinterWorkflow` manifest entries + `VirtualPrinterManager` | Manifest + headless provisioning | PDF, XPS, PostScript, Cloud, PWG-Raster, PCLm endpoints |
| 2 | Receive spooled PDL + content type | `PrintWorkflowVirtualPrinterSession.VirtualPrinterDataAvailable`, `args.SourceContent` | `VirtualPrinterBackgroundTask` | |
| 3 | Preferred input format negotiation | `PreferredInputFormat` (`application/oxps` \| `application/postscript`) | Manifest + E2E | Per-queue; E2E requires the signed manifest preference and real-job source content type |
| 4 | Passthrough formats (no OS re-render) | `SupportedFormats/SupportedFormat Type=… MaxVersion=…` | Manifest + router + E2E | E2E requires exact `SupportedFormat` declarations, byte-for-byte PDF passthrough, and observed XPS/PostScript copy routes |
| 5 | File-printer "Save As" target | `OutputFileTypes`, `args.GetTargetFileAsync()` → `StorageFile` | `VirtualPrinterBackgroundTask + E2E` | Omit attribute for cloud/app sinks; E2E requires exact file-backed queues, real Save-As output files, byte counts, and Notepad `/p` PDF output |
| 6 | Non-file sinks (cloud / OneNote-style) | (no `OutputFileTypes`); custom write in handler | `VirtualPrinterBackgroundTask` + `PrintSink.Core` sinks | E2E validates no Save-As output, zero target bytes, package-local sink artifact bytes/content type, and validated PDF text |
| 7 | OXPS → PDF / PWG-Raster / PCLm conversion | `args.GetPdlConverter(PrintWorkflowPdlConversionType.*)`, `ConvertPdlAsync` | `VirtualPrinterBackgroundTask + E2E` | E2E requires the exact PDF/PWG-Raster/PCLm converted queue set, matching routes, byte counts, and document validation |
| 8 | XPS/OXPS passthrough (copy) | `RandomAccessStream.CopyAndCloseAsync` | `VirtualPrinterBackgroundTask + E2E` | E2E requires the exact OXPS copy route, byte count, and validated OXPS output containing source text |
| 9 | Watermark (text + image) on XPS pages | XPS Object Model | `PrintSink.Xps + E2E` | Pre-conversion; E2E requires default text, default image, and per-job UI watermark outputs with matching routes, PDF validation, extracted text, image-content evidence, and Job UI PDL metadata |
| 10 | Per-job UI / preview launched from background | `args.UILauncher.LaunchAndCompleteUIAsync`, `PrintWorkflowJobUISession.VirtualPrinterUIDataAvailable` | `PrintSink.App` Job UI + E2E | E2E requires the real packaged Job preview window, Save-As dialog, virtual-printer PDL metadata, UI Automation edits, Continue action, validated PDF output, password metadata consumption, and no Reactor render error |
| 11 | Custom print-preferences UI | `windows.printSupportSettingsUI`, `PrintSupportSettingsActivatedEventArgs`, `OwnerWindowId` modality | `PrintSink.App` Settings UI + E2E | Reused unchanged for virtual printer; E2E requires the real Windows print dialog owner, Settings window title, owner disabled/restored state, package identity, printer-selected diagnostic, and no Reactor render error |
| 12 | Print-ticket validation / resolve | `PrintSupportExtensionSession.PrintTicketValidationRequested` | `PrintSupportExtensionBackgroundTask + E2E` | E2E requires every real queue to record `Print ticket validated` from the extension task with endpoint-specific `status=Resolved` |
| 13 | PDC regeneration / custom features | `PrintDeviceCapabilitiesChanged`, `UpdatePrintDeviceCapabilities` | `PrintSupportExtensionBackgroundTask` + `PrintSink.Core` PDC engine + E2E | E2E requires the applied PDC feature and option sets for media size/type, bins, page order, stapling, resolution, and watermark mode |
| 14 | PDR localization of custom features | `GetCurrentPrintDeviceResources` / `UpdatePrintDeviceResources`, `ResourceLanguage` | Extension task + `.resw` + E2E | E2E requires PDR update status, resource count, and exact localized resource names |
| 15 | Refresh PDC on settings change | `IppPrintDevice.RefreshPrintDeviceCapabilities()` | `PrintSink.App` → Extension task + E2E | E2E requires command and Management UI refresh paths, management completion, and extension capability update timestamp at or after the refresh request |
| 16 | Get/set user default print ticket | `IppPrintDevice.UserDefaultPrintTicket`, `CanModifyUserDefaultPrintTicket` | `PrintSink.App + E2E` | E2E requires command and Management UI set/restore diagnostics for PDF default copies with requested and verified counts |
| 17 | Physical IPP PSA association + workflow activation | Temporary signed PSA extension INF, Microsoft IPP Class Driver queue, `windows.printSupportWorkflow` activation | Extension task + workflow task + E2E | Association probe only; physical target-stream replacement is not claimed |
| 18 | MXDC image quality per output quality | `PrintSupportMxdcImageQualityConfiguration`, `XpsImageQuality` | `PrintSupportExtensionBackgroundTask + E2E` | E2E requires MXDC configured and the full Text/Draft/Normal/High/Photo/Auto/Fax image-quality mapping |
| 19 | Printer-selected adaptive card in MPD | `PrintSupportExtensionSession.PrinterSelected`, `SetAdaptiveCard`, additional features/params | Extension task | API-gated via `ApiInformation`; E2E records Adaptive Card 1.0, selected queue, `PageMediaType`, `PageOutputQuality`, and `JobCopiesAllDocuments` |
| 20 | IPP attribute get behavior for installed virtual queues | `IppPrintDevice.GetPrinterAttributes` | Package command + Core adapter | Assert document-format reads expose no usable virtual-printer IPP values, matching v4 platform behavior |
| 21 | Multiple instances for concurrent jobs | `uap10:SupportsMultipleInstances="true"` + real simultaneous print submissions | Manifest + E2E | CI asserts overlapping diagnostics and valid outputs |
| 22 | Job notification compatibility hook | `PrintWorkflowJobUISession.JobNotification`, `PrintWorkflowJobBackgroundSession.JobIssueDetected` | App Job UI + workflow task | Tracked only. Defensive handlers record OS error-toast/job-issue activations; not a supported virtual-printer behavior until a deterministic E2E exists. |
| 23 | Graceful cancel / abort / fail | `PrintWorkflowSubmittedStatus`, `AbortPrintFlow(PrintWorkflowJobAbortReason.*)` | All tasks | E2E asserts Job UI cancel and corrupt-image transform failure |
| 24 | Job password option model | `JobPasswordOptions` settings model | Core + Job UI | Job UI capture is tested; virtual file output records metadata as not applicable without exposing the secret; no physical target-stream application |
| 25 | Localized printer queue display names | `DisplayName="ms-resource:…"` + `.resw` | Manifest + Strings | E2E requires the expected resource keys and resolved installed queue names |
| 26 | IPP communication-error timeout recovery | `PrintSupportExtensionSession.CommunicationErrorDetected`, `PrintSupportIppCommunicationConfiguration` | `PrintSupportExtensionBackgroundTask` | Tracked only. Defensive handler configures IPP timeouts when Windows reports a timeout; not a supported feature claim until a deterministic real-device E2E can trigger the event. |
| 27 | IPP compression compatibility handling | `PrintWorkflowJobStartingEventArgs.IsIppCompressionEnabled`, `DisableIppCompressionForJob()` | `PrintSupportWorkflowBackgroundTask` + E2E | Real IPP workflow activation records the platform compression state and keeps system rendering enabled. |
| 28 | PDL passthrough with IPP job-attribute compatibility | `SetPdlPassthroughWithJobAttributesSupported`, `IPdlPassthroughProvider2`, `PrintWorkflowPrinterJob3` | `PrintSupportExtensionBackgroundTask` + `PrintSupportWorkflowBackgroundTask` + `PrintSink.App` | Tracked only. PrintSink enables the capability and uses a scoped CsWinRT projection for `IppAttributeConverter` / `IPdlPassthroughProvider2`. When the live runtime accepts provider-v2 submission, CI records the attribute source and encoded IPP job/operation buffer sizes; if printer-specific print-ticket conversion fails, PrintSink falls back to a minimal standards-shaped IPP attribute set before falling back to provider v1. When Windows reports provider-v2 as unsupported or unusable, CI records the explicit v1 fallback and HRESULT instead of claiming row 28 support. Physical workflow passthrough-attribute state is recorded only when Windows delivers `PdlModificationRequested`. |

---

## 5. Solution and project layout

```
print-sink/
├─ PrintSink.slnx                       # XML-based solution (modern format)
├─ Directory.Build.props                # shared: net10.0-windows10.0.26100.0, Nullable, analyzers-as-errors, LangVersion
├─ Directory.Packages.props             # Central Package Management (pinned versions)
├─ .editorconfig                        # one-type-per-file & doc-comment rules enforced
├─ global.json                          # pin .NET 10 SDK
├─ docs/
│  ├─ DESIGN.md                         # this document
│  ├─ TESTING.md                        # test plan + automated E2E runbook
│  └─ BUILD.md                          # MSBuild/VS build & deploy steps
├─ src/
│  ├─ PrintSink.App/                    # WinUI 3 + Reactor packaged app (Single-project MSIX)
│  │  ├─ Package.appxmanifest
│  │  ├─ app.manifest
│  │  ├─ App.cs                           # top-level Reactor entry point
│  │  ├─ AppRoot.cs                       # Reactor root component + shell routing
│  │  ├─ AppActivation*.cs                # activation route parsing and state
│  │  ├─ ManagementScreen.cs              # management / diagnostics shell
│  │  ├─ SettingsScreen.cs                # printSupportSettingsUI surface
│  │  ├─ JobPreviewScreen.cs              # printSupportJobUI surface
│  │  ├─ WinRtPrintSource*.cs             # packaged WinRT print-source E2E harness
│  │  ├─ Config/  PrinterPdf.pdc.xml, PrinterPdf.pdr.xml, … (one per endpoint)
│  │  ├─ Strings/<lang>/Resources.resw, IppMediaTypes.resw, CustomMediaTypes.resw
│  │  └─ Assets/
│  ├─ PrintSink.Cli/                     # System.CommandLine + Hex1b companion app
│  │  ├─ Program.cs
│  │  ├─ Commands/                        # queue, manifest, pdc, ticket, sink, tui
│  │  └─ Tui/                             # Hex1b views and terminal state
│  ├─ PrintSink.Tasks/                   # CsWinRT component (WinRT.Host.dll producer)
│  │  ├─ VirtualPrinterBackgroundTask.cs
│  │  ├─ PrintSupportWorkflowBackgroundTask.cs
│  │  └─ PrintSupportExtensionBackgroundTask.cs
│  ├─ PrintSink.Core/                    # pure .NET, no print-stack dependency
│  │  ├─ Pdl/        (PdlFormat.cs, PdlRouter.cs, IPdlConverter.cs, …)
│  │  ├─ Endpoints/  (VirtualEndpoint.cs, EndpointKind.cs, ISink.cs, FileSink.cs, CloudSink.cs)
│  │  ├─ Capabilities/ (PrintDeviceCapabilitiesEditor.cs, CustomFeature.cs, MediaSize.cs, …)
│  │  ├─ Tickets/    (IppAttributeMapper.cs, AttributeMergePolicyOptions.cs, …)
│  │  ├─ Watermark/  (WatermarkOptions.cs, TextWatermark.cs, ImageWatermark.cs)
│  │  ├─ Settings/   (ISettingsStore.cs, LocalSettingsStore.cs)
│  │  └─ Abstractions/ (IVirtualPrinterJob.cs, IPrintTicket.cs, … adapters over WinRT)
│  ├─ PrintSink.Xps/                     # C++/WinRT XPS-OM component (.vcxproj)
│  │  ├─ XpsPageWatermarker.{h,cpp,idl}
│  │  ├─ XpsSequentialDocument.{h,cpp,idl}
│  │  ├─ XpsPageWrapper.{h,cpp,idl}
│  │  └─ SynchronizedSequentialStream.{h,cpp,idl}
│  └─ PrintSink.Xps.Projections/         # C# projection of PrintSink.Xps (.csproj)
└─ tests/
   ├─ PrintSink.Core.Tests/              # MSTest on Microsoft.Testing.Platform (.NET 10)
   ├─ PrintSink.Cli.Tests/               # command parsing, output, and Hex1b state tests
   ├─ PrintSink.Xps.Tests/               # exercises XPS-OM via the C# projection
   └─ PrintSink.App.Tests/               # packaged WinUI test app (in-package, MTP)
```

**Naming:** root namespace `PrintSink`, with logical sub-namespaces (`PrintSink.Core.Pdl`,
`PrintSink.Cli.Commands`, `PrintSink.Tasks`, `PrintSink.App.Screens`, `PrintSink.Xps`). The
activatable-class IDs in the manifest therefore read `PrintSink.Tasks.VirtualPrinterBackgroundTask`,
etc. Assembly name = project name; the root namespace token is always `PrintSink`.

### 5.1 Project settings (key properties)

`PrintSink.Tasks.csproj`
```xml
<PropertyGroup>
  <TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
  <Nullable>enable</Nullable>
  <UseWinUI>true</UseWinUI>
  <CsWinRTComponent>true</CsWinRTComponent>
  <CsWinRTWindowsMetadata>10.0.26100.0</CsWinRTWindowsMetadata>
</PropertyGroup>
```

`PrintSink.App.csproj` (single-project MSIX, self-contained Windows App SDK)
```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
  <TargetPlatformMinVersion>10.0.26100.0</TargetPlatformMinVersion>
  <RootNamespace>PrintSink</RootNamespace>
  <Platforms>x64;ARM64</Platforms>
  <RuntimeIdentifiers>win-x64;win-arm64</RuntimeIdentifiers>
  <UseWinUI>true</UseWinUI>
  <EnableMsixTooling>true</EnableMsixTooling>
  <WindowsPackageType>MSIX</WindowsPackageType>
  <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
  <Nullable>enable</Nullable>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Microsoft.UI.Reactor" />
</ItemGroup>
```

The Reactor template defaults to an unpackaged development app. PrintSink keeps the Reactor entry point
and component model, but the project is explicitly MSIX-shaped because the PSA contracts require package
identity and manifest extensions.

`PrintSink.Cli.csproj`
```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <RootNamespace>PrintSink.Cli</RootNamespace>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="System.CommandLine" />
  <PackageReference Include="Hex1b" />
</ItemGroup>
```

---

## 6. MSIX manifest design

The manifest is the single source of truth for which queues exist and which contracts route to which
entry points. PrintSink declares **six virtual printers** to exercise every code path, plus the three
shared PSA contracts, plus the in-process WinRT activation hosts.

```xml
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  xmlns:printsupport="http://schemas.microsoft.com/appx/manifest/printsupport/windows10"
  xmlns:printsupport2="http://schemas.microsoft.com/appx/manifest/printsupport/windows10/2"
  IgnorableNamespaces="uap uap10 rescap printsupport printsupport2">

  <Applications>
    <Application Id="App" Executable="PrintSink.exe" EntryPoint="$targetentrypoint$"
                 uap10:SupportsMultipleInstances="true">
      <uap:VisualElements DisplayName="PrintSink" .../>
      <Extensions>

        <!-- Shared PSA contracts (foreground UI + background extension) -->
        <printsupport:Extension Category="windows.printSupportSettingsUI" EntryPoint="PrintSink.App"/>
        <printsupport:Extension Category="windows.printSupportJobUI"      EntryPoint="PrintSink.App"/>
        <printsupport:Extension Category="windows.printSupportExtension"
                                EntryPoint="PrintSink.Tasks.PrintSupportExtensionBackgroundTask"/>
        <printsupport:Extension Category="windows.printSupportWorkflow"
                                EntryPoint="PrintSink.Tasks.PrintSupportWorkflowBackgroundTask"/>

        <!-- Virtual printer queues (Software Endpoints) -->

        <!-- (a) OXPS → PDF, file output -->
        <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow"
                                 EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
          <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:PrinterPdfName"
              PrinterUri="printsink:print-to-pdf" PreferredInputFormat="application/oxps"
              OutputFileTypes="pdf" PdcFile="Config\PrinterPdf.pdc.xml" PdrFile="Config\PrinterPdf.pdr.xml">
            <printsupport2:SupportedFormats>
              <printsupport2:SupportedFormat Type="application/pdf" MaxVersion="1.7"/>
            </printsupport2:SupportedFormats>
          </printsupport2:PrintSupportVirtualPrinter>
        </printsupport2:Extension>

        <!-- (b) OXPS passthrough → XPS file -->
        <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow"
                                 EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
          <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:PrinterXpsName"
              PrinterUri="printsink:print-to-xps" PreferredInputFormat="application/oxps"
              OutputFileTypes="xps;oxps" PdcFile="Config\PrinterXps.pdc.xml" PdrFile="Config\PrinterXps.pdr.xml">
            <printsupport2:SupportedFormats>
              <printsupport2:SupportedFormat Type="application/oxps" MaxVersion="1.0"/>
              <printsupport2:SupportedFormat Type="application/vnd.ms-xpsdocument" MaxVersion="1.0"/>
            </printsupport2:SupportedFormats>
          </printsupport2:PrintSupportVirtualPrinter>
        </printsupport2:Extension>

        <!-- (c) PostScript file output -->
        <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow"
                                 EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
          <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:PrinterPsName"
              PrinterUri="printsink:print-to-ps" PreferredInputFormat="application/postscript"
              OutputFileTypes="ps" PdcFile="Config\PrinterPostScript.pdc.xml" PdrFile="Config\PrinterPostScript.pdr.xml">
            <printsupport2:SupportedFormats>
              <printsupport2:SupportedFormat Type="application/postscript" MaxVersion="3.0"/>
            </printsupport2:SupportedFormats>
          </printsupport2:PrintSupportVirtualPrinter>
        </printsupport2:Extension>

        <!-- (d) Cloud sink (NO OutputFileTypes → no Save-As dialog) -->
        <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow"
                                 EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
          <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:PrinterCloudName"
              PrinterUri="printsink:print-to-cloud" PreferredInputFormat="application/oxps"
              PdcFile="Config\PrinterCloud.pdc.xml" PdrFile="Config\PrinterCloud.pdr.xml">
            <printsupport2:SupportedFormats>
              <printsupport2:SupportedFormat Type="application/pdf" MaxVersion="1.7"/>
            </printsupport2:SupportedFormats>
          </printsupport2:PrintSupportVirtualPrinter>
        </printsupport2:Extension>

        <!-- (e) PWG-Raster file output (raster pipeline) -->
        <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow"
                                 EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
          <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:PrinterPwgrName"
              PrinterUri="printsink:print-to-pwgr" PreferredInputFormat="application/oxps"
              OutputFileTypes="pwgr" PdcFile="Config\PrinterPwgRaster.pdc.xml" PdrFile="Config\PrinterPwgRaster.pdr.xml"/>
        </printsupport2:Extension>

        <!-- (f) PCLm file output (mobile raster pipeline) -->
        <printsupport2:Extension Category="windows.printSupportVirtualPrinterWorkflow"
                                 EntryPoint="PrintSink.Tasks.VirtualPrinterBackgroundTask">
          <printsupport2:PrintSupportVirtualPrinter DisplayName="ms-resource:PrinterPclmName"
              PrinterUri="printsink:print-to-pclm" PreferredInputFormat="application/oxps"
              OutputFileTypes="pclm" PdcFile="Config\PrinterPclm.pdc.xml" PdrFile="Config\PrinterPclm.pdr.xml"/>
        </printsupport2:Extension>
      </Extensions>
    </Application>
  </Applications>

  <Capabilities>
    <rescap:Capability Name="runFullTrust"/>
    <Capability Name="privateNetworkClientServer"/>
  </Capabilities>

  <!-- In-process WinRT activation of background tasks (hosted by CsWinRT WinRT.Host.dll) -->
  <Extensions>
    <Extension Category="windows.activatableClass.inProcessServer">
      <InProcessServer>
        <Path>WinRT.Host.dll</Path>
        <ActivatableClass ActivatableClassId="PrintSink.Tasks.VirtualPrinterBackgroundTask"      ThreadingModel="both"/>
        <ActivatableClass ActivatableClassId="PrintSink.Tasks.PrintSupportWorkflowBackgroundTask" ThreadingModel="both"/>
        <ActivatableClass ActivatableClassId="PrintSink.Tasks.PrintSupportExtensionBackgroundTask" ThreadingModel="both"/>
      </InProcessServer>
    </Extension>
    <Extension Category="windows.activatableClass.inProcessServer">
      <InProcessServer>
        <Path>PrintSink.Xps.dll</Path>
        <ActivatableClass ActivatableClassId="PrintSink.Xps.XpsPageWatermarker"   ThreadingModel="both"/>
        <ActivatableClass ActivatableClassId="PrintSink.Xps.XpsSequentialDocument" ThreadingModel="both"/>
      </InProcessServer>
    </Extension>
  </Extensions>
</Package>
```

**Manifest attribute rules enforced by design** (from the MSIX Print Support Virtual Printer spec):

- `PreferredInputFormat` ∈ {`application/oxps`, `application/postscript`}; default OXPS. Anything else
  fails installation — PrintSink only emits these two.
- `OutputFileTypes` present ⇒ file printer + Save-As dialog ⇒ `GetTargetFileAsync()` returns a real
  `StorageFile`. Absent ⇒ custom sink, no Save-As (cloud endpoint (d)).
- `SupportedFormat MaxVersion` must be `Major.Minor` numeric — validated in the PDC/manifest lint step.
- `PdcFile` mandatory and must be valid PDC XML — install fails otherwise; covered by manifest/PDC
  linting and signed-package E2E.
- `PdrFile` optional; present only where we ship localized custom features.
- Initial package PDC files stay inside the Windows-provisioned static subset: custom media size
  options use the observed `PortraitImageableSize`, `MediaSizeHeight`, `MediaSizeWidth` order, and
  package-root PrintSink custom features are job-scoped (`JobWatermarkMode`). Wider ticket features
  remain in the Core mapper and extension path instead of being forced into the initial PDC shape.

---

## 7. Detailed component design

### 7.1 `PrintSink.Tasks.VirtualPrinterBackgroundTask` (the core sink)

Implements `IBackgroundTask`. On `Run`:

1. Cast `taskInstance.TriggerDetails` to `PrintWorkflowVirtualPrinterTriggerDetails`; take a
   `BackgroundTaskDeferral`.
2. Get `PrintWorkflowVirtualPrinterSession`; capture `session.Printer` (`IppPrintDevice`).
3. **Subscribe `VirtualPrinterDataAvailable` before `session.Start()`** (strict ordering — late
   subscription loses the event).

On `VirtualPrinterDataAvailable(args)`:

1. Resolve the endpoint from `printDevice.PrinterUri` → `EndpointKind` (PrintSink.Core).
2. Read `args.SourceContent` (`ContentType`, `GetInputStream()`).
3. Decide whether UI is needed (`IVirtualPrinterPolicy.IsUiRequired(printTicket, endpoint)`); if so and
   `args.UILauncher.IsUILaunchEnabled()`, call `LaunchAndCompleteUIAsync()` and honor
   `UserCanceled` → `Canceled`.
4. Acquire the sink:
   - file endpoints: `await args.GetTargetFileAsync()` → open R/W random-access stream;
   - cloud endpoint: construct `CloudSink` (no target file).
5. Transform per (source content type, endpoint):
   - **OXPS → PDF/PWGR/PCLm**: optionally watermark via `PrintSink.Xps`, then
     `args.GetPdlConverter(conversionType).ConvertPdlAsync(args.GetJobPrintTicket(), input, output)`.
   - **PDF/PS passthrough** (declared `SupportedFormat`): `RandomAccessStream.CopyAndCloseAsync`.
   - **OXPS → XPS file**: copy.
6. `finally` → `args.CompleteJob(status)`; complete the deferral.

The handler is a thin adapter; all branching/decision logic lives in `PrintSink.Core` behind
`IVirtualPrinterJob`, `IPdlRouter`, and `IPdlTransformer` so it is unit-testable. The task class itself
only marshals WinRT objects into and out of the core.

**Conversion type mapping** (`PrintSink.Core.Pdl.PdlRouter`):

| Source | Endpoint target | Action |
| --- | --- | --- |
| `application/oxps` | pdf | `XpsToPdf` (+ optional watermark) |
| `application/oxps` | pwgr | `XpsToPwgr` |
| `application/oxps` | pclm | `XpsToPclm` |
| `application/oxps` | xps/oxps | copy |
| `application/pdf` | pdf | copy (passthrough) |
| `application/postscript` | ps | copy (passthrough) |

### 7.2 `PrintSink.Tasks.PrintSupportExtensionBackgroundTask`

Subscribes (before `Start`): `PrintTicketValidationRequested`, `PrintDeviceCapabilitiesChanged`, and
(API-gated) `PrinterSelected` and `CommunicationErrorDetected`. Implements:

- **Print-ticket validation** → `SetPrintTicketValidationStatus(WorkflowPrintTicketValidationStatus.Resolved)`
  after running `PrintSink.Core.Tickets` constraint checks.
- **PDC regeneration** — delegates to `PrintSink.Core.Capabilities.PrintDeviceCapabilitiesEditor`, which
  adds a custom namespace and injects custom features (media size/type, resolution, input/output bins,
  staple, page order, etc.) into the live `XmlDocument`, then `UpdatePrintDeviceCapabilities`. The editor
  is a pure function `(XmlDocument, IReadOnlyList<CustomFeature>) → XmlDocument`, fully unit tested.
  Real capability-refresh diagnostics record the applied custom feature and option names.
- **PDR localization** — when `GetCurrentPrintDeviceResources` is present, walks `.resw` subtrees under
  the `ResourceLanguage` context and inserts any missing localized strings, then
  `UpdatePrintDeviceResources`. Real capability-refresh diagnostics record the localized resource names.
- **MXDC image quality** — sets `args.MxdcImageQualityConfiguration` text/draft/normal/high/photo/auto/fax
  to chosen `XpsImageQuality` values and records the full mapping during real capability refresh.
- **Printer-selected adaptive card** — builds an Adaptive Card, records the selected queue and card version,
  and requests `PageMediaType`, `PageOutputQuality`, and `JobCopiesAllDocuments` within
  `AllowedAdditionalFeaturesAndParametersCount`.
- **IPP communication timeout recovery** — when Windows raises `CommunicationErrorDetected` for a
  timeout, updates `PrintSupportIppCommunicationConfiguration` timeouts when the platform allows it and
  records diagnostic evidence.

**Concurrency hardening** (carried from the reference sample, mandatory in-process): a
`RunHandlerSafely` wrapper with a `volatile bool _isCancelled`, an `Interlocked` active-handler counter,
and `Canceled` handling, so a torn-down session (`0x3E3`) never propagates into the app process and the
deferral is completed exactly once.

### 7.3 `PrintSink.Tasks.PrintSupportWorkflowBackgroundTask`

The physical-printer PSA path is an association and activation probe for
`windows.printSupportWorkflow`. It does not replace the physical printer's output stream.

- `JobStarting` records workflow activation and leaves system rendering enabled.
- `PdlModificationRequested` honors Job UI cancel/fail semantics, consumes per-job UI options so they
  cannot leak to the next virtual-printer job, and records pass-through diagnostics if Windows delivers
  that event.
- `JobIssueDetected` records issue kind, HRESULT, toast-skip state, and UI-launch availability when
  Windows raises the event. It remains a tracked compatibility hook until a deterministic E2E can
  trigger it.
- The task does not call `SetSkipSystemRendering` or `CreateJobOnPrinter[WithAttributes]`.
- Package-local diagnostics always record physical workflow start and failure/cancel. Physical
  pass-through diagnostics are optional evidence until target-stream replacement is a supported feature.

### 7.4 `PrintSink.App` — Reactor shell + activation router

`App.cs` is the top-level Reactor entry point. It starts `ReactorApp.Run<AppRoot>()`, and `AppRoot`
owns the WinUI window, shell state, and screen selection. Activation is still resolved through
`AppInstance.GetCurrent().GetActivatedEventArgs().Kind`, but the result is routed to Reactor screens
instead of XAML pages:

- `Launch` → **ManagementScreen** (diagnostics: list installed PrintSink queues, install/remove
  virtual queues, get/set `UserDefaultPrintTicket`, trigger `RefreshPrintDeviceCapabilities`, IPP URL
  info).
- `PrintSupportSettingsUI` → **SettingsScreen**, created **modal to `OwnerWindowId`** via
  `Win32Interop.GetWindowFromWindowId` (the v4 WinAppSDK requirement).
- `PrintSupportJobUI` → **JobPreviewScreen**; subscribes
  `PrintWorkflowJobUISession.{PdlDataAvailable, JobNotification, VirtualPrinterUIDataAvailable}` then
  `session.Start()`. `VirtualPrinterUIDataAvailable` records the real job title, source application,
  and source PDL content type, renders a preview from `args.SourceContent`, and persists user choices
  (watermark options and job-password metadata) to `ISettingsStore` for the background task to read
  back. Virtual file outputs record password metadata as not applicable instead of sending it anywhere,
  and the secret must not appear in diagnostics or generated documents.
  `JobNotification` records job status/error context if Windows activates the app from a job
  notification toast; it is a tracked deferred compatibility hook, not part of the supported
  virtual-printer flow.
- Headless automation sets package-local `JobUiOptions` through `printsink-app.exe --disable-job-ui`.
  Background tasks then skip `LaunchAndCompleteUIAsync` and process jobs directly. The normal default is
  restored with `printsink-app.exe --enable-job-ui`.

The UI is made from small one-type-per-file Reactor components in `PrintSink.App`. State that must
cross the UI/background boundary lives in `PrintSink.Core` models and stores, not in WinUI controls.

Because the background task cannot mutate XPS while the UI is up, the UI→background handshake is: UI
collects options → writes to local settings → returns `Completed` → background task reads options and
performs the XPS-OM transform. This mirrors the documented constraint that only the background task may
change PDL data.

### 7.5 `PrintSink.Xps` (C++/WinRT)

- `XpsPageWatermarker` — `SetWatermarkText(text, fontSize, xOffset, yOffset)`,
  `SetWatermarkImage(path, dpiX, dpiY, w, h)`, `SetWatermarkImageEnabled(bool)`.
- `XpsSequentialDocument` — wraps `PrintWorkflowObjectModelSourceFileContent`, raises
  `XpsGenerationFailed`, exposes `GetWatermarkedStream(watermarker) → IInputStream`.
- `XpsPageWrapper`, `SynchronizedSequentialStream` — page iteration + thread-safe streaming between the
  XPS-OM producer thread and the consumer.

Consumed from C# through `PrintSink.Xps.Projections` (CsWinRT-projected). Registration-free activation
via a side-by-side manifest in the package.

### 7.6 `PrintSink.Core` (the testable heart)

Pure .NET. No `Windows.Graphics.Printing.*` event types leak in; instead thin **abstractions**:

```
IVirtualPrinterJob   { ContentType, EndpointUri, GetInput(), GetTarget(), GetPrintTicket(), Complete(status) }
IPdlRouter           { PdlPlan Resolve(string contentType, VirtualEndpoint endpoint) }
IPdlTransformer      { TransformAsync(Stream pdl, VirtualEndpoint endpoint, PdlPlan plan, WatermarkOptions options) }
IXpsWatermarker      { ApplyAsync(Stream xps, PdlFormat sourceFormat, WatermarkOptions options) }
IPrintDeviceCapabilitiesEditor { XmlDocument Apply(XmlDocument pdc, IReadOnlyList<CustomFeature> features) }
IIppAttributeMapper  { IDictionary<string,IppAttributeValue> FromPrintTicket(...); ... Remove(...); }
ISettingsStore       { read/write watermark + job options }
ISink                { Task WriteAsync(Stream pdl, ...) }  → FileSink, CloudSink
```

The `Tasks` classes implement adapters that wrap the real WinRT args into these interfaces. This is what
makes ~90% of the logic unit-testable off the print stack (see §9).

### 7.7 `PrintSink.Cli` — command line + TUI

`PrintSink.Cli` is a normal .NET console app. It is useful before the package is installed, during
support calls, and in CI checks that should not start a WinUI process.

- `System.CommandLine` commands:
  - `queues` — list expected and installed PrintSink queues when the local print stack is available.
  - `queues install` / `queues remove` — provision or remove the virtual-printer queues through the
    packaged app execution alias so the Windows API runs under package identity.
  - `manifest lint` — validate package manifest entries, preferred input formats, passthrough formats,
    PDC/PDR paths, and endpoint consistency.
  - `pdc validate` — validate PDC/PDR XML and custom feature wiring.
  - `ticket map` — convert a fixture print ticket into the IPP attribute model and show merge results.
  - `sink test` — run a fixture PDL stream through the selected sink without a live PSA activation.
  - `tui` — start the Hex1b terminal UI.
- Hex1b TUI:
  - queue and endpoint list;
  - manifest/PDC validation status;
  - recent diagnostics/events from the local store;
  - focusable operator actions for refreshing the dashboard and installing/removing virtual queues
    through the packaged app execution alias;
  - fixture-driven route and sink test runner.

The CLI/TUI uses `PrintSink.Core` abstractions and OS/package tooling. It does not try to instantiate
live PSA event objects; those stay behind the activation adapters. The TUI is a Hex1b app workload:
console presentation for normal use, headless presentation for tests and automation. The `hex1b` tool
and Hex1b MCP server are acceptable support tooling for inspecting widget tree/focus/state, sending
keys or mouse input, capturing terminal output, and scripting assertions.

---

## 8. Cross-cutting concerns

- **Lifecycle / deferrals:** every background entry takes the task deferral first and completes it
  exactly once in `finally`; event deferrals (`args.GetDeferral()`) are always completed. In-process
  cancellation races are handled with the `Interlocked`/`volatile` pattern (§7.2).
- **Threading:** XPS-OM runs on its own thread; `SynchronizedSequentialStream` bridges to the converter.
  UI marshals via the WinUI dispatcher.
- **Error model:** transform failures → `CompleteJob(Failed)` / `AbortPrintFlow(JobFailed)`; user cancel
  → `Canceled`/`UserCanceled`. No exception is allowed to escape an in-process handler.
- **Security:** least-privilege capabilities (`runFullTrust` for the packaged PSA process and
  `privateNetworkClientServer` for IPP printer communication). Target files are only those the user
  selected via the OS Save-As broker (no arbitrary path access). Job-password settings store only
  encrypted/hash-ready values; virtual file output records their presence without applying or exposing
  them, and PrintSink does not apply them to a physical workflow target stream.
- **Localization:** queue display names and custom PDC features via `.resw` (`ms-resource:`), resolved
  against `ResourceLanguage`.
- **Observability:** `EventSource`/ETW tracing in `PrintSink.Core` (provider `PrintSink-Diagnostics`) for
  job lifecycle, format decisions, conversion timings — usable with WPR/PerfView for field diagnosis.
  A small package-local JSON event store keeps recent job diagnostics available to the Hex1b TUI and
  serializes writes across the app, extension task, workflow task, and virtual-printer task so concurrent
  activations do not lose route or completion evidence.
- **Versioning:** package `Version` Major.Minor.Build.Revision; `AppxAutoIncrementPackageRevision` on
  publish.

---

## 9. Testing strategy

Microsoft's current guidance for WinUI 3 / Windows App SDK is **MSTest running on the
Microsoft.Testing.Platform (MTP)**, including the **packaged WinUI MSTest app template** for tests that
must run inside the app's package identity. PrintSink uses MTP/MSTest on **.NET 10** throughout.

### 9.1 Test layers

1. **Unit tests — `PrintSink.Core.Tests`** (MSTest / MTP, plain .NET, runs under `dotnet test` and in
   CI). Covers the bulk of the logic because Core has no print-stack dependency:
   - `PdlRouter`: every (content type × endpoint) → expected `PdlPlan` (conversion type / copy /
     reject). Table-driven `[DataRow]`.
   - `PrintDeviceCapabilitiesEditor`: golden-file tests — input PDC + custom features → expected PDC
     XML (custom media size/type/resolution/bins/staple/page-order; namespace injection; idempotency).
   - `IppAttributeMapper`: print-ticket → attributes; `media-size` removal; merge-policy behavior.
   - `WatermarkOptions` round-trip through `ISettingsStore` (in-memory + local).
   - Manifest lint: validate `PreferredInputFormat` ∈ allowed set, `MaxVersion` numeric, `PdcFile`
     present, `OutputFileTypes` ↔ endpoint kind consistency (a unit test over the shipped manifest +
     PDC files).
2. **CLI tests — `PrintSink.Cli.Tests`** (MSTest / MTP, plain .NET): cover command parsing, exit codes,
   output formatting, manifest/PDC validation commands, and Hex1b state transitions without requiring a
   real terminal session. Hex1b tests use a headless terminal, input sequences or an automator, and
   terminal snapshots so assertions are made against the rendered surface.
3. **Component tests — `PrintSink.Xps.Tests`** (MTP, x64/ARM64): drive the C++/WinRT component through
   its C# projection with a small fixture OXPS document; assert the watermarked stream is non-empty,
   parses as valid XPS, and contains the watermark glyph run / image part. `XpsGenerationFailed` path
   asserted with a corrupt fixture.
4. **Packaged app tests — `PrintSink.App.Tests`** (packaged WinUI MSTest app, MTP): run with package
   identity to validate activation routing logic, `OwnerWindowId` modality wiring, settings persistence
   visible across the UI/background boundary, and Reactor screen behavior.
5. **End-to-end print-stack automation — `docs/TESTING.md` + `tests/e2e`.** The live PSA activation
   (real spooler, broker, OS rendering to OXPS, Save-As broker) cannot be faithfully mocked, so E2E runs
   as scripted Windows automation in CI and on clean Windows 11 26100+ VMs:
   - Build and install the signed MSIX, run `printsink-app.exe --install-virtual-printers`, and assert
     all six queues appear (`Get-Printer`).
   - Launch the packaged management UI, inspect it through UI Automation, invoke queue lifecycle,
     queue-refresh, capability-refresh, default-copy, and Job UI mode actions, and assert the real
     Windows printer list plus package diagnostics reflect each action.
   - Assert the signed manifest keeps `uap10:SupportsMultipleInstances="true"` and submit overlapping
     real print jobs so concurrent activations are proven by output files and diagnostics.
   - Print from a real Win32 print harness to every endpoint, and print a real Notepad `/p` text
     document to `PrintSink - PDF`.
   - Assert route diagnostics for every real job, including source content type, target format, copy
     versus conversion action, and route reason. Output validation waits for the package-local
     `Job completed` event; the event carries route details so completion evidence is self-contained.
   - Assert `PrintSupportExtensionBackgroundTask` diagnostics from real OS activations: ticket
     validation on every queue, capability refresh with custom features, PDR update, MXDC image
     quality configuration, and printer-selected Adaptive Card 1.0 setup with `PageMediaType`,
     `PageOutputQuality`, and `JobCopiesAllDocuments`.
   - Assert capability-refresh causality: management UI refresh requests must be followed by a later
     `Capabilities updated` diagnostic from `PrintSupportExtensionBackgroundTask`.
   - Assert localized queue-name evidence: each virtual printer manifest entry must use the expected
     `ms-resource:` display-name key and Windows must report each resolved installed queue name.
   - Set and restore `IppPrintDevice.UserDefaultPrintTicket` for a real virtual queue, then assert the
     persisted default copy count through package-local diagnostics.
   - Assert `IppPrintDevice.GetPrinterAttributes` against a real virtual queue exposes no usable
     `document-format-default` or `document-format-supported` values, matching the PSA v4 platform
     behavior for virtual printers.
   - Generate and sign a temporary PSA extension INF, install it with `pnputil`, connect local
     Microsoft IPP Class Driver queues to the in-process E2E IPP printer, assert the installed PSA AUMID
     device property, prove stopped/rejecting IPP state traffic, prove the extension task validates
     real print tickets for that queue, and submit a real print job that records physical workflow
     start/compression state. Physical pass-through is recorded only when Windows delivers
     `PdlModificationRequested`; document-output assertions run through the PrintSink virtual queues.
   - Assert real outputs: PDF and PCLm open with PDFPig; PDF text contains `foo`; the Notepad `/p`
     PDF path is non-empty and contains `foo`; XPS/OXPS is an OPC
     package with XPS content types, parseable fixed pages, interleaved piece support, and `foo`; PS
     starts with `%!PS` and has resolved page count, bounding box, page trailer, trailer, and EOF markers;
     PWG Raster has valid raster magic, sane page header fields, and a non-blank page body; PCLm has
     PDF/PCLm markers and raster image content; cloud has no Save-As output but writes a package-local
     sink artifact that is validated as PDF output.
   - Send a real source PDF through Windows' PDL passthrough provider and assert the output is
     byte-for-byte identical to the input PDF.
   - Launch the packaged WinRT print-source harness, drive the real Windows print dialog to
     `PrintSink - PDF`, and assert a valid PDF containing the source text.
   - Assert virtual-printer DisplayName resources in the signed manifest and verify the installed
     queue names that Windows reports.
   - Launch Settings UI from the real Windows print dialog and assert owner-window modality: the print
     dialog is disabled while Settings is open and restored when Settings closes.
   - Settings/defaults: set package-local endpoint text and image watermarks, call
     `IppPrintDevice.RefreshPrintDeviceCapabilities`, print real jobs with Job UI disabled, and assert
     the default watermarks appear in the outputs.
   - Failure path: configure a corrupt package-local image watermark, print a real PDF job with Job UI
     disabled, and assert `Job failed` with exception/HRESULT detail, no output, and queue persistence.
   - Job UI: assert virtual-printer PDL metadata for the real job title, source application, and OXPS
     content type is received; assert watermark changes are applied; and assert cancel aborts the real
     print flow while leaving the selected target empty and recording `Job canceled`.
   - Queue persistence: after provisioning, management UI inspection, extension refresh, default-ticket
     edits, every real print path, Settings UI, Job UI complete, and Job UI cancel, assert all six
     queues still appear in `Get-Printer`.
   - Required additions for any feature-bearing change: if ManagementScreen, Settings UI, PDC refresh,
     passthrough, source printing, or a new sink behavior changes, add the corresponding real E2E
     assertion in the same commit.

### 9.2 Test tooling

- `global.json` opts `dotnet test` into the .NET 10 `Microsoft.Testing.Platform` runner.
- `Directory.Packages.props` pins: `MSTest` (including MTP),
  `Microsoft.Testing.Extensions.CodeCoverage`, `Microsoft.Windows.CsWinRT`, `Microsoft.WindowsAppSDK`,
  `Microsoft.UI.Reactor`, `System.CommandLine`, and `Hex1b`.
- Coverage gate via MTP code-coverage extension; **Core ≥ 90%** line coverage (it holds the logic that
  matters); Tasks/App excluded from the hard gate (thin adapters / require live stack).
- CI runs unit + Xps + packaged-app tests on Windows runners (MSBuild, x64 and ARM64), then calls
  `.\test-e2e.ps1 -BuildPackage` to build a signed MSIX and run the scripted real print-stack E2E
  suite against the installed package.

---

## 10. Build, packaging, deployment

- **Build:** Visual Studio 2026 or the root build script (`.\build.ps1 -Configuration Release`).
  `dotnet` CLI is **not** supported for the full solution (C++/WinRT). Unit-test projects that don't
  reference the native component still run under `dotnet test`; `PrintSink.Cli` also builds and runs
  under `dotnet run`.
- **Build order:** `PrintSink.Xps` (.winmd+.dll) → `PrintSink.Xps.Projections` → `PrintSink.Core` →
  `PrintSink.Tasks` (.winmd + WinRT.Host.dll) → `PrintSink.App` (packages everything) plus
  `PrintSink.Cli`. MSBuild targets
  copy `PrintSink.Tasks.winmd` and the XPS side-by-side manifest into the package output, as in the
  reference sample.
- **Packaging:** single-project MSIX (`EnableMsixTooling`, `WindowsPackageType=MSIX`,
  `WindowsAppSDKSelfContained=true`). Requires the **Single-project MSIX Packaging Tools** VS extension.
- **Signing:** package signed with a trusted cert (`AppxPackageSigningEnabled=true`,
  `PackageCertificateThumbprint`). For lab installs, enable test-signing and trust the dev cert.
- **Architectures:** x64 and ARM64.
- **Install / test:** Add-AppxPackage the signed `.msix`, then run
  `printsink-app.exe --install-virtual-printers` from the app execution alias. Loose development
  registration is useful for F5, but the alias/provisioning path is verified against the signed MSIX.
  For debugging an activation, use VS "Debug Installed App Package" with "Do not launch, but debug my
  code when it starts".

---

## 11. Coding standards (enforced)

- **One type per file** — exactly one `class`/`struct`/`interface`/`enum`/`record`/`delegate` per `.cs`
  file; filename = type name. Enforced via an analyzer rule + an `.editorconfig`/CI lint check.
- **Triple-slash XML on all public members** — `<summary>`, `<param>`, `<returns>`, `<exception>` where
  applicable; `GenerateDocumentationFile=true` and **CS1591 as error** so any undocumented public member
  fails the build.
- **Nullable enabled**; warnings as errors; .NET analyzers + `Microsoft.CodeAnalysis.NetAnalyzers` at
  `AnalysisLevel=latest-all`.
- **Async**: no sync-over-async in new code paths; the background-task adapters that must block (WinRT
  event handlers are `void`) isolate blocking to the task boundary only.
- Central Package Management; `Directory.Build.props` for shared TFM/lang/analyzer settings.

---

## 12. Resolved design decisions (no open questions)

1. **Host stack:** Windows App SDK / WinUI 3 on .NET 10 (not UWP). — Most modern, matches current MS
   sample.
2. **Foreground UI model:** Microsoft.UI.Reactor. — Code-first WinUI keeps the app compact while
   retaining the packaged MSIX shape required by PSA activation.
3. **Native XPS via C++/WinRT** (`PrintSink.Xps`) — required for full watermarking; accept MSBuild-only
   build.
4. **Six virtual printers** (PDF, XPS, PostScript, Cloud, PWG-Raster, PCLm) to exercise file output,
   cloud sink, passthrough, and every PDL converter — full feature coverage, not a single demo queue.
5. **`PrintSink.Core` abstraction layer** so logic is unit-testable off the print stack — chosen over
   testing only via the live stack.
6. **CLI/TUI model:** `System.CommandLine` for scriptable commands and Hex1b for terminal UI. The
   project references the Hex1b NuGet package.
7. **Virtual Printer WinRT projections:** target a Windows App SDK / `Microsoft.Windows.SDK.NET.Ref`
   build that projects the stable virtual-printer surface. The reference sample temporarily
   `#if VIRTUAL_PRINTER_DISABLED`-gated these because an older ref package conflicted with
   `IppPrintDevice`. PrintSink pins the stable projections and adds scoped CsWinRT private projections
   only for newer contract-19 members that exist in Windows metadata but are missing from the managed ref
   assembly.
8. **Testing:** MSTest on Microsoft.Testing.Platform, .NET 10, plus scripted Windows E2E — the current
   Microsoft-recommended stack.
9. **Localization shipped** (display names + custom features via `.resw`/PDR), not deferred.

---

## 13. Milestones

| M | Deliverable |
| --- | --- |
| M0 | Repo scaffolding: `PrintSink.slnx`, `Directory.Build/Packages.props`, `.editorconfig`, `global.json`, analyzer/doc gates, Reactor MSIX shell, CLI package wiring. |
| M1 | `PrintSink.Core` + full unit tests (router, PDC editor, IPP mapper, settings), plus `PrintSink.Cli` commands and tests. |
| M2 | `PrintSink.Xps` + `PrintSink.Xps.Projections` + component tests (watermark fidelity). |
| M3 | `PrintSink.Tasks`: VirtualPrinter + Extension + Workflow background tasks (adapters over Core). |
| M4 | `PrintSink.App`: Reactor activation router, Settings UI (modal), Job UI/preview, Management UI; `PrintSink.Cli tui` Hex1b dashboard. |
| M5 | Manifest (6 queues + 3 contracts + activation hosts), PDC/PDR/`.resw`, single-project MSIX, signing. |
| M6 | Packaged-app tests + E2E automation; CI on Windows runners (x64/ARM64). |
| M7 | Full E2E validation pass on hosted Windows runner and clean VM; docs (`BUILD.md`, `TESTING.md`) finalized. |

**Definition of done:** every supported feature in §4 implemented; rows 22, 26, and 28 keep defensive
handlers plus deferred evidence until Windows exposes deterministic triggers; all
unit/component/packaged tests green; the E2E automation passes for all six queues including PDF
passthrough, WinRT source printing, watermark, settings modality, PDC/PDR/MXDC extension refresh,
printer selection, ticket validation, and cancel paths, plus user default print-ticket updates.

---

## 14. References

- Print Support App v4 API design guide —
  https://learn.microsoft.com/en-us/windows-hardware/drivers/devapps/print-support-app-v4-design-guide
- MSIX Manifest Specification for Print Support Virtual Printer —
  https://learn.microsoft.com/en-us/windows-hardware/drivers/devapps/msix-manifest-specification-print-support-virtual-printer
- End of servicing plan for third-party printer drivers on Windows —
  https://learn.microsoft.com/en-us/windows-hardware/drivers/print/end-of-servicing-plan-for-third-party-printer-drivers-on-windows
- Microsoft print OEM samples: https://github.com/microsoft/print-oem-samples
- Microsoft.UI.Reactor NuGet package: https://www.nuget.org/packages/Microsoft.UI.Reactor
- Hex1b NuGet package: https://www.nuget.org/packages/Hex1b
- System.CommandLine NuGet package: https://www.nuget.org/packages/System.CommandLine
- `Windows.Graphics.Printing.PrintSupport`, `Windows.Graphics.Printing.Workflow`,
  `Windows.Devices.Printers` WinRT namespaces; CsWinRT (`Microsoft.Windows.CsWinRT`).

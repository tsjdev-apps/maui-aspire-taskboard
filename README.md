# .NET MAUI Aspire TaskBoard

A complete .NET 10 sample showing a native .NET MAUI task board, an ASP.NET Core Minimal API, and Aspire development orchestration. Create, edit, complete, delete, and refresh tasks persisted in SQLite. Use the diagnostics page to follow HTTP requests, delays, failures, retries, and timeouts in the Aspire dashboard.

The application also works without Aspire. Aspire adds coordinated startup, endpoint discovery, and observability; it does not replace MVVM, the API, or application deployment.

## Architecture

| Project | Responsibility |
| --- | --- |
| `TaskBoard.Contracts` | DTOs, validation limits, Problem Details fields; no MAUI or Aspire dependency |
| `TaskBoard.Api` | Minimal API, EF Core SQLite, development diagnostics |
| `TaskBoard.ClientCore` | Typed HTTP client, explicit resilience, toolkit ViewModels, English/German resources |
| `TaskBoard.Maui` | XAML pages, Shell navigation, native connectivity, light/dark styling |
| `TaskBoard.ServiceDefaults` | Backend discovery, health checks, OpenTelemetry |
| `TaskBoard.MauiServiceDefaults` | Native discovery and telemetry initialization |
| `TaskBoard.AppHost` | API orchestration and MAUI target registration by project path |
| `tests/*` | Emulator-free unit and isolated SQLite integration tests |

See [architecture](docs/architecture.md) for the dependency diagram and implementation choices.

## Requirements and versions

Versions were checked against NuGet on October 8, 2026.

- .NET SDK **10.0.401**, pinned in `global.json` with patch roll-forward and prereleases disabled.
- .NET MAUI workloads for the platforms you build. Install through Visual Studio or `dotnet workload restore TaskBoard.slnx`.
- Aspire SDK **13.6.1**, resolved automatically by the AppHost SDK.
- `Aspire.Hosting.Maui` **13.6.1-preview.1.26506.6** with matching **13.6.1** Dev Tunnels integration.
- MAUI Controls/Core **10.0.110**, MVVM Toolkit **8.4.2**, EF Core **10.0.12**.
- A trusted development HTTPS certificate: `dotnet dev-certs https --trust`.
- For mobile Aspire targets: Dev Tunnels CLI, a signed-in account, internet access, and configured emulator/simulator tooling.

All ordinary NuGet versions live in `Directory.Packages.props`. The Aspire SDK version belongs in the project SDK declaration. Aspire 13.6 implicitly supplies `Aspire.Hosting.AppHost`; adding a central version for that implicit reference causes NU1009.

The [official Aspire MAUI integration](https://aspire.dev/integrations/dotnet/maui/) is a preview and Visual Studio integration remains incomplete. Keep the hosting preview and SDK compatible when upgrading; review [troubleshooting](docs/troubleshooting.md).

## Supported platforms

| Target | Development host | Additional requirements |
| --- | --- | --- |
| Windows | Windows | Windows SDK, MAUI Windows workload; unpackaged app |
| Android | Windows or macOS (Linux builds where supported by MAUI tooling) | Android SDK, JDK, emulator or device |
| iOS | macOS | Compatible Xcode, iOS workload, simulator; signing for devices |
| Mac Catalyst | macOS | Compatible Xcode and Mac Catalyst workload |

The project includes Apple target frameworks on macOS and Windows targeting on Windows. This avoids trying to build Apple applications locally on Windows. No Windows-to-Mac remote build configuration is supplied.

## Build and test

From the repository root:

```powershell
dotnet --version
dotnet build src/TaskBoard.AppHost/TaskBoard.AppHost.csproj
dotnet build src/TaskBoard.ClientCore/TaskBoard.ClientCore.csproj
dotnet test tests/TaskBoard.ClientCore.Tests/TaskBoard.ClientCore.Tests.csproj
dotnet test tests/TaskBoard.Api.Tests/TaskBoard.Api.Tests.csproj
dotnet test tests/TaskBoard.AppHost.Tests/TaskBoard.AppHost.Tests.csproj
dotnet build src/TaskBoard.Maui/TaskBoard.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet build src/TaskBoard.Maui/TaskBoard.Maui.csproj -f net10.0-android
```

On macOS build `-f net10.0-ios` or `-f net10.0-maccatalyst` instead. Builds restore packages automatically. To restore all host-supported targets explicitly, use `dotnet restore TaskBoard.slnx`.

Run the test projects individually: a solution-wide test/build can evaluate MAUI workloads. Application tests need neither Aspire nor mobile tooling. AppHost tests use fake tunnel commands and Aspire's readiness watcher, without a dashboard, CLI, emulator, or public tunnels. Add `--collect:"XPlat Code Coverage"` to the API/client test commands for coverage.

## Run with Aspire

```powershell
dotnet run --project src/TaskBoard.AppHost
```

Open the dashboard URL printed by the host. The API starts automatically. Use the Start command on the Windows or Mac Catalyst MAUI resource in the dashboard to build and launch the native client. MAUI targets are started on demand; opening the AppHost alone does not launch them.

In Development, **`taskboard-android` is also registered in the default launch profiles**. On macOS, an iOS simulator target is registered as well. Their API and telemetry tunnels remain stopped until you start a mobile resource. Seeing the Android resource does not require selecting a separate profile or installing Dev Tunnels first; deploying it does require the mobile prerequisites below.

The client uses `https+http://taskapi` when Aspire supplies endpoint configuration. The API resource is named `taskapi`. No emulator IP addresses are used in this mode.

### Mobile targets and Dev Tunnels

First install the [Dev Tunnels tooling](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started), then sign in:

```powershell
devtunnel user login
dotnet run --project src/TaskBoard.AppHost
```

Start **`taskboard-android`** from the dashboard to start its tunnels and build, install, and launch the app in an Android emulator. Both mobile platforms use an API Dev Tunnel and `WithOtlpDevTunnel()` for client telemetry. A failed tunnel start fails the mobile Start command with an error; desktop targets continue to work independently.

Create and start an Android Virtual Device with the Android Device Manager first. With one running emulator, no device configuration is needed. To select one of multiple running emulators, set `TaskBoard:AndroidEmulatorId` to its **adb serial**, through environment variables or a command-line argument:

```powershell
dotnet run --project src/TaskBoard.AppHost -- --TaskBoard:AndroidEmulatorId=emulator-5554
```

Replace the example serial with one from `adb devices` (run it from your Android SDK's `platform-tools` directory if it is not on PATH). The pinned Aspire integration passes this value to `adb -s`, so an AVD name from `emulator -list-avds` cannot be used here. The dashboard Start command deploys to a running emulator; it does not create an AVD.

The optional `android` launch profile and existing `--TaskBoard:EnableMobileTargets=true` switch now prepare the tunnels at AppHost startup instead of waiting for the first mobile Start command. They are no longer required to make Android visible. All mobile resources are limited to Development.

**Starting a mobile resource or choosing the tunnel-preparation profile exposes the unauthenticated demonstration API through an anonymously accessible tunnel. Use disposable, non-sensitive data only.** Public tunnel URLs are not access controls. Anyone with the URL can read or modify this task board. The AppHost refuses explicit mobile tunnel preparation outside Development and omits mobile resources there. Stop the AppHost when finished; never use this setup to expose production data. The backend has no authentication by design.

## Run without Aspire

Start the API directly in one terminal:

```powershell
dotnet run --project src/TaskBoard.Api --launch-profile http
```

The API listens on `http://localhost:5243`. SQLite is created under `src/TaskBoard.Api/App_Data/`; two demonstration tasks are seeded only when a Development database is first created. Emptying the board does not reseed it.

For Windows, build and launch the client through Visual Studio using `TaskBoard.Maui` as the startup project, or run:

```powershell
dotnet build src/TaskBoard.Maui/TaskBoard.Maui.csproj -t:Run -f net10.0-windows10.0.19041.0
```

The embedded `src/TaskBoard.Maui/clientsettings.json` provides the standalone URL. Edit it and rebuild for other devices. Windows can also override it before launch:

```powershell
$env:TaskBoard__ApiBaseUrl = "https://your-development-api.example/"
```

On mobile use an HTTPS endpoint trusted by the device, for example a separately hosted authenticated development environment. Environment variables in a desktop shell do not automatically reach an independently installed mobile application. This sample never disables certificate validation or broadly enables cleartext traffic. The Windows HTTP default is a local development convenience; configure a conventional HTTPS URL for deployment.

## Demonstrate traces and failures

1. Launch the client through Aspire and refresh the board.
2. Create or edit a task. Open dashboard traces and find the client HTTP span with its API child span.
3. On Diagnostics, trigger a normal request, the 1.5-second delay, and HTTP 503.
4. Observe three attempts for the failing GET, warning logs, and the displayed duration.
5. Trigger the 5-second timeout demo. Each attempt has a 3-second limit; the complete operation has a 12-second limit.
6. Switch device language between English and German and restart; the UI uses standard `.resx` satellite resources.
7. Switch system appearance to check light and dark themes.

Slow/failure endpoints exist only in API Development. Simulation controls appear only in MAUI Debug. The network indicator reports device connectivity, while request results show whether the API is reachable.

Request and navigation failures display localized errors and restore loading state so actions can be retried. A failed refresh preserves the previous board. The full HTTP exchange has a 12-second limit, including response-body buffering; writes are never retried automatically. Configuration or initial UI failures show a startup error page with diagnostic details in logs.

## With and without Aspire

| Concern | Standalone | Aspire |
| --- | --- | --- |
| API startup | Start the API yourself | AppHost starts it |
| Client URL | Configure a reachable URL per environment | Logical service name resolves supplied endpoints |
| Mobile local access | Arrange reachable HTTPS networking | Optional managed Dev Tunnels |
| Logs and traces | Configure a collector separately | Dashboard receives both applications' telemetry |
| Deployment | Publish API and native app using their tooling | Development AppHost is not a deployment solution |

Aspire makes demonstrations and local debugging easier. It adds packages, a preview integration, startup infrastructure, and optional account/network dependencies for tunnels. A single API and client remain easy to run without it.

## Documentation

- [Getting started from scratch](docs/getting-started.md)
- [Adding Aspire to an existing MAUI application](docs/adding-aspire.md)
- [Architecture and behavior](docs/architecture.md)
- [Troubleshooting](docs/troubleshooting.md)

MIT licensed. Build outputs, databases, local configuration, tunnel state, and credentials are ignored. Only `TaskBoard.slnx` is used; there is no legacy solution file.

# Troubleshooting

Use this guide with the pinned versions in `global.json` and `Directory.Packages.props`. Record the failing command and its first actual error before changing package versions or security settings.

## Missing or mismatched workloads

Run from the repository root:

```powershell
dotnet --version
dotnet --info
dotnet workload list
dotnet workload restore TaskBoard.slnx
```

The global SDK must resolve to the .NET 10 feature band selected here. An unrelated installed .NET 11 preview should not be selected. Install the workload through Visual Studio or the matching .NET 10 SDK. If restore requests elevation, use the machine's normal administrator workflow. A NuGet MAUI Controls package is not a substitute for Android, Apple, or Windows workloads.

MAUI packages can receive servicing updates independently of workload manifests. If a package requires newer platform tooling, update the relevant workload set and inspect the required Android/Xcode/Windows SDK version reported by the build.

## Aspire CLI not found

The project AppHost can run using `dotnet run --project src/TaskBoard.AppHost`; its SDK resolves bundled orchestration tooling. A separately installed Aspire CLI is optional. If using `aspire run`, follow the [official CLI installation guide](https://aspire.dev/get-started/install-cli/) and restart your terminal so PATH refreshes.

Do not attempt to install an old `aspire` .NET workload. For missing project templates run `dotnet new install Aspire.ProjectTemplates@13.6.1`.

## Package compatibility and central management

Keep the Aspire SDK and Dev Tunnels at 13.6.1 with the MAUI 13.6.1 preview used here. Check package availability on nuget.org; do not guess preview build numbers. Upgrade as a reviewed group and rerun AppHost/native builds.

- NU1008: remove `Version` from ordinary project PackageReferences; define it centrally.
- NU1009 for `Aspire.Hosting.AppHost`: remove its central PackageVersion. Aspire 13.6's SDK creates that implicit reference.
- Missing MAUI assemblies: retain explicit Controls in MAUI and Core in native defaults.
- Extension methods not found: check the integration package and namespace. `IMauiInitializeService` is in `Microsoft.Maui.Hosting`; retry option extensions are in `Microsoft.Extensions.Http.Resilience`.
- Duplicate resilience handlers: native defaults should register discovery globally while ClientCore registers the task client's single policy.

Use `dotnet restore TaskBoard.slnx` after correcting references. The configuration intentionally uses only nuget.org.

## AppHost startup failures

Build the host by itself first:

```powershell
dotnet build src/TaskBoard.AppHost/TaskBoard.AppHost.csproj
dotnet run --project src/TaskBoard.AppHost --launch-profile http
```

Check that the API project exists at the referenced path and that MAUI is registered through `AddMauiProject`, not a ProjectReference. The host's generated `Projects.TaskBoard_Api` class requires the API reference.

Port conflicts appear in host/dashboard logs. Stop the process you own or adjust the repository's launch profiles. The default API profiles use ports 5243/7243, and the AppHost profiles use separate dashboard/resource-service ports.

Mobile deployment requires Dev Tunnels tooling and sign-in. The default Development configuration registers Android and keeps mobile tunnels stopped until a mobile resource is started. Desktop startup does not require Dev Tunnels. The host intentionally rejects explicit mobile tunnel preparation outside Development and omits mobile resources there.

The dashboard waits for native targets to be started on demand. A MAUI resource being stopped initially is expected. Its Start command builds and launches the app; inspect resource logs if deployment fails.

## Dev Tunnel authentication or access

```powershell
devtunnel user login
dotnet run --project src/TaskBoard.AppHost
```

Use an account supported by the Dev Tunnels CLI and inspect login errors in a normal terminal. Hosting authentication is still required even with anonymous incoming access.

Allow outbound HTTPS, DNS, and long-lived tunnel connections. Corporate proxies, VPNs, firewall policy, and captive portals can interfere. Do not expose private task content to diagnose connectivity: the opt-in API tunnel allows unauthenticated CRUD. Stop the AppHost after the demonstration.

Do not paste tunnel credentials, dashboard authentication tokens, or local secrets into source files.

## Android emulator networking

`taskboard-android` is registered in every Development launch profile. If only `taskboard-windows` is visible, stop the existing AppHost, rebuild, and restart it: a running AppHost does not reload its resource registrations. Confirm that `DOTNET_ENVIRONMENT` is `Development`. The default `https` and `http` profiles already set it; no extra mobile switch is necessary.

Start the Android resource through the dashboard after launching an emulator. The mobile Start command starts the API and OTLP tunnels on demand and waits for their readiness; missing CLI/login prerequisites are reported during this step. The optional `android` profile prepares the tunnels earlier, at AppHost startup. Check that the Android API reference uses the tunnel and that it has `WithOtlpDevTunnel()`. Restart the resource after configuration changes so injected endpoints refresh.

Confirm the emulator is installed, its virtual device is running, and the Android SDK/JDK match the installed workload. Use the Android Device Manager to launch it, or list AVDs with `emulator -list-avds` and start one with `emulator -avd <avd-name>` from the Android SDK's `emulator` directory. The Aspire resource deploys the app to a running emulator.

With one running emulator, no selection setting is required. With several, list their serials using `adb devices` in the SDK's `platform-tools` directory and select one with `dotnet run --project src/TaskBoard.AppHost -- --TaskBoard:AndroidEmulatorId=emulator-5554`, or set `TaskBoard__AndroidEmulatorId` in the shell. Replace the example with the actual adb serial. The pinned integration passes the ID to `adb -s`, so do not use an AVD name for this setting.

For a standalone installed client, host `localhost` refers to the emulator itself. Supply a reachable, trusted HTTPS URL in `clientsettings.json` and rebuild. This sample does not change certificate validation or enable unrestricted cleartext traffic. No emulator-specific IP address belongs in Aspire-mode client code.

## iOS simulator and Mac Catalyst

Run Apple targets on macOS with compatible Xcode and workloads. Windows has no local iOS simulator. Mac Catalyst can use host endpoints; iOS simulator resources in this sample use Dev Tunnels.

Simulator connectivity failures usually involve target startup, tunnel authentication, or stale injected configuration. Start the resource through the AppHost, inspect logs, and restart it after fixing configuration. For standalone runs choose an HTTPS URL trusted by the Apple platform. Do not disable App Transport Security broadly to work around a bad certificate.

## HTTPS certificates

```powershell
dotnet dev-certs https --check
dotnet dev-certs https --trust
```

Use the platform's trust prompts. Desktop trust does not automatically extend to mobile simulators/devices. API tunnels provide their own externally reachable HTTPS endpoints; the local development API still needs a usable server certificate.

To isolate API startup problems locally on Windows, use `dotnet run --project src/TaskBoard.Api --launch-profile http`. This is a standalone development option, not a reason to weaken production HTTPS. Never replace HttpClient certificate validation with an always-true callback.

## Service discovery failures

Check the exact spelling `taskapi` in AppHost and `https+http://taskapi` in MAUI startup. Verify `builder.AddServiceDefaults()` registers discovery and that the native resource has `WithReference(api)` or its tunnel equivalent.

Launch through the dashboard to receive Aspire configuration. Launching a previously installed app independently does not inherit host endpoints. Inspect the resource's environment view locally to verify `services__taskapi__*` entries; avoid sharing tokens or full environment dumps.

Standalone launches use the embedded conventional URL. Change `TaskBoard:ApiBaseUrl` and rebuild, or override `TaskBoard__ApiBaseUrl` for a process that actually inherits your shell environment.

## Missing logs or telemetry

Check both defaults projects and their registrations:

- Backend: `builder.AddServiceDefaults()`, ASP.NET Core/HTTP instrumentation, and conditional OTLP export.
- Native: `builder.AddServiceDefaults()` and `OpenTelemetryInitializer`.
- Mobile: `WithOtlpDevTunnel()`.
- AppHost: target started from dashboard and backend visible/running.

Instrumentation propagates trace context for real HttpClient requests. A normal request should produce client and server spans under one trace. Health checks are filtered from backend traces.

Allow exporter traffic and ensure configuration is applied before provider initialization. Missing logs can also be caused by configured log levels. The API deliberately suppresses ordinary database command logs and avoids task-body logging.

## Unexpected timeouts or retries

The diagnostics slow button uses 1500 ms and should finish within the 3-second attempt limit. The 5000 ms button intentionally exceeds that limit. GET requests can make three attempts in total; their measured duration includes retries. The total operation is capped at 12 seconds. The standard circuit breaker can temporarily reject requests after repeated failures.

The full HTTP exchange, including a stalled response body, also has a 12-second HttpClient timeout. Request failures, malformed responses, circuit rejection, and navigation faults show an error while restoring the command's loading state. Failed refreshes preserve the last successful board and synchronization time. Inspect the native resource's logs for `UI operation failed`; unexpected faults include their exception details in logs.

Writes are never retried automatically. If a save fails after reaching the server, refresh before saving again. Repeated POSTs could otherwise duplicate tasks. Canceling a request does not roll back a write already committed by the API.

If Save succeeded but closing the editor failed, another Save uses the returned task ID and updates the same task. Closing errors leave the editor open and allow another navigation attempt.

## Native startup error page

A bad standalone API URL or a failure while initializing configuration, services, telemetry, or the initial XAML displays a localized startup error page. Correct the configuration and restart the app. Inspect `Native application initialization failed` in the native process's console/debug output for the underlying exception; the page does not display internal details.

After building Debug Windows and the API, these optional checks briefly launch the app and verify that the native process survives both failure modes:

```powershell
pwsh -NoProfile -File tests/Invoke-WindowsSmokeTest.ps1 -FailureMode UnavailableApi
pwsh -NoProfile -File tests/Invoke-WindowsSmokeTest.ps1 -FailureMode InvalidApiUrl
```

## SQLite schema or locked database

The local file is `src/TaskBoard.Api/App_Data/taskboard.db` unless a connection string overrides it. Stop the API before resetting its disposable database. The tutorial uses `EnsureCreatedAsync`, not automatic migrations: a changed model does not upgrade an existing file. Delete only your local demonstration database or introduce EF migrations when preserving data matters.

A custom SQLite connection string must point to an existing parent directory. Avoid launching several API processes against the same demo file. Tests use unique temporary files with pooling disabled and delete only their own files.

Deleting all tasks does not restore examples: seeds are inserted when the schema is first created.

## Duplicate native environment keys

MAUI 10.0.110 normalizes matching ASPNETCORE_ and DOTNET_ keys and can throw for duplicates such as `ENVIRONMENT`. `MauiProgram` now excludes matching ASPNETCORE_ entries while building, preferring DOTNET_ settings, and restores the process environment afterward. The optional Windows smoke script deliberately keeps both environment prefixes to test this workaround. If an older binary still fails with a duplicate-key exception, rebuild and relaunch it.

In PowerShell, remove an unwanted environment variable with `Remove-Item Env:\ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue`. An empty variable can still participate in MAUI's configuration mapping. Do not change machine-wide settings just to run this sample.

## Visual Studio and platform limits

Preview MAUI hosting does not have complete Visual Studio integration. Use `TaskBoard.slnx` and start the project AppHost through the terminal if the IDE's combined startup/debug experience is incomplete. Native resource Start commands are available through the dashboard; standalone native debugging remains useful.

Windows builds Windows and Android locally. macOS builds Android, iOS, and Mac Catalyst with compatible platform tooling. This sample does not configure remote Apple builds from Windows.

Compilation does not establish device connectivity, UI behavior, or trace delivery. See [verification](verification.md) for precisely what was executed and the remaining platform checks.

# Add Aspire to an existing MAUI application

Start with an existing .NET 10 MAUI application that already communicates with an ASP.NET Core API through a typed HttpClient. Keep that client abstraction, DTOs, ViewModels, persistence, and native UI. Aspire changes the development startup/configuration path and adds telemetry; it does not require rewriting the application.

Examples use this repository's `TaskBoard.*` names. Adapt paths to your solution. The implementation uses Aspire 13.6.1 and its matching MAUI hosting preview, verified on October 8, 2026. Consult the [official MAUI integration](https://aspire.dev/integrations/dotnet/maui/) when changing versions.

## 1. Prepare the existing solution

Check target frameworks and SDK selection:

```powershell
dotnet --version
dotnet workload list
dotnet build src/TaskBoard.Api/TaskBoard.Api.csproj
dotnet test tests/TaskBoard.ClientCore.Tests/TaskBoard.ClientCore.Tests.csproj
```

Pin a .NET 10 SDK in `global.json`, keep an SLNX solution, and ensure the application works with a conventional base URL first. Introduce Central Package Management if not already enabled. This sample's root configuration can be reused; compare it with your repository rather than overwriting existing requirements.

## 2. Add AppHost and both defaults projects

Install the official Aspire templates, then generate:

```powershell
dotnet new install Aspire.ProjectTemplates@13.6.1
dotnet new aspire-apphost -n TaskBoard.AppHost -o src/TaskBoard.AppHost --framework net10.0 --no-restore
dotnet new aspire-servicedefaults -n TaskBoard.ServiceDefaults -o src/TaskBoard.ServiceDefaults --framework net10.0 --no-restore
dotnet new maui-aspire-servicedefaults -n TaskBoard.MauiServiceDefaults -o src/TaskBoard.MauiServiceDefaults --skipRestore
dotnet sln TaskBoard.slnx add src/TaskBoard.AppHost/TaskBoard.AppHost.csproj src/TaskBoard.ServiceDefaults/TaskBoard.ServiceDefaults.csproj src/TaskBoard.MauiServiceDefaults/TaskBoard.MauiServiceDefaults.csproj
dotnet add src/TaskBoard.AppHost/TaskBoard.AppHost.csproj reference src/TaskBoard.Api/TaskBoard.Api.csproj
dotnet add src/TaskBoard.Api/TaskBoard.Api.csproj reference src/TaskBoard.ServiceDefaults/TaskBoard.ServiceDefaults.csproj
dotnet add src/TaskBoard.Maui/TaskBoard.Maui.csproj reference src/TaskBoard.MauiServiceDefaults/TaskBoard.MauiServiceDefaults.csproj
```

The MAUI defaults template comes from the MAUI workload. Use separate native and backend defaults: web middleware and ASP.NET Core instrumentation belong in backend defaults, while native telemetry initialization belongs in native defaults.

Add central package entries for the two hosting integrations:

```xml
<PackageVersion Include="Aspire.Hosting.Maui" Version="13.6.1-preview.1.26506.6" />
<PackageVersion Include="Aspire.Hosting.DevTunnels" Version="13.6.1" />
```

In the new AppHost:

```xml
<Project Sdk="Aspire.AppHost.Sdk/13.6.1">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AspireUseCliBundle>true</AspireUseCliBundle>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.Maui" />
    <PackageReference Include="Aspire.Hosting.DevTunnels" />
    <ProjectReference Include="../TaskBoard.Api/TaskBoard.Api.csproj" />
  </ItemGroup>
</Project>
```

Ordinary package versions belong in `Directory.Packages.props`. The AppHost SDK version stays in its SDK declaration and supplies the implicit AppHost package itself. Do not centrally declare that implicit package.

## 3. Modify the existing API startup

Before:

```csharp
var builder = WebApplication.CreateBuilder(args);
// Existing database and application services.
var app = builder.Build();
// Existing routes.
app.Run();
```

After:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
// Existing database and application services.
var app = builder.Build();
// Existing routes.
app.MapDefaultEndpoints();
app.Run();
```

Keep persistence configuration compatible with direct startup. TaskBoard uses a local SQLite file independent of AppHost, so API startup does not depend on orchestration. Its readiness check also probes database connectivity. Health endpoints are mapped in Development by the default template.

## 4. Register the backend and MAUI by path

Add to `src/TaskBoard.AppHost/AppHost.cs`:

```csharp
var builder = DistributedApplication.CreateBuilder(args);
var api = builder.AddProject<Projects.TaskBoard_Api>("taskapi")
    .WithHttpHealthCheck("/health");
var maui = builder.AddMauiProject("taskboard", "../TaskBoard.Maui/TaskBoard.Maui.csproj");

if (OperatingSystem.IsWindows())
{
    maui.AddWindowsDevice().WithReference(api);
}
if (OperatingSystem.IsMacOS())
{
    maui.AddMacCatalystDevice().WithReference(api);
}
```

Add `builder.Build().Run();` after all registrations. Only the API receives a ProjectReference. MAUI is registered through its `.csproj` path because its native target frameworks differ from the host's regular framework. Some older examples show a MAUI ProjectReference; this repository follows the path-based integration.

The resource name `taskapi` is the discovery contract between the host and client. Keep it identical in both places.

## 5. Modify MAUI configuration and client registration

A standalone application typically starts with:

```csharp
builder.Services.AddHttpClient<ITaskApiClient, TaskApiClient>(client =>
    client.BaseAddress = new Uri("https://your-api.example/"));
```

TaskBoard replaces that registration with native defaults plus an environment-aware base address:

```csharp
builder.AddServiceDefaults();
var hasDiscoveredApi = builder.Configuration.GetSection("services:taskapi").GetChildren().Any();
var baseAddress = hasDiscoveredApi
    ? new Uri("https+http://taskapi")
    : GetStandaloneAddress(builder.Configuration["TaskBoard:ApiBaseUrl"]);
builder.Services.AddTaskBoardClient(baseAddress);
```

This code is in `MauiProgram.cs`. The same file reads an embedded `clientsettings.json` and environment variables before registering services. `GetStandaloneAddress` validates a conventional absolute HTTP/HTTPS URL and normalizes its trailing slash.

Keep the native initialization/configuration providers supplied by MAUI. AppHost passes service endpoints at launch. The embedded standalone settings contain only `TaskBoard:ApiBaseUrl`, so they do not replace discovered endpoints.

The logical scheme prefers HTTPS when available. Do not translate it to emulator IPs or copy a tunnel URL into the client. Host references supply the correct endpoint for each registered target.

## 6. Register mobile targets and configure on-demand Dev Tunnels

TaskBoard registers mobile targets whenever the AppHost runs in Development. Registration and tunnel startup are separate: the targets are visible with the default launch profiles, while the tunnels start when a mobile resource is started. The core registration uses the official integration:

```csharp
if (builder.Environment.IsDevelopment())
{
    var tunnel = builder.AddDevTunnel("taskapi-tunnel")
        .WithAnonymousAccess()
        .WithReference(api.GetEndpoint("https"));

    var emulatorId = builder.Configuration["TaskBoard:AndroidEmulatorId"];
    maui.AddAndroidEmulator("taskboard-android", string.IsNullOrWhiteSpace(emulatorId) ? null : emulatorId)
        .WithOtlpDevTunnel()
        .WithReference(api, tunnel);
    if (OperatingSystem.IsMacOS())
    {
        maui.AddiOSSimulator().WithOtlpDevTunnel().WithReference(api, tunnel);
    }
}
```

Desktop resources use direct host connectivity. Mobile resources reference the API tunnel; `WithOtlpDevTunnel()` supplies the separate telemetry route.

Also copy the startup handling in [AppHost.cs](../src/TaskBoard.AppHost/AppHost.cs) and [MobileTunnelStarter.cs](../src/TaskBoard.AppHost/MobileTunnelStarter.cs). AppHost marks both Dev Tunnel resources with `WithExplicitStart()` in the default profiles and uses `OnBeforeResourceStarted` on the mobile resources to start them through `ResourceCommandService`. The helper waits for tunnel health and completed readiness events before mobile deployment, serializes concurrent Android/iOS starts, reuses running tunnels, and reports startup failures. The registration snippet above alone would start tunnels automatically.

Install [Dev Tunnels CLI](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started), then:

```powershell
devtunnel user login
dotnet run --project src/TaskBoard.AppHost
```

The default Development profiles already include `taskboard-android` alongside the desktop target. The optional `android` AppHost launch profile sets `TaskBoard__EnableMobileTargets=true`, preparing tunnels at host startup rather than waiting for a mobile Start command. The equivalent command-line switch remains available. Mobile resources are omitted outside Development, and explicit tunnel preparation is rejected there.

Hosting requires account authentication even though the demonstration tunnel permits anonymous incoming requests. Allow outbound HTTPS and long-lived tunnel connections through firewalls/proxies. Start an installed Android emulator or a configured iOS simulator on the appropriate host, then use the dashboard's Start command on the mobile resource. To choose one of several running Android emulators, pass `--TaskBoard:AndroidEmulatorId=emulator-5554` after `--`, replacing the serial with one from `adb devices`. The pinned integration uses `adb -s`; AVD names are not serials and cannot be passed here. Without a configured serial, it targets the single running emulator with `adb -e`.

Starting a mobile resource or explicitly preparing its tunnels exposes API CRUD without authentication. Use only disposable example data, never credentials or private tasks. Stop the host when finished. Tunnel URLs are public endpoints, not secrets or authorization. Published native apps use their conventional deployment URL.

## 7. Enable telemetry and avoid duplicate resilience policies

Backend defaults register ASP.NET Core and HTTP instrumentation, runtime metrics, and OpenTelemetry logging. Native defaults register HTTP instrumentation and an `IMauiInitializeService`:

```csharp
public void Initialize(IServiceProvider services)
{
    _ = services.GetService<MeterProvider>();
    _ = services.GetService<TracerProvider>();
    _ = services.GetService<LoggerProvider>();
}
```

Native startup resolves providers explicitly because it is not an ASP.NET Core host. Both defaults enable OTLP export only when `OTEL_EXPORTER_OTLP_ENDPOINT` exists. AppHost supplies collector endpoints and authentication; do not embed dashboard tokens.

The task client defines one standard resilience handler in ClientCore. Native defaults therefore configure global discovery and leave resilience to typed clients. Copying the template's default resilience registration without adjusting this would stack policies. Backend default retries also disable unsafe HTTP methods.

TaskBoard retries GET twice, disables automatic retries on all writes, uses 3-second attempt and 12-second total limits, and forwards CancellationToken. Use the same policy outside Aspire. OpenTelemetry's HttpClient/ASP.NET Core instrumentation handles W3C trace propagation; the task DTOs need no telemetry fields.

## 8. Build and verify behavior

```powershell
dotnet build src/TaskBoard.AppHost/TaskBoard.AppHost.csproj
dotnet test tests/TaskBoard.ClientCore.Tests/TaskBoard.ClientCore.Tests.csproj
dotnet test tests/TaskBoard.Api.Tests/TaskBoard.Api.Tests.csproj
dotnet dev-certs https --trust
dotnet run --project src/TaskBoard.AppHost
```

Use the dashboard's native resource Start command. Refresh tasks and locate the corresponding HTTP client/server spans. Use the Debug diagnostics page for normal, delayed, failed, and timed-out requests. Check backend warnings and retry attempts. Verify a standalone native launch still uses the conventional URL.

A compiled host confirms API compatibility, while native runtime checks establish endpoint injection, certificate trust, emulator connectivity, and telemetry export. Treat these as separate validation steps; see [verification](verification.md).

## Changed files checklist

| Add | Modify |
| --- | --- |
| AppHost project, resource registrations, launch profiles | SLNX solution entries |
| Backend ServiceDefaults project | API project references and startup |
| Native MauiServiceDefaults project | MAUI project references and MauiProgram |
| Central hosting/telemetry package entries | Typed client registration and explicit retry policy |
| Standalone settings and diagnostics if absent | Documentation and ignore rules |

No domain model, task DTO, database, or ViewModel needs an Aspire hosting dependency. The integration can be removed without changing the REST contract.

# Create the solution from scratch

These commands reproduce the infrastructure used here. Run them in a new directory, not over an existing checkout. Use a .NET 10 SDK and the matching platform workloads. The final application code is in this repository; templates initially supply only infrastructure and sample content.

## 1. Select the SDK and install templates

Create `global.json` first:

```json
{
  "sdk": {
    "version": "10.0.401",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

Check and install the required tooling:

```powershell
dotnet --version
dotnet workload list
dotnet new install Aspire.ProjectTemplates@13.6.1
dotnet new list aspire
dotnet new list maui
```

On Windows use Visual Studio's .NET MAUI workload or install `dotnet workload install maui` from a suitably privileged terminal. On macOS also install compatible Xcode and accept its license. Workload installation changes the machine and can take time; use the workload set appropriate for your SDK. The MAUI workload supplies `maui` and `maui-aspire-servicedefaults`; the Aspire package supplies `aspire-apphost` and `aspire-servicedefaults`.

The Aspire CLI is optional for this project-based host. If desired, install it using the instructions at [Aspire CLI installation](https://aspire.dev/get-started/install-cli/). `dotnet run` is sufficient here because the SDK's `AspireUseCliBundle` resolves orchestration tooling. Aspire is not a .NET workload in this setup.

## 2. Generate the application projects

```powershell
dotnet new sln -n TaskBoard --format slnx
dotnet new maui -n TaskBoard.Maui -o src/TaskBoard.Maui --no-restore
dotnet new webapi -n TaskBoard.Api -o src/TaskBoard.Api -f net10.0 --no-restore
dotnet new aspire-servicedefaults -n TaskBoard.ServiceDefaults -o src/TaskBoard.ServiceDefaults --framework net10.0 --no-restore
dotnet new maui-aspire-servicedefaults -n TaskBoard.MauiServiceDefaults -o src/TaskBoard.MauiServiceDefaults --skipRestore
dotnet new aspire-apphost -n TaskBoard.AppHost -o src/TaskBoard.AppHost --framework net10.0 --no-restore
dotnet new classlib -n TaskBoard.Contracts -o src/TaskBoard.Contracts -f net10.0 --no-restore
dotnet new classlib -n TaskBoard.ClientCore -o src/TaskBoard.ClientCore -f net10.0 --no-restore
dotnet new xunit -n TaskBoard.ClientCore.Tests -o tests/TaskBoard.ClientCore.Tests -f net10.0 --no-restore
dotnet new xunit -n TaskBoard.Api.Tests -o tests/TaskBoard.Api.Tests -f net10.0 --no-restore
dotnet new xunit -n TaskBoard.AppHost.Tests -o tests/TaskBoard.AppHost.Tests -f net10.0 --no-restore
```

These template names and options were executed with SDK 10.0.401. Do not create a legacy `.sln`.

## 3. Add the project references and solution entries

```powershell
dotnet add src/TaskBoard.Api/TaskBoard.Api.csproj reference src/TaskBoard.Contracts/TaskBoard.Contracts.csproj src/TaskBoard.ServiceDefaults/TaskBoard.ServiceDefaults.csproj
dotnet add src/TaskBoard.ClientCore/TaskBoard.ClientCore.csproj reference src/TaskBoard.Contracts/TaskBoard.Contracts.csproj
dotnet add src/TaskBoard.Maui/TaskBoard.Maui.csproj reference src/TaskBoard.ClientCore/TaskBoard.ClientCore.csproj src/TaskBoard.MauiServiceDefaults/TaskBoard.MauiServiceDefaults.csproj
dotnet add src/TaskBoard.AppHost/TaskBoard.AppHost.csproj reference src/TaskBoard.Api/TaskBoard.Api.csproj
dotnet add tests/TaskBoard.Api.Tests/TaskBoard.Api.Tests.csproj reference src/TaskBoard.Api/TaskBoard.Api.csproj
dotnet add tests/TaskBoard.ClientCore.Tests/TaskBoard.ClientCore.Tests.csproj reference src/TaskBoard.ClientCore/TaskBoard.ClientCore.csproj
dotnet add tests/TaskBoard.AppHost.Tests/TaskBoard.AppHost.Tests.csproj reference src/TaskBoard.AppHost/TaskBoard.AppHost.csproj
Get-ChildItem -Path src,tests -Filter *.csproj -Recurse | ForEach-Object { dotnet sln TaskBoard.slnx add $_.FullName }
```

On macOS use explicit `dotnet sln TaskBoard.slnx add <project>` commands for each project instead of the PowerShell enumeration.

**Do not add a ProjectReference from AppHost to MAUI.** The hosting integration takes a project path. The AppHost targets `net10.0`; the native application targets platform-specific frameworks.

## 4. Configure packages centrally

Copy the root `Directory.Build.props`, `Directory.Packages.props`, `NuGet.config`, and `.editorconfig` from this repository. They enable nullable analysis, C# 14, deterministic compilation, and central package versions from nuget.org.

Replace template PackageReferences with the package lists in this repository's project files, removing their `Version` attributes. In particular:

- MAUI explicitly references `Microsoft.Maui.Controls` and configuration/logging packages.
- MauiServiceDefaults explicitly references `Microsoft.Maui.Core`.
- ClientCore references the MVVM toolkit and HTTP/discovery/resilience libraries.
- API references EF Core SQLite. Remove unused OpenAPI template code and its PackageReference.
- AppHost references `Aspire.Hosting.Maui` and `Aspire.Hosting.DevTunnels`.
- Tests reference xUnit, test SDK, runner, coverage, and (API tests) MVC Testing.

Keep `Sdk="Aspire.AppHost.Sdk/13.6.1"` in the AppHost project. This version is an SDK reference, not an ordinary PackageReference. It implicitly supplies the AppHost runtime package; do not add `Aspire.Hosting.AppHost` to central PackageVersion entries.

The official MAUI defaults template originally uses `$(MauiVersion)` and older telemetry dependencies. Here explicit, centrally verified MAUI 10.0.110 and OpenTelemetry 1.19 versions replace those values.

## 5. Implement the application

Use the focused files under `src/` as the implementation:

1. Add DTOs and shared validation to Contracts.
2. Add the DbContext/entities, database initializer, and task/demo endpoint modules to API.
3. Replace the API's template Program with database, Problem Details, Service Defaults, and route registration.
4. Add the typed client, three ViewModels, and navigation/connectivity abstractions to ClientCore.
5. Add the English/German resource files.
6. Replace the MAUI counter page with Tasks, Editor, and Diagnostics XAML pages. Wire constructor injection and Shell routes.
7. Add the standalone `clientsettings.json` embedded resource.
8. Replace default resilience registration in native defaults with per-client policy registration in ClientCore; retain global discovery and telemetry initialization.

The platform-specific entry points and manifests remain based on the official MAUI template. The project declares Apple targets only on macOS and the Windows target only on Windows.

## 6. Register Aspire resources and native defaults

The core registration is:

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

Copy the Development mobile block from `AppHost.cs` together with `MobileTunnelStarter.cs` for emulator/simulator targets. The default AppHost profiles register `taskboard-android`; its dashboard Start command prepares the tunnels on demand. The optional `android` profile prepares them at host startup. See [adding Aspire](adding-aspire.md) for networking and telemetry details.

In `MauiProgram.cs`, call `builder.AddServiceDefaults()`, detect discovered `taskapi` endpoints, and pass the appropriate base URI to `AddTaskBoardClient`. Do not add another standard resilience handler on top of the task client's policy.

## 7. Build, run, and verify

```powershell
dotnet build src/TaskBoard.AppHost/TaskBoard.AppHost.csproj
dotnet test tests/TaskBoard.ClientCore.Tests/TaskBoard.ClientCore.Tests.csproj
dotnet test tests/TaskBoard.Api.Tests/TaskBoard.Api.Tests.csproj
dotnet test tests/TaskBoard.AppHost.Tests/TaskBoard.AppHost.Tests.csproj
dotnet build src/TaskBoard.Maui/TaskBoard.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet dev-certs https --trust
dotnet run --project src/TaskBoard.AppHost
```

Use your host's native target framework on macOS. In the dashboard start the native resource, create a task, refresh, and inspect its trace. To compare standalone behavior, stop the AppHost and follow the [README standalone instructions](../README.md#run-without-aspire).

Keep generated databases, build outputs, secrets, and local settings out of source control. The root ignore rules cover these artifacts. Read [troubleshooting](troubleshooting.md) before changing certificates or networking policies.

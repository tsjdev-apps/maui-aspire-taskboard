# Optional native smoke check; ordinary xUnit tests do not run this script.
# Run from the repository root after building the Windows application and API.
param(
    [switch]$UseDiscovery,
    [ValidateSet('None','UnavailableApi','InvalidApiUrl')]
    [string]$FailureMode = 'None'
)

$ErrorActionPreference = 'Stop'
if (![OperatingSystem]::IsWindows()) {
    throw 'This smoke check requires Windows and PowerShell 7.'
}
if ($UseDiscovery -and $FailureMode -eq 'InvalidApiUrl') {
    throw 'InvalidApiUrl checks standalone configuration; omit -UseDiscovery.'
}

$repositoryRoot = (Get-Location).Path
$apiPath = Join-Path $repositoryRoot 'src/TaskBoard.Api/bin/Debug/net10.0/TaskBoard.Api.exe'
$nativeDirectory = Join-Path $repositoryRoot 'src/TaskBoard.Maui/bin/Debug/net10.0-windows10.0.19041.0/win-x64'
$nativePath = Join-Path $nativeDirectory 'TaskBoard.Maui.exe'
if (!(Test-Path -LiteralPath $apiPath) -or !(Test-Path -LiteralPath $nativePath)) {
    throw 'Build TaskBoard.Api and the Windows MAUI target in Debug first.'
}

$runtimeDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('taskboard-smoke-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runtimeDirectory | Out-Null
$environmentKeys = @('ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_CONTENTROOT','ConnectionStrings__taskboard',
    'TaskBoard__ApiBaseUrl','services__taskapi__http__0','services__taskapi__https__0','services__taskapi__https__1',
    'services__taskapi__http__1','OTEL_EXPORTER_OTLP_ENDPOINT')
$previousEnvironment = @{}
foreach ($key in $environmentKeys) {
    $previousEnvironment[$key] = [Environment]::GetEnvironmentVariable($key)
}

$apiProcess = $null
$native = $null
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_CONTENTROOT = Join-Path $repositoryRoot 'src/TaskBoard.Api'
    $env:ConnectionStrings__taskboard = 'Data Source=' + (Join-Path $runtimeDirectory 'tasks.db') + ';Pooling=False'
    $env:TaskBoard__ApiBaseUrl = 'http://localhost:5343/'
    foreach ($key in $environmentKeys | Where-Object { $_ -like 'services__*' -or $_ -eq 'OTEL_EXPORTER_OTLP_ENDPOINT' }) {
        Remove-Item -LiteralPath ('Env:\' + $key) -ErrorAction SilentlyContinue
    }
    if ($UseDiscovery) {
        $env:services__taskapi__http__0 = 'http://localhost:5343'
    }

    $stdout = Join-Path $runtimeDirectory 'api.stdout.log'
    $apiArguments = @('--urls','http://localhost:5343','--Logging:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics','Information')
    $apiProcess = Start-Process -FilePath $apiPath -ArgumentList $apiArguments -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError (Join-Path $runtimeDirectory 'api.stderr.log') -PassThru
    $ready = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        try {
            $ready = (Invoke-RestMethod 'http://localhost:5343/health') -eq 'Healthy'
            if ($ready) { break }
        }
        catch {
            if ($apiProcess.HasExited) { throw 'The isolated API failed to start; inspect the printed log directory.' }
        }
        Start-Sleep -Milliseconds 500
    }
    if (!$ready) { throw 'The isolated API did not become healthy on port 5343.' }

    # Keep both environment prefixes to cover the native MAUI duplicate-key workaround.
    Remove-Item -LiteralPath Env:\ASPNETCORE_CONTENTROOT,Env:\ConnectionStrings__taskboard -ErrorAction SilentlyContinue
    if ($FailureMode -eq 'UnavailableApi') {
        if ($UseDiscovery) { $env:services__taskapi__http__0 = 'http://localhost:5344' }
        else { $env:TaskBoard__ApiBaseUrl = 'http://localhost:5344/' }
    }
    if ($FailureMode -eq 'InvalidApiUrl') { $env:TaskBoard__ApiBaseUrl = 'not-an-absolute-url' }

    # The application is visible because this is an explicitly invoked native runtime check.
    $nativeStdout = Join-Path $runtimeDirectory 'native.stdout.log'
    $native = Start-Process -FilePath $nativePath -WorkingDirectory $nativeDirectory -RedirectStandardOutput $nativeStdout -RedirectStandardError (Join-Path $runtimeDirectory 'native.stderr.log') -PassThru
    $requestObserved = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        $native.Refresh()
        if ($native.HasExited) { throw "The native client exited with code $($native.ExitCode)." }
        $requestObserved = switch ($FailureMode) {
            'UnavailableApi' { @(Select-String -LiteralPath $nativeStdout -Pattern 'UI operation failed').Count -gt 0 }
            'InvalidApiUrl' { $native.MainWindowTitle -match '^TaskBoard - (Startup error|Startfehler)$' }
            default { @(Select-String -LiteralPath $stdout -Pattern 'Request finished .* GET .*:5343/api/tasks - 200').Count -gt 0 }
        }
        if ($requestObserved) { break }
        Start-Sleep -Milliseconds 500
    }
    if (!$requestObserved) { throw 'The expected native success/error state was not observed.' }
    Start-Sleep -Seconds 1
    $native.Refresh()
    if ($native.HasExited) { throw 'The native client exited while presenting the response.' }

    [pscustomobject]@{
        Mode = if ($UseDiscovery) { 'Service discovery' } else { 'Standalone' }
        ApiHealth = 'Healthy'
        NativeAutomaticTaskLoad = switch ($FailureMode) {
            'UnavailableApi' { 'Connection failure handled; process remains alive' }
            'InvalidApiUrl' { 'Startup error window; process remains alive' }
            default { 'HTTP 200' }
        }
        Logs = $runtimeDirectory
    }
}
finally {
    if ($null -ne $native -and !$native.HasExited) { Stop-Process -Id $native.Id }
    if ($null -ne $apiProcess -and !$apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id }
    foreach ($key in $environmentKeys) {
        if ($null -eq $previousEnvironment[$key])
        {
            Remove-Item -LiteralPath ('Env:\' + $key) -ErrorAction SilentlyContinue
        }
        else
        {
            [Environment]::SetEnvironmentVariable($key, $previousEnvironment[$key])
        }
    }
    Write-Host "Smoke check logs: $runtimeDirectory"
}

[CmdletBinding()]
param(
    [string]$GodotExecutable,
    [int]$Port = 0,
    [switch]$Headless
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$serverProject = Join-Path $repositoryRoot 'server/src/Devilution.Server/Devilution.Server.csproj'
$serverExecutable = Join-Path $repositoryRoot 'server/src/Devilution.Server/bin/Debug/net10.0/Devilution.Server.exe'
$clientProject = Join-Path $repositoryRoot 'godot/Devilution.Client'
$captureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('DevilutionY-Godot-' + [guid]::NewGuid().ToString('N'))
$stdoutPath = Join-Path $captureRoot 'server.stdout.log'
$stderrPath = Join-Path $captureRoot 'server.stderr.log'
New-Item -ItemType Directory -Path $captureRoot | Out-Null

if ([string]::IsNullOrWhiteSpace($GodotExecutable)) {
    $godotCommand = Get-Command 'Godot_v4.7.1-stable_mono_win64.exe' -ErrorAction SilentlyContinue
    if ($null -eq $godotCommand)
        { throw 'Godot 4.7.1 Mono was not found. Pass -GodotExecutable explicitly.' }
    $GodotExecutable = $godotCommand.Source
}

$serverStartTime = Get-Date
if (-not (Test-Path -LiteralPath $serverExecutable))
    { throw "The built server executable was not found at $serverExecutable. Build the server project first." }
$serverArguments = "--port $Port --save-root `"$captureRoot/save`""
$serverProcess = Start-Process -FilePath $serverExecutable -ArgumentList $serverArguments -WorkingDirectory $repositoryRoot -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru

try {
    $actualPort = $null
    $contentHash = $null
    $rulesetHash = $null
    for ($attempt = 0; $attempt -lt 120 -and $null -eq $actualPort; $attempt++) {
        Start-Sleep -Milliseconds 250
        if ($serverProcess.HasExited) {
            throw "Authoritative server exited during startup. See $stderrPath"
        }
        $output = Get-Content -LiteralPath $stdoutPath -Raw -ErrorAction SilentlyContinue
        if ($output -match 'listening on [^:]+:(\d+)') { $actualPort = [int]$Matches[1] }
        if ($output -match 'Content manifest: ([0-9a-fA-F]+)') { $contentHash = $Matches[1] }
        if ($output -match 'Ruleset identity: ([0-9a-fA-F]+)') { $rulesetHash = $Matches[1] }
    }
    if ($null -eq $actualPort -or [string]::IsNullOrWhiteSpace($rulesetHash)) {
        throw "Timed out waiting for the authoritative server identity. See $stdoutPath and $stderrPath"
    }

    $env:DEVILUTION_SERVER_HOST = '127.0.0.1'
    $env:DEVILUTION_SERVER_PORT = $actualPort
    # The frozen wire contract currently carries the combined ruleset identity
    # in content_manifest_hash for compatibility with the native client.
    $env:DEVILUTION_CONTENT_HASH = $rulesetHash
    $env:DEVILUTION_RULESET_HASH = $rulesetHash
    $godotArguments = if ($Headless) { "--headless --path `"$clientProject`" --quit-after 120" } else { "--path `"$clientProject`"" }
    $godotProcess = Start-Process -FilePath $GodotExecutable -ArgumentList $godotArguments -WorkingDirectory $repositoryRoot -PassThru -Wait
    if ($godotProcess.ExitCode -ne 0)
        { throw "Godot exited with code $($godotProcess.ExitCode)." }
}
finally {
    if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
        $serverProcess.WaitForExit()
    }
    Get-Process -Name 'Devilution.Server' -ErrorAction SilentlyContinue |
        Where-Object { $_.StartTime -ge $serverStartTime } |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "Server logs: $captureRoot"
}

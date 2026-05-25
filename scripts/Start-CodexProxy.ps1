[CmdletBinding()]
param(
    [string]$Proxy = "http://127.0.0.1:7890",
    [switch]$Restart,
    [switch]$PatchCodexConfig,
    [switch]$UnpatchCodexConfig,
    [switch]$InstallDesktopShortcut,
    [switch]$NoLaunch,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-ProxyUrl {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "Proxy cannot be empty."
    }

    $trimmed = $Value.Trim()
    if ($trimmed -notmatch "^[a-zA-Z][a-zA-Z0-9+.-]*://") {
        $trimmed = "http://$trimmed"
    }

    $uri = [System.Uri]$trimmed
    if (-not $uri.Host -or $uri.Port -lt 0) {
        throw "Proxy must include host and port, for example http://127.0.0.1:7890."
    }

    return $uri.AbsoluteUri.TrimEnd("/")
}

function Get-CodexApp {
    $package = Get-AppxPackage -Name "OpenAI.Codex" |
        Sort-Object Version -Descending |
        Select-Object -First 1

    if (-not $package) {
        throw "OpenAI.Codex AppX package was not found for the current Windows user."
    }

    $appDir = Join-Path $package.InstallLocation "app"
    $exe = Join-Path $appDir "Codex.exe"
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "Codex.exe was not found at $exe."
    }

    [pscustomobject]@{
        Package = $package
        AppDir = $appDir
        Exe = $exe
    }
}

function Set-ProxyEnvironment {
    param([string]$ProxyUrl)

    $bypass = "localhost,127.0.0.1,::1"
    $pairs = @(
        @("HTTP_PROXY", $ProxyUrl),
        @("HTTPS_PROXY", $ProxyUrl),
        @("ALL_PROXY", $ProxyUrl),
        @("http_proxy", $ProxyUrl),
        @("https_proxy", $ProxyUrl),
        @("all_proxy", $ProxyUrl),
        @("NO_PROXY", $bypass),
        @("no_proxy", $bypass),
        @("NODE_USE_ENV_PROXY", "1")
    )

    foreach ($entry in $pairs) {
        [System.Environment]::SetEnvironmentVariable($entry[0], $entry[1], "Process")
    }
}

function Stop-CodexAppProcesses {
    param([string]$InstallLocation)

    $targets = Get-Process -ErrorAction SilentlyContinue |
        Where-Object {
            $path = $_.Path
            if (-not $path) { return $false }
            return $path.StartsWith($InstallLocation, [System.StringComparison]::OrdinalIgnoreCase)
        }

    foreach ($process in $targets) {
        Write-Host "Stopping $($process.ProcessName) pid=$($process.Id)"
        if (-not $DryRun) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}

function Update-CodexConfigProxy {
    param(
        [string]$ProxyUrl,
        [bool]$Enable
    )

    $configPath = Join-Path $env:USERPROFILE ".codex\config.toml"
    if (-not (Test-Path -LiteralPath $configPath)) {
        Write-Host "Codex config not found, skipping: $configPath"
        return
    }

    $content = Get-Content -LiteralPath $configPath -Raw
    $begin = "# BEGIN codex-proxy launcher"
    $end = "# END codex-proxy launcher"
    $pattern = "(?ms)^\s*$([regex]::Escape($begin)).*?^\s*$([regex]::Escape($end))\r?\n?"
    $clean = [regex]::Replace($content, $pattern, "")

    if ($Enable) {
        $escaped = $ProxyUrl.Replace("\", "\\").Replace('"', '\"')
        $block = @"

$begin
HTTP_PROXY = "$escaped"
HTTPS_PROXY = "$escaped"
ALL_PROXY = "$escaped"
http_proxy = "$escaped"
https_proxy = "$escaped"
all_proxy = "$escaped"
NO_PROXY = "localhost,127.0.0.1,::1"
no_proxy = "localhost,127.0.0.1,::1"
NODE_USE_ENV_PROXY = "1"
$end
"@

        $tablePattern = "(?m)^\[mcp_servers\.node_repl\.env\]\s*$"
        if ([regex]::IsMatch($clean, $tablePattern)) {
            $newContent = [regex]::Replace(
                $clean.TrimEnd(),
                $tablePattern,
                "[mcp_servers.node_repl.env]$block",
                1
            ) + [Environment]::NewLine
        } else {
            $newContent = $clean.TrimEnd() + @"

[mcp_servers.node_repl.env]$block
"@ + [Environment]::NewLine
        }
    } else {
        $newContent = $clean
    }

    $backup = "$configPath.codex-proxy.bak"
    Write-Host "Updating $configPath"
    Write-Host "Backup: $backup"
    if (-not $DryRun) {
        Copy-Item -LiteralPath $configPath -Destination $backup -Force
        Set-Content -LiteralPath $configPath -Value $newContent -Encoding UTF8
    }
}

function Install-Shortcut {
    param(
        [string]$ScriptPath,
        [string]$ProxyUrl
    )

    $desktop = [Environment]::GetFolderPath("Desktop")
    $shortcutPath = Join-Path $desktop "Codex Proxy.lnk"
    $powershell = Join-Path $env:SystemRoot "System32\WindowsPowerShell\v1.0\powershell.exe"
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`" -Proxy `"$ProxyUrl`" -Restart -PatchCodexConfig"

    Write-Host "Installing desktop shortcut: $shortcutPath"
    if (-not $DryRun) {
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $powershell
        $shortcut.Arguments = $arguments
        $shortcut.WorkingDirectory = Split-Path -Parent $ScriptPath
        $shortcut.IconLocation = (Get-CodexApp).Exe
        $shortcut.Save()
    }
}

$proxyUrl = Normalize-ProxyUrl $Proxy
$codex = Get-CodexApp
$scriptPath = $PSCommandPath

Write-Host "Codex: $($codex.Exe)"
Write-Host "Proxy: $proxyUrl"

if ($UnpatchCodexConfig) {
    Update-CodexConfigProxy -ProxyUrl $proxyUrl -Enable:$false
}

if ($PatchCodexConfig) {
    Update-CodexConfigProxy -ProxyUrl $proxyUrl -Enable:$true
}

if ($InstallDesktopShortcut) {
    Install-Shortcut -ScriptPath $scriptPath -ProxyUrl $proxyUrl
}

if ($NoLaunch) {
    Write-Host "NoLaunch set; not starting Codex."
    exit 0
}

if ($Restart) {
    Stop-CodexAppProcesses -InstallLocation $codex.Package.InstallLocation
    Start-Sleep -Milliseconds 600
}

Set-ProxyEnvironment -ProxyUrl $proxyUrl

$chromeBypass = "<-loopback>;localhost;127.0.0.1;::1"
$arguments = @(
    "--proxy-server=$proxyUrl",
    "--proxy-bypass-list=$chromeBypass"
)

Write-Host "Starting Codex with proxy arguments."
if (-not $DryRun) {
    Start-Process -FilePath $codex.Exe -WorkingDirectory $codex.AppDir -ArgumentList $arguments
}

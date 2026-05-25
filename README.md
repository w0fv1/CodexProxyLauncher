# Codex Proxy Launcher

Start the Windows Codex Desktop app through a local proxy without enabling the Windows system proxy.

Default proxy:

```powershell
http://127.0.0.1:7890
```

## Download / Run

Use the GUI executable:

```powershell
.\dist\CodexProxyLauncher.exe
```

The app lets you configure:

- proxy URL
- whether to close existing Microsoft Store Codex processes before launch
- whether to patch `%USERPROFILE%\.codex\config.toml` for Codex child processes

It also has buttons to launch Codex, create a desktop shortcut, save config, and remove the config patch.

Configuration is saved to:

```text
%APPDATA%\CodexProxyLauncher\config.ini
```

## What It Does

The launcher finds the Microsoft Store Codex install, injects proxy settings into the process environment, and starts `Codex.exe` with Electron/Chromium proxy flags:

- `HTTP_PROXY`, `HTTPS_PROXY`, and `ALL_PROXY`
- lowercase proxy env variants
- `NO_PROXY` for local addresses
- `NODE_USE_ENV_PROXY=1`
- `--proxy-server=http://127.0.0.1:7890`
- `--proxy-bypass-list=<-loopback>;localhost;127.0.0.1;::1`

It can also patch `%USERPROFILE%\.codex\config.toml` so Codex child MCP processes inherit the same proxy environment.

## Build

No .NET SDK is required for the current build. It uses the C# compiler included with Windows .NET Framework:

```powershell
.\build.cmd
```

Output:

```text
dist\CodexProxyLauncher.exe
```

Optional modern build path: install the .NET SDK if you want to port this to .NET 8/9 and produce a self-contained single-file app. The current repository does not require that.

## PowerShell Fallback

Run:

```powershell
.\launch-codex-proxy.cmd
```

That command closes the existing Microsoft Store Codex app process tree and starts Codex again with proxy injection enabled.

## PowerShell Desktop Shortcut

Run:

```powershell
.\install-desktop-shortcut.cmd
```

It creates `Codex Proxy.lnk` on the desktop. Use that shortcut instead of the normal Codex shortcut when you want Codex Desktop to use `127.0.0.1:7890`.

## Verification

After launch, the Codex main process command line should include:

```text
--proxy-server=http://127.0.0.1:7890
```

The launcher was validated by sending a real message from Codex Desktop and receiving an `OK` response through the proxy.

## Manual Commands

Start without closing current Codex processes:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-CodexProxy.ps1
```

Restart Codex and patch child-process env config:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-CodexProxy.ps1 -Restart -PatchCodexConfig
```

Remove the config patch:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start-CodexProxy.ps1 -UnpatchCodexConfig -NoLaunch -DryRun
```

Remove `-DryRun` after checking the output.

## Notes

- This is intentionally process-scoped. It does not change Windows system proxy settings.
- The config patch is surrounded by `# BEGIN codex-proxy launcher` and `# END codex-proxy launcher`.
- A backup is written to `%USERPROFILE%\.codex\config.toml.codex-proxy.bak` before modifying Codex config.

# Codex Proxy Launcher

A tiny Windows GUI launcher that starts Codex Desktop through a process-scoped proxy without enabling the Windows system proxy.

Default proxy:

```text
http://127.0.0.1:7890
```

## Use

Download the EXE from the latest release:

```text
https://github.com/w0fv1/CodexProxyLauncher/releases/latest
```

Or build it locally:

```cmd
build.cmd
dist\CodexProxyLauncher.exe
```

## Features

- Configure the proxy URL.
- Start Microsoft Store Codex with `HTTP_PROXY`, `HTTPS_PROXY`, `ALL_PROXY`, lowercase variants, and Electron `--proxy-server`.
- Optionally close existing Microsoft Store Codex processes before launch.
- Optionally patch `%USERPROFILE%\.codex\config.toml` so Codex child MCP processes inherit proxy env.
- Create a desktop shortcut.
- Remove the config patch.

Config is saved at:

```text
%APPDATA%\CodexProxyLauncher\config.ini
```

## Build

No .NET SDK is required. The build uses the C# compiler that ships with Windows .NET Framework:

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

Run:

```cmd
build.cmd
```

The output is:

```text
dist\CodexProxyLauncher.exe
```

## Files

- `CodexProxyLauncher.cs`: the complete WinForms app.
- `build.cmd`: compiles the EXE.
- `README.md`: usage and build notes.

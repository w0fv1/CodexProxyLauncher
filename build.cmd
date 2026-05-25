@echo off
setlocal

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo csc.exe not found at %CSC%
  exit /b 1
)

if not exist dist mkdir dist

"%CSC%" ^
  /nologo ^
  /target:winexe ^
  /platform:anycpu ^
  /optimize+ ^
  /out:dist\CodexProxyLauncher.exe ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:Microsoft.CSharp.dll ^
  CodexProxyLauncher.cs

if errorlevel 1 exit /b %errorlevel%

echo Built dist\CodexProxyLauncher.exe

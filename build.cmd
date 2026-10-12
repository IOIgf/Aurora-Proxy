@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
 echo Please install .NET Framework 4.0 or 4.8 first.
 pause
 exit /b 1
)
"%CSC%" /nologo /resource:src\Emoji.bin,AuroraProxy.Emoji /target:winexe /platform:anycpu /optimize+ /utf8output /win32manifest:app.manifest /win32icon:aurora.ico /out:AuroraProxy.exe /reference:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Web.Extensions.dll,System.Security.dll,Accessibility.dll src\*.cs
if errorlevel 1 (
 pause
 exit /b 1
)
echo Build succeeded: AuroraProxy.exe
pause

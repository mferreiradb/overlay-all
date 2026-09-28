@echo off
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo csc.exe do .NET Framework 4 nao encontrado.
  exit /b 1
)
"%CSC%" /nologo /target:winexe /optimize+ /out:"%~dp0OverlayAll.exe" ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  "%~dp0OverlayAll.cs"
if errorlevel 1 exit /b 1
echo OK: %~dp0OverlayAll.exe

@echo off
rem ============================================================
rem  Pasha build script (no .NET SDK required)
rem  Compiles with the csc.exe bundled in the .NET Framework.
rem ============================================================
setlocal

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [ERROR] csc.exe not found. .NET Framework 4.x is required.
  exit /b 1
)

echo Using compiler: %CSC%

"%CSC%" -nologo -target:winexe -out:Pasha.exe -optimize+ -win32manifest:app.manifest -reference:System.dll -reference:System.Core.dll -reference:System.Drawing.dll -reference:System.Windows.Forms.dll src\*.cs

if errorlevel 1 (
  echo [ERROR] Build failed.
  exit /b 1
)

copy /y app.config Pasha.exe.config >nul

echo.
echo [OK] Build complete: Pasha.exe
endlocal

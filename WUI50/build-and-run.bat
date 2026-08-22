@echo off
setlocal
cd /d "%~dp0"

set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
if not exist "%MSBUILD%" set "MSBUILD=%ProgramFiles(x86)%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
if not exist "%MSBUILD%" (
    echo [error] Visual Studio 2022 MSBuild not found.
    echo         Install "Visual Studio 2022 Community" with .NET desktop development.
    pause
    exit /b 1
)

if not exist "%~dp0..\venv50\Scripts\python.exe" (
    echo Python backend not found. Running setup ...
    call "%~dp0..\PianoTrans-GPU50-Install.bat"
    if errorlevel 1 exit /b 1
)

echo Building PianoTrans WUI-50+ ...
"%MSBUILD%" PianoTrans.WUI50.csproj -restore -p:Configuration=Debug -p:Platform=x64
if errorlevel 1 (
    echo [error] Build failed.
    pause
    exit /b 1
)

set "EXE=%~dp0bin\x64\Debug\net8.0-windows10.0.26100.0\win-x64\PianoTrans-WUI50.exe"
if not exist "%EXE%" (
    echo [error] Build output not found: %EXE%
    pause
    exit /b 1
)

start "" "%EXE%" %*
exit /b 0

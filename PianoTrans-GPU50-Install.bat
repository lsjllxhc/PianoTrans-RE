@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0"

echo ============================================================
echo  PianoTrans RTX 50 GPU environment setup
echo  (Python 3.12 venv + PyTorch 2.7.1 + cu128)
echo ============================================================
echo.

set "VENV=%~dp0venv50"
set "LOCAL_TORCH_WHEEL=%~dp0wheels\torch-2.7.1+cu128-cp312-cp312-win_amd64.whl"
set "PY=py -3.12"

%PY% -I -c "import sys; raise SystemExit(0 if sys.version_info[:2] == (3, 12) and sys.maxsize > 2**32 else 1)" >nul 2>nul
if not errorlevel 1 goto :python_ok
set "PY=python"
%PY% -I -c "import sys; raise SystemExit(0 if sys.version_info[:2] == (3, 12) and sys.maxsize > 2**32 else 1)" >nul 2>nul
if errorlevel 1 goto :need_python

:python_ok

echo [1/4] Preparing Python 3.12 virtual environment ...
if not exist "%VENV%\Scripts\python.exe" (
    %PY% -m venv "%VENV%"
    if errorlevel 1 goto :error
) else (
    echo       venv50 already exists.
)

"%VENV%\Scripts\python.exe" -I -c "import torch, sys; sys.exit(0 if str(torch.version.cuda).startswith('12.8') and '+cu128' in torch.__version__ else 1)" >nul 2>nul
if not errorlevel 1 goto :torch_ready

echo [2/4] Installing PyTorch 2.7.1+cu128 (about 3.3 GB download) ...
if exist "%LOCAL_TORCH_WHEEL%" (
    echo       Using local wheel: %LOCAL_TORCH_WHEEL%
    "%VENV%\Scripts\python.exe" -m pip install "%LOCAL_TORCH_WHEEL%"
) else (
    "%VENV%\Scripts\python.exe" -m pip install torch==2.7.1+cu128 --index-url https://download.pytorch.org/whl/cu128
)
if errorlevel 1 goto :error

:torch_ready
echo [3/4] Installing piano-transcription-inference and audio packages ...
"%VENV%\Scripts\python.exe" -m pip install -r "%~dp0requirements-gpu50.txt"
if errorlevel 1 goto :error

echo [4/4] Checking CUDA / GPU ...
"%VENV%\Scripts\python.exe" -I -c "import torch; print('PyTorch:', torch.__version__); print('CUDA build:', torch.version.cuda); print('CUDA available:', torch.cuda.is_available()); print('GPU:', torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'not found')"
if errorlevel 1 goto :error

echo.
echo Setup finished. You can now double-click PianoTrans-GPU50.bat
echo (or run it from the command line with audio/video file arguments).
pause
exit /b 0

:need_python
echo [error] Python 3.12 was not found via "py -3.12".
echo         Install 64-bit Python 3.12 from https://www.python.org/downloads/
echo         and tick "Add python.exe to PATH" / "py launcher" during setup.
pause
exit /b 1

:error
echo.
echo [error] Setup failed. See the messages above.
pause
exit /b 1

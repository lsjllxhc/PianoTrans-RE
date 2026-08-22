# -*- coding: utf-8 -*-
"""
Build the three PianoTrans WUI-50+ distribution folders:

  <repo>          source-only, for GitHub
  release-online  compiled app + model + ffmpeg, Python deps downloaded by user
  release-offline compiled app + model + ffmpeg + Python + wheels + venv

Run with the system Python 3.12:

  py -3.12 tools\\make_releases.py
"""

from __future__ import annotations

import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DIST = ROOT.parent

GIT_DIR = DIST / "git"
ONLINE_DIR = DIST / "release-online"
OFFLINE_DIR = DIST / "release-offline"

RELEASE_OUT = (
    ROOT
    / "WUI50"
    / "bin"
    / "x64"
    / "Release"
    / "net8.0-windows10.0.26100.0"
    / "win-x64"
)

CHECKPOINT_SRC = (
    ROOT
    / "piano_transcription_inference_data"
    / "note_F1=0.9677_pedal_F1=0.9186.pth"
)
FFMPEG_SRC = ROOT / "ffmpeg" / "ffmpeg.exe"
VENV_SRC = ROOT / "venv50"
WHEELS_SRC = ROOT / "wheels"

SOURCE_README = """# PianoTrans WUI-50+

WinUI 3 native piano-transcription front end with an RTX 50 (Blackwell, sm_120)
compatible PyTorch backend.

This repository contains **source code only**. It does not contain compiled
binaries, the pretrained checkpoint, ffmpeg, or Python dependency packages.

## Supported GPUs

- NVIDIA RTX 50 series: Blackwell, compute capability `sm_120`.
- Older NVIDIA GPUs down to `sm_50` that are still supported by CUDA 12.8.
- Any other machine falls back to CPU automatically.
- The backend is PyTorch `2.7.1+cu128` (CUDA 12.8), which also works on
  RTX 30/40 series and earlier supported NVIDIA cards.

## Repository layout

```text
WUI50/                         WinUI 3 front end (C# / XAML source)
  Pages/                       Home, Settings, About pages
  Models/                      Queue job model
  Services/                    Queue manager, settings, Python process bridge
  PianoTrans.WUI50.csproj
modern50/
  PianoTrans-Worker.py         Headless worker used by the WinUI app
  PianoTrans-GPU50.py          Optional tkinter fallback launcher
tools/
  make_releases.py             Builds git / release-online / release-offline folders
requirements-gpu50.txt
PianoTrans-GPU50-Install.bat   Creates venv50 and installs PyTorch cu128 + deps
PianoTrans-WUI50.bat           Builds and launches the WinUI app
```

## Build from source

Prerequisites:

1. Windows 10/11 x64.
2. Visual Studio 2022 Community or Build Tools with .NET desktop workload.
3. 64-bit Python 3.12 (the `py -3.12` launcher is used by the setup script).
4. Pretrained checkpoint:

   Download from Zenodo:

   ```text
   https://zenodo.org/record/4034264/files/CRNN_note_F1%3D0.9677_pedal_F1%3D0.9186.pth?download=1
   ```

   Save it as:

   ```text
   piano_transcription_inference_data\\note_F1=0.9677_pedal_F1=0.9186.pth
   ```

5. ffmpeg:

   Put `ffmpeg.exe` into `ffmpeg\\ffmpeg.exe`, or install ffmpeg and add it to
   `PATH`.

Build and run:

```bat
PianoTrans-GPU50-Install.bat
PianoTrans-WUI50.bat
```

The setup script downloads PyTorch 2.7.1+cu128 and the transcription packages.
`PianoTrans-WUI50.bat` builds the WinUI app with MSBuild and starts it.

## Notes

- The old PyInstaller `PianoTrans.exe` is intentionally not part of this
  repository. It bundles PyTorch 1.10 / CUDA 11 and has no `sm_120` kernels.
- The checkpoint belongs to ByteDance's piano_transcription project; please
  keep their license/attribution when redistributing it.
"""

ONLINE_README = """# PianoTrans WUI-50+ (Online installer release)

This release contains the compiled WinUI 3 app, the pretrained model and
ffmpeg. The Python runtime packages are **not** included and are downloaded by
the setup script.

## Quick start

0. Make sure 64-bit Python 3.12 is installed (the setup script uses `py -3.12`).

1. Double-click:

   ```text
   PianoTrans-GPU50-Install.bat
   ```

   It downloads about 3.3 GB (PyTorch 2.7.1 + CUDA 12.8 and dependencies).
   Internet access is required.

2. Double-click:

   ```text
   PianoTrans-WUI50.exe
   ```

   or use `PianoTrans-WUI50.bat`.

The first launch creates a `venv50` folder next to the app.

## GPU support

- RTX 50 / Blackwell `sm_120`: supported through PyTorch 2.7.1 + CUDA 12.8.
- Older NVIDIA GPUs supported by CUDA 12.8 also work.
- If CUDA is unavailable, the backend automatically uses CPU.

## Included files

```text
PianoTrans-WUI50.exe         WinUI 3 application (self-contained)
PianoTrans-WUI50.bat         Launcher that checks the Python backend first
PianoTrans-GPU50-Install.bat Downloads and installs Python dependencies
modern50\\PianoTrans-Worker.py
piano_transcription_inference_data\\*.pth
ffmpeg\\ffmpeg.exe
requirements-gpu50.txt
README.md
```

## Troubleshooting

- GPU is not detected: install a recent NVIDIA driver, then rerun the app.
- CUDA error / no kernel image: use Settings -> CPU mode, or make sure
  `PianoTrans-GPU50-Install.bat` installed `torch 2.7.1+cu128`.
- Log file: `%LOCALAPPDATA%\\PianoTrans-WUI50\\app.log`.
"""

OFFLINE_README = """# PianoTrans WUI-50+ (Offline release)

Everything is included in this folder: the compiled WinUI 3 app, the pretrained
model, ffmpeg, Python 3.12, all dependency wheels and a ready-to-use `venv50`.
No internet connection is needed.

## Quick start

Double-click:

```text
PianoTrans-WUI50.exe
```

or use `PianoTrans-WUI50.bat`.

If you move the folder and the prebuilt `venv50` stops working, run:

```text
PianoTrans-GPU50-Install.bat
```

It recreates `venv50` from the bundled `python312` and installs every package
from the bundled `wheels` directory without internet access.

## GPU support

- RTX 50 / Blackwell `sm_120`: supported through PyTorch 2.7.1 + CUDA 12.8.
- Older NVIDIA GPUs supported by CUDA 12.8 also work.
- If CUDA is unavailable, the backend automatically uses CPU.

## Included files

```text
PianoTrans-WUI50.exe         WinUI 3 application (self-contained)
PianoTrans-WUI50.bat         Launcher
PianoTrans-GPU50-Install.bat Offline dependency installer / repair tool
modern50\\PianoTrans-Worker.py
piano_transcription_inference_data\\*.pth
ffmpeg\\ffmpeg.exe
requirements-gpu50.txt
python312\\                  Portable Python 3.12 runtime
venv50\\                     Prebuilt Python environment
wheels\\                     All dependency wheels
README.md
```

## Troubleshooting

- GPU is not detected: install a recent NVIDIA driver, then rerun the app.
- CUDA error / no kernel image: use Settings -> CPU mode.
- If the app reports that Python was not found, run
  `PianoTrans-GPU50-Install.bat` once.
- Log file: `%LOCALAPPDATA%\\PianoTrans-WUI50\\app.log`.
"""

LAUNCHER_BAT = r"""@echo off
setlocal
cd /d "%~dp0"

if not exist "%~dp0PianoTrans-WUI50.exe" (
    echo [error] PianoTrans-WUI50.exe not found.
    pause
    exit /b 1
)

set "VENV_PY=%~dp0venv50\Scripts\python.exe"

if not exist "%VENV_PY%" goto :setup
"%VENV_PY%" -I -c "import sys" >nul 2>nul
if not errorlevel 1 goto :run

:setup
echo Python backend is missing or broken. Running setup ...
call "%~dp0PianoTrans-GPU50-Install.bat"
if errorlevel 1 (
    pause
    exit /b 1
)

:run
start "" "%~dp0PianoTrans-WUI50.exe" %*
exit /b 0
"""

OFFLINE_INSTALL_BAT = r"""@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0"

echo ============================================================
echo  PianoTrans WUI-50+ offline dependency setup
echo ============================================================
echo.

set "BASE_PY=%~dp0python312\python.exe"
set "VENV=%~dp0venv50"
set "WHEELS=%~dp0wheels"

if not exist "%BASE_PY%" (
    echo [error] Bundled Python not found: %BASE_PY%
    pause
    exit /b 1
)

if not exist "%WHEELS%" (
    echo [error] Bundled wheels folder not found: %WHEELS%
    pause
    exit /b 1
)

"%VENV%\Scripts\python.exe" -I -c "import sys; raise SystemExit(0 if sys.version_info[:2] == (3, 12) else 1)" >nul 2>nul
if not errorlevel 1 goto :venv_ok

echo [1/3] Recreating venv50 with the bundled Python ...
if exist "%VENV%" rmdir /s /q "%VENV%"
"%BASE_PY%" -I -m venv "%VENV%"
if errorlevel 1 goto :error

:venv_ok
"%VENV%\Scripts\python.exe" -I -c "import torch, sys; sys.exit(0 if str(torch.version.cuda).startswith('12.8') and '+cu128' in torch.__version__ else 1)" >nul 2>nul
if not errorlevel 1 goto :torch_ready

echo [2/3] Installing PyTorch 2.7.1+cu128 from local wheels ...
"%VENV%\Scripts\python.exe" -I -m pip install --no-index --find-links "%WHEELS%" torch==2.7.1+cu128
if errorlevel 1 goto :error

:torch_ready
echo [3/3] Installing transcription packages from local wheels ...
"%VENV%\Scripts\python.exe" -I -m pip install --no-index --find-links "%WHEELS%" -r "%~dp0requirements-gpu50.txt"
if errorlevel 1 goto :error

echo.
echo Offline setup finished. You can now run PianoTrans-WUI50.exe.
pause
exit /b 0

:error
echo.
echo [error] Setup failed. See the messages above.
pause
exit /b 1
"""

GITIGNORE = """# Build output
WUI50/bin/
WUI50/obj/
.vs/
*.user
*.suo

# Python environment and downloaded dependencies
venv50/
wheels/

# Large binary assets that are distributed through releases
piano_transcription_inference_data/*.pth
ffmpeg/

# Local user output
out/
__pycache__/
*.pyc
"""


def clean(target: Path) -> None:
    if target.exists():
        shutil.rmtree(target)
    target.mkdir(parents=True)


def copy_tree(src: Path, dst: Path, ignore_names: set[str] | None = None) -> None:
    def ignore(directory: str, names: list[str]) -> set[str]:
        ignored = set(ignore_names or set())
        if directory:
            current = Path(directory)
            if "site-packages" in current.parts:
                ignored |= {n for n in names if n == "__pycache__" or n.endswith(".pyc")}
        return {n for n in names if n in ignored or n == "__pycache__" or n.endswith(".pyc")}

    shutil.copytree(src, dst, ignore=ignore, dirs_exist_ok=True)


def copy_file(src: Path, dst: Path) -> None:
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)


def write_text(path: Path, text: str, newline: str = "\n") -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text.replace("\r\n", "\n"), encoding="utf-8", newline=newline)


def make_git() -> None:
    clean(GIT_DIR)

    copy_tree(ROOT / "WUI50", GIT_DIR / "WUI50", {"bin", "obj", ".vs"})
    copy_tree(ROOT / "modern50", GIT_DIR / "modern50")
    copy_tree(ROOT / "tools", GIT_DIR / "tools")

    for name in [
        "requirements-gpu50.txt",
        "PianoTrans-GPU50-Install.bat",
        "PianoTrans-WUI50.bat",
    ]:
        copy_file(ROOT / name, GIT_DIR / name)

    write_text(GIT_DIR / "README.md", SOURCE_README)
    write_text(GIT_DIR / ".gitignore", GITIGNORE)

    sln = GIT_DIR / "PianoTrans-WUI50.sln"
    subprocess.run(
        ["dotnet", "new", "sln", "-n", "PianoTrans-WUI50", "--format", "sln", "-o", str(GIT_DIR)],
        check=False,
    )
    if not sln.exists():
        sln.write_text("", encoding="utf-8")
    subprocess.run(
        ["dotnet", "sln", str(sln), "add", str(GIT_DIR / "WUI50" / "PianoTrans.WUI50.csproj")],
        check=False,
    )

    print(f"[ok] git source folder -> {GIT_DIR}")


def copy_app_output(target: Path) -> None:
    for item in RELEASE_OUT.iterdir():
        if item.name.endswith(".pdb"):
            continue
        dst = target / item.name
        if item.is_dir():
            copy_tree(item, dst)
        else:
            copy_file(item, dst)


def make_online() -> None:
    clean(ONLINE_DIR)

    copy_app_output(ONLINE_DIR)
    copy_tree(ROOT / "modern50", ONLINE_DIR / "modern50")
    copy_file(FFMPEG_SRC, ONLINE_DIR / "ffmpeg" / "ffmpeg.exe")
    copy_file(CHECKPOINT_SRC, ONLINE_DIR / "piano_transcription_inference_data" / CHECKPOINT_SRC.name)

    for name in ["requirements-gpu50.txt", "PianoTrans-GPU50-Install.bat"]:
        copy_file(ROOT / name, ONLINE_DIR / name)

    write_text(ONLINE_DIR / "PianoTrans-WUI50.bat", LAUNCHER_BAT, newline="\r\n")
    write_text(ONLINE_DIR / "README.md", ONLINE_README)

    print(f"[ok] online release folder -> {ONLINE_DIR}")


def copy_python_runtime(target: Path) -> None:
    base_python = Path(sys.executable).resolve().parent
    dst = target / "python312"

    def ignore(directory: str, names: list[str]) -> set[str]:
        current = Path(directory)
        try:
            rel = current.relative_to(base_python)
        except ValueError:
            rel = Path(".")

        parts = rel.parts
        if not parts:
            return {n for n in names if n in {"Doc", "include", "tcl", "Tools"}}
        if parts[0] in {"Doc", "include", "tcl", "Tools"}:
            return set(names)

        ignored: set[str] = set()
        if rel == Path("Lib"):
            ignored.update({"site-packages", "test", "idlelib", "tkinter", "turtledemo"})

        return {n for n in names if n in ignored or n == "__pycache__" or n.endswith(".pyc")}

    shutil.copytree(base_python, dst, ignore=ignore, dirs_exist_ok=True)
    print(f"[ok] bundled Python copied from {base_python}")


def make_offline() -> None:
    clean(OFFLINE_DIR)

    copy_app_output(OFFLINE_DIR)
    copy_tree(ROOT / "modern50", OFFLINE_DIR / "modern50")
    copy_file(FFMPEG_SRC, OFFLINE_DIR / "ffmpeg" / "ffmpeg.exe")
    copy_file(CHECKPOINT_SRC, OFFLINE_DIR / "piano_transcription_inference_data" / CHECKPOINT_SRC.name)
    copy_file(ROOT / "requirements-gpu50.txt", OFFLINE_DIR / "requirements-gpu50.txt")

    print("copying Python runtime ...")
    copy_python_runtime(OFFLINE_DIR)

    print("copying wheels ...")
    copy_tree(WHEELS_SRC, OFFLINE_DIR / "wheels", {"*.pyc"})

    print("copying prebuilt venv50 ...")
    copy_tree(VENV_SRC, OFFLINE_DIR / "venv50")

    cfg = OFFLINE_DIR / "venv50" / "pyvenv.cfg"
    if cfg.exists():
        text = cfg.read_text(encoding="utf-8")
        text = text.replace(str(Path(sys.executable).resolve().parent), str((OFFLINE_DIR / "python312").resolve()))
        cfg.write_text(text, encoding="utf-8")

    write_text(OFFLINE_DIR / "PianoTrans-GPU50-Install.bat", OFFLINE_INSTALL_BAT, newline="\r\n")
    write_text(OFFLINE_DIR / "PianoTrans-WUI50.bat", LAUNCHER_BAT, newline="\r\n")
    write_text(OFFLINE_DIR / "README.md", OFFLINE_README)

    print(f"[ok] offline release folder -> {OFFLINE_DIR}")


def main() -> None:
    if not RELEASE_OUT.joinpath("PianoTrans-WUI50.exe").exists():
        print("[error] Release build not found. Build Release/x64 first.")
        print(f"        {RELEASE_OUT}")
        raise SystemExit(1)
    if not CHECKPOINT_SRC.exists():
        print(f"[error] Checkpoint not found: {CHECKPOINT_SRC}")
        raise SystemExit(1)
    if not FFMPEG_SRC.exists():
        print(f"[error] ffmpeg not found: {FFMPEG_SRC}")
        raise SystemExit(1)

    make_git()
    make_online()
    make_offline()


if __name__ == "__main__":
    main()

# PianoTrans-RE (Offline release)

**Version:** v0.2  
**Updated:** 2026-08-23  
**License:** GNU General Public License v3.0  
**GitHub:** <https://github.com/lsjllxhc/PianoTrans-RE>

Everything is included in this folder: the compiled WinUI 3 app, the pretrained
model, ffmpeg, Python 3.12, all dependency wheels and a ready-to-use `venv50`.
No internet connection is needed.

Replace `logo.png` in the release root to change the in-app and About-page
logo.

## Quick start

Double-click:

```text
PianoTrans-RE.exe
```

or use `PianoTrans-RE.bat`.

If you move the folder and the prebuilt `venv50` stops working, run:

```text
PianoTrans-GPU50-Install.bat
```

Use Settings -> Language to switch between Chinese and English.

It recreates `venv50` from the bundled `python312` and installs every package
from the bundled `wheels` directory without internet access.

## GPU support

- RTX 50 / Blackwell `sm_120`: supported through PyTorch 2.7.1 + CUDA 12.8.
- Older NVIDIA GPUs supported by CUDA 12.8 also work.
- If CUDA is unavailable, the backend automatically uses CPU.

## Included files

```text
PianoTrans-RE.exe         WinUI 3 application (self-contained)
PianoTrans-RE.bat         Launcher
PianoTrans-GPU50-Install.bat Offline dependency installer / repair tool
modern50\PianoTrans-Worker.py
piano_transcription_inference_data\*.pth
ffmpeg\ffmpeg.exe
requirements-gpu50.txt
python312\                  Portable Python 3.12 runtime
venv50\                     Prebuilt Python environment
wheels\                     All dependency wheels
logo.png
logo.ico
LICENSE
README.md
README.zh-CN.md
```

## Troubleshooting

- GPU is not detected: install a recent NVIDIA driver, then rerun the app.
- CUDA error / no kernel image: use Settings -> CPU mode.
- If the app reports that Python was not found, run
  `PianoTrans-GPU50-Install.bat` once.
- Log file: `%LOCALAPPDATA%\PianoTrans-RE\app.log`.

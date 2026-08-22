# PianoTrans-RE (Online installer release)

**Version:** v0.2  
**Updated:** 2026-08-23  
**License:** GNU General Public License v3.0  
**GitHub:** <https://github.com/lsjllxhc/PianoTrans-RE>

This release contains the compiled WinUI 3 app, the pretrained model and
ffmpeg. The Python runtime packages are **not** included and are downloaded by
the setup script.

Replace `logo.png` in the release root to change the in-app and About-page
logo.

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
   PianoTrans-RE.exe
   ```

   or use `PianoTrans-RE.bat`.

The first launch creates a `venv50` folder next to the app.

## GPU support

- RTX 50 / Blackwell `sm_120`: supported through PyTorch 2.7.1 + CUDA 12.8.
- Older NVIDIA GPUs supported by CUDA 12.8 also work.
- If CUDA is unavailable, the backend automatically uses CPU.

## Included files

```text
PianoTrans-RE.exe         WinUI 3 application (self-contained)
PianoTrans-RE.bat         Launcher that checks the Python backend first
PianoTrans-GPU50-Install.bat Downloads and installs Python dependencies
modern50\PianoTrans-Worker.py
piano_transcription_inference_data\*.pth
ffmpeg\ffmpeg.exe
requirements-gpu50.txt
logo.png
LICENSE
README.md
README.zh-CN.md
```

## Troubleshooting

- GPU is not detected: install a recent NVIDIA driver, then rerun the app.
- CUDA error / no kernel image: use Settings -> CPU mode, or make sure
  `PianoTrans-GPU50-Install.bat` installed `torch 2.7.1+cu128`.
- Log file: `%LOCALAPPDATA%\PianoTrans-RE\app.log`.

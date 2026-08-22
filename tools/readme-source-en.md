# PianoTrans-RE

**Version:** v0.2  
**Updated:** 2026-08-23  
**License:** GNU General Public License v3.0  
**GitHub:** <https://github.com/lsjllxhc/PianoTrans-RE>

WinUI 3 native piano-transcription front end with an RTX 50 (Blackwell, sm_120)
compatible PyTorch backend.

This repository contains **source code only**. It does not contain compiled
binaries, the pretrained checkpoint, ffmpeg, or Python dependency packages.

Replace `logo.png` in the repository root to change the in-app and About-page
logo.

## Supported GPUs

- NVIDIA RTX 50 series: Blackwell, compute capability `sm_120`.
- Older NVIDIA GPUs down to `sm_50` that are still supported by CUDA 12.8.
- Any other machine falls back to CPU automatically.
- The backend is PyTorch `2.7.1+cu128` (CUDA 12.8).

## Repository layout

```text
WUI50/                         WinUI 3 front end (C# / XAML source)
  Pages/                       Home, Settings, Help, About pages
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
PianoTrans-RE.bat              Builds and launches the WinUI app
logo.png                       Replaceable application logo
README.md / README.zh-CN.md
```

## Build from source

Prerequisites:

1. Windows 10/11 x64.
2. Visual Studio 2022 Community or Build Tools with .NET desktop workload.
3. 64-bit Python 3.12 (the `py -3.12` launcher is used by the setup script).
4. Pretrained checkpoint:

   ```text
   https://zenodo.org/record/4034264/files/CRNN_note_F1%3D0.9677_pedal_F1%3D0.9186.pth?download=1
   ```

   Save it as:

   ```text
   piano_transcription_inference_data\note_F1=0.9677_pedal_F1=0.9186.pth
   ```

5. ffmpeg:

   Put `ffmpeg.exe` into `ffmpeg\ffmpeg.exe`, or install ffmpeg and add it to
   `PATH`.

Build and run:

```bat
PianoTrans-GPU50-Install.bat
PianoTrans-RE.bat
```

The setup script downloads PyTorch 2.7.1+cu128 and the transcription packages.

## User-adjustable recognition settings

The Settings page exposes onset/offset/frame/pedal thresholds, peak-picking
neighbors, minimum note duration, segment overlap percentage, MIDI BPM and
inference batch size. The Help page explains every parameter.

## Notes

- The old PyInstaller `PianoTrans.exe` is intentionally not part of this
  repository. It bundles PyTorch 1.10 / CUDA 11 and has no `sm_120` kernels.
- The checkpoint belongs to ByteDance's piano_transcription project; please
  keep their license/attribution when redistributing it.

# PianoTrans-RE（离线版）

**版本：** v0.2  
**更新时间：** 2026-08-23  
**License：** GNU General Public License v3.0  
**GitHub：** <https://github.com/lsjllxhc/PianoTrans-RE>

本目录已包含全部内容：WinUI 3 应用、预训练模型、ffmpeg、Python 3.12、
全部依赖 wheel 以及配置好的 `venv50`，无需联网。

如需更换 Logo，请替换本目录根部的 `logo.png`。

## 快速开始

双击：

```text
PianoTrans-RE.exe
```

或使用 `PianoTrans-RE.bat`。

如果移动整个目录后 `venv50` 失效，请运行：

```text
PianoTrans-GPU50-Install.bat
```

设置页可以切换中文 / English 界面语言。

脚本会用自带的 `python312` 和 `wheels` 完全离线重建环境。

## 显卡支持

- RTX 50 / Blackwell `sm_120`：通过 PyTorch 2.7.1 + CUDA 12.8 支持。
- 其他 CUDA 12.8 支持的 NVIDIA 显卡也可使用。
- 没有可用 CUDA 时自动使用 CPU。

## 目录内容

```text
PianoTrans-RE.exe         自包含 WinUI 3 应用
PianoTrans-RE.bat         启动器
PianoTrans-GPU50-Install.bat 离线修复 / 重建依赖
modern50\PianoTrans-Worker.py
piano_transcription_inference_data\*.pth
ffmpeg\ffmpeg.exe
requirements-gpu50.txt
python312\                 便携 Python 3.12
venv50\                    预配置 Python 环境
wheels\                    全部依赖 wheel
logo.png
logo.ico
LICENSE
README.md
README.zh-CN.md
```

## 常见问题

- 未检测到 GPU：安装较新的 NVIDIA 驱动后重试。
- 出现 CUDA no kernel image：请到设置中切换为 CPU，或重新运行安装脚本。
- 日志：`%LOCALAPPDATA%\PianoTrans-RE\app.log`。

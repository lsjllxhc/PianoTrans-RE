# PianoTrans-RE（在线安装版）

**版本：** v0.2  
**更新时间：** 2026-08-23  
**License：** GNU General Public License v3.0  
**GitHub：** <https://github.com/lsjllxhc/PianoTrans-RE>

本版本包含已编译的 WinUI 3 应用、预训练模型和 ffmpeg，不包含 Python 依赖包。
Python 依赖由安装脚本联网下载。

如需更换 Logo，请替换本目录根部的 `logo.png`。

## 快速开始

0. 请先安装 64 位 Python 3.12。

1. 双击：

   ```text
   PianoTrans-GPU50-Install.bat
   ```

   脚本会下载约 3.3 GB 的 PyTorch 2.7.1 + CUDA 12.8 及相关依赖，需要联网。

2. 双击：

   ```text
   PianoTrans-RE.exe
   ```

   或使用 `PianoTrans-RE.bat`。

首次安装后会在应用旁生成 `venv50` 目录。

设置页可以切换中文 / English 界面语言。

## 显卡支持

- RTX 50 / Blackwell `sm_120`：通过 PyTorch 2.7.1 + CUDA 12.8 支持。
- 其他 CUDA 12.8 支持的 NVIDIA 显卡也可使用。
- 没有可用 CUDA 时自动使用 CPU。

## 目录内容

```text
PianoTrans-RE.exe         自包含 WinUI 3 应用
PianoTrans-RE.bat         启动器
PianoTrans-GPU50-Install.bat 在线安装 Python 依赖
modern50\PianoTrans-Worker.py
piano_transcription_inference_data\*.pth
ffmpeg\ffmpeg.exe
requirements-gpu50.txt
logo.png
logo.ico
LICENSE
README.md
README.zh-CN.md
```

## 常见问题

- 未检测到 GPU：安装较新的 NVIDIA 驱动后重试。
- 出现 CUDA no kernel image：请确认安装脚本已安装 `torch 2.7.1+cu128`，
  或到设置中切换为 CPU。
- 日志：`%LOCALAPPDATA%\PianoTrans-RE\app.log`。

# PianoTrans-RE

**版本：** v0.2  
**更新时间：** 2026-08-23  
**License：** GNU General Public License v3.0  
**GitHub：** <https://github.com/lsjllxhc/PianoTrans-RE>

基于 WinUI 3 的原生钢琴转录前端，后端兼容 RTX 50（Blackwell，sm_120）。

本仓库只包含源代码，不包含编译产物、预训练模型、ffmpeg 和 Python 依赖包。

如需更换软件 Logo，请替换仓库根目录的 `logo.png`。界面侧边栏和「关于」页
都会读取这张图片。

## 显卡支持

- NVIDIA RTX 50：Blackwell，`sm_120`。
- 更早但仍受 CUDA 12.8 支持的 NVIDIA 显卡。
- 无可用 CUDA 时自动回退 CPU。
- 后端使用 PyTorch `2.7.1+cu128`（CUDA 12.8）。

## 仓库结构

```text
WUI50/                         WinUI 3 前端（C# / XAML 源码）
  Pages/                       主页、设置、帮助、关于页面
  Models/                      队列任务模型
  Services/                    队列管理、设置、Python 进程桥接
  PianoTrans.WUI50.csproj
modern50/
  PianoTrans-Worker.py         WinUI 使用的无界面 worker
  PianoTrans-GPU50.py          可选的 tkinter 备用启动器
tools/
  make_releases.py             生成 git / release-online / release-offline 三个目录
requirements-gpu50.txt
PianoTrans-GPU50-Install.bat   创建 venv50 并安装 PyTorch cu128 与依赖
PianoTrans-RE.bat              构建并启动 WinUI 应用
logo.png                       可替换的应用 Logo
README.md / README.zh-CN.md
```

## 从源码构建

前置条件：

1. Windows 10/11 x64。
2. Visual Studio 2022 Community 或 Build Tools，安装 .NET 桌面开发负载。
3. 64 位 Python 3.12。
4. 下载预训练 checkpoint：

   ```text
   https://zenodo.org/record/4034264/files/CRNN_note_F1%3D0.9677_pedal_F1%3D0.9186.pth?download=1
   ```

   保存为：

   ```text
   piano_transcription_inference_data\note_F1=0.9677_pedal_F1=0.9186.pth
   ```

5. 将 `ffmpeg.exe` 放入 `ffmpeg\ffmpeg.exe`，或安装 ffmpeg 并加入 `PATH`。

构建并运行：

```bat
PianoTrans-GPU50-Install.bat
PianoTrans-RE.bat
```

## 可调识别参数

设置页提供 onset / offset / frame / 踏板阈值、峰值邻域、最短音符时长、
分段重叠率、MIDI BPM、推理批大小等参数。帮助页有每个参数的详细解释。

## 说明

- 旧 PyInstaller `PianoTrans.exe` 不包含在本仓库中；它使用 PyTorch 1.10 /
  CUDA 11，没有 `sm_120` kernel。
- checkpoint 来自 ByteDance 的 piano_transcription 项目，分发时请保留其
  许可与署名。

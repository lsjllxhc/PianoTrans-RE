using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace PianoTrans.WUI50.Services;

public static class LocalizationService
{
    private static readonly List<FrameworkElement> Roots = new();

    private static readonly Dictionary<string, string> ZhToEn = new()
    {
        ["主页"] = "Home",
        ["设置"] = "Settings",
        ["帮助"] = "Help",
        ["关于"] = "About",
        ["打开文件"] = "Open files",
        ["开始"] = "Start",
        ["停止"] = "Stop",
        ["清理已完成"] = "Clear finished",
        ["也可以把音频 / 视频文件直接拖进这个窗口"] = "You can also drag audio / video files into this window",
        ["移除"] = "Remove",
        ["Python 未找到: "] = "Python not found: ",
        ["等待中"] = "Waiting",
        ["处理中"] = "Processing",
        ["已完成"] = "Completed",
        ["失败"] = "Failed",
        ["准备中"] = "Preparing",
        ["读取音频"] = "Reading audio",
        ["推理中"] = "Inferring",
        ["后处理"] = "Post-processing",
        ["写入 MIDI"] = "Writing MIDI",
        ["完成"] = "Done",
        ["已停止"] = "Stopped",
        ["错误"] = "Error",

        ["界面语言"] = "Language",
        ["推理设备"] = "Inference device",
        ["GPU（50 系 / CUDA 12.8）"] = "GPU (RTX 50 / CUDA 12.8)",
        ["GPU 模式下如果没有可用 CUDA 设备，后端会自动回退到 CPU。"] = "In GPU mode the backend automatically falls back to CPU when no CUDA device is available.",
        ["输出文件夹"] = "Output folder",
        ["浏览…"] = "Browse...",
        ["打开输出文件夹"] = "Open output folder",
        ["生成的 MIDI 会使用输入文件名，并保存到这个文件夹。"] = "Generated MIDI files use the input file name and are saved to this folder.",
        ["最短音符时长"] = "Minimum note duration",
        ["短于这个时长的音符会在写 MIDI 前被过滤掉（秒）。"] = "Notes shorter than this value are filtered out before writing MIDI (seconds).",
        ["识别参数"] = "Recognition parameters",
        ["Onset 阈值"] = "Onset threshold",
        ["Offset 阈值"] = "Offset threshold",
        ["Frame 阈值"] = "Frame threshold",
        ["踏板 Offset"] = "Pedal offset",
        ["Onset 峰值邻域"] = "Onset peak neighbor",
        ["Offset 峰值邻域"] = "Offset peak neighbor",
        ["踏板峰值邻域"] = "Pedal peak neighbor",
        ["MIDI BPM"] = "MIDI BPM",
        ["分段重叠率 (%)"] = "Segment overlap (%)",
        ["推理批大小"] = "Inference batch size",
        ["恢复默认"] = "Reset defaults",
        ["保存设置"] = "Save settings",
        ["设置已保存。"] = "Settings saved.",
        ["重叠率越高，段边界错误越少但推理越慢；0 表示无重叠、速度最快。帮助页有每个参数的详细说明。"] = "Higher overlap reduces segment-boundary errors but slows inference. 0 means no overlap and is fastest. The Help page explains every parameter.",

        ["以下参数都位于「设置 → 识别参数」中，点击「保存设置」后对新开始的队列生效。"] = "These settings are in Settings -> Recognition parameters. They apply to newly started queues after saving.",
        ["GPU：优先使用 NVIDIA 显卡，50 系使用 CUDA 12.8 / sm_120；无可用 GPU 时自动退回 CPU。"] = "GPU: prefers an NVIDIA card. RTX 50 uses CUDA 12.8 / sm_120. Falls back to CPU when no GPU is available.",
        ["CPU：强制使用处理器推理，速度较慢但兼容性最好。"] = "CPU: forces processor inference. Slower but most compatible.",
        ["所有生成的 MIDI 都写入这个文件夹，文件名与输入媒体文件同名。"] = "All generated MIDI files are written to this folder using the input file name.",
        ["判断一个音符何时开始。调低会识别出更多音符，但可能把噪声或泛音误判成新音符；调高则相反。默认 0.30。"] = "Detects when a note starts. Lower values detect more notes but may turn noise or harmonics into false notes. Higher values do the opposite. Default 0.30.",
        ["判断一个音符何时结束。调低更容易结束音符，调高则音符可能被延长。默认 0.30。"] = "Detects when a note ends. Lower values end notes more easily; higher values may extend notes. Default 0.30.",
        ["判断某个音高在当前帧是否仍在响。该值会影响长音是否被中途切断。默认 0.10。"] = "Detects whether a pitch is still active in the current frame. This affects whether long notes are cut off early. Default 0.10.",
        ["判断延音踏板何时松开。调低会让踏板释放更敏感，调高会让踏板延长。默认 0.20。"] = "Detects when the sustain pedal is released. Lower values make release more sensitive; higher values extend the pedal. Default 0.20.",
        ["在候选点前后检查多少帧都保持单调，才算一个真正的 onset / offset 峰。邻域越大越抗噪，但快速连续音符可能被合并；邻域越小越灵敏，但容易重复检测。默认：onset 2，offset 4，踏板 4。"] = "How many frames around a candidate must stay monotonic for a true onset/offset peak. Larger neighbors resist noise but may merge fast repeated notes; smaller neighbors are more sensitive but may duplicate detections. Defaults: onset 2, offset 4, pedal 4.",
        ["持续时间小于该值的音符不会写入 MIDI。用于过滤误检碎片音。默认 0.05 秒。"] = "Notes shorter than this value are not written to MIDI. Used to filter false fragment notes. Default 0.05 seconds.",
        ["音频按 10 秒一段送入模型，相邻段之间可以重叠。重叠越高，段边界越稳定、准确率通常越好，但段数增多、推理变慢；0 表示不重叠、速度最快。默认 50%。"] = "Audio is fed to the model in 10-second segments which may overlap. Higher overlap makes segment boundaries more stable and usually improves accuracy, but creates more segments and slower inference. 0 means no overlap and is fastest. Default 50%.",
        ["写入 MIDI 的速度值。只影响 MIDI 在宿主软件里的网格显示，不改变识别结果。默认 120。"] = "The tempo written into the MIDI file. It only affects grid display in a DAW and does not change recognition results. Default 120.",
        ["一次送入多少 10 秒片段。GPU 可尝试 2～4 提高速度，代价是更多显存；CPU 建议保持 1。"] = "How many 10-second segments are sent to the model at once. On GPU try 2-4 for speed at the cost of VRAM. On CPU keep 1.",
        ["调参建议"] = "Tuning tips",
        ["多音：适当调高 Onset / Frame 阈值，或增大最短音符时长。"] = "Extra notes: raise Onset / Frame thresholds or increase minimum note duration.",
        ["漏音：适当调低 Onset / Frame 阈值，并减小 onset 峰值邻域。"] = "Missing notes: lower Onset / Frame thresholds and reduce the onset peak neighbor.",
        ["结束点不准：优先微调 Offset 阈值和 Frame 阈值。"] = "Inaccurate note endings: fine-tune Offset and Frame thresholds first.",
        ["段边界问题：提高分段重叠率。"] = "Segment-boundary problems: increase segment overlap.",

        ["版本 v0.2"] = "Version v0.2",
        ["GitHub：https://github.com/lsjllxhc/PianoTrans-RE"] = "GitHub: https://github.com/lsjllxhc/PianoTrans-RE",
        ["更新时间：2026-08-23"] = "Updated: 2026-08-23",
        ["WinUI 3 原生界面 / PyTorch 2.7.1 + CUDA 12.8 转录后端"] = "WinUI 3 native UI / PyTorch 2.7.1 + CUDA 12.8 transcription backend",
        ["支持 RTX 50 系列（Blackwell, sm_120）以及更早的支持 CUDA 的 NVIDIA 显卡，无 CUDA 时自动使用 CPU。"] = "Supports RTX 50 (Blackwell, sm_120) and older CUDA-capable NVIDIA GPUs. Falls back to CPU when CUDA is unavailable.",
        ["使用说明"] = "Usage",
        ["• 主页：打开 / 拖入媒体文件，按「开始」处理队列，按「停止」中止。"] = "• Home: open or drag media files, press Start to process the queue, press Stop to cancel.",
        ["• 已完成的任务会记录在本机，下次打开不会重复处理。"] = "• Completed tasks are remembered locally and will not be processed again.",
        ["• 设置：切换 GPU / CPU、输出目录、识别参数、最短音符过滤、分段重叠率。"] = "• Settings: GPU/CPU, output folder, recognition parameters, short-note filter and segment overlap.",
        ["• 帮助：查看每个识别参数的含义。"] = "• Help: explains every recognition parameter.",

        ["转录失败"] = "Transcription failed",
        ["关闭"] = "Close",
        ["已跳过 {0} 个文件：不支持、已存在或已完成。"] = "Skipped {0} file(s): unsupported, already exists, or already completed.",
        ["已添加 {0} 个文件到队列。"] = "Added {0} file(s) to the queue.",
        ["队列里没有等待处理的任务。"] = "There are no waiting tasks in the queue.",
        ["已停止当前队列。"] = "The current queue was stopped.",
        ["当前任务使用 CPU 推理。"] = "The current task is using CPU inference.",
        ["「{0}」转录失败。\n\n{1}"] = "Transcription failed for \"{0}\".\n\n{1}",
        ["启动转录进程失败。\n\n"] = "Failed to start the transcription process.\n\n",
        ["未找到 Python 转录后端。\n\n请先运行 PianoTrans-GPU50-Install.bat，或检查 venv50 与 modern50\\PianoTrans-Worker.py 是否完整。"] = "Python transcription backend not found.\n\nRun PianoTrans-GPU50-Install.bat first, or check venv50 and modern50\\PianoTrans-Worker.py.",
        ["转录进程异常退出（代码 {0}）。\n\n{1}"] = "The transcription process exited unexpectedly (code {0}).\n\n{1}",
        ["无法打开输出文件夹："] = "Cannot open output folder: ",
        ["输出文件夹无效，请重新选择。"] = "The output folder is invalid. Please choose another one.",
        ["没有可添加的媒体文件。"] = "There are no supported media files to add.",
    };

    public static string CurrentLanguage { get; private set; } = "zh-CN";

    public static bool IsEnglish => CurrentLanguage == "en-US";

    private static readonly Dictionary<string, string> EnToZh;

    static LocalizationService()
    {
        EnToZh = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in ZhToEn)
        {
            EnToZh.TryAdd(pair.Value, pair.Key);
        }
    }

    public static string T(string text)
    {
        if (IsEnglish)
        {
            return ZhToEn.TryGetValue(text, out var en) ? en : text;
        }

        return EnToZh.TryGetValue(text, out var zh) ? zh : text;
    }

    public static string Format(string zhTemplate, params object?[] args)
        => string.Format(T(zhTemplate), args);

    public static void SetLanguage(string language)
    {
        CurrentLanguage = language == "en-US" ? "en-US" : "zh-CN";
        foreach (var root in Roots)
        {
            Apply(root);
        }
    }

    public static void Register(FrameworkElement root)
    {
        if (!Roots.Contains(root))
        {
            Roots.Add(root);
        }

        root.Loaded += (_, _) => Apply(root);
        if (root.IsLoaded)
        {
            Apply(root);
        }
    }

    public static void Apply(FrameworkElement root)
    {
        Traverse(root);
    }

    private static void Traverse(DependencyObject current)
    {
        Localize(current);
        var count = VisualTreeHelper.GetChildrenCount(current);
        for (var i = 0; i < count; i++)
        {
            Traverse(VisualTreeHelper.GetChild(current, i));
        }
    }

    private static void Localize(DependencyObject current)
    {
        switch (current)
        {
            case TextBlock textBlock:
                if (!string.IsNullOrEmpty(textBlock.Text))
                {
                    textBlock.Text = T(textBlock.Text);
                }
                break;

            case Button button:
                if (button.Content is string buttonText)
                {
                    button.Content = T(buttonText);
                }
                break;

            case HyperlinkButton hyperlink:
                if (hyperlink.Content is string linkText)
                {
                    hyperlink.Content = T(linkText);
                }
                break;

            case NavigationViewItem navigationItem:
                if (navigationItem.Content is string navText)
                {
                    navigationItem.Content = T(navText);
                }
                break;

            case NumberBox numberBox:
                if (numberBox.Header is string numberHeader)
                {
                    numberBox.Header = T(numberHeader);
                }
                break;

            case RadioButton radioButton:
                if (radioButton.Content is string radioText)
                {
                    radioButton.Content = T(radioText);
                }
                break;

            case RadioButtons radioButtons:
                if (radioButtons.Header is string radioHeader)
                {
                    radioButtons.Header = T(radioHeader);
                }
                break;

            case TextBox textBox:
                if (textBox.Header is string textHeader)
                {
                    textBox.Header = T(textHeader);
                }
                if (!string.IsNullOrEmpty(textBox.PlaceholderText))
                {
                    textBox.PlaceholderText = T(textBox.PlaceholderText);
                }
                break;

            case InfoBar infoBar:
                if (!string.IsNullOrEmpty(infoBar.Title))
                {
                    infoBar.Title = T(infoBar.Title);
                }
                if (!string.IsNullOrEmpty(infoBar.Message))
                {
                    infoBar.Message = T(infoBar.Message);
                }
                break;
        }
    }
}

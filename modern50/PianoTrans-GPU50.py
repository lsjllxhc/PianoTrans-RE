# -*- coding: utf-8 -*-
"""
PianoTrans RTX 50 (Blackwell / sm_120) compatible launcher.

The bundled PianoTrans.exe was built in 2022 with PyTorch 1.10.2 + CUDA 11.1.
That build only contains GPU kernels up to sm_86, so an RTX 50 series GPU fails
with a message like:

    CUDA error: no kernel image is available for execution on the device

This script keeps the original GUI/queue behaviour, reuses the same pretrained
checkpoint and the bundled ffmpeg, but runs on a modern PyTorch 2.7.1+cu128
(CUDA 12.8) which includes sm_120 kernels for RTX 50 GPUs.

Run it through PianoTrans-GPU50.bat.  Use --cpu to force CPU, --no-gui to
process files and exit without opening the GUI.
"""

import os
import sys

# This file lives in <PianoTrans-v1.0>/modern50.  Keep the parent directory with
# the old bundled torch/numpy/librosa folders OFF sys.path: those are Python 3.7
# packages belonging to the old PianoTrans.exe and would shadow the venv50
# site-packages if this script were placed in the repository root.
ROOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FFMPEG_DIR = os.path.join(ROOT_DIR, "ffmpeg")
DEFAULT_CHECKPOINT = os.path.join(
    ROOT_DIR,
    "piano_transcription_inference_data",
    "note_F1=0.9677_pedal_F1=0.9186.pth",
)

# Make audioread/librosa find the ffmpeg.exe that already ships with PianoTrans.
if os.path.isdir(FFMPEG_DIR):
    os.environ["PATH"] = FFMPEG_DIR + os.pathsep + os.environ.get("PATH", "")

import numpy as np
import audioread
import librosa
import torch
from piano_transcription_inference import PianoTranscription, sample_rate


def _load_audio(path, sr=sample_rate, mono=True, dtype=np.float32):
    """Load audio through the bundled ffmpeg.

    piano-transcription-inference 0.0.6 ships an old audioread helper that
    references `librosa.core.audio.util.buf_to_float`, which no longer exists
    in librosa 1.x.  This is the same ffmpeg-backed loader with the small
    conversion helpers reimplemented for modern librosa/numpy.
    """
    y = []

    with audioread.audio_open(
        os.path.realpath(path),
        backends=[audioread.ffdec.FFmpegAudioFile],
    ) as input_file:
        sr_native = input_file.samplerate
        n_channels = input_file.channels

        for frame in input_file:
            # audioread returns interleaved int16 byte buffers.
            frame_float = np.frombuffer(frame, dtype="<i2").astype(dtype) / 32768.0
            y.append(frame_float)

    if not y:
        raise RuntimeError("No audio stream found in: {}".format(path))

    y = np.concatenate(y)
    if n_channels > 1:
        y = y.reshape((-1, n_channels)).T
        if mono:
            y = np.mean(y, axis=0)
    elif mono:
        y = np.squeeze(y)

    if sr is not None and sr != sr_native:
        y = librosa.resample(y, orig_sr=sr_native, target_sr=sr, res_type="soxr_hq")

    y = np.ascontiguousarray(y, dtype=dtype)
    return y


def _torch_cuda_version():
    """Return (major, minor) for torch.version.cuda, or () if unavailable."""
    s = (torch.version.cuda or "").strip()
    try:
        parts = []
        for p in s.split("."):
            if p.isdigit():
                parts.append(int(p))
            else:
                break
        return tuple(parts)
    except Exception:
        return ()


def _select_device(force_cpu=False):
    """Pick cuda/cpu, with a few safety checks for RTX 50 cards."""
    visible = os.environ.get("CUDA_VISIBLE_DEVICES", "").strip()

    if force_cpu or visible == "-1":
        print("Forcing CPU inference (CUDA disabled).")
        return "cpu"

    if not torch.cuda.is_available():
        print("torch.cuda.is_available() returned False.")
        if torch.version.cuda is None:
            print("[warning] This PyTorch build has no CUDA support.")
            print("[warning] Run PianoTrans-GPU50-Install.bat first.")
        else:
            print("[warning] CUDA runtime/driver could not be initialised.")
        print("Falling back to CPU.")
        return "cpu"

    cuda_ver = _torch_cuda_version()
    print("PyTorch: {}  CUDA build: {}".format(torch.__version__, torch.version.cuda))
    print("Visible CUDA device(s): {}".format(torch.cuda.device_count()))

    has_blackwell = False
    for i in range(torch.cuda.device_count()):
        name = torch.cuda.get_device_name(i)
        cap = torch.cuda.get_device_capability(i)
        if cap is None:
            cap = (0, 0)
        arch = "sm_{}{}".format(cap[0], cap[1])
        print("  GPU {}: {}  (compute capability {})".format(i, name, arch))
        if cap[0] >= 12:
            has_blackwell = True
            print("    -> RTX 50 / Blackwell detected. sm_120 kernels are required.")

    if has_blackwell and cuda_ver and cuda_ver < (12, 8):
        print("[warning] RTX 50 needs PyTorch built with CUDA 12.8 or newer.")
        print("[warning] This PyTorch build uses CUDA {}. Falling back to CPU.".format(
            ".".join(str(x) for x in cuda_ver)))
        return "cpu"

    return "cuda"


class Transcribe(object):
    def __init__(self, checkpoint_path=None, force_cpu=False):
        from queue import Queue
        from threading import Thread

        self.checkpoint_path = checkpoint_path or DEFAULT_CHECKPOINT
        self.force_cpu = force_cpu
        self.transcriptor = None
        self.queue = Queue()
        Thread(target=self.worker, daemon=True).start()

    def hr(self):
        print("--------------------------------------------------------------------------------")

    def enqueue(self, files):
        for file in files:
            print("Queue: {}".format(file))
            self.queue.put(file)

    def worker(self):
        from traceback import print_exc

        device = _select_device(force_cpu=self.force_cpu)

        self.hr()
        try:
            self.transcriptor = PianoTranscription(
                device=device,
                checkpoint_path=self.checkpoint_path,
            )
        except Exception:
            print_exc()
            if device != "cpu":
                print("[fallback] GPU initialisation failed, retrying on CPU ...")
                try:
                    self.transcriptor = PianoTranscription(
                        device="cpu",
                        checkpoint_path=self.checkpoint_path,
                    )
                except Exception:
                    print_exc()
                    print("[error] Model could not be loaded even on CPU.")

        while True:
            file = self.queue.get()
            try:
                self.inference(file)
            except Exception:
                print_exc()
                print("[hint] If the error above mentions CUDA, try PianoTrans-GPU50.bat --cpu")
            finally:
                self.queue.task_done()
                if self.queue.empty():
                    self.hr()
                    print("Queue finished.")
                    self.hr()

    def inference(self, file):
        from time import time

        if self.transcriptor is None:
            raise RuntimeError("The transcription model was not initialised.")

        self.hr()
        print("Transcribe: {}".format(file))

        output_midi_path = "{}.mid".format(file)
        audio = _load_audio(file, sr=sample_rate, mono=True)

        transcribe_time = time()
        transcribed_dict = self.transcriptor.transcribe(audio, output_midi_path)
        print("Transcribe time: {:.3f} s".format(time() - transcribe_time))

        n_notes = len(transcribed_dict.get("est_note_events", ()))
        n_pedals = len(transcribed_dict.get("est_pedal_events", ()))
        print("Notes: {}  Pedal events: {}".format(n_notes, n_pedals))


class Gui(object):
    def __init__(self, transcribe):
        from tkinter import Button, Menu, Tk, scrolledtext

        self.transcribe = transcribe
        self.root = Tk()
        self.root.title("PianoTrans (RTX 50 / PyTorch 2.7)")
        self.root.config(menu=Menu(self.root))

        self.textbox = scrolledtext.ScrolledText(self.root)
        sys.stdout.write = sys.stderr.write = self.output

        button = Button(self.root, text="Add files to queue", command=self.open)
        button.pack()
        self.textbox.pack(expand="yes", fill="both")

        if not self.transcribe.queue.empty():
            self.root.after(0, lambda: None)
        self.root.after(200, self._check_queue)
        self.root.mainloop()

    def _check_queue(self):
        """Keep the GUI responsive while files are processed."""
        try:
            if self.root.winfo_exists():
                self.root.after(200, self._check_queue)
        except Exception:
            pass

    def open(self):
        from tkinter import filedialog

        files = filedialog.askopenfilenames(
            title="Select audio/video files (hold CTRL for multiple)",
            filetypes=[("audio/video files", "*")],
        )
        files = self.root.tk.splitlist(files)
        self.transcribe.enqueue(files)

    def output(self, s):
        self.textbox.insert("end", s)
        self.textbox.see("end")


def _usage(command):
    print("\nUsage:")
    print("  {} [options] [file1 file2 ...]".format(command))
    print("Options:")
    print("  --cpu      force CPU inference")
    print("  --gpu      force GPU inference (default when CUDA is available)")
    print("  --no-gui   process the files and exit without opening the GUI")


def main(argv=None):
    from tkinter import TclError

    args = list(sys.argv[1:] if argv is None else argv)
    force_cpu = "--cpu" in args
    force_gpu = "--gpu" in args or "--cuda" in args
    no_gui = "--no-gui" in args
    files = tuple(a for a in args if not a.startswith("--"))

    if force_cpu and force_gpu:
        print("[warning] Both --cpu and --gpu were given; using --gpu.")
        force_cpu = False

    if not os.path.exists(DEFAULT_CHECKPOINT):
        print("[error] Checkpoint not found:")
        print("  {}".format(DEFAULT_CHECKPOINT))
        print("Please put the piano-transcription checkpoint back into the")
        print("piano_transcription_inference_data folder.")
        return 1

    transcribe = Transcribe(
        checkpoint_path=DEFAULT_CHECKPOINT,
        force_cpu=force_cpu,
    )

    if files:
        transcribe.enqueue(files)

    if no_gui:
        transcribe.queue.join()
        return 0

    try:
        Gui(transcribe)
    except TclError as e:
        print("Error open GUI: {}".format(e))
        _usage(sys.argv[0])
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())

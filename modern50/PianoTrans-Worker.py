# -*- coding: utf-8 -*-
"""
Headless transcription worker used by the WinUI 3 front end.

Protocol
--------
The C# app writes a small JSON manifest and starts:

    venv50\\Scripts\\python.exe modern50\\PianoTrans-Worker.py --manifest <file>

Every line written to stdout that starts with '{' is one JSON message:

    {"type":"worker_start", "torch":"...", "cuda_build":"..."}
    {"type":"device", "device":"cuda"}
    {"type":"job_start",  "index":0, "input":"...", "output":"..."}
    {"type":"progress",   "index":0, "progress":0.42, "stage":"inference"}
    {"type":"job_done",   "index":0, "notes":123, "pedals":12, "elapsed":4.2,
                          "filtered_short_notes":3}
    {"type":"job_error",  "index":0, "message":"..."}
    {"type":"worker_done"}

Any other stdout line (for example the old Python package banner) must be
ignored by the front end.
"""

import argparse
import json
import os
import sys
import time
import traceback

# Keep the old Python 3.7 torch/numpy/librosa folders in the repository root
# OFF sys.path.  This file lives in modern50/, so its directory is safe.
ROOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FFMPEG_DIR = os.path.join(ROOT_DIR, "ffmpeg")
DEFAULT_CHECKPOINT = os.path.join(
    ROOT_DIR,
    "piano_transcription_inference_data",
    "note_F1=0.9677_pedal_F1=0.9186.pth",
)

if os.path.isdir(FFMPEG_DIR):
    os.environ["PATH"] = FFMPEG_DIR + os.pathsep + os.environ.get("PATH", "")

import numpy as np
import audioread
import librosa
import torch
from piano_transcription_inference import PianoTranscription
from piano_transcription_inference.utilities import (
    RegressionPostProcessor,
    write_events_to_midi,
)
from piano_transcription_inference.pytorch_utils import move_data_to_device


def emit(obj):
    print(json.dumps(obj, ensure_ascii=False), flush=True)


def load_audio(path, sr=16000, mono=True, dtype=np.float32):
    """Load audio/video through the bundled ffmpeg."""
    y = []
    with audioread.audio_open(
        os.path.realpath(path),
        backends=[audioread.ffdec.FFmpegAudioFile],
    ) as input_file:
        sr_native = input_file.samplerate
        n_channels = input_file.channels
        for frame in input_file:
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

    return np.ascontiguousarray(y, dtype=dtype)


def choose_device(requested):
    """Return 'cuda' when requested and available, otherwise 'cpu'."""
    visible = os.environ.get("CUDA_VISIBLE_DEVICES", "").strip()
    if requested == "cpu" or visible == "-1":
        return "cpu"
    if requested == "gpu" and torch.cuda.is_available():
        return "cuda"
    return "cpu"


def load_transcriptor(device, checkpoint_path):
    """Load the model, retrying on CPU when GPU initialisation fails."""
    try:
        transcriptor = PianoTranscription(
            device=device,
            checkpoint_path=checkpoint_path,
        )
        return transcriptor, device
    except Exception:
        if device == "cuda":
            print("GPU model load failed, retrying on CPU.", flush=True)
            traceback.print_exc()
            return PianoTranscription(device="cpu", checkpoint_path=checkpoint_path), "cpu"
        raise


def _append_output(output_dict, key, value):
    if key in output_dict:
        output_dict[key].append(value)
    else:
        output_dict[key] = [value]


def forward_with_progress(model, x, batch_size, on_progress):
    """Same mini-batch loop as piano_transcription_inference, with progress."""
    output_dict = {}
    device = next(model.parameters()).device
    pointer = 0
    while pointer < len(x):
        batch_waveform = move_data_to_device(x[pointer : pointer + batch_size], device)
        pointer += batch_size
        with torch.no_grad():
            model.eval()
            batch_output_dict = model(batch_waveform)

        for key in batch_output_dict.keys():
            _append_output(output_dict, key, batch_output_dict[key].data.cpu().numpy())

        on_progress(pointer / float(len(x)))

    for key in output_dict.keys():
        output_dict[key] = np.concatenate(output_dict[key], axis=0)

    return output_dict


def process_job(transcriptor, job, index, min_note_duration):
    input_path = job.get("input")
    output_path = job.get("output")
    emit({"type": "job_start", "index": index, "input": input_path, "output": output_path})
    started = time.time()

    try:
        emit({"type": "progress", "index": index, "progress": 0.02, "stage": "audio"})
        audio = load_audio(input_path, sr=16000, mono=True)

        audio = audio[None, :]  # (1, samples)
        audio_len = audio.shape[1]
        pad_len = (
            int(np.ceil(audio_len / transcriptor.segment_samples))
            * transcriptor.segment_samples
            - audio_len
        )
        audio = np.concatenate((audio, np.zeros((1, pad_len))), axis=1)
        segments = transcriptor.enframe(audio, transcriptor.segment_samples)

        def on_segment(fraction):
            # Audio load + inference occupy 2%..88% of the visible progress.
            emit({
                "type": "progress",
                "index": index,
                "progress": 0.02 + 0.86 * fraction,
                "stage": "inference",
            })

        output_dict = forward_with_progress(transcriptor.model, segments, 1, on_segment)

        emit({"type": "progress", "index": index, "progress": 0.90, "stage": "postprocess"})
        for key in output_dict.keys():
            output_dict[key] = transcriptor.deframe(output_dict[key])[0:audio_len]

        post_processor = RegressionPostProcessor(
            transcriptor.frames_per_second,
            classes_num=transcriptor.classes_num,
            onset_threshold=transcriptor.onset_threshold,
            offset_threshold=transcriptor.offset_threshod,
            frame_threshold=transcriptor.frame_threshold,
            pedal_offset_threshold=transcriptor.pedal_offset_threshold,
        )
        est_note_events, est_pedal_events = post_processor.output_dict_to_midi_events(output_dict)

        raw_notes = len(est_note_events)
        filtered = [
            event
            for event in est_note_events
            if (event["offset_time"] - event["onset_time"]) >= min_note_duration
        ]
        filtered_short_notes = raw_notes - len(filtered)

        emit({"type": "progress", "index": index, "progress": 0.97, "stage": "write_midi"})
        if output_path:
            out_dir = os.path.dirname(os.path.abspath(output_path))
            os.makedirs(out_dir, exist_ok=True)
            write_events_to_midi(
                start_time=0,
                note_events=filtered,
                pedal_events=est_pedal_events,
                midi_path=output_path,
            )

        emit({
            "type": "job_done",
            "index": index,
            "notes": len(filtered),
            "pedals": len(est_pedal_events),
            "elapsed": round(time.time() - started, 3),
            "filtered_short_notes": filtered_short_notes,
        })
    except Exception as exc:
        traceback.print_exc()
        emit({
            "type": "job_error",
            "index": index,
            "message": "{}: {}".format(type(exc).__name__, exc),
        })


def run_manifest(manifest):
    checkpoint = manifest.get("checkpoint") or DEFAULT_CHECKPOINT
    if not os.path.exists(checkpoint):
        raise RuntimeError("Checkpoint not found: {}".format(checkpoint))

    device = choose_device(manifest.get("device", "gpu"))
    emit({
        "type": "worker_start",
        "torch": torch.__version__,
        "cuda_build": torch.version.cuda,
    })
    emit({"type": "device", "device": device})

    transcriptor, actual_device = load_transcriptor(device, checkpoint)
    if actual_device != device:
        emit({"type": "device", "device": actual_device})

    min_note_duration = float(manifest.get("min_note_duration", 0.05))
    jobs = manifest.get("jobs", [])
    for index, job in enumerate(jobs):
        process_job(transcriptor, job, index, min_note_duration)

    emit({"type": "worker_done"})


def run_single(args):
    checkpoint = args.checkpoint or DEFAULT_CHECKPOINT
    manifest = {
        "device": args.device,
        "min_note_duration": args.min_note_duration,
        "checkpoint": checkpoint,
        "jobs": [{"input": args.input, "output": args.output}],
    }
    run_manifest(manifest)


def main(argv=None):
    parser = argparse.ArgumentParser(description="PianoTrans headless worker")
    parser.add_argument("--manifest", help="JSON manifest with device + jobs")
    parser.add_argument("--input")
    parser.add_argument("--output")
    parser.add_argument("--device", choices=("gpu", "cpu"), default="gpu")
    parser.add_argument("--min-note-duration", type=float, default=0.05)
    parser.add_argument("--checkpoint")
    args = parser.parse_args(argv)

    if args.manifest:
        with open(args.manifest, "r", encoding="utf-8") as f:
            manifest = json.load(f)
        run_manifest(manifest)
    elif args.input:
        run_single(args)
    else:
        parser.error("Either --manifest or --input is required")


if __name__ == "__main__":
    main()

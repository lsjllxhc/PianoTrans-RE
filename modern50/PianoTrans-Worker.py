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
from piano_transcription_inference.utilities import RegressionPostProcessor
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


def _accumulate_segment_outputs(accumulators, counts, outputs, base_segment_index, step_samples, frames_needed):
    """Overlap-add one model batch into float32 accumulators."""
    for key, value in outputs.items():
        value = value.data.cpu().numpy()
        batch_size, seg_frames, classes_num = value.shape
        usable_frames = max(1, seg_frames - 1)

        if key not in accumulators:
            accumulators[key] = np.zeros((frames_needed, classes_num), dtype=np.float32)
            counts[key] = np.zeros((frames_needed, 1), dtype=np.float32)

        for i in range(batch_size):
            segment_index = base_segment_index + i
            start_frame = segment_index * step_samples // 160
            end_frame = min(frames_needed, start_frame + usable_frames)
            if end_frame <= start_frame:
                continue
            accumulators[key][start_frame:end_frame] += value[i, : end_frame - start_frame, :]
            counts[key][start_frame:end_frame] += 1.0


def forward_segments_with_overlap(model, audio, segment_samples, step_samples, batch_size, audio_len, on_progress):
    """Stream segments through the model and overlap-add predictions immediately.

    This intentionally avoids storing every segment and every model output at
    the same time, which was the cause of the previous memory explosion on
    long audio files.
    """
    padded_len = audio.shape[1]
    frames_needed = max(1, int(np.ceil(audio_len / 160.0)))
    total_segments = 1 + (padded_len - segment_samples) // step_samples if padded_len >= segment_samples else 1

    accumulators = {}
    counts = {}
    pointer = 0
    completed = 0
    device = next(model.parameters()).device

    while pointer + segment_samples <= padded_len:
        batch = []
        while len(batch) < batch_size and pointer + segment_samples <= padded_len:
            batch.append(audio[:, pointer : pointer + segment_samples])
            pointer += step_samples

        batch_count = len(batch)
        try:
            batch_waveform = move_data_to_device(np.concatenate(batch, axis=0), device)
            with torch.no_grad():
                model.eval()
                batch_output_dict = model(batch_waveform)
            _accumulate_segment_outputs(accumulators, counts, batch_output_dict, completed, step_samples, frames_needed)
            del batch_waveform, batch_output_dict
        except torch.cuda.OutOfMemoryError:
            torch.cuda.empty_cache()
            print("GPU out of memory for batch size {}, retrying this batch one segment at a time.".format(batch_count), flush=True)
            for i in range(batch_count):
                single_waveform = move_data_to_device(batch[i], device)
                with torch.no_grad():
                    model.eval()
                    single_output = model(single_waveform)
                _accumulate_segment_outputs(accumulators, counts, single_output, completed + i, step_samples, frames_needed)
                del single_waveform, single_output
            torch.cuda.empty_cache()

        del batch
        completed += batch_count
        on_progress(min(1.0, completed / float(total_segments)))

    result = {}
    for key, accumulator in accumulators.items():
        result[key] = (accumulator / np.maximum(counts[key], 1.0)).astype(np.float32)

    return result


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


def enframe_with_overlap(audio, segment_samples, overlap_percent):
    """Cut audio into 10-second segments with a configurable overlap."""
    overlap = min(75.0, max(0.0, float(overlap_percent)))
    step_samples = max(160, int(round(segment_samples * (1.0 - overlap / 100.0) / 160.0) * 160))
    batch = []
    pointer = 0
    while pointer + segment_samples <= audio.shape[1]:
        batch.append(audio[:, pointer : pointer + segment_samples])
        pointer += step_samples
    if not batch:
        batch.append(audio)
    return np.concatenate(batch, axis=0), step_samples


def overlap_add_outputs(output_dict, segment_count, seg_frames, step_samples, audio_len):
    """Average predictions of overlapping segments instead of hard cropping."""
    usable_frames = max(1, seg_frames - 1)  # drop the extra center-padding frame
    frames_needed = max(1, int(np.ceil(audio_len / 160.0)))
    result = {}

    for key, value in output_dict.items():
        value = value.reshape(segment_count, seg_frames, -1)
        classes_num = value.shape[2]
        accumulator = np.zeros((frames_needed, classes_num), dtype=np.float64)
        counts = np.zeros((frames_needed, 1), dtype=np.float64)

        for segment_index in range(segment_count):
            start_frame = int(round(segment_index * step_samples / 160.0))
            end_frame = min(frames_needed, start_frame + usable_frames)
            if end_frame <= start_frame:
                continue
            segment_output = value[segment_index, : end_frame - start_frame, :]
            accumulator[start_frame:end_frame] += segment_output
            counts[start_frame:end_frame] += 1.0

        result[key] = (accumulator / np.maximum(counts, 1.0)).astype(np.float32)

    return result


def postprocess_to_events(transcriptor, output_dict, params):
    """Run thresholding / peak picking with user-adjustable parameters."""
    post_processor = RegressionPostProcessor(
        transcriptor.frames_per_second,
        classes_num=transcriptor.classes_num,
        onset_threshold=params["onset_threshold"],
        offset_threshold=params["offset_threshold"],
        frame_threshold=params["frame_threshold"],
        pedal_offset_threshold=params["pedal_offset_threshold"],
    )

    output_dict = dict(output_dict)

    onset_output, onset_shift = post_processor.get_binarized_output_from_regression(
        output_dict["reg_onset_output"],
        threshold=params["onset_threshold"],
        neighbour=params["onset_peak_neighbor"],
    )
    output_dict["onset_output"] = onset_output
    output_dict["onset_shift_output"] = onset_shift

    offset_output, offset_shift = post_processor.get_binarized_output_from_regression(
        output_dict["reg_offset_output"],
        threshold=params["offset_threshold"],
        neighbour=params["offset_peak_neighbor"],
    )
    output_dict["offset_output"] = offset_output
    output_dict["offset_shift_output"] = offset_shift

    if "reg_pedal_offset_output" in output_dict:
        pedal_offset_output, pedal_offset_shift = post_processor.get_binarized_output_from_regression(
            output_dict["reg_pedal_offset_output"],
            threshold=params["pedal_offset_threshold"],
            neighbour=params["pedal_offset_peak_neighbor"],
        )
        output_dict["pedal_offset_output"] = pedal_offset_output
        output_dict["pedal_offset_shift_output"] = pedal_offset_shift

    est_on_off_note_vels = post_processor.output_dict_to_detected_notes(output_dict)
    est_note_events = post_processor.detected_notes_to_events(est_on_off_note_vels)

    est_pedal_events = []
    if "reg_pedal_offset_output" in output_dict:
        est_pedal_on_offs = post_processor.output_dict_to_detected_pedals(output_dict)
        est_pedal_events = post_processor.detected_pedals_to_events(est_pedal_on_offs)

    return est_note_events, est_pedal_events


def write_midi_with_bpm(start_time, note_events, pedal_events, midi_path, bpm):
    """Write MIDI with a user-selected tempo (default 120 BPM)."""
    from mido import Message, MidiFile, MidiTrack, MetaMessage

    ticks_per_beat = 384
    beats_per_second = max(1.0, bpm) / 60.0
    ticks_per_second = ticks_per_beat * beats_per_second
    microseconds_per_beat = int(round(60_000_000.0 / max(1.0, bpm)))

    midi_file = MidiFile()
    midi_file.ticks_per_beat = ticks_per_beat

    track0 = MidiTrack()
    track0.append(MetaMessage("set_tempo", tempo=microseconds_per_beat, time=0))
    track0.append(MetaMessage("time_signature", numerator=4, denominator=4, time=0))
    track0.append(MetaMessage("end_of_track", time=1))
    midi_file.tracks.append(track0)

    track1 = MidiTrack()
    message_roll = []

    for note_event in note_events:
        velocity = int(round(float(note_event.get("velocity", 0))))
        velocity = max(0, min(127, velocity))
        message_roll.append({
            "time": float(note_event["onset_time"]),
            "midi_note": int(note_event["midi_note"]),
            "velocity": velocity,
        })
        message_roll.append({
            "time": float(note_event["offset_time"]),
            "midi_note": int(note_event["midi_note"]),
            "velocity": 0,
        })

    for pedal_event in pedal_events or []:
        message_roll.append({"time": float(pedal_event["onset_time"]), "control_change": 64, "value": 127})
        message_roll.append({"time": float(pedal_event["offset_time"]), "control_change": 64, "value": 0})

    message_roll.sort(key=lambda item: item["time"])

    previous_ticks = 0
    for message in message_roll:
        this_ticks = int((message["time"] - start_time) * ticks_per_second)
        if this_ticks < 0:
            continue
        diff_ticks = max(0, this_ticks - previous_ticks)
        previous_ticks = this_ticks
        if "midi_note" in message:
            track1.append(Message("note_on", note=message["midi_note"], velocity=message["velocity"], time=diff_ticks))
        elif "control_change" in message:
            track1.append(Message("control_change", channel=0, control=message["control_change"], value=message["value"], time=diff_ticks))

    track1.append(MetaMessage("end_of_track", time=1))
    midi_file.tracks.append(track1)
    midi_file.save(midi_path)


def process_job(transcriptor, job, index, params):
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
        overlap_percent = min(75.0, max(0.0, float(params.get("segment_overlap_percent", 50))))
        step_samples = max(
            160,
            int(round(transcriptor.segment_samples * (1.0 - overlap_percent / 100.0) / 160.0) * 160),
        )

        def on_segment(fraction):
            # Audio load + inference occupy 2%..88% of the visible progress.
            emit({
                "type": "progress",
                "index": index,
                "progress": 0.02 + 0.86 * fraction,
                "stage": "inference",
            })

        batch_size = max(1, int(params.get("batch_size", 1)))
        output_dict = forward_segments_with_overlap(
            transcriptor.model,
            audio,
            transcriptor.segment_samples,
            step_samples,
            batch_size,
            audio_len,
            on_segment,
        )

        emit({"type": "progress", "index": index, "progress": 0.90, "stage": "postprocess"})

        est_note_events, est_pedal_events = postprocess_to_events(transcriptor, output_dict, params)

        raw_notes = len(est_note_events)
        min_note_duration = params["min_note_duration"]
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
            write_midi_with_bpm(
                start_time=0,
                note_events=filtered,
                pedal_events=est_pedal_events,
                midi_path=output_path,
                bpm=params["bpm"],
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

    params = {
        "min_note_duration": float(manifest.get("min_note_duration", 0.05)),
        "onset_threshold": float(manifest.get("onset_threshold", 0.30)),
        "offset_threshold": float(manifest.get("offset_threshold", 0.30)),
        "frame_threshold": float(manifest.get("frame_threshold", 0.10)),
        "pedal_offset_threshold": float(manifest.get("pedal_offset_threshold", 0.20)),
        "onset_peak_neighbor": int(manifest.get("onset_peak_neighbor", 2)),
        "offset_peak_neighbor": int(manifest.get("offset_peak_neighbor", 4)),
        "pedal_offset_peak_neighbor": int(manifest.get("pedal_offset_peak_neighbor", 4)),
        "bpm": float(manifest.get("bpm", 120)),
        "batch_size": int(manifest.get("batch_size", 1)),
        "segment_overlap_percent": float(manifest.get("segment_overlap_percent", 50)),
    }
    jobs = manifest.get("jobs", [])
    for index, job in enumerate(jobs):
        process_job(transcriptor, job, index, params)

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

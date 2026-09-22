#!/usr/bin/env python3
"""Verify opt-in ChapterDurationTests generated AAC split outputs with FFmpeg.
Usage: verify-presented-chapters.py <encoder-signal-fixtures> <split-output> <receipt.json>
No provider media or network is used. Requires ffmpeg, ffprobe and numpy.
"""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import numpy as np


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def packets(path):
    command = [shutil.which('ffprobe'), '-v', 'error', '-select_streams', 'a:0',
               '-show_packets', '-show_entries', 'packet=data_hash',
               '-show_data_hash', 'sha256', '-of', 'json', str(path)]
    result = subprocess.run(command, capture_output=True, timeout=30, check=True)
    require(not result.stderr, 'packet probe reported an error')
    return [p['data_hash'] for p in json.loads(result.stdout)['packets']]


def decode(path, channels):
    result = subprocess.run([shutil.which('ffmpeg'), '-v', 'error', '-xerror', '-i', str(path),
                             '-map', '0:a:0', '-f', 'f32le', '-'], capture_output=True, timeout=30, check=True)
    require(not result.stderr, 'independent decoder reported an error')
    values = np.frombuffer(result.stdout, dtype='<f4').astype(np.float64).reshape((-1, channels))
    require(np.isfinite(values).all(), 'nonfinite decoded output')
    return values


def cosine(a, b):
    denominator = np.linalg.norm(a) * np.linalg.norm(b)
    require(denominator > 0, 'silent signal cannot establish alignment')
    return float(a @ b / denominator)


def verify(inputs, outputs, receipt):
    receipt.unlink(missing_ok=True)
    expected = {name + '-' + mode for name in ('single-16001-16000-1-0-0',
                'single-16001-16000-1-100-100', 'single-16001-44100-2-17-31')
                for mode in ('single', 'split')}
    manifests = {p.parent.name: p for p in outputs.glob('*/manifest.json')}
    require(set(manifests) == expected, 'missing or unexpected split fixtures')
    results = []
    for name, path in sorted(manifests.items()):
        meta = json.loads(path.read_text())
        original = inputs / (meta['fixture'] + '.m4a')
        reference = inputs / (meta['fixture'] + '.s16')
        original_meta = json.loads((inputs / (meta['fixture'] + '.json')).read_text())
        require(sha(original) == meta['input_sha256'] == original_meta['mp4_sha256'], 'source AAC changed')
        require(sha(reference) == original_meta['pcm_sha256'], 'source PCM changed')
        channels = meta['channels']
        pcm = np.frombuffer(reference.read_bytes(), dtype='<i2').astype(np.float64).reshape((-1, channels)) / 32768
        original_packets = packets(original)
        previous_end = None
        for part in meta['parts']:
            output = path.parent / part['file']
            require(sha(output) == part['sha256'], 'split output changed')
            start, end = part['start_sample'], part['end_sample']
            require(0 <= start < end <= len(pcm), 'invalid presented interval')
            require(previous_end is None or start == previous_end, 'chapter gap or overlap')
            previous_end = end
            actual = decode(output, channels)
            require(len(actual) == end - start, f'{name}/{part["file"]}: sample count {len(actual)} != {end-start}')
            coded = packets(output)
            require(coded and any(original_packets[i:i + len(coded)] == coded for i in range(len(original_packets))),
                    'remuxed compressed samples are not a contiguous unchanged source interval')
            scores = []
            for channel in range(channels):
                target = pcm[start:end, channel]
                samples = actual[:, channel]
                width = min(256, len(samples))
                require(np.linalg.norm(samples[:width]) > 0, f'{name}/{part["file"]} channel {channel}: silent output head')
                score = {'whole': cosine(samples, target), 'head': cosine(samples[:width], target[:width]),
                         'tail': cosine(samples[-width:], target[-width:])}
                require(min(score.values()) > .98, f'{name}/{part["file"]} channel {channel}: lost signal alignment {score}')
                scores.append(score)
            results.append({'case': name, **part, 'decoded_samples': len(actual),
                            'unchanged_compressed_samples': len(coded), 'cosine': scores})
    receipt.write_text(json.dumps({'schema': 1, 'results': results, 'verifier_sha256': sha(Path(__file__)),
        'ffmpeg': subprocess.check_output([shutil.which('ffmpeg'), '-version'], text=True).splitlines()[0],
        'limits': 'Synthetic AAC-LC remux, mono/stereo, 16/44.1 kHz. No provider, USAC, MP3, player or installed-app acceptance.'}, indent=2) + '\n')
    print(f'PASS {len(results)} standalone AAC chapter files: exact samples, unchanged compressed payload, aligned head/tail')


if __name__ == '__main__':
    require(len(sys.argv) == 4, __doc__)
    require(shutil.which('ffmpeg') and shutil.which('ffprobe'), 'ffmpeg and ffprobe are required')
    verify(*(Path(p).resolve() for p in sys.argv[1:]))

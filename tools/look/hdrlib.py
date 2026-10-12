"""Radiance .hdr (RGBE) read/write with numpy, no other dependencies.

Used by tools/look/hdri-prep.py (the Look agent's sky preparation for Visiting Hours). Reads flat and new-style RLE
scanlines; writes new-style RLE so the output stays about the size of Poly Haven's own files.
"""
import numpy as np


def read_hdr(path):
    with open(path, "rb") as f:
        data = f.read()
    pos = 0
    # Header: lines until an empty line, then the resolution line.
    while True:
        end = data.index(b"\n", pos)
        line = data[pos:end]
        pos = end + 1
        if line.strip() == b"":
            break
    end = data.index(b"\n", pos)
    res = data[pos:end].split()
    pos = end + 1
    if res[0] != b"-Y" or res[2] != b"+X":
        raise ValueError("unsupported orientation: %r" % res)
    h, w = int(res[1]), int(res[3])
    buf = np.frombuffer(data, dtype=np.uint8, offset=pos)
    out = np.zeros((h, w, 4), dtype=np.uint8)
    p = 0
    for y in range(h):
        if w < 8 or w > 0x7FFF or buf[p] != 2 or buf[p + 1] != 2 or (buf[p + 2] & 0x80):
            out[y] = buf[p:p + w * 4].reshape(w, 4)
            p += w * 4
            continue
        if (int(buf[p + 2]) << 8 | int(buf[p + 3])) != w:
            raise ValueError("bad scanline width")
        p += 4
        for c in range(4):
            x = 0
            row = out[y, :, c]
            while x < w:
                n = int(buf[p]); p += 1
                if n > 128:
                    n -= 128
                    row[x:x + n] = buf[p]; p += 1
                else:
                    row[x:x + n] = buf[p:p + n]; p += n
                x += n
    rgbe = out.astype(np.float32)
    e = out[:, :, 3].astype(np.int32)
    scale = np.where(e > 0, np.ldexp(1.0, e - 136), 0.0).astype(np.float32)
    return rgbe[:, :, :3] * scale[:, :, None]


def _rle_channel(row):
    """New-style RLE of one channel of one scanline."""
    out = bytearray()
    w = len(row)
    x = 0
    while x < w:
        # A run of 3+ equal bytes?
        run = 1
        while x + run < w and run < 127 and row[x + run] == row[x]:
            run += 1
        if run >= 3:
            out.append(128 + run); out.append(int(row[x]))
            x += run
            continue
        # Literal span until the next run of 3+.
        start = x
        while x < w and x - start < 128:
            if x + 2 < w and row[x] == row[x + 1] == row[x + 2]:
                break
            x += 1
        out.append(x - start)
        out.extend(bytes(row[start:x]))
    return out


def write_hdr(path, rgb):
    h, w, _ = rgb.shape
    rgb = np.maximum(rgb.astype(np.float32), 0.0)
    m = rgb.max(axis=2)
    mant, ex = np.frexp(m)
    valid = m > 1e-32
    scale = np.where(valid, mant * 256.0 / np.where(valid, m, 1.0), 0.0)
    out = np.zeros((h, w, 4), dtype=np.uint8)
    out[:, :, :3] = np.clip(rgb * scale[:, :, None], 0, 255).astype(np.uint8)
    out[:, :, 3] = np.where(valid, ex + 128, 0).astype(np.uint8)
    with open(path, "wb") as f:
        f.write(b"#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n")
        f.write(("-Y %d +X %d\n" % (h, w)).encode())
        for y in range(h):
            f.write(bytes([2, 2, (w >> 8) & 0xFF, w & 0xFF]))
            for c in range(4):
                f.write(_rle_channel(out[y, :, c]))


def luminance(rgb):
    return rgb[..., 0] * 0.2126 + rgb[..., 1] * 0.7152 + rgb[..., 2] * 0.0722


def direction(u, v):
    """Equirect (u right, v down, both 0-1) to a Unity-style direction (y up). u = 0.5 looks down +Z (Unity's
    Skybox/Panoramic and our sky shader use the same mapping: azimuth = (u - 0.5) * 360, measured from +Z towards +X)."""
    az = (u - 0.5) * 2.0 * np.pi
    el = (0.5 - v) * np.pi
    return np.stack([np.cos(el) * np.sin(az), np.sin(el), np.cos(el) * np.cos(az)], axis=-1)

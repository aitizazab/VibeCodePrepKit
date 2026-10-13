"""Reads a binary FBX without Blender or Unity (Task 20) and prints what Unity's importer will see: the global axis and
unit settings, and every Model node's name, type and local translation / rotation / scaling. Used to check that a bot's
nodes come in with identity rotations and unit scale (SkylineBot sets the rig's and the spinners' rotations itself) and
that the named children of the model contract are present.

    uv run --no-project python tools/skyline/bots/fbx_inspect.py <model.fbx> [--require Plate_lid,WeakPoint,...]
"""
from __future__ import annotations

import struct
import sys
import zlib


def _read_props(buf, pos, count):
    props = []
    for _ in range(count):
        t = chr(buf[pos]); pos += 1
        if t == "Y":
            props.append(struct.unpack_from("<h", buf, pos)[0]); pos += 2
        elif t == "C":
            props.append(bool(buf[pos])); pos += 1
        elif t == "I":
            props.append(struct.unpack_from("<i", buf, pos)[0]); pos += 4
        elif t == "F":
            props.append(struct.unpack_from("<f", buf, pos)[0]); pos += 4
        elif t == "D":
            props.append(struct.unpack_from("<d", buf, pos)[0]); pos += 8
        elif t == "L":
            props.append(struct.unpack_from("<q", buf, pos)[0]); pos += 8
        elif t in "fdlib":
            n, enc, clen = struct.unpack_from("<III", buf, pos); pos += 12
            raw = buf[pos:pos + clen]; pos += clen
            if enc == 1:
                raw = zlib.decompress(raw)
            fmt = {"f": "f", "d": "d", "l": "q", "i": "i", "b": "?"}[t]
            props.append(list(struct.unpack("<%d%s" % (n, fmt), raw)))
        elif t in "SR":
            n = struct.unpack_from("<I", buf, pos)[0]; pos += 4
            raw = buf[pos:pos + n]; pos += n
            props.append(raw.decode("utf-8", "replace") if t == "S" else raw)
        else:
            raise ValueError("unknown property type %r at %d" % (t, pos))
    return props, pos


def _read_node(buf, pos, wide):
    if wide:
        end, nprops, _plen = struct.unpack_from("<QQQ", buf, pos); pos += 24
    else:
        end, nprops, _plen = struct.unpack_from("<III", buf, pos); pos += 12
    nlen = buf[pos]; pos += 1
    if end == 0:
        return None, pos
    name = buf[pos:pos + nlen].decode("ascii", "replace"); pos += nlen
    props, pos = _read_props(buf, pos, nprops)
    children = []
    while pos < end:
        child, pos = _read_node(buf, pos, wide)
        if child is None:
            break
        children.append(child)
    return {"name": name, "props": props, "children": children}, end


def parse(path):
    buf = open(path, "rb").read()
    if not buf.startswith(b"Kaydara FBX Binary"):
        raise ValueError("not a binary FBX")
    version = struct.unpack_from("<I", buf, 23)[0]
    wide = version >= 7500
    pos, nodes = 27, []
    while pos < len(buf):
        node, pos = _read_node(buf, pos, wide)
        if node is None:
            break
        nodes.append(node)
    return version, nodes


def find(nodes, name):
    return [n for n in nodes if n["name"] == name]


def p70(node):
    out = {}
    for c in node["children"]:
        if c["name"] == "Properties70":
            for p in c["children"]:
                out[p["props"][0]] = p["props"][4:]
    return out


def main(argv):
    path = argv[0]
    require = []
    if "--require" in argv:
        require = argv[argv.index("--require") + 1].split(",")
    version, nodes = parse(path)
    gs = find(nodes, "GlobalSettings")
    settings = p70(gs[0]) if gs else {}
    print("FBX", version)
    for key in ("UpAxis", "UpAxisSign", "FrontAxis", "FrontAxisSign", "CoordAxis", "CoordAxisSign", "UnitScaleFactor", "OriginalUnitScaleFactor"):
        print(f"  {key} = {settings.get(key)}")
    objects = find(nodes, "Objects")[0]["children"]
    names = {}
    ok = True
    for m in find(objects, "Model"):
        name = m["props"][1].split("\x00")[0]
        kind = m["props"][2]
        p = p70(m)
        t = p.get("Lcl Translation", [0, 0, 0])
        r = p.get("Lcl Rotation", [0, 0, 0])
        s = p.get("Lcl Scaling", [1, 1, 1])
        names[name] = kind
        flag = ""
        if any(abs(v) > 1e-3 for v in r) or any(abs(v - 1) > 1e-4 for v in s):
            flag = "  <-- not identity"
            ok = False
        print(f"  {name:22s} {kind:6s} T=({t[0]:+.4f},{t[1]:+.4f},{t[2]:+.4f}) R=({r[0]:+.2f},{r[1]:+.2f},{r[2]:+.2f}) S=({s[0]:.3f},{s[1]:.3f},{s[2]:.3f}){flag}")
    missing = [r for r in require if r not in names]
    if missing:
        print("MISSING:", ", ".join(missing))
        ok = False
    print("OK" if ok else "PROBLEMS")
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))

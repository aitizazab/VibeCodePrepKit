"""Offline syntax/type check of a Unity URP .shader's HLSL with the Windows SDK's dxc (the Look agent; agents can't run Unity).

For each Pass's HLSLPROGRAM (with the SubShader's HLSLINCLUDE prepended) it compiles the #pragma vertex and #pragma
fragment entry points as vs_6_0 / ps_6_0 (SHADER_API_D3D11 paths), the way Unity's compiler would for the base variant, plus one
extra variant per keyword list given with --kw (e.g. --kw _MAIN_LIGHT_SHADOWS_CASCADE,_SHADOWS_SOFT). "Packages/..." includes
resolve against the project's Library/PackageCache. Not a substitute for Unity (no ShaderLab checks, no other platforms),
but it catches the typos, wrong signatures and missing symbols that would turn a material pink.

    uv run python -I tools/look/shader-check.py Assets/.../X.shader [--kw A,B] [--kw C]
Exit code 0 = every entry point compiled.
"""
import glob
import os
import re
import subprocess
import sys
import tempfile

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FXC_CANDIDATES = sorted(glob.glob(r"C:\Program Files (x86)\Windows Kits\10\bin\10.*\x64\dxc.exe"), reverse=True)


def package_dirs():
    out = {}
    for d in glob.glob(os.path.join(REPO, "Library", "PackageCache", "*@*")):
        name = os.path.basename(d).split("@")[0]
        out[name] = d
    return out


_cache = {}


INCLUDE_RE = re.compile(r'^\s*#\s*(include|include_with_pragmas)\s+"([^"]+)"')


def resolve_includes(src, pkgs, stack, base_dir, name="shader"):
    """Inlines every #include textually, with #line markers so errors point at the right file and line (Unity's files
    have include guards, so ones in inactive #if branches are harmless). An include that can't be found becomes an
    #error, which only fires if its branch is active."""
    out = []
    for n, line in enumerate(src.split("\n"), start=1):
        m = INCLUDE_RE.match(line)
        if not m:
            out.append(line)
            continue
        path = m.group(2)
        missing = '#error "missing include ' + path + '"'
        if path.startswith("Packages/"):
            parts = path.split("/")
            root = pkgs.get(parts[1])
            full = os.path.normpath(os.path.join(root, *parts[2:])) if root else None
        else:
            full = os.path.normpath(os.path.join(base_dir, path))
        if full is None or not os.path.exists(full):
            out.append(missing)
            continue
        if full in stack:
            out.append("")
            continue
        if full not in _cache:
            with open(full, encoding="utf-8", errors="replace") as f:
                _cache[full] = re.sub(r"#\s*include_with_pragmas", "#include", f.read())
        fname = full.replace("\\", "/")
        out.append('#line 1 "' + fname + '"')
        out.append(resolve_includes(_cache[full], pkgs, stack | {full}, os.path.dirname(full), fname))
        out.append('#line %d "%s"' % (n + 1, name))
    return "\n".join(out)


def blocks(shader):
    include = "\n".join(re.findall(r"HLSLINCLUDE(.*?)ENDHLSL", shader, re.S))
    programs = re.findall(r"HLSLPROGRAM(.*?)ENDHLSL", shader, re.S)
    return include, programs


PRELUDE = """
#define SHADER_API_D3D11 1
#define UNITY_VERSION 60003
#define SHADER_TARGET 45
#define UNITY_REVERSED_Z 1
#define UNITY_UV_STARTS_AT_TOP 1
#define UNITY_NEAR_CLIP_VALUE (1.0)
#define UNITY_COMPILER_HLSL 1
#define UNITY_PIPELINE_URP 1
"""


def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        sys.exit(1)
    path = args[0]
    variants = [[]]
    i = 1
    while i < len(args):
        if args[i] == "--kw":
            variants.append([k for k in args[i + 1].split(",") if k])
            i += 2
        else:
            i += 1
    fxc = FXC_CANDIDATES[0] if FXC_CANDIDATES else None
    if fxc is None:
        raise SystemExit("dxc.exe not found (Windows SDK)")
    pkgs = package_dirs()
    with open(path, encoding="utf-8") as f:
        shader = f.read()
    include, programs = blocks(shader)
    failures = 0
    tmp = tempfile.mkdtemp(prefix="vh-shader-check-")
    for pi, prog in enumerate(programs):
        vs = re.search(r"#pragma\s+vertex\s+(\w+)", prog)
        ps = re.search(r"#pragma\s+fragment\s+(\w+)", prog)
        body = re.sub(r"#\s*include_with_pragmas", "#include", include + "\n" + prog)
        for kws in variants:
            defines = "".join("#define %s 1\n" % k for k in kws)
            for stage, entry, profile, stage_def in (("vs", vs, "vs_6_0", "SHADER_STAGE_VERTEX"), ("ps", ps, "ps_6_0", "SHADER_STAGE_FRAGMENT")):
                if entry is None:
                    continue
                src = PRELUDE + "#define %s 1\n" % stage_def + defines + body
                src = resolve_includes(src, pkgs, frozenset(), os.path.dirname(os.path.abspath(path)), os.path.basename(path))
                fn = os.path.join(tmp, "p%d_%s.hlsl" % (pi, stage))
                with open(fn, "w", encoding="utf-8") as f:
                    f.write(src)
                r = subprocess.run([fxc, "-nologo", "-HV", "2018", "-Wno-everything", "-T", profile, "-E", entry.group(1), fn],
                                   capture_output=True, text=True)
                label = "pass %d %s %s [%s]" % (pi, stage, entry.group(1), ",".join(kws) or "base")
                if r.returncode != 0:
                    failures += 1
                    errs = [l for l in (r.stderr + r.stdout).splitlines() if "error" in l.lower()] or (r.stderr + r.stdout).splitlines()[:6]
                    print("FAIL", label)
                    for l in errs[:12]:
                        print("   ", l)
                else:
                    print("ok  ", label)
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()

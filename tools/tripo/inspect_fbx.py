"""Headless check of a Tripo FBX (no render): mesh counts, size, bones, textures.

    blender.exe -b --factory-startup -P inspect_fbx.py -- <model.fbx>
"""
import sys
import bpy

fbx = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)

meshes = [o for o in bpy.data.objects if o.type == "MESH"]
arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
tot_v = tot_f = tot_tri = quads = 0
for o in meshes:
    me = o.data
    tot_v += len(me.vertices)
    tot_f += len(me.polygons)
    for p in me.polygons:
        tot_tri += len(p.vertices) - 2
        quads += len(p.vertices) == 4
    zs = [(o.matrix_world @ v.co).z for v in me.vertices]
    print(f"[inspect] mesh {o.name}: verts {len(me.vertices)} faces {len(me.polygons)} "
          f"height {max(zs) - min(zs):.3f} m (z {min(zs):.3f}..{max(zs):.3f}) "
          f"vertex groups {len(o.vertex_groups)} materials {[m.name for m in me.materials if m]}")
print(f"[inspect] total verts {tot_v} faces {tot_f} (quads {quads}) triangles {tot_tri}")
for a in arms:
    names = [b.name for b in a.data.bones]
    print(f"[inspect] armature {a.name}: {len(names)} bones")
    print("[inspect] bones " + ", ".join(names))
for img in bpy.data.images:
    print(f"[inspect] image {img.name} {tuple(img.size)} {img.filepath}")

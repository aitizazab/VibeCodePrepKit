"""The People pipeline's Blender side (docs/redesign/people.md "Faces and alterations"). Headless, short jobs:

    blender -b --factory-startup -P vh_blender.py -- dump  <model.fbx> <out.npz>
    blender -b --factory-startup -P vh_blender.py -- build <spec.json>

dump:  the rest-pose mesh for the numpy side (vh_texture.py / vh_face.py): vertex positions (metres, Blender Z-up,
       the character facing -Y), triangles, per-corner UVs, vertex normals, and the head bones' positions.
build: imports a Tripo FBX, then per the spec: bakes bone-scale proportions into the rest pose (mesh and skeleton),
       moves regions of vertices (trim a beard, flatten hair), deletes regions (glasses, a cord), adds accessory meshes
       parented to a bone (pens, a bun, a badge), adds the face's shape keys (MouthOpen, Smile, Frown, BrowsUp,
       BrowsDown, BrowsSad) from landmarks, cuts the face decal patch (a copy of the face's front, pushed out 0.6 mm,
       UV-projected from the front, with the same shape keys) and exports an FBX for Unity with the given textures.
"""
import json
import math
import os
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Vector, Matrix

ARGS = sys.argv[sys.argv.index("--") + 1:]


def log(*a):
    print("[vh]", *a, flush=True)


def import_fbx(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, use_anim=False, ignore_leaf_bones=False, automatic_bone_orientation=False)
    mesh = [o for o in bpy.data.objects if o.type == "MESH"]
    arm = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if len(mesh) != 1 or len(arm) != 1:
        raise RuntimeError("expected one mesh and one armature, got %d / %d" % (len(mesh), len(arm)))
    return mesh[0], arm[0]


def world_verts(obj):
    me = obj.data
    co = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    m = np.array(obj.matrix_world, dtype=np.float32)
    return co @ m[:3, :3].T + m[:3, 3]


def bone_heads(arm):
    out = {}
    for b in arm.data.bones:
        out[b.name.split(":")[-1]] = list(arm.matrix_world @ b.head_local)
        out[b.name.split(":")[-1] + ".tail"] = list(arm.matrix_world @ b.tail_local)
    return out


# ------------------------------------------------------------------------------------------------------------------
# dump
# ------------------------------------------------------------------------------------------------------------------

def dump(fbx, out):
    obj, arm = import_fbx(fbx)
    me = obj.data
    me.calc_loop_triangles()
    tris = np.empty(len(me.loop_triangles) * 3, dtype=np.int32)
    me.loop_triangles.foreach_get("vertices", tris)
    loops = np.empty(len(me.loop_triangles) * 3, dtype=np.int32)
    me.loop_triangles.foreach_get("loops", loops)
    uv = np.empty(len(me.loops) * 2, dtype=np.float32)
    me.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    nrm = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get("normal", nrm)
    m = np.array(obj.matrix_world, dtype=np.float32)
    nrm = nrm.reshape(-1, 3) @ m[:3, :3].T
    nrm /= np.maximum(1e-8, np.linalg.norm(nrm, axis=1, keepdims=True))
    np.savez_compressed(out, verts=world_verts(obj), tris=tris.reshape(-1, 3), tri_uv=uv[loops].reshape(-1, 3, 2),
                        normals=nrm, bones=json.dumps(bone_heads(arm)))
    log("dump", out, len(me.vertices), "verts", len(me.loop_triangles), "tris")


# ------------------------------------------------------------------------------------------------------------------
# build
# ------------------------------------------------------------------------------------------------------------------

def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def bake_proportions(obj, arm, scales, no_inherit=()):
    """scales: {"Spine1": [x, y, z], ...} pose-bone scales (bone axes: Y along the bone), baked into the mesh and made
    the new rest pose. Bones in no_inherit don't take their parent's scale (so a slimmer chest keeps the arms and head)."""
    if not scales:
        return
    for name in no_inherit:
        b = arm.data.bones.get("mixamorig:" + name) or arm.data.bones.get(name)
        if b is not None:
            b.inherit_scale = "NONE"
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    for name, s in scales.items():
        pb = arm.pose.bones.get("mixamorig:" + name) or arm.pose.bones.get(name)
        if pb is None:
            log("no bone", name)
            continue
        pb.scale = Vector(s if isinstance(s, list) else [s, s, s])
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    # The mesh: apply the armature deformation (a copy of the modifier, so the original stays), then the rest pose.
    mod = next(m for m in obj.modifiers if m.type == "ARMATURE")
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_copy(modifier=mod.name)
    copy = obj.modifiers[-1]
    bpy.ops.object.modifier_move_to_index(modifier=copy.name, index=0)
    bpy.ops.object.modifier_apply(modifier=copy.name)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for b in arm.data.bones:
        b.inherit_scale = "FULL"
    log("proportions baked:", ", ".join(scales))


def region_weights(co, region):
    """A soft region: an ellipsoid {center, radius:[rx,ry,rz], soft} (metres, world), optionally cut by planes
    [{"axis":"z","above":v}] (hard). Returns 0..1 per vertex."""
    c = np.array(region["center"], dtype=np.float32)
    r = np.array(region["radius"], dtype=np.float32)
    d = np.linalg.norm((co - c) / r, axis=1)
    soft = region.get("soft", 0.3)
    w = 1.0 - smoothstep(1.0 - soft, 1.0, d)
    for cut in region.get("cuts", []):
        ax = "xyz".index(cut["axis"])
        if "above" in cut:
            w *= co[:, ax] > cut["above"]
        if "below" in cut:
            w *= co[:, ax] < cut["below"]
    return w.astype(np.float32)


def move_regions(obj, moves):
    """moves: [{"region":{...}, "toward":[x,y,z] (pull toward a point by 'amount' of the distance), or
    "offset":[dx,dy,dz], or "scale_about":[x,y,z] + "scale":[sx,sy,sz]}]. Applied to the base mesh (before shape keys)."""
    if not moves:
        return
    me = obj.data
    co = world_verts(obj)
    out = co.copy()
    for mv in moves:
        w = region_weights(co, mv["region"])[:, None]
        if "toward" in mv:
            p = np.array(mv["toward"], dtype=np.float32)
            out += (p - co) * w * mv.get("amount", 0.5)
        if "offset" in mv:
            out += np.array(mv["offset"], dtype=np.float32) * w
        if "scale_about" in mv:
            p = np.array(mv["scale_about"], dtype=np.float32)
            s = np.array(mv["scale"], dtype=np.float32)
            out += ((co - p) * s + p - co) * w
    inv = np.array(obj.matrix_world.inverted(), dtype=np.float32)
    local = out @ inv[:3, :3].T + inv[:3, 3]
    me.vertices.foreach_set("co", local.ravel())
    me.update()
    log("moved", len(moves), "regions")


def delete_regions(obj, regions):
    """Deletes whole loose parts whose vertices mostly fall inside any region (glasses, a cord)."""
    if not regions:
        return
    co = world_verts(obj)
    inside = np.zeros(len(co), dtype=bool)
    for r in regions:
        inside |= region_weights(co, r) > 0.5
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    seen = np.zeros(len(bm.verts), dtype=bool)
    kill = []
    for v in bm.verts:
        if seen[v.index]:
            continue
        part, stack = [], [v]
        seen[v.index] = True
        while stack:
            x = stack.pop()
            part.append(x)
            for e in x.link_edges:
                y = e.other_vert(x)
                if not seen[y.index]:
                    seen[y.index] = True
                    stack.append(y)
        idx = np.array([p.index for p in part])
        if len(idx) < len(bm.verts) * 0.5 and inside[idx].mean() > 0.6:
            kill.extend(part)
    if kill:
        bmesh.ops.delete(bm, geom=kill, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    log("deleted", len(kill), "verts in loose parts")


def is_point(v):
    return isinstance(v, list) and len(v) == 3 and all(isinstance(x, (int, float)) for x in v)


def add_shape_keys(obj, face):
    """face: landmarks in world metres (Blender, the character facing -Y): eyeL, eyeR, mouthL, mouthR, mouth (centre of
    the lip line), chin, browL, browR; plus 'scale' (head size, metres between the eyes' centres)."""
    if not face:
        return {}
    co = world_verts(obj)
    L = {k: np.array(v, dtype=np.float32) for k, v in face.items() if is_point(v)}
    eye = float(np.linalg.norm(L["eyeL"] - L["eyeR"]))       # interocular distance: the face's unit
    up = np.array([0, 0, 1], dtype=np.float32)
    fwd = np.array([0, -1, 0], dtype=np.float32)
    front = co[:, 1] < (L["mouth"][1] + eye * 0.9)            # the front half of the head
    mouth = L["mouth"]
    halfw = float(np.linalg.norm(L["mouthL"] - L["mouthR"])) * 0.5
    keys = {}
    obj.shape_key_add(name="Basis", from_mix=False)

    def key(name, delta):
        k = obj.shape_key_add(name=name, from_mix=False)
        inv = np.array(obj.matrix_world.inverted(), dtype=np.float32)
        local = (co + delta) @ inv[:3, :3].T + inv[:3, 3]
        k.data.foreach_set("co", local.ravel())
        keys[name] = float(np.abs(delta).max())

    # MouthOpen: the jaw drops. Everything under the lip line within the jaw's reach moves down and a touch back;
    # the lip line itself is the hinge band (the decal's mouth interior sits there and stretches open with it).
    rel = co - mouth
    jaw_reach = region_weights(co, {"center": list(mouth - up * eye * 0.55), "radius": [halfw * 2.4, eye * 1.3, eye * 1.05], "soft": 0.55})
    below = smoothstep(-eye * 0.02, -eye * 0.10, rel[:, 2])  # 0 above the line, 1 a little below it
    w = (jaw_reach * below * front)[:, None]
    open_amt = eye * face.get("jaw", 0.30)
    key("MouthOpen", w * (-up * open_amt + (-fwd) * open_amt * 0.25))

    # Smile: the corners up and out, the cheeks lifted.
    def corner(p, sign, dz, dx, rad):
        g = region_weights(co, {"center": list(p), "radius": [rad, rad * 1.2, rad], "soft": 0.95})
        return (g * front)[:, None] * (up * dz + np.array([sign, 0, 0], dtype=np.float32) * dx)
    sm = eye * face.get("smile", 0.16)
    key("Smile", corner(L["mouthL"], 1 if L["mouthL"][0] > L["mouthR"][0] else -1, sm, sm * 0.45, halfw * 1.0)
        + corner(L["mouthR"], 1 if L["mouthR"][0] > L["mouthL"][0] else -1, sm, sm * 0.45, halfw * 1.0)
        + corner((L["eyeL"] + L["mouthL"]) * 0.5, 0, sm * 0.35, 0, eye * 0.35)
        + corner((L["eyeR"] + L["mouthR"]) * 0.5, 0, sm * 0.35, 0, eye * 0.35))
    fr = eye * face.get("frown", 0.13)
    key("Frown", corner(L["mouthL"], 0, -fr, 0, halfw * 0.9) + corner(L["mouthR"], 0, -fr, 0, halfw * 0.9))

    def brow(p, dz, rad, inner_shift=0.0):
        g = region_weights(co, {"center": list(p), "radius": [rad * 1.3, rad, rad * 0.8], "soft": 0.9})
        return (g * front)[:, None] * up * dz
    br = eye * face.get("brow", 0.11)
    key("BrowsUp", brow(L["browL"], br, eye * 0.45) + brow(L["browR"], br, eye * 0.45))
    inL = L["browL"] + (L["browR"] - L["browL"]) * 0.28
    inR = L["browR"] + (L["browL"] - L["browR"]) * 0.28
    key("BrowsDown", brow(inL, -br * 0.8, eye * 0.32) + brow(inR, -br * 0.8, eye * 0.32))
    key("BrowsSad", brow(inL, br * 0.9, eye * 0.3) + brow(inR, br * 0.9, eye * 0.3))
    log("shape keys", keys)
    return keys


def decal_patch(obj, face, size_px):
    """A copy of the face's front (the faces inside the decal rectangle whose normals face forward), pushed out along
    the normals by 0.6 mm in every shape key, with UVs projected from the front onto the rectangle
    face['decal'] = [x0, z0, x1, z1] (world metres). Keeps the vertex groups, so it skins like the head."""
    rect = face.get("decal")
    if not rect:
        return None
    x0, z0, x1, z1 = rect
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.duplicate()
    dec = bpy.context.active_object
    dec.name = obj.name + "_FaceDecal"
    co = world_verts(dec)
    me = dec.data
    nrm = np.empty(len(me.polygons) * 3, dtype=np.float32)
    me.polygons.foreach_get("normal", nrm)
    nrm = nrm.reshape(-1, 3)
    cen = np.empty(len(me.polygons) * 3, dtype=np.float32)
    me.polygons.foreach_get("center", cen)
    cen = cen.reshape(-1, 3)
    m = np.array(dec.matrix_world, dtype=np.float32)
    cen = cen @ m[:3, :3].T + m[:3, 3]
    nrm = nrm @ m[:3, :3].T
    keep = (cen[:, 0] > x0) & (cen[:, 0] < x1) & (cen[:, 2] > z0) & (cen[:, 2] < z1) & (nrm[:, 1] < -0.2) \
        & (cen[:, 1] < face["mouth"][1] + 0.03)
    # Nothing standing well proud of the eyes' plane above the mouth (the nose's tip, a frame's front).
    eye_y = min(face["eyeL"][1], face["eyeR"][1])
    keep &= ~((cen[:, 1] < eye_y - face.get("proud", 0.05)) & (cen[:, 2] > face["mouth"][2] + 0.012))
    # The glasses' frames and cords (vh_face.py's list, carried as the "_NoDecal" vertex group): the drawing is never
    # laid over them, so a frame passes in front of the drawn eyes.
    vg = dec.vertex_groups.get("_NoDecal")
    if vg is not None:
        flag = np.zeros(len(me.vertices), dtype=bool)
        for v in me.vertices:
            for g in v.groups:
                if g.group == vg.index and g.weight > 0.5:
                    flag[v.index] = True
        for i, poly in enumerate(me.polygons):
            if keep[i] and flag[list(poly.vertices)].any():
                keep[i] = False
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not keep[f.index]], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(me)
    bm.free()
    me.update()
    # Push out along the vertex normals, in every key (the same offset, so the keys' deltas stay the face's).
    vn = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get("normal", vn)
    vn = vn.reshape(-1, 3) * 0.0006
    for kb in me.shape_keys.key_blocks:
        c = np.empty(len(me.vertices) * 3, dtype=np.float32)
        kb.data.foreach_get("co", c)
        kb.data.foreach_set("co", (c.reshape(-1, 3) + vn).ravel())
    base = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.shape_keys.key_blocks[0].data.foreach_get("co", base)
    me.vertices.foreach_set("co", base)
    me.update()
    # UVs: the front projection of the rest position onto the rectangle.
    rest = world_verts(dec)
    uvl = me.uv_layers.active
    li = np.empty(len(me.loops), dtype=np.int32)
    me.loops.foreach_get("vertex_index", li)
    p = rest[li]
    uv = np.stack([(p[:, 0] - x0) / (x1 - x0), (p[:, 2] - z0) / (z1 - z0)], axis=1)
    uvl.data.foreach_set("uv", uv.ravel())
    # Its own material slot (Unity gives it the decal material).
    mat = bpy.data.materials.new("FaceDecal")
    me.materials.clear()
    me.materials.append(mat)
    log("decal patch", len(me.polygons), "faces")
    return dec


def add_accessories(arm, items, collection):
    """items: [{"name", "bone", "prim": "cylinder|sphere|cube|torus", "location":[x,y,z] (world, rest),
    "rotation":[deg x,y,z], "scale":[x,y,z], "color":[r,g,b], "segments":n}]. Each is skinned 100% to its bone
    (a vertex group + armature modifier), one material per colour."""
    out = []
    for it in items or []:
        prim = it.get("prim", "cylinder")
        seg = it.get("segments", 16)
        if prim == "cylinder":
            bpy.ops.mesh.primitive_cylinder_add(vertices=seg, radius=1, depth=2)
        elif prim == "sphere":
            bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=max(6, seg // 2), radius=1)
        elif prim == "torus":
            bpy.ops.mesh.primitive_torus_add(major_segments=seg, minor_segments=8, major_radius=1, minor_radius=it.get("minor", 0.12))
        elif prim == "cone":
            bpy.ops.mesh.primitive_cone_add(vertices=seg, radius1=1, radius2=it.get("radius2", 0), depth=2)
        else:
            bpy.ops.mesh.primitive_cube_add(size=2)
            if it.get("bevel", 0) > 0:
                mod = bpy.context.active_object.modifiers.new("bevel", "BEVEL")
                mod.width = it["bevel"]
                mod.segments = 3
                bpy.ops.object.modifier_apply(modifier="bevel")
        o = bpy.context.active_object
        o.name = it["name"]
        o.location = Vector(it["location"])
        o.rotation_euler = [math.radians(a) for a in it.get("rotation", [0, 0, 0])]
        o.scale = Vector(it.get("scale", [0.01, 0.01, 0.01]))
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        bpy.ops.object.shade_smooth()
        mat = bpy.data.materials.new("Acc_" + it["name"])
        mat.diffuse_color = list(it.get("color", [0.5, 0.5, 0.5])) + [1.0]
        o.data.materials.append(mat)
        vg = o.vertex_groups.new(name=("mixamorig:" + it["bone"]) if arm.data.bones.get("mixamorig:" + it["bone"]) else it["bone"])
        vg.add(range(len(o.data.vertices)), 1.0, "REPLACE")
        mod = o.modifiers.new("Armature", "ARMATURE")
        mod.object = arm
        o.parent = arm
        out.append(o)
    if out:
        log("accessories", [o.name for o in out])
    return out


def make_lod(obj, ratio):
    """<Name>_LOD1: the body decimated (no shape keys: no face at that distance), same skin weights and material.
    VhPeopleBuild puts it in the prefab's LODGroup."""
    if not ratio or ratio >= 1:
        return None
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.duplicate()
    lod = bpy.context.active_object
    lod.name = obj.name + "_LOD1"
    if lod.data.shape_keys:
        lod.shape_key_clear()
    mod = lod.modifiers.new("decimate", "DECIMATE")
    mod.ratio = ratio
    bpy.ops.object.modifier_move_to_index(modifier="decimate", index=0)
    bpy.ops.object.modifier_apply(modifier="decimate")
    log("LOD1", len(lod.data.polygons), "faces")
    return lod


def set_textures(obj, tex):
    """Points the body material at the given images (absolute paths), so the FBX exports them alongside."""
    if not tex:
        return
    mat = obj.data.materials[0]
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    for key, sock in (("base", "Base Color"), ("normal", None)):
        p = tex.get(key)
        if not p:
            continue
        img = bpy.data.images.load(p, check_existing=True)
        node = nt.nodes.new("ShaderNodeTexImage")
        node.image = img
        if sock:
            nt.links.new(node.outputs["Color"], bsdf.inputs[sock])
        else:
            img.colorspace_settings.name = "Non-Color"
            nm = nt.nodes.new("ShaderNodeNormalMap")
            nt.links.new(node.outputs["Color"], nm.inputs["Color"])
            nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])


def rest_matrices(arm):
    return {b.name.split(":")[-1]: (arm.matrix_world @ b.matrix_local).copy() for b in arm.data.bones}


def follow(before, after, bone, p):
    """A point given relative to the original rest pose, carried along by its bone's change (proportions)."""
    if bone not in before:
        return list(p)
    return list((after[bone] @ before[bone].inverted()) @ Vector(p))


def follow_face(face, before, after):
    f = dict(face)
    for k, v in face.items():
        if is_point(v):
            f[k] = follow(before, after, "Head", v)
    x0, z0, x1, z1 = face["decal"]
    y = face["mouth"][1]
    a = follow(before, after, "Head", [x0, y, z0])
    b = follow(before, after, "Head", [x1, y, z1])
    f["decal"] = [a[0], a[2], b[0], b[2]]
    return f


def save_preview(path, obj, dec, acc=None):
    """The shape keys for a numpy preview (vh_preview.py): per object, rest verts, triangles, UVs and key deltas."""
    data = {}
    for tag, o in (("body", obj), ("decal", dec)):
        if o is None:
            continue
        me = o.data
        me.calc_loop_triangles()
        tris = np.empty(len(me.loop_triangles) * 3, dtype=np.int32)
        me.loop_triangles.foreach_get("vertices", tris)
        loops = np.empty(len(me.loop_triangles) * 3, dtype=np.int32)
        me.loop_triangles.foreach_get("loops", loops)
        uv = np.empty(len(me.loops) * 2, dtype=np.float32)
        me.uv_layers.active.data.foreach_get("uv", uv)
        base = world_verts(o)
        data[tag + "_verts"] = base
        data[tag + "_tris"] = tris.reshape(-1, 3)
        data[tag + "_uv"] = uv.reshape(-1, 2)[loops].reshape(-1, 3, 2)
        nrm = np.empty(len(me.vertices) * 3, dtype=np.float32)
        me.vertices.foreach_get("normal", nrm)
        data[tag + "_normals"] = nrm.reshape(-1, 3)
        if me.shape_keys:
            m = np.array(o.matrix_world, dtype=np.float32)
            for kb in me.shape_keys.key_blocks[1:]:
                c = np.empty(len(me.vertices) * 3, dtype=np.float32)
                kb.data.foreach_get("co", c)
                c = c.reshape(-1, 3) @ m[:3, :3].T + m[:3, 3]
                data[tag + "_key_" + kb.name] = c - base
    av, at, ac = [], [], []
    off = 0
    for o in acc or []:
        me = o.data
        me.calc_loop_triangles()
        tris = np.empty(len(me.loop_triangles) * 3, dtype=np.int32)
        me.loop_triangles.foreach_get("vertices", tris)
        av.append(world_verts(o))
        at.append(tris.reshape(-1, 3) + off)
        ac.append(np.tile(np.array(me.materials[0].diffuse_color[:3], dtype=np.float32), (len(tris) // 3, 1)))
        off += len(me.vertices)
    if av:
        data["acc_verts"] = np.concatenate(av)
        data["acc_tris"] = np.concatenate(at)
        data["acc_cols"] = np.concatenate(ac)
    np.savez_compressed(path, **data)


def build(spec_path):
    spec = json.load(open(spec_path, encoding="utf8"))
    obj, arm = import_fbx(spec["fbx"])
    obj.name = spec["name"]
    # Tripo's FBX carries custom normals: after the back of the head is reshaped they would still shade its second
    # face, so normals are recomputed from the geometry.
    if obj.data.has_custom_normals:
        bpy.context.view_layer.objects.active = obj
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
        log("custom normals cleared")
    # 1. Per-vertex moves (trim a beard, flatten hair), in the original rest pose (the dump's vertex order).
    if spec.get("vertex_moves"):
        delta = np.load(spec["vertex_moves"])["delta"]
        if len(delta) != len(obj.data.vertices):
            raise RuntimeError("vertex_moves: %d deltas for %d vertices" % (len(delta), len(obj.data.vertices)))
        co = world_verts(obj) + delta
        inv = np.array(obj.matrix_world.inverted(), dtype=np.float32)
        obj.data.vertices.foreach_set("co", (co @ inv[:3, :3].T + inv[:3, 3]).ravel())
        obj.data.update()
        log("vertex moves applied, max", float(np.abs(delta).max()))
    # The decal's exclusions (dump order, so before anything deletes vertices) as a vertex group.
    if spec.get("face", {}).get("decal_exclude") and os.path.exists(spec["face"]["decal_exclude"]):
        idx = np.load(spec["face"]["decal_exclude"])["index"]
        if len(idx):
            g = obj.vertex_groups.new(name="_NoDecal")
            g.add([int(i) for i in idx], 1.0, "REPLACE")
            log("no-decal vertices", len(idx))
    # 2. Proportions; everything placed in the original pose below follows its bone.
    before = rest_matrices(arm)
    bake_proportions(obj, arm, spec.get("proportions"), spec.get("no_inherit", []))
    after = rest_matrices(arm)
    regions = []
    for r in spec.get("delete", []):
        r = dict(r)
        r["center"] = follow(before, after, r.get("bone", "Head"), r["center"])
        regions.append(r)
    delete_regions(obj, regions)
    move_regions(obj, spec.get("moves"))
    face = follow_face(spec["face"], before, after) if spec.get("face") else None
    keys = add_shape_keys(obj, face) if face else {}
    dec = decal_patch(obj, face, 0) if face else None
    items = []
    for it in spec.get("accessories", []):
        it = dict(it)
        it["location"] = follow(before, after, it["bone"], it["location"])
        items.append(it)
    acc = add_accessories(arm, items, None)
    for o in (obj, dec):
        if o is not None and o.vertex_groups.get("_NoDecal") is not None:
            o.vertex_groups.remove(o.vertex_groups["_NoDecal"])
    lod = make_lod(obj, spec.get("lod_ratio", 0.3))
    out = spec["out"]
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=out, use_selection=False, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", use_mesh_modifiers=False,
        mesh_smooth_type="FACE", use_tspace=False, add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        bake_anim=False, path_mode="STRIP", embed_textures=False, use_armature_deform_only=False)
    if spec.get("preview"):
        save_preview(spec["preview"], obj, dec, acc)
    info = {"out": out, "verts": len(obj.data.vertices), "keys": keys, "decal": dec.name if dec else None,
            "decal_faces": len(dec.data.polygons) if dec else 0, "accessories": [o.name for o in acc],
            "lod": lod.name if lod else None, "lod_faces": len(lod.data.polygons) if lod else 0,
            "face": face, "bones": bone_heads(arm)}
    json.dump(info, open(spec.get("info", os.path.splitext(out)[0] + ".build.json"), "w"), indent=1)
    log("exported", out)


if __name__ == "__main__":
    if ARGS[0] == "dump":
        dump(ARGS[1], ARGS[2])
    elif ARGS[0] == "build":
        build(ARGS[1])
    else:
        raise SystemExit("unknown mode " + ARGS[0])

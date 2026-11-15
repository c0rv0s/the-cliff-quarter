# Builds the hooded wanderer in Blender and exports Assets/Models/Wanderer.fbx.
# Run: Blender --background --python Art/build_wanderer.py
# Joints are empties (pivot at the joint); each mesh carries one material named for its palette role.
import bpy, bmesh, math, os
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FBX = os.path.join(ROOT, "Assets", "Models", "Wanderer.fbx")
BLEND = os.path.join(ROOT, "Art", "Wanderer.blend")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

COLORS = {
    "Tunic": "F3E8D2", "Cape": "2C4C7C", "Trim": "DAAE57", "Sash": "B66547", "Trousers": "5E5648",
    "Leather": "6B4A35", "Skin": "E2B48C", "Eyes": "26262F", "Glove": "8A6448",
}
def srgb(h):
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c]
MATS = {}
for name, hexv in COLORS.items():
    m = bpy.data.materials.new(name); m.diffuse_color = (*srgb(hexv), 1); MATS[name] = m

joints = {}
meshes = []
def joint(name, loc, parent=None):
    o = bpy.data.objects.new(name, None); o.location = Vector(loc)
    scene.collection.objects.link(o); joints[name] = (o, parent); return o

def finish(bm, name, material, at, smooth=True, solidify=0.0):
    """bm is in world space; the object origin sits at the joint `at`."""
    origin = joints[at][0].location
    for v in bm.verts: v.co -= origin
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = smooth
    o = bpy.data.objects.new("m_" + name, me); o.location = origin
    scene.collection.objects.link(o); me.materials.append(MATS[material])
    if solidify:
        mod = o.modifiers.new("solid", "SOLIDIFY"); mod.thickness = solidify; mod.offset = 0
    meshes.append((o, at)); return o

def loft(rings, seg=16, cap_bottom=True, cap_top=True):
    """rings: (center, rx, ry) horizontal ellipses from bottom to top."""
    bm = bmesh.new(); loops = []
    for c, rx, ry in rings:
        c = Vector(c)
        loops.append([bm.verts.new((c.x + rx * math.cos(a), c.y + ry * math.sin(a), c.z)) for a in [i * 2 * math.pi / seg for i in range(seg)]])
    for a, b in zip(loops, loops[1:]):
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((a[i], a[j], b[j], b[i]))
    if cap_bottom: bm.faces.new(list(reversed(loops[0])))
    if cap_top: bm.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm

def ellipsoid(center, radii, seg=16, rings=10, shape=None):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1)
    for v in bm.verts:
        p = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2]))
        if shape: p = shape(p)
        v.co = p + Vector(center)
    return bm

def tube(points, radius, seg=8):
    """Closed ring tube along a closed path of points."""
    bm = bmesh.new(); n = len(points); loops = []
    for i, p in enumerate(points):
        p = Vector(p); t = (Vector(points[(i + 1) % n]) - Vector(points[i - 1])).normalized()
        u = t.cross(Vector((0, 0, 1))).normalized() if abs(t.z) < .95 else Vector((1, 0, 0))
        w = t.cross(u).normalized()
        loops.append([bm.verts.new(p + (u * math.cos(a) + w * math.sin(a)) * radius) for a in [k * 2 * math.pi / seg for k in range(seg)]])
    for i in range(n):
        a, b = loops[i], loops[(i + 1) % n]
        for k in range(seg):
            bm.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm

def sheet(fn, nu, nv):
    """Open surface from fn(u, v) -> Vector, u across, v down."""
    bm = bmesh.new()
    grid = [[bm.verts.new(fn(i / nu, j / nv)) for i in range(nu + 1)] for j in range(nv + 1)]
    for j in range(nv):
        for i in range(nu):
            bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    return bm

# ---------- skeleton (character faces -Y, +X is the character's left) ----------
joint("Wanderer", (0, 0, 0))
joint("Hips", (0, 0, .95), "Wanderer")
joint("Chest", (0, 0, 1.06), "Hips")
joint("Head", (0, .005, 1.47), "Chest")
joint("CapeUpper", (0, .13, 1.44), "Chest")
joint("CapeLower", (0, .2, 1.05), "CapeUpper")
joint("ScarfTail", (-.05, .2, 1.46), "Chest")
for side, s in (("L", 1), ("R", -1)):
    joint("UpperArm." + side, (s * .215, .0, 1.39), "Chest")
    joint("Forearm." + side, (s * .235, .0, 1.14), "UpperArm." + side)
    joint("Hand." + side, (s * .24, -.005, .91), "Forearm." + side)
    joint("Thigh." + side, (s * .095, 0, .9), "Hips")
    joint("Shin." + side, (s * .1, -.005, .5), "Thigh." + side)
    joint("Foot." + side, (s * .1, 0, .09), "Shin." + side)

# ---------- body ----------
finish(loft([((0, 0, .98), .165, .125), ((0, .0, 1.12), .185, .13), ((0, .005, 1.27), .2, .135), ((0, .01, 1.36), .2, .125),
             ((0, .01, 1.42), .15, .1), ((0, .01, 1.47), .07, .06)], 20), "Torso", "Tunic", "Chest")
finish(loft([((0, 0, .97), .178, .136), ((0, 0, 1.06), .182, .138)], 20), "Sash", "Sash", "Chest")
finish(ellipsoid((.07, -.13, 1.0), (.045, .025, .06)), "SashKnot", "Sash", "Chest")
finish(loft([((.08, -.125, .78), .03, .012), ((.075, -.13, .9), .035, .012), ((.07, -.135, .99), .03, .012)], 8), "SashTail", "Sash", "Chest")
strap = [(.19 * math.cos(a), .14 * math.sin(a), 1.2 + .17 * math.cos(a + .35)) for a in [i * 2 * math.pi / 28 for i in range(28)]]
finish(tube(strap, .016, 6), "Strap", "Leather", "Chest")
finish(ellipsoid((0, -.01, .955), (.165, .125, .07)), "Pelvis", "Trousers", "Hips")
finish(loft([((0, .01, .66), .225, .17), ((0, .005, .8), .205, .155), ((0, 0, .93), .178, .133), ((0, 0, 1.0), .168, .127)], 22, False, False), "TunicSkirt", "Tunic", "Hips", solidify=.012)
finish(loft([((0, .01, .645), .228, .173), ((0, .01, .675), .229, .174)], 22, False, False), "SkirtHem", "Trim", "Hips", solidify=.01)
finish(ellipsoid((-.215, .05, .86), (.06, .1, .085), 12, 8), "Satchel", "Leather", "Hips")
finish(ellipsoid((-.27, .05, .885), (.012, .095, .05), 8, 6), "SatchelFlap", "Sash", "Hips")

# Head: skin, eyes, hood up with a swept point, scarf wrap.
finish(ellipsoid((0, -.012, 1.605), (.13, .135, .142), 18, 12), "Face", "Skin", "Head")
for s in (1, -1):
    finish(ellipsoid((s * .05, -.133, 1.618), (.018, .009, .028), 8, 6), "Eye" + ("L" if s > 0 else "R"), "Eyes", "Head")
    finish(ellipsoid((s * .078, -.118, 1.578), (.022, .01, .014), 8, 6), "Cheek" + ("L" if s > 0 else "R"), "Sash", "Head")
finish(ellipsoid((0, -.145, 1.59), (.019, .015, .021), 8, 6), "Nose", "Skin", "Head")
def hood_shape(p):
    if p.y > 0: p.y *= 1.0 + .5 * max(0, p.z / .19)  # back of the crown sweeps out
    if p.y > .08 and p.z > -.02: p.z -= (p.y - .08) * .45  # and droops toward the cape
    if p.z > .1: p.y += (p.z - .1) * .6
    return p
hood = ellipsoid((0, .01, 1.63), (.165, .17, .19), 24, 16, hood_shape)
cut = [f for f in hood.faces if f.calc_center_median().y < -.035 and 1.44 < f.calc_center_median().z < 1.76 and abs(f.calc_center_median().x) < .15]
bmesh.ops.delete(hood, geom=cut, context="FACES")
bmesh.ops.delete(hood, geom=[v for v in hood.verts if not v.link_faces], context="VERTS")
boundary = [e for e in hood.edges if len(e.link_faces) == 1]
loop = []
if boundary:
    e0 = boundary[0]; v = e0.verts[0]; used = set()
    while True:
        loop.append(v.co.copy())
        nxt = [e for e in v.link_edges if len(e.link_faces) == 1 and e not in used]
        if not nxt: break
        used.add(nxt[0]); v = nxt[0].other_vert(v)
        if v.co == loop[0]: break
finish(hood, "Hood", "Cape", "Head", solidify=.018)
if len(loop) > 6: finish(tube(loop, .013, 6), "HoodTrim", "Trim", "Head")
finish(loft([((0, .01, 1.42), .105, .1), ((0, .0, 1.46), .125, .115), ((0, -.008, 1.51), .115, .11)], 18), "Scarf", "Sash", "Head")

# Cape: one continuous drape (neck, over the shoulders, flaring hem), split at a hinge for secondary motion.
PROFILE = [(1.505, .11, .1, 150), (1.43, .3, .175, 118), (1.3, .305, .185, 96), (1.05, .31, .2, 86), (.5, .37, .255, 82)]
def cape_at(z):
    for (z0, rx0, ry0, w0), (z1, rx1, ry1, w1) in zip(PROFILE, PROFILE[1:]):
        if z <= z0 and z >= z1:
            t = (z0 - z) / (z0 - z1); t = t * t * (3 - 2 * t) if z0 > 1.4 else t
            return rx0 + (rx1 - rx0) * t, ry0 + (ry1 - ry0) * t, w0 + (w1 - w0) * t
    return PROFILE[-1][1:]
def cape(z_top, z_bottom, rows):
    def f(u, v):
        z = z_top + (z_bottom - z_top) * v
        rx, ry, wrap = cape_at(z)
        a = (u - .5) * math.radians(2 * wrap)
        fold = .016 * math.sin(u * 20) * max(0, (1.35 - z)) * 2.6
        back = .02 + max(0, 1.43 - z) * .07
        return Vector((math.sin(a) * (rx + fold), math.cos(a) * (ry + fold) + back, z))
    return sheet(f, 28, rows)
finish(cape(1.505, 1.05, 10), "CapeUpperCloth", "Cape", "CapeUpper", solidify=.014)
finish(cape(1.05, .5, 9), "CapeLowerCloth", "Cape", "CapeLower", solidify=.014)
finish(cape(.53, .5, 1), "CapeHem", "Trim", "CapeLower", solidify=.022)
finish(ellipsoid((.075, -.11, 1.465), (.03, .02, .03), 10, 8), "Clasp", "Trim", "Chest")
finish(loft([((-.07, .19, 1.2), .028, .012), ((-.07, .2, 1.32), .045, .014), ((-.05, .19, 1.45), .04, .016)], 8), "ScarfTailCloth", "Sash", "ScarfTail")

# Limbs.
for side, s in (("L", 1), ("R", -1)):
    x = s * .215
    finish(loft([((x + s * .02, 0, 1.13), .052, .052), ((x + s * .012, 0, 1.28), .06, .058), ((x, 0, 1.41), .066, .062), ((x - s * .02, 0, 1.45), .05, .05)], 12), "Sleeve." + side, "Tunic", "UpperArm." + side)
    finish(ellipsoid((x - s * .005, 0, 1.375), (.064, .06, .058), 12, 8), "Shoulder." + side, "Tunic", "UpperArm." + side)
    finish(loft([((s * .24, -.005, .94), .04, .038), ((s * .238, 0, 1.05), .047, .044), ((s * .235, 0, 1.16), .05, .048)], 12), "Forearm." + side, "Tunic", "Forearm." + side)
    finish(loft([((s * .24, -.005, .935), .046, .044), ((s * .24, -.005, .975), .048, .046)], 12), "Cuff." + side, "Sash", "Forearm." + side)
    finish(ellipsoid((s * .245, -.012, .86), (.038, .045, .058), 10, 8), "Glove." + side, "Glove", "Hand." + side)
    finish(ellipsoid((s * .215, -.04, .875), (.016, .016, .03), 6, 5), "Thumb." + side, "Glove", "Hand." + side)
    finish(loft([((s * .1, 0, .5), .062, .064), ((s * .098, 0, .7), .074, .076), ((s * .095, 0, .92), .085, .085)], 14), "Thigh." + side, "Trousers", "Thigh." + side)
    finish(loft([((s * .1, .0, .3), .056, .058), ((s * .1, -.005, .52), .064, .066)], 14), "Shin." + side, "Trousers", "Shin." + side)
    finish(loft([((s * .1, 0, .07), .06, .066), ((s * .1, 0, .22), .062, .064), ((s * .1, 0, .33), .07, .072), ((s * .1, 0, .35), .072, .074)], 14), "BootShaft." + side, "Leather", "Shin." + side)
    finish(loft([((s * .1, 0, .325), .073, .075), ((s * .1, 0, .355), .074, .076)], 14), "BootCuff." + side, "Trim", "Shin." + side)
    def foot_shape(p):
        if p.z < -.02: p.z = -.02 - (p.z + .02) * .2
        if p.y < 0: p.z *= 1 + p.y * 1.6
        return p
    finish(ellipsoid((s * .1, -.05, .06), (.062, .125, .06), 14, 10, foot_shape), "Boot." + side, "Leather", "Foot." + side)

# ---------- hierarchy and export ----------
bpy.context.view_layer.update()
for name, (obj, parent) in list(joints.items()) + [(o.name, (o, a)) for o, a in meshes]:
    if parent:
        p = joints[parent][0]; world = obj.matrix_world.copy()
        obj.parent = p; obj.matrix_world = world
bpy.context.view_layer.update()
os.makedirs(os.path.dirname(FBX), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=FBX, object_types={"EMPTY", "MESH"}, use_mesh_modifiers=True, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=False,
                         mesh_smooth_type="FACE", add_leaf_bones=False, use_custom_props=False)
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
deps = bpy.context.evaluated_depsgraph_get(); tris = 0
for o in scene.objects:
    if o.type == "MESH":
        me = o.evaluated_get(deps).to_mesh(); me.calc_loop_triangles(); tris += len(me.loop_triangles)
print("WANDERER_EXPORTED", FBX, "objects", len(scene.objects), "tris~", tris)

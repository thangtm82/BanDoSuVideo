"""Blender background script: blender -b -t 4 --python this_file -- scene.json output.png

Procedural blockout; Blender 4.x/5.x compatible. No external asset downloads.
"""
import json
import math
import sys
from pathlib import Path

def render(scene_file, output_file, lighting_preset="bright_final"):
    import bpy
    from mathutils import Vector
    scene_data = json.loads(Path(scene_file).read_text(encoding="utf-8"))
    env = scene_data.get("environment", {})
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    materials = {}
    def srgb_to_linear(channel):
        return channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4
    def material(color):
        if color not in materials:
            m = bpy.data.materials.new("H2-" + color)
            m.diffuse_color = tuple(srgb_to_linear(int(color[i:i+2], 16)/255) for i in (1,3,5)) + (1,)
            m.use_nodes = True
            bsdf = m.node_tree.nodes.get("Principled BSDF")
            if bsdf:
                bsdf.inputs["Base Color"].default_value = m.diffuse_color
                bsdf.inputs["Roughness"].default_value = 0.85
            materials[color] = m
        return materials[color]
    for inst in scene_data["instances"]:
        x, y, z = inst["position"]
        dx, dy, dz = inst["dimensions"]
        bpy.ops.mesh.primitive_cube_add(size=1, location=(x, z, y))
        obj = bpy.context.object
        obj.name = inst["id"]
        obj.dimensions = (dx, dz, dy)
        obj.rotation_euler.z = -float(inst["rotation_y"])
        obj.data.materials.append(material(inst["color"]))
        roof = inst.get("roof", {})
        if roof.get("style") == "hip":
            roof_height = float(roof["height"])
            bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=1, radius2=0,
                depth=roof_height, location=(x, z, y + dy / 2 + roof_height / 2),
                rotation=(0, 0, math.pi / 4 - float(inst["rotation_y"])))
            top = bpy.context.object
            top.name = inst["id"] + "-roof"
            top.scale = (dx / math.sqrt(2), dz / math.sqrt(2), 1)
            top.data.materials.append(material(roof["color"]))
    # Shared procedural details, identical IDs/transforms in both renderers.
    for detail in scene_data.get("details", []):
        x, y, z = detail["position"]
        dx, dy, dz = detail["size"]
        if detail["shape"] == "box":
            bpy.ops.mesh.primitive_cube_add(size=1, location=(x, z, y))
            part = bpy.context.object
            part.dimensions = (dx, dz, dy)
            part.rotation_euler.z = -float(detail.get("rotation_y", 0))
        elif detail["shape"] == "cone":
            bpy.ops.mesh.primitive_cone_add(vertices=int(detail.get("sides", 8)),
                radius1=1, radius2=0, depth=dy, location=(x, z, y))
            part = bpy.context.object
            part.scale = (dx/2, dz/2, 1)
            part.rotation_euler.z = -float(detail.get("rotation_y", 0))
        else:
            raise ValueError("Unsupported detail shape: " + str(detail["shape"]))
        part.name = detail["id"]
        part.data.materials.append(material(detail["color"]))
    terrain = env.get("terrain_mesh")
    if terrain:
        vertices = [(p[0], p[2], p[1]) for p in terrain["vertices"]]
        faces = [tuple(triangle) for triangle in terrain["triangles"]]
        mesh = bpy.data.meshes.new("H2 terrain heightfield")
        mesh.from_pydata(vertices, [], faces)
        mesh.update()
        ground = bpy.data.objects.new("H2 terrain heightfield", mesh)
        bpy.context.collection.objects.link(ground)
        ground.data.materials.append(material(terrain.get("color", "#758265")))
    else:
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, -1))
        ground = bpy.context.object
        ground.name = "H2 terrain blockout"
        ground.dimensions = (1200, 1200, 2)
        ground.data.materials.append(material(env.get("lighting", {}).get("ground_color", "#758265")))
    def strip_mesh(name, points, width, color, elevation=0.12):
        """Generate a real flat ribbon mesh from Y-up path coordinates."""
        if len(points) < 2:
            return
        verts = []
        faces = []
        for i, p in enumerate(points):
            before = points[max(0, i - 1)]
            after = points[min(len(points) - 1, i + 1)]
            vx, vz = after[0] - before[0], after[2] - before[2]
            length = math.hypot(vx, vz)
            if length < 1e-6:
                vx, vz, length = 1.0, 0.0, 1.0
            nx, nz = -vz / length * width / 2, vx / length * width / 2
            verts.extend([(p[0] + nx, p[2] + nz, elevation),
                          (p[0] - nx, p[2] - nz, elevation)])
            if i:
                k = 2 * i
                faces.append((k - 2, k - 1, k + 1, k))
        mesh = bpy.data.meshes.new(name + "-mesh")
        mesh.from_pydata(verts, [], faces)
        mesh.update()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        obj.data.materials.append(material(color))
    for road in env.get("roads", []):
        strip_mesh("road-" + str(road["id"]), road["points"],
                   float(road.get("width", 5)), road.get("color", "#aa9678"), 0.12)
    river = env.get("river", {})
    strip_mesh("river", river.get("points", []), float(river.get("width", 14)),
               river.get("color", "#4b899d"), 0.18)
    cameras = scene_data.get("cameras", [])
    cam = cameras[0] if cameras else {"position": [520,260,500], "target": [0,0,0], "fov": 45}
    bpy.ops.object.camera_add(location=(cam["position"][0], cam["position"][2], cam["position"][1]))
    camera = bpy.context.object
    target = Vector((cam["target"][0], cam["target"][2], cam["target"][1]))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "PERSP"
    # Three.js uses vertical FOV; Blender AUTO sensor fit crops landscape views.
    vertical_fov = float(cam.get("fov", 45))
    if not 1 < vertical_fov < 179:
        raise ValueError("Camera vertical FOV must be between 1 and 179 degrees")
    camera.data.sensor_fit = "VERTICAL"
    camera.data.sensor_height = 24.0
    camera.data.lens = camera.data.sensor_height / (2 * math.tan(math.radians(vertical_fov) / 2))
    scene.camera = camera
    # Lighting 3.0: configurable studio daylight; defaults prioritize readable
    # shadows and warm direct illumination without washing out albedo colors.
    presets = {
        "daylight": {"world": 0.50, "sun": 2.6, "fill": 12000,
                     "exposure": 0.10, "sky": (0.42, 0.57, 0.73, 1)},
        "bright": {"world": 0.65, "sun": 3.1, "fill": 18000,
                   "exposure": 0.20, "sky": (0.48, 0.62, 0.78, 1)},
        "bright_final": {"world": 0.55, "sun": 3.0, "fill": 12000,
                         "exposure": 0.15, "sky": (0.48, 0.62, 0.78, 1)},
        "dramatic": {"world": 0.30, "sun": 2.9, "fill": 5000,
                     "exposure": 0.0, "sky": (0.28, 0.39, 0.53, 1)},
    }
    if lighting_preset not in presets:
        raise ValueError("Unknown lighting preset: " + lighting_preset)
    lighting = presets[lighting_preset]
    # EEVEE requires an explicit World Background shader for environment lighting.
    if scene.world is None:
        scene.world = bpy.data.worlds.new("H2 Daylight World")
    scene.world.use_nodes = True
    nodes = scene.world.node_tree.nodes
    background = nodes.get("Background") or nodes.new("ShaderNodeBackground")
    output = nodes.get("World Output") or nodes.new("ShaderNodeOutputWorld")
    scene.world.node_tree.links.new(background.outputs["Background"], output.inputs["Surface"])
    background.inputs["Color"].default_value = lighting["sky"]
    background.inputs["Strength"].default_value = lighting["world"]
    # Sun light keeps brightness consistent across the entire town.
    bpy.ops.object.light_add(type="SUN", location=(0, 0, 250))
    sun = bpy.context.object
    sun.name = "H2 daylight sun"
    sun.rotation_euler = (math.radians(28), math.radians(-28), math.radians(-35))
    sun.data.energy = lighting["sun"]
    sun.data.color = (1.0, 0.91, 0.79)
    sun.data.angle = math.radians(3.5 if lighting_preset == "bright_final" else 2.5)
    bpy.ops.object.light_add(type="AREA", location=(100, -150, 350))
    fill = bpy.context.object
    fill.name = "H2 soft fill"
    fill.data.energy = lighting["fill"]
    fill.data.color = (0.78, 0.88, 1.0)
    fill.data.shape = "DISK"
    fill.data.size = 300
    # AgX compresses highlights and desaturates this low-poly blockout.
    # Standard gives a clearer technical preview with visible material hues.
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "Medium High Contrast"
    scene.view_settings.exposure = lighting["exposure"]
    scene.view_settings.gamma = 1.0
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"
    path = Path(output_file).resolve()
    path.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    if not path.is_file() or path.stat().st_size == 0:
        raise RuntimeError("Blender produced no PNG: " + str(path))
    print("H2 V6.2 Blender render:", path)

if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    if len(args) not in (2, 3):
        raise SystemExit("Usage: blender -b --python v6_2_blender_preview.py -- scene.json output.png [bright_final|daylight|bright|dramatic]")
    render(args[0], args[1], args[2] if len(args) == 3 else "bright_final")

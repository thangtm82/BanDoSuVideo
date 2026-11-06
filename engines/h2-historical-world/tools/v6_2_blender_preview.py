"""Blender background script: blender -b -t 4 --python this_file -- scene.json output.png

Procedural blockout; Blender 4.x/5.x compatible. No external asset downloads.
"""
import json
import math
import sys
from pathlib import Path

def render(scene_file, output_file):
    import bpy
    from mathutils import Vector
    scene_data = json.loads(Path(scene_file).read_text(encoding="utf-8"))
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    materials = {}
    def material(color):
        if color not in materials:
            m = bpy.data.materials.new("H2-" + color)
            m.diffuse_color = tuple(int(color[i:i+2], 16)/255 for i in (1,3,5)) + (1,)
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
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, -1))
    ground = bpy.context.object
    ground.name = "H2 terrain blockout"
    ground.dimensions = (1200, 1200, 2)
    ground.data.materials.append(material("#6c755c"))
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
    # EEVEE requires an explicit World Background shader for environment lighting.
    if scene.world is None:
        scene.world = bpy.data.worlds.new("H2 Daylight World")
    scene.world.use_nodes = True
    nodes = scene.world.node_tree.nodes
    background = nodes.get("Background") or nodes.new("ShaderNodeBackground")
    output = nodes.get("World Output") or nodes.new("ShaderNodeOutputWorld")
    scene.world.node_tree.links.new(background.outputs["Background"], output.inputs["Surface"])
    background.inputs["Color"].default_value = (0.64, 0.74, 0.82, 1.0)
    background.inputs["Strength"].default_value = 0.45
    # Sun light keeps brightness consistent across the entire town.
    bpy.ops.object.light_add(type="SUN", location=(0, 0, 250))
    sun = bpy.context.object
    sun.name = "H2 daylight sun"
    sun.rotation_euler = (math.radians(35), math.radians(-25), math.radians(-30))
    sun.data.energy = 1.8
    sun.data.angle = math.radians(5)
    bpy.ops.object.light_add(type="AREA", location=(100, -150, 350))
    fill = bpy.context.object
    fill.name = "H2 soft fill"
    fill.data.energy = 25000
    fill.data.shape = "DISK"
    fill.data.size = 500
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.exposure = -0.5
    path = Path(output_file).resolve()
    path.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    if not path.is_file() or path.stat().st_size == 0:
        raise RuntimeError("Blender produced no PNG: " + str(path))
    print("H2 V6.2 Blender render:", path)

if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    if len(args) != 2:
        raise SystemExit("Usage: blender -b --python v6_2_blender_preview.py -- scene.json output.png")
    render(*args)

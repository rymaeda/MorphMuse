import bpy

# Limpa a cena
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

# Importa os dois arquivos STL
bpy.ops.import_mesh.stl(filepath="C:/Users/rymae/Source/MorphMuse/CambamTestFiles/canoa1.stl")
top_obj = bpy.context.selected_objects[0]
top_obj.name = "Topo"

bpy.ops.import_mesh.stl(filepath="C:/Users/rymae/Source/MorphMuse/CambamTestFiles/canoa2.stl")
base_obj = bpy.context.selected_objects[0]
base_obj.name = "Base"

# Aplica escala e transforma em ambos
for obj in [top_obj, base_obj]:
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Cria um modificador Shrinkwrap no objeto superior
bpy.context.view_layer.objects.active = top_obj
bpy.ops.object.modifier_add(type='SHRINKWRAP')
top_obj.modifiers["Shrinkwrap"].target = base_obj
top_obj.modifiers["Shrinkwrap"].wrap_method = 'NEAREST_SURFACEPOINT'
top_obj.modifiers["Shrinkwrap"].offset = -0.1

# Adiciona um modificador Lattice para deformação adicional
bpy.ops.object.lattice_add(location=top_obj.location)
lattice = bpy.context.active_object
lattice.scale = (2, 2, 1)
lattice.name = "Lattice"

# Aplica o modificador Lattice
bpy.context.view_layer.objects.active = top_obj
bpy.ops.object.modifier_add(type='LATTICE')
top_obj.modifiers["Lattice"].object = lattice

# Modo de edição para deformar o lattice
bpy.context.view_layer.objects.active = lattice
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.transform.translate(value=(0, 0, -0.5))
bpy.ops.object.mode_set(mode='OBJECT')

# Exporta o resultado como STL
bpy.ops.export_mesh.stl(filepath="C:/Users/rymae/Source/MorphMuse/CambamTestFiles/canoas.stl")
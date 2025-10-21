import bpy
import os

# Caminho do arquivo STL
stl_path = "C:/Users/rymae/Source/MorphMuse/CambamTestFiles/canoa1.stl"

# Verifica se o arquivo existe
if os.path.exists(stl_path):
    bpy.ops.wm.append(
        filepath=stl_path,
        directory=os.path.dirname(stl_path),
        filename=os.path.basename(stl_path)
    )
else:
    print("Arquivo STL não encontrado.")
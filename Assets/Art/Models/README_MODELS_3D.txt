=============================================================
FRAGA: A NOITE ESQUECIDA - ESTRUTURA DE ASSETS 3D REALISTAS
=============================================================

Esta pasta foi configurada para você importar qualquer modelo 3D realista (.FBX ou .OBJ)
da Unity Asset Store, Sketchfab, CGTrader ou Blender.

ONDE COLOCAR SEUS ARQUIVOS:
1. Árvores (Carballos, Carvalhos, Pinheiros, Troncos):
   -> Assets/Art/Models/Trees/

2. Vegetação (Samambaias/Fentos, Arbustos, Flores, Cogumelos):
   -> Assets/Art/Models/Vegetation/

3. Pedras, Rochas e Megálitos:
   -> Assets/Art/Models/Rocks/

4. Armas e Ferramentas (Machado, Tocha, Arco, Lampião):
   -> Assets/Art/Models/Weapons/

5. Construções e Props (Fogueira, Barricadas, Caixotes, Bancada):
   -> Assets/Art/Models/Props/

COMO SINCRONIZAR AUTOMATICAMENTE:
Após arrastar seus modelos .fbx/.obj e texturas para as pastas acima:
No menu do topo da Unity, clique em:
   Fraga Native -> 2. Sincronizar Modelos 3D Externos (FBX/OBJ -> Prefabs)

O script vai:
1. Detectar os modelos importados.
2. Aplicar automaticamente os Shaders realistas do Fraga (casca molhada, folhagem SSS, granito com musgo).
3. Adicionar colisores físicos (Capsule/Mesh Collider).
4. Atualizar os prefabs oficiais em Assets/Resources/Prefabs.

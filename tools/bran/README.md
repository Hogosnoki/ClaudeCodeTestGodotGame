# Bran: how the Swordsman's body was made

The Swordsman's body is a mesh (`godot/DaggerCave/Art/bran.mesh`) built from the concept art.

1. `gen.py` sends a front crop of the concept sheet to the public Hunyuan3D-2 image-to-3D Space (Hugging Face)
   and saves the untextured shape it returns.
2. `clean.py` keeps the body, removes the sword and other thin leftovers (voxel carve and opening), and
   re-meshes it with marching cubes, decimated to about 50k triangles. (The mesh faces about 37 degrees off the
   body axis, so it is turned to face +X before anything else.)
3. `align.py`, `project.py`, `color.py` fit the mesh silhouette to the art (front and back views) and project the
   art's colours onto it. `zones.py` holds hand-drawn polygons over the art (hair, face, cowl, fur, bracers,
   tabard, boots...) that name each area; `paint3.py` turns those into a material class per vertex and a palette
   colour (with the art's light and shade kept as variation).
4. `build_bran.py` straightens the wide stance, pulls the fists to the rig's hands, turns the head back to face
   forward, weights every vertex to the hero skeleton's bones (the cloak to the cape bones), and writes
   `bran.mesh` (`BRAN` header, bone names, positions, colours, classes, two bones and a weight per vertex, indices).

`HeroDesign.Bran.cs` reads that file and adds it to the hero rig as skinned parts (one per material); the sword and
the eyes are added in code. Needs Python with numpy, scipy, scikit-image, trimesh, fast-simplification and pillow.

import sys, shutil
from gradio_client import Client, handle_file
c=Client("tencent/Hunyuan3D-2", verbose=False)
img=handle_file('/tmp/claude-0/i3d/bran_front.png')
try:
    r=c.predict(caption="", image=img, mv_image_front=None, mv_image_back=None, mv_image_left=None, mv_image_right=None,
                steps=30, guidance_scale=5.0, seed=1234, octree_resolution=256, check_box_rembg=True, num_chunks=8000, randomize_seed=False,
                api_name="/generation_all")
    print("RESULT", r[0], r[1], r[3])
    shutil.copy(r[0],'/tmp/claude-0/i3d/bran_white.glb'); shutil.copy(r[1],'/tmp/claude-0/i3d/bran_tex.glb')
except Exception as e:
    print("ERR", repr(e)[:600])

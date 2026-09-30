import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
im=Image.open('/tmp/claude-0/-home-user-ClaudeCodeTestGodotGame/d547b921-d674-5409-8b27-e8f3e7b4479e/images/1.webp').convert('RGB')
A=np.asarray(im).astype(float)
bg=np.median(A[:40,600:900].reshape(-1,3),axis=0)
diff=np.linalg.norm(A-bg,axis=2)
fg=diff>34
def fig_mask(x0,x1,y0,y1,clear=()):
    m=np.zeros_like(fg); m[y0:y1,x0:x1]=fg[y0:y1,x0:x1]
    for (a,b,c,d) in clear: m[c:d,a:b]=False
    m=ndimage.binary_closing(m,iterations=2)
    lab,n=ndimage.label(m); sizes=ndimage.sum(m,lab,range(1,n+1)); return lab==(np.argmax(sizes)+1)
front=fig_mask(20,590,20,1215,clear=[(0,178,0,300),(0,80,300,625)])
back=fig_mask(560,800,40,530)
def bbox(m):
    ys,xs=np.where(m); return xs.min(),xs.max(),ys.min(),ys.max()
print('front bbox',bbox(front),'back bbox',bbox(back))
Image.fromarray((front*255).astype(np.uint8)).save('mask_front.png'); Image.fromarray((back*255).astype(np.uint8)).save('mask_back.png')
np.save('mask_front.npy',front); np.save('mask_back.npy',back)

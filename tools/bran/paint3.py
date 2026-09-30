import numpy as np, os
from scipy import sparse as sp
os.chdir('/tmp/claude-0/i3d')
from zones import *
d=np.load('bran_c.npz'); V=d['V'].astype(float); F=d['F']; n=len(V)
r=np.load('cls_raw.npz'); cls=r['cls'].copy()
c=np.load('bran_color.npz'); col=c['col']/255.0; have=c['have']
L=(0.3*col[:,0]+0.59*col[:,1]+0.11*col[:,2]); warm=col[:,0]-col[:,2]
known=cls>=0
# straps: brighter, warmer cloth inside the dark tunic/trouser zones
strap=known&(cls==TUNIC)&have&(L>0.24)&(warm>0.05)
cls[strap]=LEATHER
edges=np.unique(np.sort(np.concatenate([F[:,[0,1]],F[:,[1,2]],F[:,[2,0]]]),axis=1),axis=0)
A=sp.coo_matrix((np.ones(len(edges)),(edges[:,0],edges[:,1])),shape=(n,n)); A=(A+A.T).tocsr()
NC=10
known=cls>=0
for it in range(200):
    votes=np.zeros((n,NC))
    for k in range(NC): votes[:,k]=A@((cls==k)&known).astype(float)
    tot=votes.sum(1); upd=(~known)&(tot>0)
    if not upd.any(): break
    cls[upd]=votes[upd].argmax(1); known=known|upd
cls[cls<0]=TUNIC
# majority smoothing
for it in range(2):
    votes=np.zeros((n,NC))
    for k in range(NC): votes[:,k]=A@(cls==k).astype(float)+ (cls==k)*1.0
    cls=votes.argmax(1)
# the head, by geometry (using the head as it is once turned to face forward)
Vt=np.load('bran_skin.npz')['V']
hx,hy,hz=Vt[:,0],Vt[:,1],Vt[:,2]
head=(hy>0.68)&(np.hypot(hx-0.04,hz+0.03)<0.17)&(hx>-0.16)
cls[head]=SKIN
cls[head&((hy>0.835)|(hx<0.0)|((np.abs(hz+0.015)>0.085)&(hy>0.75)))]=HAIR
cls[head&(cls==SKIN)&(hy<0.77)&(hx>0.05)]=STUB
PAL={SKIN:(0.56,0.39,0.29),HAIR:(0.12,0.08,0.055),NAVY:(0.085,0.12,0.24),LEATHER:(0.36,0.19,0.085),TUNIC:(0.06,0.06,0.07),
     FUR:(0.64,0.59,0.5),BOOT:(0.2,0.11,0.06),GLOVE:(0.1,0.065,0.04),STUB:(0.36,0.25,0.2),KNEE:(0.17,0.1,0.06)}
base=np.array([PAL[k] for k in cls])
Ls=np.clip(L,0.05,0.9); var=np.ones(n)
for k in range(NC):
    m=cls==k
    if m.sum()>10: var[m]=np.clip((Ls[m]/np.mean(Ls[m]))**(1.0 if k in (SKIN,HAIR,STUB) else 0.5),0.5,1.6)
out=np.clip(base*var[:,None],0,1)
np.savez('bran_paint.npz',col=out,cls=cls)
from render import render
from PIL import Image
ims=[render(V,F,colors=out*255,yaw=y,size=640,light=(0.3,0.5,0.8)) for y in (270,0,90)]
o=Image.new('RGB',(640*3,640)); [o.paste(i,(640*k,0)) for k,i in enumerate(ims)]; o.save('paint_views.png')
print([(NAMES[k],int((cls==k).sum())) for k in range(NC)])

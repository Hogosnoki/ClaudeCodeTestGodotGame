import numpy as np, os
from PIL import Image
from scipy import ndimage, spatial
os.chdir('/tmp/claude-0/i3d')
d=np.load('bran_c.npz'); V=d['V'].astype(float); F=d['F']
IMG=np.asarray(Image.open('/tmp/claude-0/-home-user-ClaudeCodeTestGodotGame/d547b921-d674-5409-8b27-e8f3e7b4479e/images/1.webp').convert('RGB')).astype(float)
H,Wd=IMG.shape[:2]
def vnormals(V,F):
    n=np.zeros_like(V); t=V[F]; fn=np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0])
    for k in range(3): np.add.at(n,F[:,k],fn)
    return n/(np.linalg.norm(n,axis=1)[:,None]+1e-12)
N=vnormals(V,F)
def depth_map(u,v,dep,shape):
    zb=np.full(shape,-1e9)
    tri=np.stack([u[F],v[F],dep[F]],axis=2)  # (nf,3,3)
    for t in tri:
        xs,ys,zs=t[:,0],t[:,1],t[:,2]
        x0,x1=int(max(np.floor(xs.min()),0)),int(min(np.ceil(xs.max()),shape[1]-1)); y0,y1=int(max(np.floor(ys.min()),0)),int(min(np.ceil(ys.max()),shape[0]-1))
        if x1<x0 or y1<y0: continue
        gx,gy=np.meshgrid(np.arange(x0,x1+1),np.arange(y0,y1+1))
        dd=(ys[1]-ys[2])*(xs[0]-xs[2])+(xs[2]-xs[1])*(ys[0]-ys[2])
        if abs(dd)<1e-9: continue
        w0=((ys[1]-ys[2])*(gx-xs[2])+(xs[2]-xs[1])*(gy-ys[2]))/dd
        w1=((ys[2]-ys[0])*(gx-xs[2])+(xs[0]-xs[2])*(gy-ys[2]))/dd
        w2=1-w0-w1; m=(w0>=-0.02)&(w1>=-0.02)&(w2>=-0.02)
        z=w0*zs[0]+w1*zs[1]+w2*zs[2]
        sub=zb[y0:y1+1,x0:x1+1]; up=m&(z>sub); sub[up]=z[up]
    return zb
def sample(u,v,mask,erode=3):
    img=ndimage.gaussian_filter(IMG,(1.2,1.2,0))
    em=ndimage.binary_erosion(mask,iterations=erode)
    ui=np.clip(np.round(u).astype(int),0,Wd-1); vi=np.clip(np.round(v).astype(int),0,H-1)
    col=img[vi,ui]; ok=em[vi,ui]
    return col,ok
col=np.zeros((len(V),3)); have=np.zeros(len(V),bool); src=np.zeros(len(V))
# front
sc,u0,v0,s=np.load('align_front.npy'); mask=np.load('mask_front.npy')
u=u0-V[:,2]*s; v=v0-V[:,1]*s
zb=depth_map(u,v,V[:,0],(H,Wd))
c,ok=sample(u,v,mask)
ui=np.clip(np.round(u).astype(int),0,Wd-1); vi=np.clip(np.round(v).astype(int),0,H-1)
vis=V[:,0]>=zb[vi,ui]-0.02
front_ok=ok&vis&(N[:,0]>0.3)
col[front_ok]=c[front_ok]; have|=front_ok; src[front_ok]=1
np.save('front_ok.npy',front_ok)
print('front colored',front_ok.sum(),'of',len(V))
np.savez('color_stage1.npz',col=col,have=have)

# back
sc,u0,v0,s=np.load('align_back.npy'); mask=np.load('mask_back.npy')
# the back figure is small: sample from the un-upscaled image with a tighter blur
u=u0+V[:,2]*s; v=v0-V[:,1]*s
zb=depth_map(u,v,-V[:,0],(H,Wd))
c,ok=sample(u,v,mask,erode=2)
ui=np.clip(np.round(u).astype(int),0,Wd-1); vi=np.clip(np.round(v).astype(int),0,H-1)
vis=(-V[:,0])>=zb[vi,ui]-0.02
back_ok=ok&vis&(N[:,0]<-0.3)&~have
col[back_ok]=c[back_ok]; have|=back_ok
print('back colored',back_ok.sum())
# diffuse to the rest (sides, hidden): iterate neighbour averaging over mesh edges, seeded by coloured verts
edges=np.unique(np.sort(np.concatenate([F[:,[0,1]],F[:,[1,2]],F[:,[2,0]]]),axis=1),axis=0)
import scipy.sparse as sp
n=len(V)
A=sp.coo_matrix((np.ones(len(edges)),(edges[:,0],edges[:,1])),shape=(n,n)); A=(A+A.T).tocsr()
deg=np.asarray(A.sum(1)).ravel()
known=have.copy(); cc=col.copy()
for it in range(400):
    w=known.astype(float)
    num=A@(cc*w[:,None]); den=A@w
    upd=(~known)&(den>0)
    if not upd.any(): break
    cc[upd]=num[upd]/den[upd][:,None]; known=known|upd
print('diffused iters',it,'uncolored',(~known).sum())
# a few smoothing passes over the diffused (non-projected) vertices only, so seams soften
for it in range(6):
    avg=(A@cc)/deg[:,None]
    sel=~have
    cc[sel]=0.5*cc[sel]+0.5*avg[sel]
np.savez('bran_color.npz',col=np.clip(cc,0,255),have=have)
from render import render
ims=[render(V,F,colors=cc,yaw=y,size=640,light=(0.3,0.5,0.8)) for y in (270,0,90)]
o=Image.new('RGB',(640*3,640)); [o.paste(i,(640*k,0)) for k,i in enumerate(ims)]; o.save('color_views.png')

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

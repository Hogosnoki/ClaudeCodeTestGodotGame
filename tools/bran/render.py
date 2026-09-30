import numpy as np
from PIL import Image
def render(V,F,colors=None,yaw=0.0,size=700,axis_up=1,light=(0.4,0.7,0.6),bg=(40,44,52)):
    """orthographic z-buffer render; yaw rotates about up axis (degrees)"""
    V=np.asarray(V,float).copy()
    a=np.radians(yaw); c,s=np.cos(a),np.sin(a)
    R=np.array([[c,0,s],[0,1,0],[-s,0,c]])
    V=V@R.T
    lo=V.min(0); hi=V.max(0)
    sc=size*0.9/max(hi[1]-lo[1],hi[0]-lo[0])
    cx=(lo[0]+hi[0])/2; cy=(lo[1]+hi[1])/2
    X=(V[:,0]-cx)*sc+size/2; Y=size/2-(V[:,1]-cy)*sc; Z=V[:,2]
    img=np.zeros((size,size,3),np.float32); img[:]=bg
    zb=np.full((size,size),-1e9,np.float32)
    L=np.array(light); L=L/np.linalg.norm(L)
    tri=V[F]; n=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]); ln=np.linalg.norm(n,axis=1)+1e-12; n=n/ln[:,None]
    shade=np.clip(n@L,0,1)*0.75+0.25
    if colors is None: fc=np.full((len(F),3),200.0)
    else: fc=np.asarray(colors)[F].mean(1)
    order=np.argsort(Z[F].mean(1))
    for i in order:
        f=F[i]
        xs=X[f]; ys=Y[f]; zs=Z[f]
        x0,x1=int(max(xs.min(),0)),int(min(xs.max()+1,size-1)); y0,y1=int(max(ys.min(),0)),int(min(ys.max()+1,size-1))
        if x1<=x0 or y1<=y0: continue
        gx,gy=np.meshgrid(np.arange(x0,x1+1),np.arange(y0,y1+1))
        d=(ys[1]-ys[2])*(xs[0]-xs[2])+(xs[2]-xs[1])*(ys[0]-ys[2])
        if abs(d)<1e-9: continue
        w0=((ys[1]-ys[2])*(gx-xs[2])+(xs[2]-xs[1])*(gy-ys[2]))/d
        w1=((ys[2]-ys[0])*(gx-xs[2])+(xs[0]-xs[2])*(gy-ys[2]))/d
        w2=1-w0-w1
        m=(w0>=-0.001)&(w1>=-0.001)&(w2>=-0.001)
        z=w0*zs[0]+w1*zs[1]+w2*zs[2]
        sub=zb[y0:y1+1,x0:x1+1]
        upd=m&(z>sub)
        sub[upd]=z[upd]
        img[y0:y1+1,x0:x1+1][upd]=fc[i]*shade[i]
    return Image.fromarray(np.clip(img,0,255).astype(np.uint8))

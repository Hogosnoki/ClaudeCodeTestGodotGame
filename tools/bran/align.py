import numpy as np
from PIL import Image, ImageDraw
d=np.load('bran_c.npz'); V=d['V']; F=d['F']
front=np.load('mask_front.npy'); back=np.load('mask_back.npy')
def sil(view,u0,v0,s,shape,flip):
    im=Image.new('L',(shape[1],shape[0]),0); dr=ImageDraw.Draw(im)
    if view=='front': u=u0-V[:,2]*s
    else: u=u0+V[:,2]*s
    v=v0-V[:,1]*s
    for f in F:
        dr.polygon([(u[f[0]],v[f[0]]),(u[f[1]],v[f[1]]),(u[f[2]],v[f[2]])],fill=255)
    return np.asarray(im)>0
def score(m,img,ignore):
    inter=(m&img).sum(); prec=inter/max(m.sum(),1)
    tgt=img&~ignore; rec=(m&tgt).sum()/max(tgt.sum(),1)
    return 2*prec*rec/(prec+rec+1e-9)
if __name__=='__main__':
    import itertools, time
    H,Wd=front.shape
    ignore=np.zeros_like(front); ignore[560:,:200]=True; ignore[:,:0]=True
    # initial: head top y=-? figure top/bottom in image
    ys,xs=np.where(front); top=ys.min(); bot=1190
    s0=(bot-top)/1.685
    best=(0,)
    # coarse-to-fine
    for s in np.linspace(s0*0.94,s0*1.06,7):
        for v0 in np.linspace(top+0.875*s-12,top+0.875*s+12,5):
            for u0 in np.linspace(300,380,9):
                m=sil('front',u0,v0,s,front.shape,True)
                sc=score(m,front,ignore)
                if sc>best[0]: best=(sc,u0,v0,s)
    print('coarse',best)
    sc,u0,v0,s=best
    for ds in np.linspace(-0.02,0.02,5):
        for dv in (-6,-3,0,3,6):
            for du in (-6,-3,0,3,6):
                m=sil('front',u0+du,v0+dv,s*(1+ds),front.shape,True); sc2=score(m,front,ignore)
                if sc2>best[0]: best=(sc2,u0+du,v0+dv,s*(1+ds))
    print('fine',best)
    np.save('align_front.npy',np.array(best))

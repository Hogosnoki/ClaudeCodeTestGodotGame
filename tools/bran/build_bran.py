import numpy as np, os, struct
HEADTURN=-30.0
os.chdir('/tmp/claude-0/i3d')
from rig import J
from zones import *
d=np.load('bran_c.npz'); V=d['V'].astype(float).copy(); F=d['F'].astype(np.int64)
p=np.load('bran_paint.npz'); col=p['col']; cls=p['cls']
n=len(V)
def sstep(a,b,x):
    t=np.clip((x-a)/(b-a),0,1); return t*t*(3-2*t)
# ---- straighten the stance: boots and shins closer to the rig's legs (mesh legs splay wider than the rig's)
x,y,z=V[:,0],V[:,1],V[:,2]
legmask=(x>-0.12)&(y<-0.15)
sgn=np.sign(z)
shift=0.11*sstep(-0.25,-0.62,y)*sstep(0.03,0.09,np.abs(z))*legmask
V[:,2]=z-sgn*shift
# arms: pull the forearms a little in and back toward the rig's arm lines
armmask=(np.abs(z)>0.15)&(y>-0.2)&(y<0.5)&(x>-0.12)
# pull both fists to where the rig's hands (and the sword's grip) are: (0.016,-0.1,+-0.2)
fr=sstep(0.3,-0.06,y)*armmask
tgt_r=np.array([0.016,-0.1,0.2]); cur_r=np.array([0.09,-0.066,0.234])
tgt_l=np.array([0.016,-0.1,-0.2]); cur_l=np.array([0.127,-0.067,-0.212])
for i in np.where(armmask)[0]:
    dl=(tgt_r-cur_r) if z[i]>0 else (tgt_l-cur_l)
    V[i]+=dl*fr[i]
# ---- the head is turned ~30 deg toward his left in the art: turn it back to face forward (twisting through the neck)
HC=np.array([0.04,-0.03])
def rotxz(P,deg):
    a=np.radians(deg); c,sn=np.cos(a),np.sin(a)
    q=P-HC; return np.stack([q[:,0]*c+q[:,1]*sn, -q[:,0]*sn+q[:,1]*c],1)+HC
hd=(np.hypot(V[:,0]-HC[0],V[:,2]-HC[1])<0.19)&(V[:,1]>0.6)&(V[:,0]>-0.16)
hw=sstep(0.6,0.7,V[:,1])*hd
for i in np.where(hw>0)[0]:
    q=rotxz(V[i:i+1][:,[0,2]],HEADTURN*hw[i]); V[i,0]=q[0,0]; V[i,2]=q[0,1]
# ---- rig segments
seg={}
def S(name,a,b): seg[name]=(np.array(a,float),np.array(b,float))
S('hips',J['hips'],J['spine']); S('spine',J['spine'],J['chest']); S('chest',J['chest'],J['neck']); S('neck',J['neck'],J['head']); S('head',J['head'],(0.02,0.9,0))
for k,zz in (('_r',1),('_l',-1)):
    S('clav'+k,J['clav'+k],J['uarm'+k]); S('uarm'+k,J['uarm'+k],J['farm'+k]); S('farm'+k,J['farm'+k],J['hand'+k]); S('hand'+k,J['hand'+k],(0.018,-0.125,0.2*zz))
    S('thigh'+k,J['thigh'+k],J['shin'+k]); S('shin'+k,J['shin'+k],J['foot'+k]); S('foot'+k,J['foot'+k],(0.12,-0.78,0.095*zz))
for a,b in (('cape0','cape1'),('cape1','cape2'),('cape2','cape3')): S(a,J[a],J[b])
S('cape3',J['cape3'],(-0.15,-0.6,0))
names=list(seg.keys())
def dist_to(P,a,b):
    ab=b-a; t=np.clip(((P-a)@ab)/(ab@ab+1e-12),0,1); q=a+np.outer(t,ab); return np.linalg.norm(P-q,axis=1)
D={k:dist_to(V,*seg[k]) for k in names}
sigma=0.05
W=np.zeros((n,len(names)))
idx={k:i for i,k in enumerate(names)}
x,y,z=V[:,0],V[:,1],V[:,2]
side_r=z>=0
# which bones may each vertex use
allow=np.zeros((n,len(names)),bool)
def allow_for(mask,bones):
    for b in bones: allow[mask,idx[b]]=True
legs=(y<0.03)&(x>-0.1)
slab=(cls==NAVY)&(x<-0.085)&(y<0.52)&(y>-0.62)&(np.abs(z)<0.2)
arms=(np.abs(z)>0.15)&(y>-0.2)&(y<0.66)&~slab&~legs
torso=~legs&~arms&~slab
headz=y>0.6
allow_for(legs&side_r,['hips','thigh_r','shin_r','foot_r'])
allow_for(legs&~side_r,['hips','thigh_l','shin_l','foot_l'])
cen=legs&(np.abs(z)<0.04); allow_for(cen,['thigh_r','thigh_l'])
allow_for(torso&~headz,['hips','spine','chest','neck'])
allow_for(headz&~arms,['neck','head','chest'])
allow_for(arms&side_r,['clav_r','uarm_r','farm_r','hand_r','chest'])
allow_for(arms&~side_r,['clav_l','uarm_l','farm_l','hand_l','chest'])
allow_for(slab,['chest','cape0','cape1','cape2','cape3'])
Dm=np.full((n,len(names)),np.inf)
for k in names: Dm[:,idx[k]]=np.where(allow[:,idx[k]],D[k],np.inf)
dmin=Dm.min(1)
for k in names:
    W[:,idx[k]]=np.where(np.isfinite(Dm[:,idx[k]]),np.exp(-((Dm[:,idx[k]]-np.where(np.isfinite(dmin),dmin,0))/0.035)**2),0)
# slab: follow the torso at the top, swing below
ws=slab
top=sstep(0.15,0.5,y)
W[ws,:]=0
for k in ('cape0','cape1','cape2','cape3'):
    pass
cy=np.array([J['cape0'][1],J['cape1'][1],J['cape2'][1],J['cape3'][1]])
for i in np.where(ws)[0]:
    yy=y[i]
    cw=np.zeros(4)
    if yy>=cy[0]: cw[0]=1
    elif yy<=cy[3]: cw[3]=1
    else:
        j=np.searchsorted(-cy,-yy)-1; t=(cy[j]-yy)/(cy[j]-cy[j+1]); cw[j]=1-t; cw[j+1]=t
    tt=top[i]
    tt=max(tt,0.72)
    W[i,idx['chest']]=tt
    for j in range(4): W[i,idx['cape%d'%j]]=(1-tt)*cw[j]
# top two per vertex
o=np.argsort(-W,axis=1)[:,:2]
wa=W[np.arange(n),o[:,0]]; wb=W[np.arange(n),o[:,1]]
tot=wa+wb; tot[tot<1e-9]=1
bad=(wa+wb)<1e-6
wa=np.where(bad,1,wa/tot); 
o[bad,0]=idx['hips']; o[bad,1]=idx['hips']
print('no-weight verts',bad.sum())
frac=np.clip(wa,0,1)
# export
used=sorted(set(o.ravel().tolist())); remap={b:i for i,b in enumerate(used)}
tab=[names[b] for b in used]
print('bones used',tab)
with open('bran.mesh','wb') as f:
    f.write(b'BRAN'); f.write(struct.pack('<III',n,len(F),len(tab)))
    for nm in tab:
        bn=nm.encode(); f.write(struct.pack('<B',len(bn))); f.write(bn)
    f.write(V.astype('<f4').tobytes())
    f.write((np.clip(col,0,1)*255).round().astype(np.uint8).tobytes())
    f.write(cls.astype(np.uint8).tobytes())
    f.write(np.array([remap[b] for b in o[:,0]],np.uint8).tobytes())
    f.write(np.array([remap[b] for b in o[:,1]],np.uint8).tobytes())
    f.write((frac*255).round().astype(np.uint8).tobytes())
    f.write(F.astype('<u2').tobytes())
print('wrote bran.mesh',os.path.getsize('bran.mesh'))
np.savez('bran_skin.npz',V=V,F=F,o=o,frac=frac,names=np.array(names))

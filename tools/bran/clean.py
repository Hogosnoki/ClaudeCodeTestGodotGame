import trimesh, numpy as np, sys
from scipy import ndimage
from skimage import measure
import fast_simplification as fs
d=np.load('bran_main.npz'); F=d['F']; W=np.load('W0.npy')
m=trimesh.Trimesh(W,F,process=False)
pitch=0.007
vg=m.voxelized(pitch).fill()
mat=vg.matrix.copy()
idx=np.argwhere(mat); pts=vg.indices_to_points(idx)
L=np.load('sword_line.npy'); c=L[:3]; dd=L[3:]; dd/=np.linalg.norm(dd)
t=(pts-c)@dd; dist=np.linalg.norm((pts-c)-np.outer(t,dd),axis=1)
blade=(dist<0.04)&(t>-0.6)&(t<0.5)
# guard: a bar through the axis point at t~0.56, perpendicular-ish; carve a ball + slab
g=c+0.56*dd
guard=(np.linalg.norm(pts-g,axis=1)<0.085)&(pts[:,0]>g[0]-0.10)&(pts[:,1]<g[1]+0.05)&(pts[:,1]>g[1]-0.06)
kill=blade|guard
print('kill',blade.sum(),guard.sum())
mat[tuple(idx[kill].T)]=False
# thin leftovers of the sword around the fist (blade stub, crossguard): open them away within a box
lo=vg.points_to_indices(np.array([-0.02,-0.36,0.04])); hi=vg.points_to_indices(np.array([0.36,0.08,0.34]))
lo=np.round(lo).astype(int); hi=np.round(hi).astype(int)
a=np.minimum(lo,hi); b=np.maximum(lo,hi)+1
sl=tuple(slice(max(a[k],0),min(b[k],mat.shape[k])) for k in range(3))
box=mat[sl]
rr=3
zz,yy,xx=np.mgrid[-rr:rr+1,-rr:rr+1,-rr:rr+1]; ball=(xx**2+yy**2+zz**2)<=rr*rr
op=ndimage.binary_opening(box,structure=ball)
print('opened away',int(box.sum()-op.sum()))
mat[sl]=op
lab,n=ndimage.label(mat); sizes=ndimage.sum(mat,lab,range(1,n+1)); mat=lab==(np.argmax(sizes)+1)
sm=ndimage.gaussian_filter(mat.astype(np.float32),0.8)
verts,faces,_,_=measure.marching_cubes(sm,0.5)
faces=faces[:,::-1]
wv=trimesh.transform_points(verts,vg.transform)
V2,F2=fs.simplify(wv.astype(np.float32),faces.astype(np.int32),target_count=50000,agg=5)
np.savez('bran_clean.npz',V=V2,F=F2); print(V2.shape,F2.shape)

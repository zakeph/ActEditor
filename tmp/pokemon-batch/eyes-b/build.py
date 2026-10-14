from detail import raw, ROOT, NAMES
from PIL import Image, ImageDraw
from collections import Counter
import numpy as np, json

COLORS={'gengar':[74,41,74],'geodude':[66,58,49],'gible':[49,66,82],'glaceon':[49,58,74],'goldeen':[82,82,90],'haxorus':[66,66,0],'hydreigon':[41,33,41],'jolteon':[90,74,16],'kingdra':[74,90,148],'leafeon':[58,41,25],'lucario':[16,74,123],'ludicolo':[74,107,74],'metagross':[49,58,99],'milotic':[99,41,41]}
def color_mask(a,colors):
    out=np.zeros(a.shape[:2],bool)
    for c in colors:out|=(a[:,:,:3]==c).all(-1)&(a[:,:,3]>0)
    return out
def ink(a):return (a[:,:,0]==a[:,:,1])&(a[:,:,1]==a[:,:,2])&(a[:,:,0]<=16)&(a[:,:,3]>0)
def components(mask):
    seen=set(); out=[]
    for y,x in np.argwhere(mask):
        x,y=int(x),int(y)
        if (x,y) in seen:continue
        stack=[(x,y)]; seen.add((x,y)); comp=[]
        while stack:
            p=stack.pop(); comp.append(p)
            for dx in (-1,0,1):
                for dy in (-1,0,1):
                    q=(p[0]+dx,p[1]+dy)
                    if 0<=q[0]<mask.shape[1] and 0<=q[1]<mask.shape[0] and mask[q[1],q[0]] and q not in seen:seen.add(q);stack.append(q)
        out.append(comp)
    return out
def bbox(pts):
    return min(x for x,y in pts),min(y for x,y in pts),max(x for x,y in pts),max(y for x,y in pts)
def in_box(mask,box):
    x0,y0,x1,y1=box
    return [(int(x),int(y)) for y,x in np.argwhere(mask) if x0<=x<=x1 and y0<=y<=y1]
def near(a,x,y,offsets):
    k=ink(a);return [(x+dx,y+dy) for dx,dy in offsets if 0<=x+dx<a.shape[1] and 0<=y+dy<a.shape[0] and k[y+dy,x+dx]]
def hull(points):
    ps=sorted(set(points))
    if len(ps)<=2:return ps
    def cross(o,a,b):return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
    lo=[];hi=[]
    for p in ps:
        while len(lo)>=2 and cross(lo[-2],lo[-1],p)<=0:lo.pop()
        lo.append(p)
    for p in reversed(ps):
        while len(hi)>=2 and cross(hi[-2],hi[-1],p)<=0:hi.pop()
        hi.append(p)
    return lo[:-1]+hi[:-1]
def inside(p,poly):
    if len(poly)<3:return False
    signs=[(poly[(i+1)%len(poly)][0]-v[0])*(p[1]-v[1])-(poly[(i+1)%len(poly)][1]-v[1])*(p[0]-v[0]) for i,v in enumerate(poly)]
    return all(s>=0 for s in signs) or all(s<=0 for s in signs)
def pupil(name,side,a):
    k=ink(a); out=[]
    if name in ('gengar','hydreigon','leafeon'):return []
    if side=='back' and name in ('haxorus','ludicolo','metagross','milotic'):return []
    if name=='haxorus':
        for y,x in np.argwhere(color_mask(a,[(255,255,255)])):out+=near(a,int(x),int(y),[(0,1),(1,0),(1,1)])
    elif name=='geodude':
        cs=[c for c in components(color_mask(a,[(255,255,255),(206,222,206)])) if len(c)>=2]
        if side=='front':
            cs=sorted(cs,key=lambda c:bbox(c)[0])
            for j,c in enumerate(cs):
                x0,y0,x1,y1=bbox(c)
                out+=in_box(k,(x1+1,y0,x1+1,y1)) if j==0 else in_box(k,(x0-1,y0,x0-1,y1))
                out+=[p for p in in_box(k,(x0,y0,x1,y1)) if inside(p,hull(c))]
        else:
            for c in cs:
                x0,y0,x1,y1=bbox(c);out+=in_box(k,(x0,y0,x1+1,y1))
    elif name=='gible':
        white=[(int(x),int(y)) for y,x in np.argwhere(color_mask(a,[(255,255,255)]))]
        if white:
            top=min(y for x,y in white);eye=[p for p in white if p[1]<=top+(3 if side=='front' else 2)]
            x0,y0,x1,y1=bbox(eye)
            out+=in_box(k,(x0+1,y0,x1,y1)) if side=='front' else in_box(k,(x0,y0,x1+2,y1))
    elif name=='goldeen':
        cs=components(color_mask(a,[(0,107,99),(66,173,165)]))
        for c in cs:
            x0,y0,x1,y1=bbox(c)
            if side=='front':out+=in_box(k,(x0,y0,x1,y1))
            else:out+=in_box(k,(x0,y0,x1+1,y1))
    elif name=='lucario':
        c=[(int(x),int(y)) for y,x in np.argwhere(color_mask(a,[(222,66,66),(165,49,49)]))]
        if c:
            x0,y0,x1,y1=bbox(c)
            out+=[p for p in in_box(k,(x0,y0,x1,y1)) if inside(p,hull(c))] if side=='front' else in_box(k,(x0,y0,x1+1,y1))
    elif name=='kingdra':
        c=[(int(x),int(y)) for y,x in np.argwhere(color_mask(a,[(189,58,49)]))]
        if c:
            x0,y0,x1,y1=bbox(c);out+=in_box(k,(x0,y0,x1+1,y1+(side=='front')))
    elif name=='metagross':
        for c in components(color_mask(a,[(214,74,82),(132,74,74)])):
            out+=in_box(k,bbox(c))
    elif name=='ludicolo':
        white=color_mask(a,[(255,255,255)])
        # Pupil holes split the white sclera into disconnected pieces in oblique poses.
        groups=[]
        for p in sorted([(int(x),int(y)) for y,x in np.argwhere(white)]):
            if not groups or p[0]-max(x for x,y in groups[-1])>3:groups.append([])
            groups[-1].append(p)
        for c in groups:
            x0,y0,x1,y1=bbox(c)
            for x,y in in_box(k,(x0,y0,x1,y1)):
                if any(white[y,l] for l in range(x0,x)) and any(white[y,r] for r in range(x+1,x1+1)):out.append((x,y))
                elif any(white[t,x] for t in range(y0,y)) and any(white[b,x] for b in range(y+1,y1+1)):out.append((x,y))
    elif name=='jolteon':
        purple=color_mask(a,[(74,8,123)]);cs=[c for c in components(purple) if len(c)<=8]
        for c in cs:
            x0,y0,x1,y1=bbox(c)
            if len(c)==1:
                out+=near(a,x0,y0,[(0,-1)])
                continue
            # The small purple cluster belongs to the iris; the large cluster is ear lining.
            local=in_box(k,(x0-1,y0-1,x1+1,y1+1))
            white=color_mask(a,[(255,255,255)])
            possible=[]
            for comp in components(k):
                if any(p in local for p in comp) and len(comp)<=12 and any(any(0<=x+dx<a.shape[1] and 0<=y+dy<a.shape[0] and white[y+dy,x+dx] for dx in (-1,0,1) for dy in (-1,0,1)) for x,y in comp):possible+=comp
            if side=='back':
                lights=in_box(white,(x0-1,y0-1,x1+1,y1+1))
                if lights:
                    gx,gy=max(lights,key=lambda p:p[0]);possible=[p for p in possible if gx-1<=p[0]<=gx and gy<=p[1]<=gy+2]
            out+=possible
    elif name=='milotic':
        for c in components(color_mask(a,[(255,255,230)])):
            if len(c)>2:continue
            for x,y in c:
                if y<a.shape[0]*0.55:out+=near(a,x,y,[(1,0),(0,1),(1,1)])
    return sorted(set(out),key=lambda p:(p[1],p[0]))

def write(name,reviewed=False):
    cfg={'name':name,'color':COLORS[name],'notes':'Neutral ink is RGB0/8/16. Per-frame pupil anchors; original colored iris and pupil shades remain untouched.','reviewed':reviewed}
    for side in ('front','back'):
        m,a=raw(name,side);frames=[]
        for f in a:
            pts=pupil(name,side,f); frames.append([y*m['width']+x for x,y in pts])
        cfg[side]={'width':m['width'],'height':m['height'],'frames':frames}
    if name in ('gengar','hydreigon','leafeon'):cfg['notes']='Reviewed eyes: iris/pupil pixels are colored rather than neutral RGB<=16; no neutral pupil pixels to preserve. Back eye absent or also colored.'
    (ROOT/f'{name}.json').write_text(json.dumps(cfg,separators=(',',':')))
    print(name,[(s,Counter(map(len,cfg[s]['frames']))) for s in ('front','back')])
    return cfg
def review(name):
    cfg=write(name)
    for side in ('front','back'):
        m,a=raw(name,side); crops=[]
        for i,f in enumerate(a):
            pts=[(p%m['width'],p//m['width']) for p in cfg[side]['frames'][i]]
            if not pts:continue
            x0,y0,x1,y1=bbox(pts);pad=5
            x0=max(0,x0-pad); y0=max(0,y0-pad);x1=min(m['width'],x1+pad+1);y1=min(m['height'],y1+pad+1)
            z=f[y0:y1,x0:x1].copy();z[ink(z),:3]=COLORS[name]
            for x,y in pts:z[y-y0,x-x0,:3]=[255,0,255]
            im=Image.fromarray(z); crops.append((i,im))
        if not crops:continue
        cw=max(im.width for _,im in crops)*6+8;ch=max(im.height for _,im in crops)*6+22
        per=10;out=Image.new('RGB',(cw*per,ch*((len(crops)+per-1)//per)),(110,126,137));d=ImageDraw.Draw(out)
        for j,(i,im) in enumerate(crops):
            x=(j%per)*cw;y=(j//per)*ch;d.text((x,y),str(i),fill='white');im=im.resize((im.width*6,im.height*6),Image.Resampling.NEAREST);out.paste(im,(x,y+18),im)
        out.save(ROOT/f'{name}-{side}-mask-review.png')

if __name__=='__main__':
    import sys
    for n in sys.argv[1:]:review(n)

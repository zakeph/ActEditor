from inspect import *
from collections import Counter
PROBE=ROOT.parent/'probe'
def raw(name,side):
    import numpy as np
    m=json.loads((PROBE/f'{name}-{side}.json').read_text(encoding='utf-8-sig')); a=np.fromfile(PROBE/f'{name}-{side}.bgra',dtype='uint8').reshape(m['count'],m['height'],m['width'],4)
    return m,a[:,:,:,[2,1,0,3]]
def grid(name,side,frame=0,box=None):
    m,a=raw(name,side); im=Image.fromarray(a[frame]); im=im.crop(box) if box else im
    ox,oy=box[:2] if box else (0,0)
    unit=14; out=Image.new('RGB',((im.width+3)*unit,(im.height+3)*unit),'#77818a');d=ImageDraw.Draw(out)
    rgb=im.convert('RGB')
    for y in range(im.height):
        d.text((0,(y+2)*unit),str(y+oy),fill='white')
        for x in range(im.width):
            c=im.getpixel((x,y));c=c[:3] if c[3] else (119,129,138)
            d.rectangle(((x+2)*unit,(y+2)*unit,(x+3)*unit-1,(y+3)*unit-1),fill=c,outline='#666666')
    for x in range(im.width):d.text(((x+2)*unit,0),str(x+ox),fill='white')
    out.save(ROOT/f'{name}-{side}-grid-{frame}.png')
    count=Counter(tuple(p) for row in a[frame] for p in row if p[3]); palette=list(count)
    labels='0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz'
    code={c:labels[i] for i,c in enumerate(palette)}
    print(name,side,frame,'palette', {code[c]:c[:3] for c in palette})
    x0,y0,x1,y1=box if box else (0,0,m['width'],m['height'])
    for y in range(y0,y1):print(f'{y:02}', ''.join(code.get(tuple(p),'.') for p in a[frame,y,x0:x1]))
if __name__=='__main__':
    grid('haxorus','front',0,(7,15,25,31))
    grid('jolteon','front',0,(3,13,17,27))
    grid('jolteon','back',0,(35,12,49,27))
    grid('ludicolo','front',0,(27,26,49,45))
    grid('milotic','front',0,(18,20,38,40))

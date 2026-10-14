from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import json, sys
ROOT=Path(r'D:\xampp\htdocs\ActEditor\tmp\pokemon-batch\eyes-b')
SRC=Path(r'C:\Users\ShivaN\Desktop\pokes')
NAMES='gengar geodude gible glaceon goldeen haxorus hydreigon jolteon kingdra leafeon lucario ludicolo metagross milotic'.split()
def read(name,side):
    p=SRC/(name+('-back' if side=='back' else '')+'.gif')
    im=Image.open(p); frames=[]
    for i in range(im.n_frames):
        im.seek(i); frames.append(im.convert('RGBA').copy())
    return frames
def sheet(name,side,count=12,scale=4):
    frames=read(name,side); w,h=frames[0].size
    indices=sorted(set(round(i*(len(frames)-1)/(count-1)) for i in range(count)))
    out=Image.new('RGB',((w*scale+8)*4,(h*scale+26)*((len(indices)+3)//4)),(110,126,137))
    d=ImageDraw.Draw(out)
    for j,i in enumerate(indices):
        x=(j%4)*(w*scale+8); y=(j//4)*(h*scale+26)
        out.paste(frames[i].resize((w*scale,h*scale),Image.Resampling.NEAREST),(x,y+20),frames[i].resize((w*scale,h*scale),Image.Resampling.NEAREST))
        d.text((x+3,y+3),f'{name} {side} #{i}',fill='white')
    out.save(ROOT/f'{name}-{side}-sheet.png')
def overview():
    out=Image.new('RGB',(1000,len(NAMES)*180),(110,126,137)); d=ImageDraw.Draw(out)
    for row,n in enumerate(NAMES):
        for side,col in [('front',0),('back',500)]:
            f=read(n,side)[0]; f=f.resize((f.width*3,f.height*3),Image.Resampling.NEAREST)
            out.paste(f,(col+150,row*180),f); d.text((col+3,row*180+10),n+' '+side,fill='white')
    out.save(ROOT/'overview.png')
if __name__=='__main__':
    if len(sys.argv)==1: overview()
    else:
        for n in sys.argv[1:]:
            for s in ['front','back']: sheet(n,s)

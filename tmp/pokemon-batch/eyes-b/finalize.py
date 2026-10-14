from build import *

NOTES={
'gengar':'Red eye interiors have no neutral ink pupils in any pose. Back eyes are hidden. Preserve no neutral pixels; existing colored eye borders are untouched.',
'geodude':'Front pupils follow white sclera and are two vertical ink pixels per eye, reduced during partial blinks. Back has a visible profile eye with one pupil ink pixel in every frame. Reviewed all 209 frames.',
'gible':'White eye glint/sclera locates the five-pixel front black pupil and three-pixel rear profile black pupil. Upper eyelid and body outlines are excluded. Counts consistent in all 250 frames.',
'goldeen':'Green/cyan iris anchors inner neutral pupil. Front pupil varies from one to four pixels with eye rotation; rear profile pupil is one or two pixels at the right edge of its small iris. All 228 frames reviewed.',
'haxorus':'Unique white eye glint anchors one or two neutral pupil pixels immediately right/below it. Frames53,54,125 are blinks with no glint/pupil. Rear eye is hidden. All 367 frames reviewed.',
'hydreigon':'Central eye uses red/brown iris and dark colored pupil/border, with no neutral ink in its interior. Side heads have no eyes. Rear visible eye is also colored. No neutral pupil mask.',
'jolteon':'Front main pupil is a four-pixel ink cluster adjacent to purple iris and white glint. The tiny far eye retains one ink pupil pixel when present; outer head contour is excluded. Rear profile retains three inner ink pupil pixels; isolated eye perimeter ink is excluded. All 198 frames reviewed.',
'kingdra':'Red iris and white sclera anchor two front neutral pupil pixels (one during blink), and one rear profile pupil pixel (zero during two blinks). Existing colored iris remains untouched. All 108 frames reviewed.',
'leafeon':'Both front pupils are dark brown RGB58,41,25 rather than neutral ink. Neutral pixels adjacent to the far eye are head contour. Rear eyes are hidden. No neutral pupil mask.',
'lucario':'Front neutral pupils lie inside the convex region of bright/dark red iris and vary between one and two ink pixels. Rear small profile eye retains one neutral pupil pixel to the right of red iris. Eyebrow/perimeter ink is excluded. All 286 frames reviewed.',
'ludicolo':'White sclera groups locate both pupils, including poses where the pupil separates the sclera into disconnected parts. Two to four neutral pupil pixels per frame are preserved. Rear eyes hidden. All 159 frames reviewed.',
'metagross':'Two red/dark-red iris groups enclose three neutral pupil pixels total in every front frame. Eye perimeter ink is excluded. Rear eyes are hidden. All 208 frames reviewed.',
'milotic':'Small isolated white eye glint locates three neutral pupil pixels to its right/below in all 191 front frames. Eyeliner is excluded. Rear eye is hidden throughout 199 frames.'}

stats=[]
for name,note in NOTES.items():
    p=ROOT/f'{name}.json';cfg=json.loads(p.read_text())
    cfg['notes']=note;cfg['reviewed']=True;cfg['inkColors']=[]
    total=0
    for side in ('front','back'):
        m,a=raw(name,side);part=cfg[side]
        assert (part['width'],part['height'],len(part['frames']))==(m['width'],m['height'],m['count'])
        palette={tuple(c[:3]) for c in m['palette']}
        assert tuple(cfg['color']) in palette,(name,side,'outline color not in source palette')
        for i,ps in enumerate(part['frames']):
            assert len(ps)==len(set(ps))
            for pi in ps:
                y,x=divmod(pi,m['width']);assert 0<=x<m['width'] and 0<=y<m['height']
                assert ink(a[i])[y,x],(name,side,i,pi,'not opaque neutral<=16')
            total+=len(ps)
        cfg['inkColors']=sorted(set(tuple(c[:3]) for c in m['palette'] if c[0]==c[1]==c[2] and c[0]<=16)|set(map(tuple,cfg['inkColors'])))
    p.write_text(json.dumps(cfg,separators=(',',':')))
    stats.append({'name':name,'outline':cfg['color'],'frontFrames':len(cfg['front']['frames']),'backFrames':len(cfg['back']['frames']),'preservedPixels':total})
(ROOT/'validation.json').write_text(json.dumps({'ok':True,'species':13,'frames':sum(s['frontFrames']+s['backFrames'] for s in stats),'stats':stats},indent=2))
print(json.dumps({'ok':True,'species':13,'frames':sum(s['frontFrames']+s['backFrames'] for s in stats),'preservedPixels':sum(s['preservedPixels'] for s in stats)}))

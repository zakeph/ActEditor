using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GRF.FileFormats.ActFormat;
using GRF.FileFormats.SprFormat;
using GRF.FileFormats.PalFormat;
using GRF.Image;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Brushes = System.Drawing.Brushes;
using ActAction = GRF.FileFormats.ActFormat.Action;

// One-off artifact generation, separate from the application's product code.
public static class SprigatitoTrial {
    sealed class SourceFrame { public byte[] Pixels; public int Width, Height, Delay; public SpriteIndex Index; }
    sealed class View { public List<SourceFrame> Frames = new List<SourceFrame>(); public int Bottom, OffsetY; }
    static void Require(bool ok, string message) { if (!ok) throw new InvalidDataException(message); }
    static View ReadGif(string path) {
        var view = new View();
        using (var gif = Image.FromFile(path)) {
            int count = gif.GetFrameCount(FrameDimension.Time);
            var delays = gif.GetPropertyItem(0x5100).Value;
            for (int f = 0; f < count; f++) {
                gif.SelectActiveFrame(FrameDimension.Time, f);
                using (var bitmap = new Bitmap(gif.Width, gif.Height, PixelFormat.Format32bppArgb)) {
                    using (var g = Graphics.FromImage(bitmap)) { g.Clear(System.Drawing.Color.Transparent); g.DrawImageUnscaled(gif, 0, 0); }
                    var bits = bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    byte[] pixels = new byte[bitmap.Width * bitmap.Height * 4];
                    for (int y=0;y<bitmap.Height;y++) Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),pixels,y*bitmap.Width*4,bitmap.Width*4);
                    bitmap.UnlockBits(bits);
                    for (int p=0;p<pixels.Length;p+=4) {
                        if (pixels[p+3]==0) pixels[p]=pixels[p+1]=pixels[p+2]=0;
                        else view.Bottom=Math.Max(view.Bottom,p/4/bitmap.Width);
                    }
                    int delay=BitConverter.ToInt32(delays,f*4)*10;
                    Require(delay>0,"GIF frame has no positive duration");
                    view.Frames.Add(new SourceFrame { Pixels=pixels, Width=bitmap.Width, Height=bitmap.Height, Delay=delay });
                }
            }
        }
        // Keep the source canvas stable across all poses.
        view.OffsetY=view.Frames[0].Height/2-(view.Bottom+1);
        return view;
    }
    static int Gcd(int a,int b) { while(b!=0) { int next=a%b; a=b; b=next; } return a; }
    static int Tick(View v) { return v.Frames.Select(f=>f.Delay).Aggregate(Gcd); }
    static string Hash(byte[] bytes) { using(var sha=SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(bytes)); }
    static Bitmap ToBitmap(GrfImage original) {
        var image=original.Copy(); image.Convert(GrfImageType.Bgra32);
        var bmp=new Bitmap(image.Width,image.Height,PixelFormat.Format32bppArgb);
        var bits=bmp.LockBits(new Rectangle(0,0,bmp.Width,bmp.Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
        for(int y=0;y<bmp.Height;y++) Marshal.Copy(image.Pixels,y*bmp.Width*4,IntPtr.Add(bits.Scan0,y*bits.Stride),bmp.Width*4);
        bmp.UnlockBits(bits); return bmp;
    }
    static bool IsFront(int d) { return d==0||d==1||d==6||d==7; }
    static void NativePng(Act act,int direction,string path) {
        var image=ActImaging.Imaging.GenerateImage(act,direction,0,BitmapScalingMode.NearestNeighbor);
        Require(image!=null,"Native renderer returned no image");
        var bitmap=ActImaging.Imaging.ForceRender(image,BitmapScalingMode.NearestNeighbor);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(bitmap);
        using(var stream=File.Create(path)) encoder.Save(stream);
    }
    public static void Run(string frontPath,string backPath,string templateAct,string templateSpr,string output) {
        Directory.CreateDirectory(output);
        string actPath=Path.Combine(output,"sprigatito.act"),sprPath=Path.Combine(output,"sprigatito.spr");
        Require(!File.Exists(actPath)&&!File.Exists(sprPath),"Output pair already exists; use a new output directory");
        var template=new Act(File.ReadAllBytes(templateAct),File.ReadAllBytes(templateSpr));
        Require(template.NumberOfActions==40,"Unexpected template action count");
        var front=ReadGif(frontPath); var back=ReadGif(backPath);
        var frames=front.Frames.Concat(back.Frames).ToList();
        var colors=new Dictionary<int,byte>();
        foreach(var f in frames) for(int p=0;p<f.Pixels.Length;p+=4) {
            if(f.Pixels[p+3]==0) continue;
            int color=(f.Pixels[p+2]<<16)|(f.Pixels[p+1]<<8)|f.Pixels[p];
            if(!colors.ContainsKey(color)) {
                Require(colors.Count<255,"More than 255 opaque colors; trial refuses lossy quantization");
                colors.Add(color,(byte)(colors.Count+1));
            }
        }
        byte[] palette=new byte[1024]; palette[0]=255;palette[2]=255;
        foreach(var entry in colors) { int p=entry.Value*4;palette[p]=(byte)(entry.Key>>16);palette[p+1]=(byte)(entry.Key>>8);palette[p+2]=(byte)entry.Key;palette[p+3]=255; }
        var spr=new Spr();spr.Palette=new Pal();spr.Palette.SetPalette(palette);
        var unique=new Dictionary<string,SpriteIndex>();
        foreach(var f in frames) {
            string hash=f.Width+"x"+f.Height+":"+Hash(f.Pixels); SpriteIndex index;
            if(!unique.TryGetValue(hash,out index)) {
                var indexed=new byte[f.Width*f.Height];
                for(int p=0;p<indexed.Length;p++) if(f.Pixels[p*4+3]!=0) {
                    int color=(f.Pixels[p*4+2]<<16)|(f.Pixels[p*4+1]<<8)|f.Pixels[p*4];
                    indexed[p]=colors[color];
                }
                index=spr.InsertAny(new GrfImage(indexed,f.Width,f.Height,GrfImageType.Indexed8,palette));
                unique.Add(hash,index);
            }
            f.Index=index;
        }
        var act=new Act(spr);act.Actions.Clear();
        for(int a=0;a<40;a++) {
            var view=IsFront(a%8)?front:back;int tick=Tick(view);
            var action=new ActAction();action.AnimationSpeed=tick/25f;
            foreach(var f in view.Frames) for(int repeat=0;repeat<f.Delay/tick;repeat++) {
                var frame=new Frame();
                frame.Layers.Add(new Layer(f.Index,spr.GetImage(f.Index)) {OffsetX=0,OffsetY=view.OffsetY,Mirror=template[a,0,0].Mirror});
                action.Frames.Add(frame);
            }
            act.Actions.Add(action);
        }
        spr.Save(sprPath);act.Save(actPath);
        var saved=new Act(File.ReadAllBytes(actPath),File.ReadAllBytes(sprPath));
        Require(saved.NumberOfActions==40,"ACT lost directional slots");
        int checkedFrames=0,nativeRenders=0;
        bool[] mirrors={false,false,true,true,false,false,true,true};
        for(int a=0;a<40;a++) {
            var source=IsFront(a%8)?front:back;int tick=Tick(source),fi=0;
            Require(Math.Abs(saved[a].AnimationSpeed*25-tick)<0.001,"GIF timing changed");
            Require(saved[a].Frames.Count==source.Frames.Sum(f=>f.Delay/tick),"Timeline length changed");
            foreach(var original in source.Frames) for(int repeat=0;repeat<original.Delay/tick;repeat++) {
                var frame=saved[a,fi];Require(frame.Layers.Count==1,"Unexpected layer count");
                var layer=frame.Layers[0];Require(layer.Mirror==mirrors[a%8],"Wrong directional reflection");
                Require(layer.OffsetX==0&&layer.OffsetY==source.OffsetY,"Frame alignment changed");
                var decoded=saved.Sprite.GetImage(layer).Copy();decoded.Convert(GrfImageType.Bgra32);
                Require(decoded.Width==original.Width&&decoded.Height==original.Height,"Sprite canvas changed");
                Require(decoded.Pixels.Length==original.Pixels.Length,"Pixel buffer size changed");
                for(int p=0;p<decoded.Pixels.Length;p+=4) {
                    Require(decoded.Pixels[p+3]==original.Pixels[p+3],"Transparency changed");
                    if(original.Pixels[p+3]!=0) for(int c=0;c<3;c++) Require(decoded.Pixels[p+c]==original.Pixels[p+c],"Opaque color changed");
                }
                if(a<8) { var rendered=ActImaging.Imaging.GenerateImage(saved,a,fi,BitmapScalingMode.NearestNeighbor);Require(rendered!=null,"Native renderer failed");nativeRenders++; }
                fi++;checkedFrames++;
            }
        }
        NativePng(saved,0,Path.Combine(output,"renderer-front.png"));
        NativePng(saved,4,Path.Combine(output,"renderer-back.png"));
        using(var sheet=new Bitmap(768,430)) using(var g=Graphics.FromImage(sheet)) using(var font=new Font("Arial",10)) {
            g.Clear(System.Drawing.Color.FromArgb(235,239,243));g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
            for(int d=0;d<8;d++) {
                int x=d%4*192,y=d/4*215;var layer=saved[d,0].Layers[0];
                using(var bmp=ToBitmap(saved.Sprite.GetImage(layer))) {
                    if(layer.Mirror) bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    g.DrawLine(Pens.LightSlateGray,x+12,y+187,x+180,y+187);
                    g.DrawImage(bmp,new Rectangle(x+96+3*(layer.OffsetX-bmp.Width/2),y+187+3*(layer.OffsetY-bmp.Height/2),bmp.Width*3,bmp.Height*3));
                }
                g.DrawString("Position "+d+" / "+(IsFront(d)?"front":"back")+(layer.Mirror?" / mirror":""),font,Brushes.Black,x+6,y+194);
            }
            sheet.Save(Path.Combine(output,"directions.png"),ImageFormat.Png);
        }
        var pngs=new Dictionary<int,string>();
        for(int i=0;i<saved.Sprite.Images.Count;i++) using(var bmp=ToBitmap(saved.Sprite.GetImage(i))) using(var stream=new MemoryStream()) {
            bmp.Save(stream,ImageFormat.Png);pngs.Add(i,"data:image/png;base64,"+Convert.ToBase64String(stream.ToArray()));
        }
        var json=new StringBuilder("[");
        for(int d=0;d<8;d++) {
            if(d>0)json.Append(',');json.Append('[');
            for(int f=0;f<saved[d].Frames.Count;f++) {
                if(f>0)json.Append(',');var layer=saved[d,f,0];
                json.Append("{\"src\":\"").Append(pngs[layer.SpriteIndex]).Append("\",\"mirror\":").Append(layer.Mirror?"true":"false").Append(",\"y\":").Append(layer.OffsetY).Append(",\"ms\":").Append((int)Math.Round(saved[d].AnimationSpeed*25)).Append('}');
            }
            json.Append(']');
        }
        json.Append(']');
        string html=@"<!doctype html><html lang='es'><meta charset='utf-8'><title>Sprigatito · prueba ACT/SPR</title>
<style>body{font:16px system-ui;background:#edf1f5;color:#172331;margin:32px}h1{font-size:24px}main{display:grid;grid-template-columns:repeat(4,192px);gap:12px}figure{margin:0;background:#fff;border:1px solid #cbd5e1;border-radius:12px;overflow:hidden}canvas{display:block;image-rendering:pixelated}figcaption{padding:10px;font-size:13px}button{margin-bottom:20px;padding:8px 16px;cursor:pointer}@media(max-width:850px){main{grid-template-columns:repeat(2,192px)}}</style>
<h1>Sprigatito · prueba ACT/SPR</h1><p>Delante: 80 ms · Detrás: 90 ms · Tamaño: 3×</p><button>Pausar</button><main></main><script>const data="+json+@";let running=true,elapsed=0,last=performance.now();
const views=data.map((frames,d)=>{const figure=document.createElement('figure'),canvas=document.createElement('canvas'),caption=document.createElement('figcaption');canvas.width=192;canvas.height=194;caption.textContent='Posición '+d+' · '+([0,1,6,7].includes(d)?'delantera':'trasera')+(frames[0].mirror?' · reflejada':'');figure.append(canvas,caption);document.querySelector('main').append(figure);frames.forEach(f=>{f.img=new Image();f.img.src=f.src});return{frames,ctx:canvas.getContext('2d'),duration:frames.reduce((s,f)=>s+f.ms,0)}});
document.querySelector('button').onclick=()=>{running=!running;document.querySelector('button').textContent=running?'Pausar':'Reanudar'};
function draw(now){if(running)elapsed+=now-last;last=now;views.forEach(v=>{let t=elapsed%v.duration,f=v.frames[0];for(const candidate of v.frames){f=candidate;if(t<candidate.ms)break;t-=candidate.ms}const c=v.ctx;c.clearRect(0,0,192,194);c.strokeStyle='#cbd5e1';c.beginPath();c.moveTo(12,187);c.lineTo(180,187);c.stroke();if(!f.img.complete||!f.img.naturalWidth)return;c.save();c.translate(96,187+3*f.y);if(f.mirror)c.scale(-1,1);c.imageSmoothingEnabled=false;c.drawImage(f.img,-f.img.width*1.5,-f.img.height*1.5,f.img.width*3,f.img.height*3);c.restore()});requestAnimationFrame(draw)}requestAnimationFrame(draw);</script></html>";
        File.WriteAllText(Path.Combine(output,"preview.html"),html,new UTF8Encoding(false));
        string report="Sprigatito ACT/SPR trial\nFront: "+front.Frames.Count+" frames / "+front.Frames.Sum(f=>f.Delay)+" ms / offset Y "+front.OffsetY+"\nBack: "+back.Frames.Count+" frames / "+back.Frames.Sum(f=>f.Delay)+" ms / offset Y "+back.OffsetY+"\nExact opaque colors: "+colors.Count+"\nUnique indexed sprites: "+unique.Count+"\nACT actions: "+saved.NumberOfActions+"\nRound-trip frames checked: "+checkedFrames+"\nNative ACT renderer frames checked: "+nativeRenders+"\nPASS: opaque colors, alpha, timing, mirrors, alignment, slots and native rendering.\n";
        File.WriteAllText(Path.Combine(output,"verification.txt"),report);Console.WriteLine(report);
    }
}


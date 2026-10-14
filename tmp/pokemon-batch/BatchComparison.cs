using System;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using GRF.FileFormats.ActFormat;
using GRF.Image;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

public static class BatchComparison {
    static BatchConfig Config;
    static void Require(bool condition,string message) {if(!condition) throw new InvalidDataException(message);}
    static Act Open(string folder) {
        return new Act(File.ReadAllBytes(Path.Combine(folder,Config.name+".act")),File.ReadAllBytes(Path.Combine(folder,Config.name+".spr")));
    }
    static Bitmap BitmapOf(GrfImage source) {
        var image=source.Copy();image.Convert(GrfImageType.Bgra32);
        var result=new Bitmap(image.Width,image.Height,PixelFormat.Format32bppArgb);
        var bits=result.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
        for(int y=0;y<image.Height;y++) Marshal.Copy(image.Pixels,y*image.Width*4,IntPtr.Add(bits.Scan0,y*bits.Stride),image.Width*4);
        result.UnlockBits(bits);return result;
    }
    static string Json(Act act) {
        var json=new StringBuilder("{\"images\":[");
        for(int i=0;i<act.Sprite.Images.Count;i++) {
            if(i>0) json.Append(',');
            using(var bitmap=BitmapOf(act.Sprite.GetImage(i))) using(var stream=new MemoryStream()) {
                bitmap.Save(stream,ImageFormat.Png);
                json.Append("\"data:image/png;base64,").Append(Convert.ToBase64String(stream.ToArray())).Append('"');
            }
        }
        json.Append("],\"directions\":[");
        for(int d=0;d<8;d++) {
            if(d>0) json.Append(',');
            json.Append("{\"ms\":").Append((int)Math.Round(act[d].AnimationSpeed*25)).Append(",\"frames\":[");
            for(int f=0;f<act[d].Frames.Count;f++) {
                if(f>0)json.Append(',');var layer=act[d,f,0];
                json.Append('[').Append(layer.SpriteIndex).Append(',').Append(layer.Mirror?"true":"false").Append(',').Append(layer.OffsetX).Append(',').Append(layer.OffsetY).Append(']');
            }
            json.Append("]}");
        }
        return json.Append("]}").ToString();
    }
    public static void Run(string folder,string configPath) {
        Config=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<BatchConfig>(File.ReadAllText(configPath));
        var original=Open(Path.Combine(folder,"original"));var adjusted=Open(folder);
        long differences=0;int compared=0;
        Require(original.NumberOfActions==adjusted.NumberOfActions,"Actions differ");
        for(int a=0;a<original.NumberOfActions;a++) {
            Require(original[a].Frames.Count==adjusted[a].Frames.Count&&original[a].AnimationSpeed==adjusted[a].AnimationSpeed,"Timing differs");
            for(int f=0;f<original[a].Frames.Count;f++) {
                var beforeLayer=original[a,f,0];var afterLayer=adjusted[a,f,0];
                Require(beforeLayer.OffsetX==afterLayer.OffsetX&&beforeLayer.OffsetY==afterLayer.OffsetY&&beforeLayer.Mirror==afterLayer.Mirror,"Alignment or mirror differs");
                var before=original.Sprite.GetImage(beforeLayer).Copy();before.Convert(GrfImageType.Bgra32);
                var after=adjusted.Sprite.GetImage(afterLayer).Copy();after.Convert(GrfImageType.Bgra32);
                Require(before.Width==after.Width&&before.Height==after.Height,"Dimensions differ");
                int remainingBlack=0;
                for(int p=0;p<before.Pixels.Length;p+=4) {
                    if(MaskRecolor.Ink(after.Pixels,p)) remainingBlack++;
                    Require(before.Pixels[p+3]==after.Pixels[p+3],"Alpha differs");
                    if(before.Pixels[p+3]==0) continue;
                    if(before.Pixels[p]==after.Pixels[p]&&before.Pixels[p+1]==after.Pixels[p+1]&&before.Pixels[p+2]==after.Pixels[p+2])continue;
                    Require(MaskRecolor.Ink(before.Pixels,p),"Modified a non-black pixel");
                    Require(after.Pixels[p]==Config.color[2]&&after.Pixels[p+1]==Config.color[1]&&after.Pixels[p+2]==Config.color[0],"Wrong outline color");differences++;
                }
                bool frontDirection=a%8==0||a%8==1||a%8==6||a%8==7;
                Require((frontDirection?Config.front:Config.back).frames.Any(m=>m.Length==remainingBlack),"Unexpected pupil count");
                compared++;
            }
        }
        

        var frontSize=original.Sprite.GetImage(original[0,0,0]);var backSize=original.Sprite.GetImage(original[4,0,0]);
        int cellWidth=Math.Max(240,Math.Max(frontSize.Width,backSize.Width)*3+24);
        int cellHeight=Math.Max(220,Math.Max(frontSize.Height,backSize.Height)*3+65);
        using(var sheet=new Bitmap(cellWidth*2,cellHeight*2)) using(var g=Graphics.FromImage(sheet)) using(var font=new Font("Segoe UI",14)) {
            g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
            for(int row=0;row<2;row++) for(int col=0;col<2;col++) {
                int x=col*cellWidth,y=row*cellHeight;
                for(int cy=0;cy<cellHeight;cy+=16)for(int cx=0;cx<cellWidth;cx+=16) {
                    using(var brush=new SolidBrush((cx/16+cy/16)%2==0?Color.FromArgb(238,238,238):Color.White))g.FillRectangle(brush,x+cx,y+cy,Math.Min(16,cellWidth-cx),Math.Min(16,cellHeight-cy));
                }
                var act=col==0?original:adjusted;int d=row==0?0:4;var layer=act[d,0,0];
                using(var bitmap=BitmapOf(act.Sprite.GetImage(layer))) {
                    if(layer.Mirror)bitmap.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    g.DrawImage(bitmap,new Rectangle(x+cellWidth/2+3*(layer.OffsetX-bitmap.Width/2),y+cellHeight-15+3*(layer.OffsetY-bitmap.Height/2),bitmap.Width*3,bitmap.Height*3));
                }
                g.FillRectangle(Brushes.White,x,y,cellWidth,38);
                g.DrawString((row==0?"Delantera":"Trasera")+" · "+(col==0?"Original":"Ajustado"),font,Brushes.Black,x+12,y+7);
            }
            sheet.Save(Path.Combine(folder,"comparacion.png"),ImageFormat.Png);
        }
        string html=@"<!doctype html><html lang='es'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Dragonite · comparación del borde</title>
<style>*{box-sizing:border-box}body{margin:24px;font:16px system-ui;color:#243044;background:#edf1f5}h1{font-size:25px;margin-bottom:8px}p{max-width:850px;line-height:1.5}header{max-width:900px}nav{display:flex;gap:16px;align-items:center;flex-wrap:wrap;margin:20px 0}button,select{font:inherit;padding:7px 12px}main{display:grid;grid-template-columns:repeat(2,minmax(300px,390px));gap:16px}figure{margin:0;border:1px solid #cbd5e1;border-radius:12px;overflow:hidden;background:#fff}figcaption{padding:12px;font-weight:600}canvas{display:block;max-width:100%;height:auto;image-rendering:pixelated;background-color:var(--base,#fff);background-image:linear-gradient(45deg,var(--tile,#eee) 25%,transparent 25%),linear-gradient(-45deg,var(--tile,#eee) 25%,transparent 25%),linear-gradient(45deg,transparent 75%,var(--tile,#eee) 75%),linear-gradient(-45deg,transparent 75%,var(--tile,#eee) 75%);background-size:32px 32px;background-position:0 0,0 16px,16px -16px,-16px 0}footer{margin:20px 0;font-size:14px}@media(max-width:680px){main{grid-template-columns:1fr}body{margin:12px}}</style>
<header><h1>Dragonite · ajuste del borde</h1><p>Negro exterior → <strong>RGB 90, 58, 33</strong>. Se recolorea el negro y se conservan las pupilas con máscaras revisadas en ambas vistas. Se mantienen la transparencia y los tiempos del GIF. La comparación muestra las imágenes de los SPR y la animación de los ACT guardados.</p></header>
<nav><button id='pause'>Pausar</button><label>Zoom <select id='zoom'><option value='2'>2×</option><option value='4' selected>4×</option><option value='6'>6×</option></select></label><label>Fondo <select id='background'><option value='light'>Claro</option><option value='dark'>Oscuro</option></select></label><label>Posiciones <select id='directions'><option value='0,4'>Delantera y trasera</option><option value='1,5'>Posiciones 1 y 5</option><option value='6,2'>Reflejadas 6 y 2</option><option value='7,3'>Reflejadas 7 y 3</option></select></label></nav><main></main>
<footer><a href='dragonite.act'>dragonite.act</a> + <a href='dragonite.spr'>dragonite.spr</a> · Mantén ambos archivos juntos y abre el ACT en Act Editor. <a href='preview.html'>Ver las ocho posiciones ajustadas</a>.</footer>
<script>const variants=["+Json(original)+","+Json(adjusted)+@"];let running=true,elapsed=0,last=performance.now(),zoom=4;const main=document.querySelector('main');
variants.forEach(v=>v.images=v.images.map(src=>{const image=new Image();image.src=src;return image}));
const cards=[];for(let row=0;row<2;row++)for(let variant=0;variant<2;variant++){const figure=document.createElement('figure'),caption=document.createElement('figcaption'),canvas=document.createElement('canvas');figure.append(caption,canvas);main.append(figure);cards.push({row,variant,caption,canvas,ctx:canvas.getContext('2d')})}
function configure(){zoom=Number(document.querySelector('#zoom').value);const dirs=document.querySelector('#directions').value.split(',').map(Number);cards.forEach(card=>{card.direction=dirs[card.row];card.caption.textContent=(card.row?'Trasera':'Delantera')+' · '+(card.variant?'Borde marrón':'Original')+' · posición '+card.direction;card.canvas.width=Math.max(...variants.flatMap(v=>v.images.map(i=>i.naturalWidth||1)))*zoom+40;card.canvas.height=Math.max(...variants.flatMap(v=>v.images.map(i=>i.naturalHeight||1)))*zoom+60})}
document.querySelector('#pause').onclick=()=>{running=!running;document.querySelector('#pause').textContent=running?'Pausar':'Reanudar'};document.querySelector('#zoom').onchange=configure;document.querySelector('#directions').onchange=configure;document.querySelector('#background').onchange=e=>{document.body.style.setProperty('--base',e.target.value==='dark'?'#334155':'#fff');document.body.style.setProperty('--tile',e.target.value==='dark'?'#283446':'#eee')};
function draw(now){if(running)elapsed+=now-last;last=now;cards.forEach(card=>{const v=variants[card.variant],action=v.directions[card.direction],frame=action.frames[Math.floor(elapsed/action.ms)%action.frames.length],img=v.images[frame[0]],c=card.ctx;c.clearRect(0,0,card.canvas.width,card.canvas.height);if(!img.complete||!img.naturalWidth)return;c.save();c.translate(card.canvas.width/2+frame[2]*zoom,card.canvas.height-20+frame[3]*zoom);if(frame[1])c.scale(-1,1);c.imageSmoothingEnabled=false;c.drawImage(img,-Math.floor(img.width/2)*zoom,-Math.floor(img.height/2)*zoom,img.width*zoom,img.height*zoom);c.restore()});requestAnimationFrame(draw)}Promise.all(variants.flatMap(v=>v.images.map(img=>img.decode()))).then(()=>{configure();requestAnimationFrame(draw)});</script></html>";
        html=html.Replace("Dragonite",Config.name).Replace("dragonite.act",Config.name+".act").Replace("dragonite.spr",Config.name+".spr").Replace("RGB 90, 58, 33","RGB "+string.Join(", ",Config.color)).Replace("Negro exterior", "Negro salvo pupilas").Replace("Borde marrón","Borde ajustado").Replace("<a href='preview.html'>Ver las ocho posiciones ajustadas</a>.","");
        File.WriteAllText(Path.Combine(folder,"comparacion.html"),html,new UTF8Encoding(false));
        string report="PASS: "+compared+" frames compared between saved original and adjusted ACT/SPR. Only source neutral ink -> chosen RGB; protected pupils retained; alpha, sizes, timing, alignment and mirrors unchanged. Changed pixel occurrences in ACT: "+differences+".\n";
        File.AppendAllText(Path.Combine(folder,"verification.txt"),report);Console.WriteLine(report);
    }
}




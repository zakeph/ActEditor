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
public class BatchView {public int width;public int height;public int[][] frames;}
public class BatchConfig {public string name;public int[] color;public BatchView front;public BatchView back;public string notes;public bool reviewed;}
public static class PokemonBatch {
    static BatchConfig Config;
    static bool AdjustOutline;
    static long ChangedPixels;
    static long PreservedBlackPixels;
    sealed class SourceFrame { public byte[] Pixels; public int Width, Height, Delay; public SpriteIndex Index; }
    sealed class View { public List<SourceFrame> Frames = new List<SourceFrame>(); public int Bottom, OffsetY; }
    static void Require(bool ok, string message) { if (!ok) throw new InvalidDataException(message); }
    static View ReadGif(string path,bool frontView) {
        var view = new View();
        var maskView=frontView?Config.front:Config.back;
        using (var gif = Image.FromFile(path)) {
            int count = gif.GetFrameCount(FrameDimension.Time);
            var delays = gif.GetPropertyItem(0x5100).Value;
            Require(gif.Width==maskView.width&&gif.Height==maskView.height&&count==maskView.frames.Length,"Mask/source dimensions or frame count differ");
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
                    
                    if(AdjustOutline) {
                        var before=(byte[])pixels.Clone();
                        ChangedPixels+=MaskRecolor.Apply(pixels,maskView.frames[f],Config.color);
                        for(int p=0;p<pixels.Length;p+=4) {
                            Require(pixels[p+3]==before[p+3],"Outline changed alpha");
                            bool different=pixels[p]!=before[p]||pixels[p+1]!=before[p+1]||pixels[p+2]!=before[p+2];
                            if(different) {
                                Require(MaskRecolor.Ink(before,p),"Outline changed a non-black pixel");
                                Require(pixels[p]==Config.color[2]&&pixels[p+1]==Config.color[1]&&pixels[p+2]==Config.color[0],"Unexpected outline color");
                            }
                            if(MaskRecolor.Ink(pixels,p)) PreservedBlackPixels++;
                        }
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
    public static void Run(string frontPath,string backPath,string templateAct,string templateSpr,string output,bool adjustOutline,string configPath) {
        Config=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<BatchConfig>(File.ReadAllText(configPath));
        Require(Config.reviewed&&Config.color.Length==3&&Config.color.Max()>16,"Config not reviewed or outline color remains ink");
        AdjustOutline=adjustOutline;ChangedPixels=0;PreservedBlackPixels=0;
        Directory.CreateDirectory(output);
        string actPath=Path.Combine(output,Config.name+".act"),sprPath=Path.Combine(output,Config.name+".spr");
        Require(!File.Exists(actPath)&&!File.Exists(sprPath),"Output pair already exists; use a new output directory");
        var template=new Act(File.ReadAllBytes(templateAct),File.ReadAllBytes(templateSpr));
        Require(template.NumberOfActions==40,"Unexpected template action count");
        var front=ReadGif(frontPath,true); var back=ReadGif(backPath,false);
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
            var source=IsFront(a%8)?front:back;int tick=Tick(source),fi=0; var renderedSprites=new HashSet<int>();
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
                if(a<8 && renderedSprites.Add(layer.SpriteIndex)) { var rendered=ActImaging.Imaging.GenerateImage(saved,a,fi,BitmapScalingMode.NearestNeighbor);Require(rendered!=null,"Native renderer failed");nativeRenders++; }
                fi++;checkedFrames++;
            }
        }
        NativePng(saved,0,Path.Combine(output,"renderer-front.png"));
        NativePng(saved,4,Path.Combine(output,"renderer-back.png"));
        string report=Config.name+" ACT/SPR\nFront: "+front.Frames.Count+" frames / "+front.Frames.Sum(f=>f.Delay)+" ms / offset Y "+front.OffsetY+"\nBack: "+back.Frames.Count+" frames / "+back.Frames.Sum(f=>f.Delay)+" ms / offset Y "+back.OffsetY+"\nExact opaque colors: "+colors.Count+"\nUnique indexed sprites: "+unique.Count+"\nACT actions: "+saved.NumberOfActions+"\nRound-trip frames checked: "+checkedFrames+"\nNative ACT renderer distinct poses per direction checked: "+nativeRenders+"\nPASS: opaque colors, alpha, timing, mirrors, alignment, slots and native rendering.\n";
        report+="Outline adjusted: "+AdjustOutline+" / RGB "+string.Join(",",Config.color)+" / changed ink pixels across GIF frames: "+ChangedPixels+" / pupil black pixels retained: "+PreservedBlackPixels+"\n";
        if(AdjustOutline) Require(PreservedBlackPixels==Config.front.frames.Sum(m=>m.Length)+Config.back.frames.Sum(m=>m.Length),"Remaining ink differs from protected pupil masks");
        File.WriteAllText(Path.Combine(output,"verification.txt"),report);Console.WriteLine(report);
    }
}






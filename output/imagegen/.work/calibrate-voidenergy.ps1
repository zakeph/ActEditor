param(
    [Parameter(Mandatory=$true)][string]$Generated,
    [Parameter(Mandatory=$true)][int]$Frame,
    [string]$OutputDir='D:\xampp\htdocs\ActEditor\output\imagegen\voidenergy-cyan-fire-v2\png'
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public static class SoftSprite {
    public static Rectangle Bounds(Bitmap b, int minimumAlpha=1) {
        int l=b.Width,t=b.Height,r=-1,d=-1;
        for(int y=0;y<b.Height;y++) for(int x=0;x<b.Width;x++) if(b.GetPixel(x,y).A>=minimumAlpha) {
            l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);d=Math.Max(d,y);
        }
        return Rectangle.FromLTRB(l,t,r+1,d+1);
    }
    static Bitmap Render(Bitmap b, RectangleF src, Rectangle dst, int w, int h) {
        Bitmap result=new Bitmap(w,h,PixelFormat.Format32bppArgb);
        using(Graphics g=Graphics.FromImage(result)) using(ImageAttributes a=new ImageAttributes()) {
            g.Clear(Color.Transparent);g.CompositingMode=CompositingMode.SourceCopy;
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            g.CompositingQuality=CompositingQuality.HighQuality;g.PixelOffsetMode=PixelOffsetMode.Half;
            g.SetClip(dst);a.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(b,dst,src.X,src.Y,src.Width,src.Height,GraphicsUnit.Pixel,a);
        }
        return result;
    }
    static double[,,] Read(Bitmap b) {
        double[,,] p=new double[b.Height,b.Width,4];
        for(int y=0;y<b.Height;y++) for(int x=0;x<b.Width;x++) {
            Color c=b.GetPixel(x,y);double a=c.A/255.0;
            p[y,x,0]=c.A;p[y,x,1]=c.R*a;p[y,x,2]=c.G*a;p[y,x,3]=c.B*a;
        }
        return p;
    }
    static double[,,] Blur(double[,,] p, double sigma, Rectangle limit) {
        int h=p.GetLength(0),w=p.GetLength(1),radius=(int)Math.Ceiling(sigma*3);
        double[] k=new double[2*radius+1];double sum=0;
        for(int i=-radius;i<=radius;i++){k[i+radius]=Math.Exp(-i*i/(2*sigma*sigma));sum+=k[i+radius];}
        for(int i=0;i<k.Length;i++)k[i]/=sum;
        double[,,] mid=new double[h,w,4],r=new double[h,w,4];
        for(int y=limit.Top;y<limit.Bottom;y++)for(int x=limit.Left;x<limit.Right;x++)for(int c=0;c<4;c++) {
            for(int d=-radius;d<=radius;d++){int xx=x+d;if(xx>=0&&xx<w)mid[y,x,c]+=p[y,xx,c]*k[d+radius];}
        }
        for(int y=limit.Top;y<limit.Bottom;y++)for(int x=limit.Left;x<limit.Right;x++)for(int c=0;c<4;c++) {
            for(int d=-radius;d<=radius;d++){int yy=y+d;if(yy>=0&&yy<h)r[y,x,c]+=mid[yy,x,c]*k[d+radius];}
        }
        return r;
    }
    public static double[] Stats(Bitmap b) {
        List<int> a=new List<int>();double sum=0,weight=0,red=0,green=0,blue=0,near=0;
        for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++) {
            Color c=b.GetPixel(x,y);if(c.A==0)continue;a.Add(c.A);sum+=c.A;
            if(c.A>=240)near++;weight+=c.A;red+=c.R*c.A;green+=c.G*c.A;blue+=c.B*c.A;
        }
        a.Sort();return new double[]{sum/a.Count,a[a.Count/2],a[(int)(a.Count*.9)],100*near/a.Count,red/weight,green/weight,blue/weight,a.Count};
    }
    public static void Calibrate(string sourcePath,string generatedPath,string outputPath) {
        using(Bitmap original=new Bitmap(sourcePath))using(Bitmap raw=new Bitmap(generatedPath)) {
            Rectangle dst=Bounds(original);RectangleF src=Bounds(raw,16);
            Bitmap rendered=Render(raw,src,dst,original.Width,original.Height);
            // Correct placement by resampling directly from the high-resolution image.
            // Do not enlarge the already-reduced raster with nearest-neighbour sampling.
            Rectangle actual=Bounds(rendered,8);
            if(actual!=dst) {
                float sx=src.Width/dst.Width,sy=src.Height/dst.Height;
                RectangleF adjusted=new RectangleF(src.X+(actual.X-dst.X)*sx,src.Y+(actual.Y-dst.Y)*sy,actual.Width*sx,actual.Height*sy);
                rendered.Dispose();rendered=Render(raw,adjusted,dst,original.Width,original.Height);
            }
            using(rendered) {
                double[,,] p=Blur(Read(rendered),.7,dst);
                List<int> reference=new List<int>();
                for(int y=0;y<original.Height;y++)for(int x=0;x<original.Width;x++) {
                    int a=original.GetPixel(x,y).A;if(a>=8)reference.Add(a);
                }
                reference.Sort();
                var ranks=new List<Tuple<double,int,int>>();int faintPixels=0;
                for(int y=dst.Top;y<dst.Bottom;y++)for(int x=dst.Left;x<dst.Right;x++) {
                    double a=p[y,x,0];if(a<8){if(a>=.5)faintPixels++;continue;}
                    double green=p[y,x,2]*255/a,blue=p[y,x,3]*255/a;
                    // Smooth blue folds carry more opacity; cyan wisps remain translucent.
                    double score=a*(.7+.3*Math.Max(0,blue-green)/255.0);
                    ranks.Add(Tuple.Create(score,x,y));
                }
                ranks.Sort((a,b)=>a.Item1.CompareTo(b.Item1));
                int start=0;
                while(start<ranks.Count) {
                    int end=start+1;while(end<ranks.Count&&Math.Abs(ranks[end].Item1-ranks[start].Item1)<1e-9)end++;
                    double q=(faintPixels+(start+end)*.5)/(faintPixels+ranks.Count),pos=q*(reference.Count-1);
                    int lo=(int)pos,hi=Math.Min(lo+1,reference.Count-1);
                    double newAlpha=reference[lo]+(reference[hi]-reference[lo])*(pos-lo);
                    for(int i=start;i<end;i++) {
                        int x=ranks[i].Item2,y=ranks[i].Item3;double ratio=newAlpha/p[y,x,0];
                        for(int c=1;c<4;c++)p[y,x,c]*=ratio;p[y,x,0]=newAlpha;
                    }
                    start=end;
                }
                p=Blur(p,.45,dst);
                double[] desired=Stats(original),means=new double[3];double weight=0;
                for(int y=0;y<original.Height;y++)for(int x=0;x<original.Width;x++) {
                    weight+=p[y,x,0];for(int c=0;c<3;c++)means[c]+=p[y,x,c+1]*255;
                }
                for(int c=0;c<3;c++)means[c]/=weight;
                using(Bitmap output=new Bitmap(original.Width,original.Height,PixelFormat.Format32bppArgb)) {
                    for(int y=0;y<original.Height;y++)for(int x=0;x<original.Width;x++) {
                        double a=p[y,x,0];int alpha=(int)Math.Round(Math.Min(255,a));if(alpha==0)continue;
                        int[] rgb=new int[3];
                        for(int c=0;c<3;c++)rgb[c]=(int)Math.Round(Math.Min(255,Math.Max(0,p[y,x,c+1]*255/a*desired[c+4]/Math.Max(.01,means[c]))));
                        output.SetPixel(x,y,Color.FromArgb(alpha,rgb[0],rgb[1],rgb[2]));
                    }
                    output.Save(outputPath,ImageFormat.Png);
                }
            }
        }
    }
}
'@
$name='³²_dv_voidenergy_cyan_{0:0000}.png' -f $Frame
$original=Join-Path 'C:\Users\ShivaN\Desktop\data\sprite\¾Ç¼¼»ç¸®\³²' $name
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$target=Join-Path $OutputDir $name
[SoftSprite]::Calibrate($original,$Generated,$target)
$originalBitmap=[System.Drawing.Bitmap]::new($original)
$targetBitmap=[System.Drawing.Bitmap]::new($target)
try {
    [pscustomobject]@{frame=$Frame;path=$target;width=$targetBitmap.Width;height=$targetBitmap.Height;originalStats=[SoftSprite]::Stats($originalBitmap);correctedStats=[SoftSprite]::Stats($targetBitmap);bounds=[SoftSprite]::Bounds($targetBitmap)} | ConvertTo-Json -Depth 4 -Compress
} finally { $originalBitmap.Dispose();$targetBitmap.Dispose() }

param(
    [double]$Strength=.58,
    [double]$Feather=4.5,
    [string]$OutputDir='D:\xampp\htdocs\ActEditor\output\imagegen\voidenergy-cyan-fire-v3\png'
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
public static class DelicateFire {
    public static double[] Finish(string source,string target,double strength,double feather) {
        using(Bitmap b=new Bitmap(source))using(Bitmap o=new Bitmap(b.Width,b.Height,PixelFormat.Format32bppArgb)) {
            int w=b.Width,h=b.Height;double[,] d=new double[h,w];
            double diagonal=Math.Sqrt(2);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                d[y,x]=b.GetPixel(x,y).A<8?0:Math.Min(Math.Min(x+1,w-x),Math.Min(y+1,h-y));
            for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
                if(x>0)d[y,x]=Math.Min(d[y,x],d[y,x-1]+1);
                if(y>0)d[y,x]=Math.Min(d[y,x],d[y-1,x]+1);
                if(x>0&&y>0)d[y,x]=Math.Min(d[y,x],d[y-1,x-1]+diagonal);
                if(x+1<w&&y>0)d[y,x]=Math.Min(d[y,x],d[y-1,x+1]+diagonal);
            }
            for(int y=h-1;y>=0;y--)for(int x=w-1;x>=0;x--) {
                if(x+1<w)d[y,x]=Math.Min(d[y,x],d[y,x+1]+1);
                if(y+1<h)d[y,x]=Math.Min(d[y,x],d[y+1,x]+1);
                if(x+1<w&&y+1<h)d[y,x]=Math.Min(d[y,x],d[y+1,x+1]+diagonal);
                if(x>0&&y+1<h)d[y,x]=Math.Min(d[y,x],d[y+1,x-1]+diagonal);
            }
            double before=0,after=0,edgeBefore=0,edgeAfter=0,max=0,moved=0,rgbChanges=0;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++) {
                Color c=b.GetPixel(x,y);double t=Math.Min(1,d[y,x]/feather);
                double fade=t*t*(3-2*t);
                if(c.A<8)fade=.035;
                int a=(int)Math.Round(c.A*strength*fade);
                if(c.A>0)a=Math.Max(1,a);
                o.SetPixel(x,y,Color.FromArgb(a,c.R,c.G,c.B));
                before+=c.A;after+=a;max=Math.Max(max,a);
                if(c.A>=8&&d[y,x]<=2){edgeBefore+=c.A;edgeAfter+=a;}
                if(a>c.A)moved++;
                Color check=o.GetPixel(x,y);
                if(check.R!=c.R||check.G!=c.G||check.B!=c.B)rgbChanges++;
            }
            o.Save(target,ImageFormat.Png);
            return new double[]{after/before,edgeAfter/edgeBefore,max,moved,rgbChanges};
        }
    }
}
'@
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$report=@()
for($i=0;$i -lt 7;$i++) {
    $name='³²_dv_voidenergy_cyan_{0:0000}.png' -f $i
    $source=Join-Path 'D:\xampp\htdocs\ActEditor\output\imagegen\voidenergy-cyan-fire-v2\png' $name
    $target=Join-Path $OutputDir $name
    $stats=[DelicateFire]::Finish($source,$target,$Strength,$Feather)
    if($stats[3] -ne 0 -or $stats[4] -ne 0){throw 'Unexpected pixel movement or color change'}
    $report += [pscustomobject]@{file=$name;width=204;height=185;strength=$Strength;featherPixels=$Feather;alphaEnergyRatio=$stats[0];edgeAlphaRatio=$stats[1];maximumAlpha=$stats[2];rgbChanges=$stats[4];addedPixels=$stats[3]}
}
$report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path (Split-Path -Parent $OutputDir) 'verificacion.json') -Encoding UTF8
$report | Format-Table file,alphaEnergyRatio,edgeAlphaRatio,maximumAlpha,rgbChanges -AutoSize

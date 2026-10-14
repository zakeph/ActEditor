param(
    [Parameter(Mandatory=$true)][string]$Generated,
    [Parameter(Mandatory=$true)][int]$Frame,
    [string]$OutputDir = 'D:\xampp\htdocs\ActEditor\output\imagegen\voidenergy-cyan-fire\png'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$drawingReferences = @('System.Drawing')
Add-Type -ReferencedAssemblies $drawingReferences -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public static class SpriteAlignment {
    public static Rectangle Bounds(Bitmap image) {
        int left=image.Width, top=image.Height, right=-1, bottom=-1;
        for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++) {
            if(image.GetPixel(x,y).A == 0) continue;
            left=Math.Min(left,x); top=Math.Min(top,y);
            right=Math.Max(right,x); bottom=Math.Max(bottom,y);
        }
        if(right<0) throw new Exception("Empty image");
        return Rectangle.FromLTRB(left,top,right+1,bottom+1);
    }
    public static void Normalize(string originalPath, string generatedPath, string targetPath) {
        using(Bitmap original=new Bitmap(originalPath))
        using(Bitmap generated=new Bitmap(generatedPath))
        using(Bitmap target=new Bitmap(original.Width,original.Height,PixelFormat.Format32bppArgb)) {
            Rectangle destination=Bounds(original), source=Bounds(generated);
            using(Graphics graphics=Graphics.FromImage(target))
            using(ImageAttributes attributes=new ImageAttributes()) {
                graphics.Clear(Color.Transparent);
                graphics.CompositingMode=CompositingMode.SourceCopy;
                graphics.CompositingQuality=CompositingQuality.HighQuality;
                graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode=PixelOffsetMode.Half;
                graphics.SetClip(destination);
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                graphics.DrawImage(generated,destination,source.X,source.Y,source.Width,source.Height,GraphicsUnit.Pixel,attributes);
            }
            Rectangle actual=Bounds(target);
            if(actual != destination) {
                using(Bitmap aligned=new Bitmap(original.Width,original.Height,PixelFormat.Format32bppArgb))
                using(Graphics graphics=Graphics.FromImage(aligned)) {
                    graphics.Clear(Color.Transparent);
                    graphics.CompositingMode=CompositingMode.SourceCopy;
                    graphics.InterpolationMode=InterpolationMode.NearestNeighbor;
                    graphics.PixelOffsetMode=PixelOffsetMode.Half;
                    graphics.SetClip(destination);
                    graphics.DrawImage(target,destination,actual.X,actual.Y,actual.Width,actual.Height,GraphicsUnit.Pixel);
                    aligned.Save(targetPath,ImageFormat.Png);
                }
            } else target.Save(targetPath,ImageFormat.Png);
        }
    }
}
'@
$fileName = '³²_dv_voidenergy_cyan_{0:0000}.png' -f $Frame
$original = Join-Path 'C:\Users\ShivaN\Desktop\data\sprite\¾Ç¼¼»ç¸®\³²' $fileName
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$target = Join-Path $OutputDir $fileName
[SpriteAlignment]::Normalize($original, $Generated, $target)
$bitmap = [System.Drawing.Bitmap]::new($target)
try {
    $bounds = [SpriteAlignment]::Bounds($bitmap)
    [pscustomobject]@{frame=$Frame;path=$target;width=$bitmap.Width;height=$bitmap.Height;x=$bounds.X;y=$bounds.Y;w=$bounds.Width;h=$bounds.Height} | ConvertTo-Json -Compress
} finally { $bitmap.Dispose() }

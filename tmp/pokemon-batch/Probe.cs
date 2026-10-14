using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

public static class PokemonProbe {
 public static void Run(string input,string output) {
  Directory.CreateDirectory(output);
  foreach(string path in Directory.GetFiles(input,"*.gif").OrderBy(p=>p)) {
   string leaf=Path.GetFileNameWithoutExtension(path);
   if(leaf=="dragonite"||leaf=="dragonite-back")continue;
   string key=leaf.EndsWith("-back")?leaf:leaf+"-front";
   using(var gif=Image.FromFile(path)) {
    int width=gif.Width,height=gif.Height,count=gif.GetFrameCount(FrameDimension.Time);
    var delayBytes=gif.GetPropertyItem(0x5100).Value;
    var palette=new Dictionary<int,long>();var delays=new List<int>();
    int cellW=width*3+12,cellH=height*3+34;
    var samples=Enumerable.Range(0,12).Select(i=>(int)Math.Round(i*(count-1)/11.0)).Distinct().ToArray();
    using(var raw=File.Create(Path.Combine(output,key+".bgra")))
    using(var sheet=new Bitmap(cellW*4,cellH*3))
    using(var sg=Graphics.FromImage(sheet))
    using(var font=new Font("Arial",11)) {
     sg.Clear(Color.FromArgb(224,228,232));sg.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;sg.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
     for(int f=0;f<count;f++) {
      gif.SelectActiveFrame(FrameDimension.Time,f);
      using(var bitmap=new Bitmap(width,height,PixelFormat.Format32bppArgb)) {
       using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);g.DrawImageUnscaled(gif,0,0);}
       var bits=bitmap.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
       var pixels=new byte[width*height*4];
       for(int y=0;y<height;y++)Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),pixels,y*width*4,width*4);
       bitmap.UnlockBits(bits);
       for(int p=0;p<pixels.Length;p+=4) {
        if(pixels[p+3]==0){pixels[p]=pixels[p+1]=pixels[p+2]=0;continue;}
        int c=(pixels[p+2]<<16)|(pixels[p+1]<<8)|pixels[p];if(!palette.ContainsKey(c))palette.Add(c,0);palette[c]++;
       }
       raw.Write(pixels,0,pixels.Length);delays.Add(BitConverter.ToInt32(delayBytes,f*4)*10);
       if(f==0)bitmap.Save(Path.Combine(output,key+".png"),ImageFormat.Png);
       int sample=Array.IndexOf(samples,f);
       if(sample>=0){int x=sample%4*cellW,y=sample/4*cellH;sg.DrawString(key+" / "+f,font,Brushes.Black,x+5,y+4);sg.DrawImage(bitmap,new Rectangle(x+6,y+28,width*3,height*3));}
      }
     }
     sheet.Save(Path.Combine(output,key+"-sheet.png"),ImageFormat.Png);
    }
    string pal=string.Join(",",palette.OrderByDescending(p=>p.Value).Select(p=>"["+(p.Key>>16)+","+((p.Key>>8)&255)+","+(p.Key&255)+","+p.Value+"]"));
    string json="{\"width\":"+width+",\"height\":"+height+",\"count\":"+count+",\"delays\":["+string.Join(",",delays)+"],\"palette\":["+pal+"]}";
    File.WriteAllText(Path.Combine(output,key+".json"),json,Encoding.UTF8);Console.WriteLine(key+" "+width+"x"+height+" frames="+count);
   }
  }
 }
}

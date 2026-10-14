using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

public class RootProbe {public int width;public int height;public int count;}
public class RootMask {public int width;public int height;public int[][] frames;}
public static class RootPlans {
 static readonly string Probe="D:\\xampp\\htdocs\\ActEditor\\tmp\\pokemon-batch\\probe";
 static readonly string Output="D:\\xampp\\htdocs\\ActEditor\\tmp\\pokemon-batch\\eyes-root";
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
 static RootMask Plan(string key,int tx,int ty,int tw,int th,int[] px,int[] py,int minX,int maxX,int minY,int maxY) {
  var meta=Json.Deserialize<RootProbe>(File.ReadAllText(Path.Combine(Probe,key+".json")));
  var raw=File.ReadAllBytes(Path.Combine(Probe,key+".bgra"));int stride=meta.width*meta.height*4;
  var masks=new int[meta.count][];var misses=new List<int>();
  for(int f=0;f<meta.count;f++) {
   if(px.Length==0){masks[f]=new int[0];continue;}
   int matches=0,foundX=0,foundY=0;
   for(int y=minY;y<=maxY&&y+th<=meta.height;y++)for(int x=minX;x<=maxX&&x+tw<=meta.width;x++) {
    bool same=true;
    for(int dy=0;dy<th&&same;dy++)for(int dx=0;dx<tw&&same;dx++) {
     int a=((ty+dy)*meta.width+tx+dx)*4,b=f*stride+((y+dy)*meta.width+x+dx)*4;
     for(int c=0;c<4;c++)if(raw[a+c]!=raw[b+c]){same=false;break;}
    }
    if(same){matches++;foundX=x;foundY=y;}
   }
   if(matches!=1){misses.Add(f);continue;}
   masks[f]=px.Select((x,i)=>(foundY+py[i]-ty)*meta.width+foundX+x-tx).ToArray();
   foreach(int index in masks[f]) {
    int p=f*stride+index*4;
    if(raw[p+3]!=255||raw[p]>16||raw[p]!=raw[p+1]||raw[p+1]!=raw[p+2])throw new Exception(key+" non-ink pupil frame "+f);
   }
  }
  if(misses.Count>0)throw new Exception(key+" unmatched frames: "+string.Join(",",misses));
  Console.WriteLine(key+": "+meta.count+" frames matched, "+px.Length+" pupil pixels/frame");
  return new RootMask{width=meta.width,height=meta.height,frames=masks};
 }
 static RootMask GlaceonBack() {
  string key="glaceon-back";var meta=Json.Deserialize<RootProbe>(File.ReadAllText(Path.Combine(Probe,key+".json")));
  var raw=File.ReadAllBytes(Path.Combine(Probe,key+".bgra"));int stride=meta.width*meta.height*4;
  var masks=new int[meta.count][];var belowColors=new Dictionary<string,int>();
  for(int f=0;f<meta.count;f++) {
   var glints=new List<int>();
   for(int y=5;y<35;y++)for(int x=35;x<60;x++) {
    int index=y*meta.width+x,p=f*stride+index*4;
    if(raw[p+3]==255&&raw[p]==255&&raw[p+1]==255&&raw[p+2]==239)glints.Add(index);
   }
   if(glints.Count==0){masks[f]=new int[0];continue;} // Twelve closed-eye poses.
   if(glints.Count!=1)throw new Exception("Glaceon rear eye glint not unique in frame "+f);
   int pupil=glints[0]+meta.width,pixel=f*stride+pupil*4;
   bool ink=raw[pixel+3]==255&&raw[pixel]<=16&&raw[pixel]==raw[pixel+1]&&raw[pixel+1]==raw[pixel+2];
   masks[f]=ink?new[]{pupil}:new int[0];
   string color=raw[pixel+2]+","+raw[pixel+1]+","+raw[pixel];if(!belowColors.ContainsKey(color))belowColors.Add(color,0);belowColors[color]++;
  }
  Console.WriteLine("Glaceon rear below-glint colors: "+string.Join("; ",belowColors.Select(c=>c.Key+"="+c.Value)));
  return new RootMask{width=meta.width,height=meta.height,frames=masks};
 }
 public static void Run() {
  Directory.CreateDirectory(Output);
  var gf=Plan("garchomp-front",28,13,5,2,new[]{29,28},new[]{13,14},15,45,2,30);
  var gb=Plan("garchomp-back",59,14,5,2,new[]{62,63},new[]{14,15},45,80,2,35);
  File.WriteAllText(Path.Combine(Output,"garchomp.json"),Json.Serialize(new{name="garchomp",color=new[]{41,41,82},front=gf,back=gb,reviewed=true,notes="Two neutral pupil pixels in each visible yellow eye; exact local yellow-iris template uniquely matched in all 89 front and 89 back frames. Body outline replaced with existing dark blue."}));
  var lf=Plan("glaceon-front",0,0,0,0,new int[0],new int[0],0,0,0,0);
  var lb=GlaceonBack();
  File.WriteAllText(Path.Combine(Output,"glaceon.json"),Json.Serialize(new{name="glaceon",color=new[]{49,58,74},front=lf,back=lb,reviewed=true,notes="Front pupils are already dark blue RGB49,58,74 and stay unchanged; neutral upper/left lines are eye borders. Rear profile protects neutral pupil pixel below the unique white glint when present; glint tracked in all171 frames. Existing dark blue used for outline."}));
 }
}

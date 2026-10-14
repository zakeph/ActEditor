$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$bin = Join-Path $root 'ActEditor\bin\Release'
[Environment]::CurrentDirectory = $bin
[Reflection.Assembly]::LoadFrom((Join-Path $bin 'GRF.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $bin 'Utilities.dll')) | Out-Null
$editor = [Reflection.Assembly]::LoadFrom((Join-Path $bin 'Act Editor.exe'))
Add-Type -ReferencedAssemblies 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\mscorlib.dll','C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll', (Join-Path $bin 'GRF.dll'),(Join-Path $bin 'Utilities.dll'),'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades\netstandard.dll' -TypeDefinition @"
using System;
using GRF.Image;
using GRF.FileFormats.SprFormat;
using GRF.FileFormats.ActFormat;
public static class ExportFixture {
 public static Act Create(bool indexed) {
  var spr = new Spr(); spr.Palette = new GRF.FileFormats.PalFormat.Pal();
  var palette = new byte[1024]; palette[4] = 255; palette[7] = 255;
  var image = indexed ? new GrfImage(new byte[] {1}, 1, 1, GrfImageType.Indexed8, palette) : new GrfImage(new byte[] {0, 0, 255, 255}, 1, 1, GrfImageType.Bgra32);
  for(int i=0;i<30;i++) spr.InsertAny(image.Copy()); var index = spr.InsertAny(image);
  var act = new Act(spr);
  var action = new GRF.FileFormats.ActFormat.Action();
  var frame = new Frame(); frame.Layers.Add(new Layer(index, image));
  action.Frames.Add(frame); act.Actions.Add(action);
  return act;
 }
 public static void Run(System.Reflection.Assembly editor, string root) {
 var merge = Array.Find(editor.GetType("ActEditor.Components.ActSaveService").GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic), m => m.Name == "MergeGifActs" && m.GetParameters().Length == 4);
 var body = new Act(System.IO.File.ReadAllBytes(System.IO.Path.Combine(root,"ActEditor/Resources/ref_body_f.act")),System.IO.File.ReadAllBytes(System.IO.Path.Combine(root,"ActEditor/Resources/ref_body_f.spr"))); body.Name="Body";
 var head = new Act(System.IO.File.ReadAllBytes(System.IO.Path.Combine(root,"ActEditor/Resources/ref_head_f.act")),System.IO.File.ReadAllBytes(System.IO.Path.Combine(root,"ActEditor/Resources/ref_head_f.spr"))); head.Name="Head"; head.AnchoredTo=body;
 var accessory=Create(true); while(accessory.Actions.Count<24) accessory.Actions.Add(new GRF.FileFormats.ActFormat.Action(accessory.Actions[0]));
 while(accessory[17].Frames.Count<23) accessory[17].Frames.Add(new Frame(accessory[17,0]));
 accessory.AnchoredTo=body;
 var single=(Act)merge.Invoke(null,new object[]{new[]{body},accessory,new Act[0],false}); var expected=body.Sprite.GetImage(body[17,0,0]).Copy(); expected.Convert(GrfImageType.Bgra32); var actual=single.Sprite.GetImage(single[17,0,0]); if(!System.Linq.Enumerable.SequenceEqual(expected.Pixels,actual.Pixels)) throw new Exception("Exported body image differs from source index="+single[17,0,0].SpriteIndex+" expected="+expected.Width+"x"+expected.Height+" actual="+actual.Width+"x"+actual.Height+" diff="+System.Linq.Enumerable.First(System.Linq.Enumerable.Range(0,Math.Min(expected.Pixels.Length,actual.Pixels.Length)),i=>expected.Pixels[i]!=actual.Pixels[i])); var combined=(Act)merge.Invoke(null,new object[]{new[]{body,head},accessory,new Act[0],false});
 for(int f=0;f<23;f++) if(combined[17,f].Layers.Count<3) throw new Exception("Body/head missing in sitting frame " + f + ": layers=" + combined[17,f].Layers.Count);
 foreach(var layer in combined[17,0].Layers) { var image=combined.Sprite.GetImage(layer); if(layer.SpriteTypeInt==0 && layer.SpriteIndex==-1) continue; if(image==null) throw new Exception("Missing body/head sprite index="+layer.SpriteIndex); if(image.GrfImageType==GrfImageType.Bgra32 && !System.Linq.Enumerable.Any(System.Linq.Enumerable.Where(image.Pixels,(b,i)=>i%4==3),b=>b>0)) throw new Exception("Body/head fully transparent index="+layer.SpriteIndex); } Console.WriteLine("PASS sitting body/head references");
 foreach (bool indexed in new[] {true, false}) foreach (bool front in new[] {true, false}) {
  var primary = Create(indexed); var reference = Create(true);
  var result = (Act)merge.Invoke(null, new object[] {front ? new Act[0] : new[] {reference}, primary, front ? new[] {reference} : new Act[0], false});
  foreach(var layer in result[0,0].Layers) if(result.Sprite.GetImage(layer) == null) throw new Exception("Missing exported layer: primaryIndexed=" + indexed + " front=" + front + " index=" + layer.SpriteIndex);
  if(result[0,0].Layers.Count != 2) throw new Exception("Missing reference layer");
  Console.WriteLine("PASS primaryIndexed=" + indexed + " front=" + front);
 }
 }
}
"@
try { [ExportFixture]::Run($editor,$root) } catch { Write-Host $_.Exception.ToString(); exit 1 }















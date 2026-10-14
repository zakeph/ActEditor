using System;
using System.Collections.Generic;

public static class MaskRecolor {
 public static bool Ink(byte[] pixels,int p) {return pixels[p+3]==255&&pixels[p]<=16&&pixels[p]==pixels[p+1]&&pixels[p+1]==pixels[p+2];}
 public static int Apply(byte[] pixels,int[] protectedIndices,int[] rgb) {
  if(pixels==null||pixels.Length%4!=0||protectedIndices==null||rgb==null||rgb.Length!=3)throw new ArgumentException("Invalid pixels, mask, or RGB");
  foreach(int value in rgb)if(value<0||value>255)throw new ArgumentException("Invalid RGB");
  var protect=new HashSet<int>();
  foreach(int i in protectedIndices) {
   if(i<0||i>=pixels.Length/4||!Ink(pixels,i*4)||!protect.Add(i))throw new ArgumentException("Mask contains non-ink, duplicate, or out-of-bounds pupil pixel");
  }
  int changed=0;
  for(int i=0;i<pixels.Length/4;i++) {
   int p=i*4;if(!Ink(pixels,p)||protect.Contains(i))continue;
   pixels[p]=(byte)rgb[2];pixels[p+1]=(byte)rgb[1];pixels[p+2]=(byte)rgb[0];changed++;
  }
  return changed;
 }
}

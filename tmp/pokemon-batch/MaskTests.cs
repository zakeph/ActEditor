using System;

public static class MaskTests {
 public static void Run() {
  byte[] pixels={0,0,0,255,16,16,16,255,0,0,0,255,16,16,16,255,17,17,17,255,16,15,16,255,0,0,0,0};
  byte[] expected={33,58,90,255,33,58,90,255,0,0,0,255,16,16,16,255,17,17,17,255,16,15,16,255,0,0,0,0};
  int changed=MaskRecolor.Apply(pixels,new[]{2,3},new[]{90,58,33});
  if(changed!=2)throw new Exception("Expected two black outline pixels changed, got "+changed);
  for(int i=0;i<pixels.Length;i++)if(pixels[i]!=expected[i])throw new Exception("Pupil/non-black/alpha mismatch at "+i);
  byte[] invalid={0,0,0,255,40,50,60,255};var before=(byte[])invalid.Clone();bool refused=false;
  try{MaskRecolor.Apply(invalid,new[]{1},new[]{90,58,33});}catch(ArgumentException){refused=true;}
  if(!refused)throw new Exception("Must reject mask protecting non-ink before mutation");
  for(int i=0;i<invalid.Length;i++)if(invalid[i]!=before[i])throw new Exception("Mutated before validating mask");
  Console.WriteLine("PASS: RGB0/16 outline recolored, both pupil shades unchanged, other colors/alpha unchanged, invalid mask rejected atomically.");
 }
}

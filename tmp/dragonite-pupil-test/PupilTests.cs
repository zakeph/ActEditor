using System;

// Regression: a black line inside the silhouette must change, while the
// pupil follows the moving eye and remains bit-for-bit identical.
public static class PupilTests {
    static byte[] Fixture(int shiftX,int shiftY) {
        var pixels=new byte[80*82*4];
        for(int p=0;p<pixels.Length;p+=4){pixels[p]=50;pixels[p+1]=140;pixels[p+2]=230;pixels[p+3]=255;}
        string[] eye={"oKKoo","oKWWK","oWKWK","oKoWK","oKoWo"};
        for(int y=0;y<eye.Length;y++)for(int x=0;x<eye[y].Length;x++) {
            int p=((16+y+shiftY)*80+23+x+shiftX)*4;
            if(eye[y][x]=='K'){pixels[p]=pixels[p+1]=pixels[p+2]=0;}
            if(eye[y][x]=='W'){pixels[p]=pixels[p+1]=pixels[p+2]=255;}
        }
        foreach(int index in new[]{0,55*80+55})pixels[index*4]=pixels[index*4+1]=pixels[index*4+2]=0;
        // Non-black near-black and transparent black must remain untouched.
        pixels[4]=pixels[5]=pixels[6]=1;pixels[11]=0;pixels[8]=pixels[9]=pixels[10]=0;
        return pixels;
    }
    static void Check(bool front,int shiftX,int shiftY) {
        var pixels=Fixture(shiftX,shiftY);var before=(byte[])pixels.Clone();
        int[] pupil={ (17+shiftY)*80+24+shiftX,(18+shiftY)*80+25+shiftX,(19+shiftY)*80+24+shiftX,(20+shiftY)*80+24+shiftX};
        int count=DragoniteRecolor.Apply(pixels,80,82,front);
        if(count!=(front?7:11))throw new Exception("Expected "+(front?7:11)+" recolored pixels, got "+count);
        for(int i=0;i<pixels.Length/4;i++) {
            int p=i*4;bool protectedPixel=front&&Array.IndexOf(pupil,i)>=0;
            bool recolor=before[p+3]==255&&before[p]==0&&before[p+1]==0&&before[p+2]==0&&!protectedPixel;
            for(int c=0;c<4;c++) {
                byte expected=recolor&&c<3?new byte[]{33,58,90}[c]:before[p+c];
                if(pixels[p+c]!=expected)throw new Exception("Incorrect pixel at "+i+", channel "+c);
            }
        }
    }
    public static void Run() {
        Check(true,0,0);Check(true,1,-5);Check(true,0,6);Check(false,0,0);
        var missing=Fixture(0,0);
        for(int p=0;p<missing.Length;p+=4)if(missing[p]==255&&missing[p+1]==255&&missing[p+2]==255)missing[p]=100;
        var before=(byte[])missing.Clone();bool refused=false;
        try{DragoniteRecolor.Apply(missing,80,82,true);}catch(InvalidOperationException){refused=true;}
        if(!refused)throw new Exception("Missing eye must refuse recoloring");
        for(int p=0;p<missing.Length;p++)if(missing[p]!=before[p])throw new Exception("Modified pixels before validating eye");
        Console.WriteLine("PASS: moving pupil preserved; interior/outer black recolored; back has no protected black; alpha/non-black unchanged; missing eye rejected before mutation.");
    }
}

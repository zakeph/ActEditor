using System;

// Catches recoloring pupils, enclosed transparent holes, deeper black layers,
// or changing RGB/alpha of pixels outside the selected exterior contour.
public static class OutlineTests {
    static void Check(string[] rows, int expectedChanges) {
        int width=rows[0].Length, height=rows.Length;
        var pixels=new byte[width*height*4];
        for(int y=0;y<height;y++) for(int x=0;x<width;x++) {
            int p=(y*width+x)*4;
            if(rows[y][x]=='.') {pixels[p]=17;pixels[p+1]=28;pixels[p+2]=39;}
            else {
                pixels[p+3]=255;
                if(rows[y][x]=='O') {pixels[p]=50;pixels[p+1]=140;pixels[p+2]=230;}
            }
        }
        var original=(byte[])pixels.Clone();
        int count=OutlineRecolor.Apply(pixels,width,height);
        if(count!=expectedChanges) throw new Exception("Expected "+expectedChanges+" exterior pixels, got "+count);
        int actual=0;
        for(int y=0;y<height;y++) for(int x=0;x<width;x++) {
            int p=(y*width+x)*4;
            bool changed=false;
            for(int c=0;c<4;c++) changed|=pixels[p+c]!=original[p+c];
            if(!changed) continue;
            actual++;
            if(rows[y][x]!='K'||pixels[p]!=33||pixels[p+1]!=58||pixels[p+2]!=90||pixels[p+3]!=255)
                throw new Exception("Changed a non-black pixel, alpha, or wrong target color");
        }
        if(actual!=expectedChanges) throw new Exception("Reported change count differs from pixels");
        if(height==9) {
            int pupil=(3*width+3)*4;
            if(pixels[pupil]!=0||pixels[pupil+1]!=0||pixels[pupil+2]!=0||pixels[pupil+3]!=255)
                throw new Exception("Interior pupil changed next to enclosed transparent hole");
        }
        if(height==7 && pixels[(2*width+2)*4]!=0)
            throw new Exception("Recolored deeper than one exterior pixel");
    }
    public static void Run() {
        Check(new[]{".........",".KKKKKKK.",".KOOOOOK.",".KOKOOOK.",".KOO.OOK.",".KOOOOOK.",".KOOOOOK.",".KKKKKKK.","........."},24);
        Check(new[]{"KKK","KOK","KKK"},8);
        Check(new[]{".......",".KKKKK.",".KKKKK.",".KKOKK.",".KKKKK.",".KKKKK.","......."},16);
        Console.WriteLine("PASS: exterior contour, enclosed pupil/hole, canvas edges, one-pixel depth, colors and alpha.");
    }
}

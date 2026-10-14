using System;

// Specific to the inspected Dragonite GIF, not a universal eye detector.
public static class DragoniteRecolor {
    public static int Apply(byte[] pixels,int width,int height,bool front) {
        if(pixels==null||width<=0||height<=0||pixels.Length!=width*height*4)
            throw new ArgumentException("Invalid BGRA canvas");
        int[] pupil=new int[0];
        if(front) {
            if(width!=80||height!=82)throw new InvalidOperationException("Unexpected Dragonite front canvas");
            int minX=width,minY=height,maxX=-1,maxY=-1,whites=0;
            // This region encloses the inspected moving eye in all 102 poses;
            // white claws and the rest of the sprite are outside it.
            for(int y=9;y<30;y++)for(int x=18;x<36;x++) {
                int p=(y*width+x)*4;
                if(pixels[p+3]!=255||pixels[p]<200||pixels[p]!=pixels[p+1]||pixels[p+1]!=pixels[p+2])continue;
                minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);whites++;
            }
            if(whites!=6||maxX-minX!=2||maxY-minY!=3)
                throw new InvalidOperationException("Eye does not match inspected Dragonite poses; refusing to recolor pupil");
            pupil=new[]{minY*width+minX,(minY+1)*width+minX+1,(minY+2)*width+minX,(minY+3)*width+minX};
            foreach(int i in pupil)if(!Black(pixels,i*4))throw new InvalidOperationException("Pupil shape changed; refusing to recolor");
        }
        int changed=0;
        for(int i=0;i<width*height;i++) {
            int p=i*4;
            if(!Black(pixels,p)||Array.IndexOf(pupil,i)>=0)continue;
            pixels[p]=33;pixels[p+1]=58;pixels[p+2]=90;changed++;
        }
        return changed;
    }
    static bool Black(byte[] p,int i) {return p[i+3]==255&&p[i]==0&&p[i+1]==0&&p[i+2]==0;}
}

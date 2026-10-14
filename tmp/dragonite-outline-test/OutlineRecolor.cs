using System;
using System.Collections.Generic;

public static class OutlineRecolor {
    public static int Apply(byte[] bgra, int width, int height) {
        if(width<=0||height<=0||bgra==null||bgra.Length!=width*height*4)
            throw new ArgumentException("Invalid BGRA canvas");
        // Flood only transparent pixels reachable from outside the canvas.
        // Four-way connectivity keeps diagonal gaps from leaking into holes.
        var outside=new bool[width*height];
        var pending=new Queue<int>();
        for(int y=0;y<height;y++) for(int x=0;x<width;x++) {
            if(x!=0&&y!=0&&x!=width-1&&y!=height-1) continue;
            int i=y*width+x;
            if(bgra[i*4+3]==0&&!outside[i]) {outside[i]=true;pending.Enqueue(i);}
        }
        int[] dx={-1,1,0,0},dy={0,0,-1,1};
        while(pending.Count>0) {
            int i=pending.Dequeue(),x=i%width,y=i/width;
            for(int d=0;d<4;d++) {
                int nx=x+dx[d],ny=y+dy[d];
                if(nx<0||ny<0||nx>=width||ny>=height) continue;
                int next=ny*width+nx;
                if(!outside[next]&&bgra[next*4+3]==0) {outside[next]=true;pending.Enqueue(next);}
            }
        }
        int changed=0;
        for(int y=0;y<height;y++) for(int x=0;x<width;x++) {
            int p=(y*width+x)*4;
            if(bgra[p+3]!=255||bgra[p]!=0||bgra[p+1]!=0||bgra[p+2]!=0) continue;
            bool edge=false;
            for(int oy=-1;oy<=1&&!edge;oy++) for(int ox=-1;ox<=1;ox++) {
                if(ox==0&&oy==0) continue;
                int nx=x+ox,ny=y+oy;
                if(nx<0||ny<0||nx>=width||ny>=height||outside[ny*width+nx]) {edge=true;break;}
            }
            if(edge) {bgra[p]=33;bgra[p+1]=58;bgra[p+2]=90;changed++;}
        }
        return changed;
    }
}

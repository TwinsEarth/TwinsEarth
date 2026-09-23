using System.Collections.Generic;
using UnityEngine;

namespace PixelToCivilization.UI
{
    /// <summary>
    /// V6.1.9(j) 程序化彩色矢量图标工厂：运行时像素几何绘制，风格对齐参考图的彩色 Emoji 质感——
    /// 资源/分类各有品牌色，控制图标用浅色。全平台（含无 Emoji 字体的 WebGL）一致渲染。按语义 key 缓存。
    /// </summary>
    public static class IconFactory
    {
        public const int R = 64;
        static readonly Dictionary<string, Sprite> _cache = new();
        static readonly Color Light = new(0.96f,0.98f,1f);

        public static Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key)) key="default";
            key=key.ToLowerInvariant();
            if (_cache.TryGetValue(key,out var s)) return s;
            var tex=new Texture2D(R,R,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var px=new Color32[R*R];
            Clear(px);
            Draw(px,key);
            tex.SetPixels32(px);tex.Apply(true,false);
            var sp=Sprite.Create(tex,new Rect(0,0,R,R),new Vector2(0.5f,0.5f),R);
            _cache[key]=sp; return sp;
        }

        static void Clear(Color32[] px){var z=new Color32(0,0,0,0);for(int i=0;i<px.Length;i++)px[i]=z;}
        static void Px(Color32[] px,int x,int y,Color c){ if(x<0||y<0||x>=R||y>=R)return; px[y*R+x]=c; }
        static void Rect(Color32[] px,int x0,int y0,int x1,int y1,Color c){ for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)Px(px,x,y,c); }
        static void Frame(Color32[] px,int x0,int y0,int x1,int y1,int t,Color c){ Rect(px,x0,y0,x1,y0+t,c);Rect(px,x0,y1-t,x1,y1,c);Rect(px,x0,y0,x0+t,y1,c);Rect(px,x1-t,y0,x1,y1,c); }
        static void Disc(Color32[] px,int cx,int cy,int rad,Color c){
            int r2=rad*rad;for(int y=-rad;y<=rad;y++)for(int x=-rad;x<=rad;x++)if(x*x+y*y<=r2)Px(px,cx+x,cy+y,c);
        }
        static void Ring(Color32[] px,int cx,int cy,int rad,int t,Color c){
            float f=0.7f;var inner=new Color((byte)(c.r*255*f),(byte)(c.g*255*f),(byte)(c.b*255*f),255);
            int o=rad*rad,i=(rad-t)*(rad-t);for(int y=-rad;y<=rad;y++)for(int x=-rad;x<=rad;x++){int q=x*x+y*y;if(q<=o&&q>=i)Px(px,cx+x,cy+y,c);}
        }
        static void Line(Color32[] px,int x0,int y0,int x1,int y1,int t,Color c){
            int dx=Mathf.Abs(x1-x0),dy=Mathf.Abs(y1-y0),sx=x0<x1?1:-1,sy=y0<y1?1:-1,err=dx-dy;
            while(true){ Disc(px,x0,y0,t/2+1,c); if(x0==x1&&y0==y1)break; int e2=2*err; if(e2>-dy){err-=dy;x0+=sx;} if(e2<dx){err+=dx;y0+=sy;} }
        }
        static void Tri(Color32[] px,int ax,int ay,int bx,int by,int cx,int cy,Color col){
            int minX=Mathf.Min(ax,bx,cx),maxX=Mathf.Max(ax,bx,cx),minY=Mathf.Min(ay,by,cy),maxY=Mathf.Max(ay,by,cy);
            for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
                if(Sign(x,y,ax,ay,bx,by)>=0&&Sign(x,y,bx,by,cx,cy)>=0&&Sign(x,y,cx,cy,ax,ay)>=0)Px(px,x,y,col);
        }
        static int Sign(int px,int py,int ax,int ay,int bx,int by){return (px-bx)*(ay-by)-(ax-bx)*(py-by);}

        // 品牌色
        static readonly Color CWood=new(0.22f,0.70f,0.30f);   // 绿
        static readonly Color CStone=new(0.68f,0.71f,0.74f);  // 石灰
        static readonly Color CFood=new(0.94f,0.66f,0.19f);   // 麦黄
        static readonly Color CGold=new(1f,0.83f,0.23f);      // 金币
        static readonly Color CSteel=new(0.50f,0.69f,0.88f);  // 钢蓝
        static readonly Color CBronze=new(0.82f,0.54f,0.29f);
        static readonly Color CPower=new(1f,0.82f,0.29f);
        static readonly Color CResearch=new(0.69f,0.59f,0.99f);// 紫
        static readonly Color CCulture=new(0.30f,0.67f,0.97f); // 蓝
        static readonly Color CGoods=new(0.85f,0.58f,0.36f);
        static readonly Color CHouse=new(0.45f,0.75f,0.99f);
        static readonly Color CMil=new(1f,0.53f,0.53f);
        static readonly Color CFarm=new(0.41f,0.86f,0.49f);
        static readonly Color CShip=new(0.45f,0.75f,0.99f);
        static readonly Color CRocket=new(0.85f,0.47f,0.95f);
        static readonly Color CTower=new(1f,0.66f,0.30f);
        static readonly Color CFactory=new(0.65f,0.85f,1f);
        static readonly Color CText=new(0.91f,0.93f,0.97f);

        static void Draw(Color32[] px,string k)
        {
            Color f=CText, w=Light;
            // —— 资源（品牌色）——
            if(k=="wood"||k=="tree"){ f=CWood; Rect(px,29,10,35,40,f); Disc(px,32,44,14,f);Disc(px,32,24,11,Light); }
            else if(k=="stone"){ f=CStone; Tri(px,14,18,50,18,32,50,f); Ring(px,32,30,9,3,Light); }
            else if(k=="food"||k=="farm"){ f=k=="farm"?CFarm:CFood; Line(px,32,12,32,52,3,f); for(int i=0;i<4;i++){Line(px,32,20+i*8,20,14+i*8,2,f);Line(px,32,20+i*8,44,14+i*8,2,f);} }
            else if(k=="gold"||k=="coin"){ f=CGold; Disc(px,32,32,20,f); Ring(px,32,32,13,3,Light); }
            else if(k=="iron"||k=="steel"||k=="gear"){ f=k=="steel"?CSteel:CFactory; Gear(px,f,Light); }
            else if(k=="bronze"){ f=CBronze; Disc(px,32,32,19,f);Ring(px,32,32,19,3,Light); }
            else if(k=="power"||k=="energy"||k=="lightning"){ f=CPower; Tri(px,36,54,18,30,34,30,f);Tri(px,28,10,46,34,30,34,f); }
            else if(k=="research"||k=="tech"||k=="science"||k=="flask"){ f=CResearch; Rect(px,26,10,38,40,f);Tri(px,26,40,38,40,32,54,f);Rect(px,29,18,35,24,Light);Rect(px,29,28,35,34,Light); }
            else if(k=="culture"||k=="book"||k=="school"){ f=CCulture; Rect(px,16,14,48,50,f);Line(px,32,14,32,50,3,Light);Line(px,16,32,48,32,3,Light); }
            else if(k=="goods"||k=="box"||k=="cargo"){ f=CGoods; Frame(px,14,18,50,46,3,f);Line(px,14,30,50,30,3,Light); }
            else if(k=="concrete"){ f=CStone; Frame(px,14,14,50,50,4,f); Rect(px,28,14,36,50,Light);Rect(px,14,28,50,36,Light); }
            else if(k=="fusion"){ f=CCulture; Ring(px,32,32,18,4,f);Disc(px,32,32,6,Light); }
            else if(k=="carbon"){ f=new Color(0.29f,0.31f,0.34f); Disc(px,32,32,16,f);Ring(px,32,32,9,3,Light); }
            else if(k=="helium3"){ f=new Color(0.40f,0.85f,0.91f); Ring(px,32,32,16,3,f);Line(px,32,16,32,48,2,Light);Line(px,16,32,48,32,2,Light); }
            // —— 建筑 / 分类 ——
            else if(k=="house"||k=="hut"||k=="home"||k=="residence"){ f=CHouse; Rect(px,18,16,46,50,f);Tri(px,14,26,32,50,50,26,new Color(0.95f,0.55f,0.45f));Rect(px,28,16,36,32,Light); }
            else if(k=="military"||k=="barracks"||k=="army"||k=="sword"){ f=CMil; Shield(px,f); }
            else if(k=="wall"||k=="great_wall"){ f=CStone; Frame(px,12,20,52,48,4,f);Rect(px,12,34,52,40,f); }
            else if(k=="tower"||k=="watchtower"||k=="arrow_tower"){ f=CTower; Rect(px,26,10,38,52,f);Rect(px,22,8,42,14,CMil); }
            else if(k=="fire_tower"){ f=CTower; Rect(px,26,10,38,52,f);Tri(px,24,52,40,52,32,60,new Color(1f,0.45f,0.2f)); }
            else if(k=="cannon_tower"||k=="bunker"){ f=CTower; Rect(px,20,14,44,50,f);Rect(px,28,14,36,26,CMil);Line(px,32,26,52,40,5,CText); }
            else if(k=="stable"){ f=CTower; Rect(px,16,20,48,48,f);Tri(px,12,30,32,50,52,30,new Color(0.7f,0.45f,0.25f)); }
            else if(k=="factory"||k=="workshop"||k=="industry"){ f=CFactory; Rect(px,14,16,50,40,f);Tri(px,14,40,26,50,26,40,f);Rect(px,40,24,46,40,Light);Rect(px,30,40,36,54,CMil); }
            else if(k=="bank"||k=="market"||k=="shop"||k=="economy"){ f=CGold; Rect(px,14,16,50,30,f); for(int i=18;i<=46;i+=14)Rect(px,i,34,i+8,50,Light); }
            else if(k=="temple"||k=="pagoda"||k=="culture_build"){ f=new Color(1f,0.84f,0.65f); for(int i=0;i<3;i++){int y=16+i*12;Rect(px,18+i*4,y,46-i*4,y+8,f);} }
            else if(k=="road"||k=="highway"||k=="railway_pre"||k=="high_speed_rail"||k=="transport"){ f=CText; Line(px,14,14,50,50,6,f); }
            else if(k=="canal"||k=="water"||k=="navy"||k=="wave"){ f=CCulture; Line(px,10,22,54,22,5,f);Line(px,10,36,54,36,5,Light); }
            else if(k=="ship"||k=="boat"){ f=CShip; Tri(px,12,30,32,46,32,30,f);Rect(px,30,26,44,34,f);Line(px,32,30,32,14,3,Light);Tri(px,32,14,48,30,32,30,Light); }
            else if(k=="air"||k=="airport"||k=="airplane"){ f=CFactory; Tri(px,10,32,54,20,54,44,f);Tri(px,30,32,54,28,54,36,Light); }
            else if(k=="rocket"||k=="space"||k=="space_elevator"){ f=CRocket; Tri(px,32,6,22,26,42,26,f);Rect(px,22,26,42,46,f);Tri(px,22,46,16,54,26,46,Light);Tri(px,42,46,48,54,38,46,Light);Disc(px,32,34,5,CGold); }
            // —— UI 控制 ——
            else if(k=="play"){ Tri(px,22,14,22,50,50,32,f); }
            else if(k=="pause"){ Rect(px,22,14,30,50,f);Rect(px,34,14,42,50,f); }
            else if(k=="forward"||k=="ff"||k=="up"||k=="plus"){ if(k=="plus"){Line(px,32,14,32,50,5,f);Line(px,14,32,50,32,5,f);} else {Tri(px,14,14,14,50,36,32,f);Tri(px,34,14,34,50,54,32,f);} }
            else if(k=="down"||k=="minus"){ Line(px,14,32,50,32,5,f); }
            else if(k=="setting"||k=="settings"||k=="debug"||k=="wrench"){ Gear(px,f,Light); }
            else if(k=="close"||k=="x"){ f=CMil; Line(px,16,16,48,48,5,f);Line(px,48,16,16,48,5,f); }
            else if(k=="back"||k=="menu"){ Line(px,14,20,50,20,4,f);Line(px,14,32,50,32,4,f);Line(px,14,44,50,44,4,f); }
            else if(k=="god"){ f=CGold; Disc(px,32,40,10,f);Tri(px,14,12,50,12,32,34,f); }
            else if(k=="save"||k=="floppy"){ f=CFactory; Frame(px,16,12,48,50,4,f);Rect(px,22,18,42,34,Light);Rect(px,36,12,48,22,f); }
            else if(k=="target"||k=="center"){ f=CFarm; Ring(px,32,32,19,3,f);Ring(px,32,32,10,3,Light);Disc(px,32,32,4,f); }
            else if(k=="help"||k=="question"){ f=CGold; Ring(px,32,32,19,3,f);Rect(px,30,24,34,38,Light);Rect(px,30,15,34,21,Light);Disc(px,32,13,3,f); }
            else if(k=="sound"||k=="volume"){ f=CShip; Rect(px,12,24,26,40,f);Tri(px,26,20,26,44,46,32,f);Ring(px,48,32,7,2,Light);Ring(px,48,32,12,2,Light); }
            else if(k=="person"||k=="npc"||k=="people"){ f=new Color(1f,0.75f,0.47f); Disc(px,32,45,9,f);for(int y=12;y<=34;y++){int hh=Mathf.RoundToInt(Mathf.Lerp(17,8,(y-12)/22f));Rect(px,32-hh,y,32+hh,y+1,f);} }
            else if(k=="build"||k=="hammer"||k=="crane"){ f=CGold; Line(px,16,16,40,40,6,f);Rect(px,34,32,52,44,Light);Rect(px,30,40,42,50,f); }
            else if(k=="select"||k=="cursor"||k=="hand"||k=="finger"){ // 手指点击：上伸食指 + 蜷起四指 + 指尖点击波纹
                f=CText;
                Ring(px,32,50,10,2,CGold);                 // 点击波纹（金色）
                Rect(px,29,22,35,46,f);Disc(px,32,46,3,f); // 食指（指尖圆角）
                Rect(px,36,20,41,30,f);Rect(px,42,20,46,28,f); // 蜷起的中指/无名指
                Rect(px,20,16,28,25,f);                    // 拇指
                Rect(px,23,8,40,24,f);                     // 掌心
                Rect(px,26,2,37,10,f);                     // 手腕
            }
            else if(k=="cart"){ f=CGoods; Frame(px,12,24,46,42,4,f);Disc(px,23,17,5,CMil);Disc(px,40,17,5,CMil);Line(px,46,33,55,25,3,Light); }
            else if(k=="fullscreen"){ f=CText; Frame(px,16,16,48,48,4,f); }
            else { f=CGold; Ring(px,32,32,18,4,f); Disc(px,32,32,5,f); } // 兜底
        }
        static void Gear(Color32[] px,Color a,Color b){
            for(int i=0;i<8;i++){float ang=i*Mathf.PI/4f;int tx=32+Mathf.RoundToInt(Mathf.Cos(ang)*18),ty=32+Mathf.RoundToInt(Mathf.Sin(ang)*18);Rect(px,tx-4,ty-4,tx+4,ty+4,a);}
            Disc(px,32,32,16,a); Ring(px,32,32,7,4,b);
        }
        static void Shield(Color32[] px,Color c){
            for(int y=10;y<=54;y++){int half=Mathf.RoundToInt(Mathf.Lerp(18,4,y<=34?(y-10)/24f:1-(y-34)/40f));Rect(px,32-half,y,32+half,y,c);}
        }
    }
}

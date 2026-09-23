using UnityEngine;
using PixelToCivilization.Data;

namespace PixelToCivilization.World
{
    /// <summary>
    /// V6.1.1 地形绘制器：把 120×120 高度场烘焙成 PBR 生物群系大贴图
    /// （Albedo / Normal / Mask），并提供 FBM 自然地貌与平滑法线，替代 V5.9.9 的硬顶点色。
    /// 生物群系按高度 + 坡度 + 噪声在沙/草/岩/雪/岸滩间自然过渡。
    /// </summary>
    public static class TerrainPainter
    {
        // —— 真实感生物群系基色（Linear 友好）——
        static readonly Color Sand = new(0.95f, 0.86f, 0.58f);   // V7.0.1 暖浅沙
        static readonly Color GrassA = new(0.47f, 0.81f, 0.31f);  // 亮柠檬草绿
        static readonly Color GrassB = new(0.57f, 0.87f, 0.38f);
        static readonly Color Rock = new(0.66f, 0.64f, 0.59f);    // 柔灰岩
        static readonly Color RockDark = new(0.52f, 0.50f, 0.46f);
        static readonly Color Snow = new(0.94f, 0.97f, 1.00f);
        static readonly Color Underwater = new(0.55f, 0.83f, 0.88f); // 透亮浅水沙
        static readonly Color Dirt = new(0.70f, 0.56f, 0.37f);   // 暖棕土

        // ---------- 可平铺 value noise / fbm ----------
        static int Hash(int x,int y,int seed){
            int h=x*374761393+y*668265263+seed*144269504; h=(h^(h>>13))*1274126177; h^=h>>16; return h&0x7fffffff; }
        static float Vn(float x,float y,int period,int seed){
            int xi=Mathf.FloorToInt(x),yi=Mathf.FloorToInt(y); float xf=x-xi,yf=y-yi;
            int x0=xi%period,x1=(xi+1)%period,y0=yi%period,y1=(yi+1)%period;
            if(x0<0)x0+=period;if(x1<0)x1+=period;if(y0<0)y0+=period;if(y1<0)y1+=period;
            float a=Hash(x0,y0,seed)/2147483647f,b=Hash(x1,y0,seed)/2147483647f,
                  cc=Hash(x0,y1,seed)/2147483647f,d=Hash(x1,y1,seed)/2147483647f;
            float u=xf*xf*(3-2*xf),v=yf*yf*(3-2*yf);
            return Mathf.Lerp(Mathf.Lerp(a,b,u),Mathf.Lerp(cc,d,u),v);
        }
        /// <summary>多倍频 FBM 自然起伏（输出约 -1..1）</summary>
        public static float FbmRidge(float x,float z,int seed,int oct=5){
            float amp=1,freq=0.018f,sum=0,norm=0;
            for(int o=0;o<oct;o++){ sum+=Vn(x*freq+o*9.1f,z*freq-o*7.3f,Mathf.Max(4,Mathf.RoundToInt(40*freq*200)),seed+o*131)*amp; norm+=amp; amp*=0.52f; freq*=2.05f; }
            return (sum/norm-0.5f)*2f;
        }

        /// <summary>双线性采样高度场</summary>
        static float SampleH(float[,] h,float u,float v){
            int n=h.GetLength(0);
            float fx=Mathf.Clamp01(u)*(n-1), fz=Mathf.Clamp01(v)*(n-1);
            int x0=Mathf.FloorToInt(fx),z0=Mathf.FloorToInt(fz);
            int x1=Mathf.Min(x0+1,n-1),z1=Mathf.Min(z0+1,n-1);
            float tx=fx-x0,tz=fz-z0;
            return Mathf.Lerp(Mathf.Lerp(h[z0,x0],h[z0,x1],tx),Mathf.Lerp(h[z1,x0],h[z1,x1],tx),tz);
        }

        /// <summary>烘焙一整套地形 PBR 贴图。res 建议 512(手机)/1024(PC)。</summary>
        public static void Bake(float[,] height, float tile, int seed, int res,
            out Texture2D albedo, out Texture2D normal, out Texture2D mask)
        {
            int n=height.GetLength(0);
            albedo=new Texture2D(res,res,TextureFormat.RGBA32,true){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear};
            normal=new Texture2D(res,res,TextureFormat.RGBA32,true){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear};
            mask  =new Texture2D(res,res,TextureFormat.RGBA32,true){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var ca=new Color32[res*res]; var cn=new Color32[res*res]; var cm=new Color32[res*res];

            float H(float u,float v)=>SampleH(height,u,v);
            for(int y=0;y<res;y++)for(int x=0;x<res;x++){
                float u=x/(float)(res-1), v=y/(float)(res-1);
                // 中心差分求世界法线（含真实坡度）
                float e=1.5f/(res-1);
                float hl=H(Mathf.Max(0,u-e),v),hr=H(Mathf.Min(1,u+e),v);
                float hd=H(u,Mathf.Max(0,v-e)),hu=H(u,Mathf.Min(1,v+e));
                Vector3 nw=new Vector3((hl-hr)*tile, 2.2f, (hd-hu)*tile).normalized;
                float slope=1f-nw.y; // 越陡越大

                float hgt=H(u,v);
                // 细节噪声（颜色斑驳 + 法线微扰）
                float grain=Vn(u*140f,v*140f,256,seed+5)*0.5f+Vn(u*60f+3,v*60f,128,seed+9)*0.5f;
                float n1=Vn(u*220f,v*220f,400,seed+3)-0.5f, n2=Vn(u*220f+7f,v*220f,400,seed+8)-0.5f;

                Color col=BiomeColor(hgt,slope,grain,seed);
                // 微明暗
                col*=0.965f+grain*0.07f;  // V7.0.1 干净玩具明暗
                ca[y*res+x]=col;

                // 法线：宏观地形法线 + 高频细节，转切线空间（地形平面 XZ）
                Vector3 detail=new Vector3(n1*1.4f,n2*1.4f,1f).normalized;
                Vector3 fn=new Vector3(nw.x+detail.x*0.35f, Mathf.Max(0.15f,nw.y), nw.z+detail.y*0.35f).normalized;
                cn[y*res+x]=new Color(fn.x*0.5f+0.5f,fn.y*0.5f+0.5f,fn.z*0.5f+0.5f,1f);

                // Mask：G=AO 微变，A=Smoothness（雪/湿沙高，草低）
                float sm=BiomeSmoothness(hgt,slope);
                byte ao=(byte)Mathf.Clamp(Mathf.RoundToInt((0.92f+grain*0.16f)*255),0,255);
                cm[y*res+x]=new Color32(0,ao,0,(byte)Mathf.Clamp(Mathf.RoundToInt(sm*255),0,255));
            }
            albedo.SetPixels32(ca);normal.SetPixels32(cn);mask.SetPixels32(cm);
            albedo.Apply(true,false);normal.Apply(true,false);mask.Apply(true,false);
            albedo.anisoLevel=8;normal.anisoLevel=8;
        }

        static Color BiomeColor(float h,float slope,float grain,int seed)
        {
            Color col;
            if (h < GameConstants.WaterLevel-0.6f) col = Color.Lerp(Underwater,Dirt,Mathf.Clamp01((h+2f)));
            else if (h <= 0.35f) col = Color.Lerp(Sand,Dirt,0.25f+grain*0.2f);             // 沙滩
            else if (h < 3.0f){                                                             // 草原（两种绿斑驳）
                Color g=Color.Lerp(GrassA,GrassB,grain);
                col=Color.Lerp(g,Dirt,Mathf.Clamp01((h-2.4f)/1.2f)*0.15f); // V7.0.1 草地少混土
            }
            else if (h < 7.0f) col=Color.Lerp(GrassB,Rock,Mathf.Clamp01((h-3f)/4f));       // 丘陵转岩
            else col=Color.Lerp(Rock,Snow,Mathf.Clamp01((h-7f)/2.5f));                      // 雪线
            // 陡坡强制裸露岩石
            float rockAmt=Mathf.Clamp01((slope-0.28f)/0.22f);
            col=Color.Lerp(col,Color.Lerp(Rock,RockDark,grain*0.4f),rockAmt);
            // 雪线以上且非极陡覆雪
            if(h>7.5f) col=Color.Lerp(col,Snow,Mathf.Clamp01(1f-rockAmt));
            return col;
        }
        static float BiomeSmoothness(float h,float slope){
            if(h>7.5f) return 0.55f;
            if(h<GameConstants.WaterLevel) return 0.4f;
            if(h<=0.35f) return 0.22f;
            if(slope>0.4f) return 0.28f;
            return 0.08f; // 草地哑光
        }

        /// <summary>中心差分平滑法线（替代 Mesh.RecalculateNormals 的硬面法线）</summary>
        public static Vector3[] SmoothNormals(float[,] height,float tile,float worldStep)
        {
            int n=height.GetLength(0);
            var normals=new Vector3[(n+1)*(n+1)];
            for(int z=0;z<=n;z++)for(int x=0;x<=n;x++){
                float hl=height[Mathf.Clamp(z,0,n-1),Mathf.Clamp(x-1,0,n-1)];
                float hr=height[Mathf.Clamp(z,0,n-1),Mathf.Clamp(x+1,0,n-1)];
                float hd=height[Mathf.Clamp(z-1,0,n-1),Mathf.Clamp(x,0,n-1)];
                float hu=height[Mathf.Clamp(z+1,0,n-1),Mathf.Clamp(x,0,n-1)];
                normals[z*(n+1)+x]=new Vector3((hl-hr)*tile,2.4f,(hd-hu)*tile).normalized;
            }
            return normals;
        }
    }
}

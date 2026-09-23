using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.Rendering;

namespace PixelToCivilization.World
{
    /// <summary>七种编队（I字/一字/V字/S字/W字/L字/O字）</summary>
    public enum BirdFormation { I, Line, V, S, W, L, O }

    /// <summary>V6.1.2 飞行路线：转圈（大/中/小 × 高/中/低）、对角往返（中心↔四角）、随机游荡；每 5 分钟随机重选</summary>
    public enum BirdPath { Circle, Diagonal, Random }

    /// <summary>
    /// V6.1.2 飞鸟群系统：
    /// ·每只鸟随机纯色或双色混色、体型 0.5~3 倍、寿命 5~20 游戏年，全图上限 200 只；
    /// ·每群 3~10 只，孤鸟在视野(25)内看到鸟群会自动加入（群上限10）；
    /// ·每 30 秒现实时间随机切换一次 I/一/V/S/W/L/O 队形，队形平滑过渡；
    /// ·鸟群绕村上空盘旋、持续拍翅；纯视觉层，移动/编队用现实时间(unscaled)，寿命用游戏年份。
    /// </summary>
    public class WildlifeBirds : MonoBehaviour
    {
        class Bird
        {
            public LodAgent Lod;
            public Transform Root, WL, WR, WL2, WR2;   // WL/WR=LV3近景翅根，WL2/WR2=LV2中景翅根
            public Vector3 Form, FormTarget;     // 当前/目标编队局部坐标（平滑过渡）
            public float Flap, Scale, LifeYears, BirthYear;
            public Flock Flock;
            public Color Color;
            public int Species;
            // 孤鸟游荡
            public bool Lone; public Vector3 LonePos, LoneDir; public float TurnCd;
        }
        class Flock
        {
            public readonly List<Bird> Birds=new();
            public BirdFormation Form;
            public BirdPath Path;
            public Vector3 Center,Anchor; public float Radius,Height,Speed,Phase,DriftPhase; public int Layer;
            // 对角往返
            public Vector3 Corner; public float PingT,PingSpeed=0.06f; public bool PingForward=true;
            // 随机游荡
            public Vector3 RandCenter,RandTarget; public float RandCd;
        }

        public const int MaxBirds = 500;   // V6.2.2 上限上调到500（最近100只完全体，其余简化体控算力）
        const float FormationSwitchSeconds = 30f;   // 现实时间：编队切换
        const float PathSwitchSeconds = 300f;       // 现实时间：飞行路线 5 分钟随机重选一遍
        const float JoinRange = 25f;                // 孤鸟视野
        const float FlockCap = 10;

        readonly List<Flock> _flocks=new();
        readonly List<Bird> _all=new();
        Transform _root;
        Vector3 _home;
        float _range=220f;   // 兜底活动半径
        WorldGenerator _terrain;
        float Range { get {   // V6.3.7(真扩展) 鸟群活动半径随真实活动疆域扩大
            if(_terrain==null)_terrain=Object.FindObjectOfType<WorldGenerator>();
            return _terrain!=null?_terrain.ActiveWorld*0.46f:_range;
        } }
        System.Random _rng;
        float _formTimer,_pathTimer;
        readonly Dictionary<int,Material> _matCache=new();

        public void Init(Vector3 villageCenter, int flocks=6, int perFlock=0)
        {
            Clear();
            _home=villageCenter; _rng=new System.Random(System.Environment.TickCount);
            _range=GameConstants.WorldSize*0.46f;
            _root=new GameObject("Birds").transform; _root.SetParent(transform);
            _formTimer=0f; _pathTimer=0f;
            int fc=Mathf.Clamp(flocks<=0?30:flocks,24,44);
            for(int f=0;f<fc;f++) SpawnFlock();
            // 孤鸟：全图游荡，可离群/入群
            int lone=8+_rng.Next(6);
            for(int i=0;i<lone && _all.Count<MaxBirds;i++) SpawnLone();
        }

        // ---------- 生成 ----------
        void SpawnFlock()
        {
            if(_all.Count>=MaxBirds)return;
            var f=new Flock{
                Anchor=RandomMapPoint(),
                Center=_home,
                Speed=0.07f+(float)_rng.NextDouble()*0.05f,
                Phase=(float)_rng.NextDouble()*Mathf.PI*2f,
                DriftPhase=(float)_rng.NextDouble()*Mathf.PI*2f,
                Form=(BirdFormation)_rng.Next(7),
            };
            RollPath(f);
            int n=3+_rng.Next(8); // 3~10
            for(int i=0;i<n && _all.Count<MaxBirds;i++)
            {
                var b=MakeBird(); b.Flock=f; b.FormTarget=FormationOffset(f.Form,i,n); b.Form=b.FormTarget;
                f.Birds.Add(b); _all.Add(b);
            }
            _flocks.Add(f);
        }

        /// <summary>随机分配飞行路线：百层绕圈(45%) / 四周折返(30%) / 全图随机(25%)</summary>
        void RollPath(Flock f)
        {
            int roll=_rng.Next(100);
            f.Path = roll<45?BirdPath.Circle : roll<75?BirdPath.Diagonal : BirdPath.Random;
            if (f.Path==BirdPath.Circle)
            {   // 100 层：每层不同直径与高度，铺满整个天空而非集中主大陆
                f.Center=f.Anchor;
                f.Layer=_rng.Next(100);
                float t=f.Layer/99f;
                f.Radius=14f+t*(Range*0.92f);     // 直径随层递增
                f.Height=16f+t*70f;               // 高度随层递增（16~86）
            }
            else if (f.Path==BirdPath.Diagonal)
            {   // 四周折返：从本群锚点飞向 8 个地图边缘方向之一，再往返
                f.Center=f.Anchor;
                float a=_rng.Next(8)*Mathf.PI/4f;
                float rr=Range*(0.75f+0.25f*(float)_rng.NextDouble());
                f.Corner=new Vector3(Mathf.Cos(a)*rr,24f+(float)_rng.NextDouble()*56f,Mathf.Sin(a)*rr);
                f.PingT=(float)_rng.NextDouble(); f.PingForward=true;
                f.PingSpeed=0.018f+(float)_rng.NextDouble()*0.02f;
            }
            else
            {   // 全图随机游荡
                f.RandCenter=f.Anchor; f.RandTarget=RandomWaypoint(); f.RandCd=0f;
                f.Height=20f+(float)_rng.NextDouble()*60f;
            }
        }

        Vector3 RandomWaypoint()
        {   // 全图随机航点（以地图中心为原点，覆盖整张地图而非主村周边）
            float ang=(float)_rng.NextDouble()*Mathf.PI*2f, rr=12f+(float)_rng.NextDouble()*(Range-12f);
            return new Vector3(Mathf.Cos(ang)*rr,0f,Mathf.Sin(ang)*rr);
        }
        Vector3 RandomMapPoint()=>RandomWaypoint();

        void SpawnLone()
        {
            var b=MakeBird(); b.Lone=true;
            var p=RandomWaypoint();
            b.LonePos=new Vector3(p.x,Height0(),p.z);
            b.LoneDir=new Vector3((float)_rng.NextDouble()-0.5f,0,(float)_rng.NextDouble()-0.5f).normalized;
            _all.Add(b);
        }

        float Height0()=>30f+(float)_rng.NextDouble()*26f;

        // V6.3.1 六品种剪影参数：body 椭球、翅长/宽、尾型(0平/1叉/2扇/3楔)、冠羽、长腿、颈长
        struct Spec{public Vector3 Body;public float WLen,WWid;public int Tail;public bool Crest,Legs,LongNeck;public float SizeBias;
            public Spec(Vector3 b,float wl,float ww,int tail,bool crest,bool legs,bool neck,float sb){Body=b;WLen=wl;WWid=ww;Tail=tail;Crest=crest;Legs=legs;LongNeck=neck;SizeBias=sb;}}
        Spec[] Specs={
            new(new Vector3(0.16f,0.15f,0.40f),0.80f,0.26f,0,false,false,false,0.80f), // 0 鸣雀（小）
            new(new Vector3(0.15f,0.13f,0.44f),1.05f,0.22f,1,false,false,false,0.85f), // 1 家燕（叉尾窄翅）
            new(new Vector3(0.22f,0.20f,0.42f),0.92f,0.34f,2,false,false,false,1.05f), // 2 鸠鸽（圆身宽翅扇尾）
            new(new Vector3(0.26f,0.22f,0.52f),1.15f,0.40f,3,false,false,false,1.35f), // 3 猛禽（大展翅指羽楔尾）
            new(new Vector3(0.18f,0.16f,0.46f),1.00f,0.24f,0,false,true,true,1.10f),    // 4 涉禽（长腿长颈直翅）
            new(new Vector3(0.21f,0.19f,0.46f),0.90f,0.30f,3,false,false,false,1.00f), // 5 鸦/椋
        };

        Bird MakeBird()
        {
            var go=new GameObject("Bird");go.transform.SetParent(_root);
            int species=_rng.Next(Specs.Length);
            var sp=Specs[species];
            RollPlumage(species,out Color bodyC,out Color wingC);
            Material bodyMat=MatOf(bodyC), wingMat=MatOf(wingC), darkMat=MatOf(new Color(0.06f,0.06f,0.07f)), beakMat=MatOf(new Color(0.86f,0.72f,0.36f));
            // —— LV2：旧精模下沉为中景 ——
            var lv2=new GameObject("LV2");lv2.transform.SetParent(go.transform,false);
            Transform wl2,wr2; BuildBirdSimple(lv2.transform,sp,bodyMat,wingMat,out wl2,out wr2);
            // —— LV3：品种化近景完全体 ——
            var lv3=new GameObject("LV3");lv3.transform.SetParent(go.transform,false);
            Transform wl,wr; BuildBirdDetailed(lv3.transform,sp,bodyMat,wingMat,darkMat,beakMat,out wl,out wr);
            float scale=(0.5f+(float)_rng.NextDouble()*2.5f)*sp.SizeBias; // 0.5~3 随机 × 品种基准
            go.transform.localScale=Vector3.one*scale;
            var lod=LODKit.Attach(go,0.8f*scale,2,0.01f,0.003f,"bird",100);
            return new Bird{
                Lod=lod,Root=go.transform,WL=wl,WR=wr,WL2=wl2,WR2=wr2,Color=bodyC,Scale=scale,Species=species,
                LifeYears=5f+(float)_rng.NextDouble()*15f,
                BirthYear=CurrentYear(),Flap=(float)_rng.NextDouble()*Mathf.PI*2f,
            };
        }

        // LV2（=V6.2.2 精模）：流线球身+头+尾+后掠V翅
        void BuildBirdSimple(Transform host,Spec sp,Material bodyMat,Material wingMat,out Transform wl,out Transform wr)
        {
            Part(host,PrimitiveType.Sphere,Vector3.zero,sp.Body,bodyMat);
            Part(host,PrimitiveType.Sphere,new Vector3(0,0.03f,sp.Body.z*0.6f),Vector3.one*sp.Body.x*0.72f,bodyMat);
            Part(host,PrimitiveType.Cube,new Vector3(0,0.01f,-sp.Body.z*0.62f),new Vector3(0.10f,0.03f,0.22f),wingMat);
            wl=MakeWing(host,-1,sp,wingMat,false); wr=MakeWing(host,1,sp,wingMat,false);
        }

        // LV3：喙/眼/胸腹羽色/初级飞羽分叉/品种尾形/冠羽/长腿长颈
        void BuildBirdDetailed(Transform host,Spec sp,Material bodyMat,Material wingMat,Material darkMat,Material beakMat,out Transform wl,out Transform wr)
        {
            float bz=sp.Body.z;
            Part(host,PrimitiveType.Sphere,Vector3.zero,sp.Body,bodyMat);                                  // 身
            Part(host,PrimitiveType.Sphere,new Vector3(0,0.03f,bz*0.6f),Vector3.one*sp.Body.x*0.72f,bodyMat); // 头
            if(sp.LongNeck) Part(host,PrimitiveType.Cylinder,new Vector3(0,0.10f,bz*0.42f),new Vector3(0.05f,0.18f,0.05f),bodyMat); // 长颈
            // 喙（小锥，朝 +Z）
            var beak=Part(host,PrimitiveType.Cylinder,new Vector3(0,0.02f,bz*0.92f),new Vector3(0.035f,0.12f,0.035f),beakMat);
            beak.transform.localRotation=Quaternion.Euler(90,0,0);
            // 双眼
            Part(host,PrimitiveType.Sphere,new Vector3( sp.Body.x*0.42f,0.09f,bz*0.66f),Vector3.one*0.028f,darkMat);
            Part(host,PrimitiveType.Sphere,new Vector3(-sp.Body.x*0.42f,0.09f,bz*0.66f),Vector3.one*0.028f,darkMat);
            // 胸腹浅色羽块
            Part(host,PrimitiveType.Sphere,new Vector3(0,-sp.Body.y*0.45f,0.02f),new Vector3(sp.Body.x*0.7f,sp.Body.y*0.5f,sp.Body.z*0.8f),wingMat);
            // 品种尾形
            BuildTail(host,sp,wingMat);
            // 冠羽
            if(sp.Crest) Part(host,PrimitiveType.Cube,new Vector3(0,sp.Body.x*0.9f,bz*0.5f),new Vector3(0.04f,0.14f,0.04f),wingMat);
            // 长腿（涉禽）
            if(sp.Legs)for(int sx=-1;sx<=1;sx+=2) Part(host,PrimitiveType.Cylinder,new Vector3(0.05f*sx,-0.18f,-0.02f),new Vector3(0.02f,0.22f,0.02f),darkMat);
            wl=MakeWing(host,-1,sp,wingMat,true); wr=MakeWing(host,1,sp,wingMat,true);
        }

        void BuildTail(Transform host,Spec sp,Material mat)
        {
            float bz=sp.Body.z;
            if(sp.Tail==1) // 叉尾（燕）：两片 V 张开
                for(int sx=-1;sx<=1;sx+=2) Part(host,PrimitiveType.Cube,new Vector3(0.06f*sx,0.01f,-bz*0.7f),new Vector3(0.02f,0.02f,0.30f),mat).transform.localRotation=Quaternion.Euler(0,sx*18f,0);
            else if(sp.Tail==2) // 扇尾（鸽）：5 片小羽扇形
                for(int i=-2;i<=2;i++) Part(host,PrimitiveType.Cube,new Vector3(i*0.04f,0.01f,-bz*0.66f),new Vector3(0.05f,0.02f,0.20f),mat).transform.localRotation=Quaternion.Euler(0,i*14f,0);
            else if(sp.Tail==3) // 楔尾（猛禽/鸦）
                Part(host,PrimitiveType.Cube,new Vector3(0,0.01f,-bz*0.72f),new Vector3(0.12f,0.03f,0.34f),mat);
            else Part(host,PrimitiveType.Cube,new Vector3(0,0.01f,-bz*0.66f),new Vector3(0.10f,0.03f,0.22f),mat);
        }

        GameObject Part(Transform host,PrimitiveType pt,Vector3 pos,Vector3 scale,Material mat)
        {
            var g=GameObject.CreatePrimitive(pt); Kill(g);
            g.transform.SetParent(host,false);g.transform.localPosition=pos;g.transform.localScale=scale;
            g.GetComponent<Renderer>().sharedMaterial=mat; return g;
        }

        // 后掠 V 形翅；detailed=LV3 时翅端再分 3 片初级飞羽
        Transform MakeWing(Transform parent,int side,Spec sp,Material mat,bool detailed)
        {
            var pivot=new GameObject("WingPivot");pivot.transform.SetParent(parent,false);
            pivot.transform.localPosition=new Vector3(0.09f*side,0.03f,0.02f);
            float wlen=sp.WLen, wwid=sp.WWid;
            var w=GameObject.CreatePrimitive(PrimitiveType.Cube);Kill(w);
            w.transform.SetParent(pivot.transform);
            w.transform.localPosition=new Vector3(0.44f*wlen*side,0f,-0.12f);
            w.transform.localRotation=Quaternion.Euler(0f,side*38f,0f);
            w.transform.localScale=new Vector3(wlen,0.018f,wwid);
            w.GetComponent<Renderer>().sharedMaterial=mat;
            if(detailed) // 初级飞羽：翅端 3 片分叉小羽（猛禽更明显）
                for(int i=0;i<3;i++){
                    var f=GameObject.CreatePrimitive(PrimitiveType.Cube);Kill(f);
                    f.transform.SetParent(pivot.transform);
                    float spread=(i-1)*0.10f;
                    f.transform.localPosition=new Vector3(side*(0.86f*wlen+i*0.06f),0f,-0.16f+spread);
                    f.transform.localRotation=Quaternion.Euler(0f,side*(38f+i*8f),0f);
                    f.transform.localScale=new Vector3(0.22f,0.014f,wwid*0.42f);
                    f.GetComponent<Renderer>().sharedMaterial=mat;
                }
            return pivot.transform;
        }

        // 自然羽色调色板（低饱和真实鸟色，杜绝荧光色）；70% 纯色，30% 翅身混色
        static readonly Color[] Plumage = {
            new(0.32f,0.36f,0.46f), // V7.0.1 炭黑→石板蓝
            new(0.54f,0.58f,0.64f), // 深灰
            new(0.72f,0.74f,0.76f), // 岩灰
            new(0.96f,0.94f,0.88f), // 米白（鸽/鸥）
            new(0.74f,0.54f,0.34f), // 暖褐
            new(0.62f,0.42f,0.26f), // 栗褐
            new(0.44f,0.64f,0.86f), // 天蓝
            new(0.38f,0.68f,0.80f), // 青蓝
            new(0.94f,0.52f,0.34f), // 珊瑚橙
            new(0.38f,0.76f,0.54f), // 翠青
        };
        void RollPlumage(out Color body,out Color wing){ RollPlumage(-1,out body,out wing); }
        void RollPlumage(int species,out Color body,out Color wing)
        {
            // 品种偏好色域：猛禽褐麻(4,5,6)、燕蓝灰(6,7,8,1)、涉禽白灰(3,2,9)、其余全色
            int[] pool = species==3? new[]{4,5,6,1} : species==1? new[]{6,7,8,0} : species==4? new[]{3,2,9,0} : null;
            Color Pick(){ Color c= pool!=null?Plumage[pool[_rng.Next(pool.Length)]]:Plumage[_rng.Next(Plumage.Length)];
                float v=0.92f+(float)_rng.NextDouble()*0.16f;
                return new Color(Mathf.Clamp01(c.r*v),Mathf.Clamp01(c.g*v),Mathf.Clamp01(c.b*v)); }
            body=Pick();
            if(_rng.NextDouble()<0.3f){ Color w=Pick(); wing=Color.Lerp(body,w,0.6f); } else wing=Color.Lerp(body,Color.white,0.18f);
        }
        // 颜色量化后缓存材质，避免每鸟一个材质实例
        Material MatOf(Color c)
        {
            int key=((Mathf.RoundToInt(c.r*31))<<10)|(Mathf.RoundToInt(c.g*31)<<5)|Mathf.RoundToInt(c.b*31);
            if(_matCache.TryGetValue(key,out var m))return m;
            var nm=ShaderHelper.Mat(c); _matCache[key]=nm;return nm;
        }
        static void Kill(GameObject g){var c=g.GetComponent<Collider>();if(c)Object.Destroy(c);}
        static float CurrentYear()=>GameManager.Instance!=null?GameManager.Instance.State.Year:0;

        // ---------- 队形 ----------
        static Vector3 FormationOffset(BirdFormation f,int slot,int total)
        {
            float t=total<=1?0f:slot/(float)(total-1);
            float mid=(total-1)*0.5f;
            switch(f)
            {
                case BirdFormation.I:    return new Vector3(0,0,(slot-mid)*1.7f);
                case BirdFormation.Line: return new Vector3((slot-mid)*1.7f,0,0);
                case BirdFormation.V:
                    if(slot==0)return Vector3.zero;
                    {int row=(slot+1)/2;float side=(slot%2==1)?-1f:1f;return new Vector3(side*1.35f*row,0,1.65f*row);}
                case BirdFormation.S: return new Vector3(Mathf.Sin(t*Mathf.PI*2f)*2.3f,0,(t-0.5f)*total*1.6f);
                case BirdFormation.W: return new Vector3(((slot%2==0)?1f:-1f)*1.25f,0,(slot-mid)*1.6f);
                case BirdFormation.L:
                {   // 前半段沿 z 竖列，到转折点后沿 x 横列，组成 L
                    int half=Mathf.Max(1,total/2);
                    if(slot<half) return new Vector3(0,0,(slot-mid)*1.7f);
                    float turnZ=(half-1-mid)*1.7f;
                    return new Vector3((slot-half+1)*1.7f,0,turnZ);
                }
                case BirdFormation.O:
                    {float a=t*Mathf.PI*2f,rr=total*0.34f;return new Vector3(Mathf.Cos(a)*rr,0,Mathf.Sin(a)*rr);}
            }
            return Vector3.zero;
        }

        void Update()
        {
            if(_root==null)return;
            float dt=Mathf.Min(0.05f,Time.unscaledDeltaTime);
            float nowYear=CurrentYear();

            // 每 30 秒现实时间换队形
            _formTimer+=dt;
            if(_formTimer>=FormationSwitchSeconds){_formTimer=0f;foreach(var f in _flocks)f.Form=(BirdFormation)Random.Range(0,7);}
            // 飞行路线每 5 分钟（300 秒）整群随机重选一遍
            _pathTimer+=dt;
            if(_pathTimer>=PathSwitchSeconds){_pathTimer=0f;foreach(var f in _flocks)RollPath(f);}

            // 群鸟飞行：转圈 / 四周折返 / 全图随机
            var detach=new List<Bird>();
            foreach(var fl in _flocks)
            {
                Vector3 lead; float heading; // 群头位置与前进朝向（弧度）
                if (fl.Path==BirdPath.Circle)
                {
                    fl.Phase+=fl.Speed*dt;
                    float ang=fl.Phase;
                    Vector3 drift=new(Mathf.Sin(Time.unscaledTime*0.05f+fl.DriftPhase)*6f,0,Mathf.Cos(Time.unscaledTime*0.04f+fl.DriftPhase)*6f);
                    Vector3 c=fl.Center+drift;
                    lead=new(c.x+Mathf.Cos(ang)*fl.Radius, fl.Height+Mathf.Sin(Time.unscaledTime*0.6f+fl.Phase)*1.2f, c.z+Mathf.Sin(ang)*fl.Radius);
                    heading=ang+Mathf.PI*0.5f; // 沿圆周切线
                }
                else if (fl.Path==BirdPath.Diagonal)
                {   // 中心 ↔ 四角之一往返
                    if(fl.PingForward){fl.PingT+=fl.PingSpeed*dt;if(fl.PingT>=1f){fl.PingT=1f;fl.PingForward=false;}}
                    else{fl.PingT-=fl.PingSpeed*dt;if(fl.PingT<=0f){fl.PingT=0f;fl.PingForward=true;}}
                    float tt=fl.PingT*fl.PingT*(3f-2f*fl.PingT);
                    Vector3 now=Vector3.Lerp(fl.Center,fl.Corner,tt);
                    lead=new(now.x,fl.Height+Mathf.Sin(Time.unscaledTime*0.6f+fl.Phase)*1.0f,now.z);
                    Vector3 dir=fl.Corner-fl.Center;
                    float sx=fl.PingForward?dir.x:-dir.x, sz=fl.PingForward?dir.z:-dir.z;
                    heading=Mathf.Atan2(sz,sx);
                }
                else
                {   // 随机游荡：整群朝随机航点缓动，到达或定时换新点
                    fl.RandCd-=dt;
                    Vector3 to=fl.RandTarget-fl.RandCenter; float step=dt*9f;
                    if(to.sqrMagnitude<=step*step || fl.RandCd<=0f){fl.RandTarget=RandomWaypoint();fl.RandCd=6f+(float)_rng.NextDouble()*8f;to=fl.RandTarget-fl.RandCenter;}
                    if(to.sqrMagnitude>1e-4f){to.Normalize();fl.RandCenter+=to*step;heading=Mathf.Atan2(to.z,to.x);}else heading=0f;
                    lead=new(fl.RandCenter.x,fl.Height+Mathf.Sin(Time.unscaledTime*0.6f+fl.Phase)*1.0f,fl.RandCenter.z);
                }

                // 目标队形随编队类型/数量刷新，并按群头朝向旋转局部偏移
                float cosA=Mathf.Cos(heading),sinA=Mathf.Sin(heading),yaw=heading*Mathf.Rad2Deg;
                for(int i=0;i<fl.Birds.Count;i++)
                    fl.Birds[i].FormTarget=FormationOffset(fl.Form,i,fl.Birds.Count);
                foreach(var b in fl.Birds)
                {
                    b.Form=Vector3.Lerp(b.Form,b.FormTarget,dt*3f);
                    var fm=b.Form;
                    Vector3 off=new(fm.x*cosA-fm.z*sinA,0,fm.x*sinA+fm.z*cosA);
                    b.Root.position=lead+off;
                    b.Root.rotation=Quaternion.Euler(0,yaw,0);
                    if(b.Lod==null||b.Lod.Tick(dt))Flap(b,dt);
                    // 离群概率（约每秒 0.8%），离群后全图游荡、仍可重新入群
                    if(_rng.NextDouble()<dt*0.008) detach.Add(b);
                }
                foreach(var db in detach){ if(fl.Birds.Contains(db)) Detach(db,fl); }
                detach.Clear();
            }
            // 孤鸟：游荡 + 视野内入群
            for(int i=_all.Count-1;i>=0;i--)
            {
                var b=_all[i];
                if(!b.Lone)continue;
                b.TurnCd-=dt;
                if(b.TurnCd<=0){b.TurnCd=1.5f+Random.value*2f;b.LoneDir=Quaternion.Euler(0,Random.Range(-50f,50f),0)*b.LoneDir;}
                b.LonePos+=b.LoneDir*dt*7f;
                // 四周折返：到地图边界即反射，不再被拉回中央主大陆
                float lim=Range;
                if(b.LonePos.x> lim&&b.LoneDir.x>0)b.LoneDir.x=-b.LoneDir.x;
                if(b.LonePos.x<-lim&&b.LoneDir.x<0)b.LoneDir.x=-b.LoneDir.x;
                if(b.LonePos.z> lim&&b.LoneDir.z>0)b.LoneDir.z=-b.LoneDir.z;
                if(b.LonePos.z<-lim&&b.LoneDir.z<0)b.LoneDir.z=-b.LoneDir.z;
                b.LonePos.x=Mathf.Clamp(b.LonePos.x,-lim,lim);b.LonePos.z=Mathf.Clamp(b.LonePos.z,-lim,lim);
                b.LonePos.y=Mathf.Lerp(b.LonePos.y,Height0(),dt);
                b.Root.position=b.LonePos;
                b.Root.rotation=Quaternion.LookRotation(b.LoneDir);
                if(b.Lod==null||b.Lod.Tick(dt))Flap(b,dt);
                TryJoin(b);
            }
            // 寿命（游戏年份）与补充
            for(int i=_all.Count-1;i>=0;i--)
            {
                var b=_all[i];
                if(nowYear-b.BirthYear>=b.LifeYears){RemoveBird(b,i);}
            }
            // 维持种群：低于 80 上限则缓慢补群
            if(_all.Count<MaxBirds*0.7f && Random.value<dt*0.25f)
            {
                if(_flocks.Count<48)SpawnFlock(); else SpawnLone();
            }
        }

        void Flap(Bird b,float dt)
        {
            b.Flap+=dt*11f;float f=Mathf.Sin(b.Flap)*30f+6f; // 6° 静息上反角，扇动 ±30°
            if(b.WL)b.WL.localRotation=Quaternion.Euler(0,0,f);
            if(b.WR)b.WR.localRotation=Quaternion.Euler(0,0,-f);
            if(b.WL2)b.WL2.localRotation=Quaternion.Euler(0,0,f);
            if(b.WR2)b.WR2.localRotation=Quaternion.Euler(0,0,-f);
        }

        int CountLone(){int c=0;foreach(var b in _all)if(b.Lone)c++;return c;}
        void Detach(Bird b,Flock fl)
        {
            if(CountLone()>=40)return;
            fl.Birds.Remove(b); b.Flock=null; b.Lone=true;
            b.LonePos=b.Root!=null?b.Root.position:Vector3.zero;
            b.LoneDir=new Vector3((float)_rng.NextDouble()-0.5f,0,(float)_rng.NextDouble()-0.5f).normalized;
        }

        void TryJoin(Bird b)
        {
            Flock best=null;float bestD=JoinRange;
            foreach(var f in _flocks)
            {
                if(f.Birds.Count>=FlockCap)continue;
                foreach(var fb in f.Birds)
                {
                    float d=(fb.Root.position-b.Root.position).sqrMagnitude;
                    if(d<bestD*bestD){bestD=Mathf.Sqrt(d);best=f;}
                }
            }
            if(best!=null)
            {
                b.Lone=false;b.Flock=best;
                b.FormTarget=FormationOffset(best.Form,best.Birds.Count,best.Birds.Count+1);
                best.Birds.Add(b);
            }
        }

        void RemoveBird(Bird b,int idx)
        {
            if(b.Flock!=null)b.Flock.Birds.Remove(b);
            if(b.Root!=null)Object.Destroy(b.Root.gameObject);
            _all.RemoveAt(idx);
            // 清理空群
            for(int i=_flocks.Count-1;i>=0;i--)if(_flocks[i].Birds.Count==0)_flocks.RemoveAt(i);
        }

        public void Clear()
        {
            if(_root!=null)Object.Destroy(_root.gameObject);
            _flocks.Clear();_all.Clear();
            foreach(var m in _matCache.Values)if(m!=null)Object.Destroy(m);
            _matCache.Clear();
        }
    }
}

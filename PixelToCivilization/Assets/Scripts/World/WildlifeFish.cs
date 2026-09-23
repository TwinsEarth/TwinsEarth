using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;

using PixelToCivilization.Rendering;

namespace PixelToCivilization.World
{
    /// <summary>四种鱼群编队（I字/一字/V字/S字）</summary>
    public enum FishFormation { I, Line, V, S }
    /// <summary>巡游路线：转圈（大/中/小圈 × 露头/浅游/潜游）、对角往返（中心↔四角）、随机游；每 5 分钟重选</summary>
    public enum FishPath { Circle, Diagonal, Random }
    /// <summary>泳层：露头（背鳍露出水面）/浅游/潜游</summary>
    public enum FishDepth { Surface, Shallow, Deep }

    /// <summary>
    /// V6.1.3 鱼群系统（对标飞鸟群，活动于水中）：
    /// ·每尾鱼随机纯色或背腹混色、体型 0.5~3 倍、4 种形态品种（纺锤/扁鲳/长条/金鱼），寿命 5~20 游戏年，全图上限 100 尾；
    /// ·每群 2~5 尾，孤鱼在视野(16)内看到鱼群自动加入（群上限 5）；
    /// ·每 50 秒现实时间随机切换 I/一/V/S 队形，平滑过渡；
    /// ·转圈游分大/中/小圈与露头/浅游/潜游三泳层，对角游为水域中心↔四角往返，随机游在水中漫游；路线每 5 分钟整群重选。
    /// 纯视觉层：移动/编队用现实时间(unscaled)，寿命用游戏年份。
    /// </summary>
    public class WildlifeFish : MonoBehaviour
    {
        class Fish
        {
            public LodAgent Lod;
            public Transform Root, Tail, Tail2; public Vector3 Form, FormTarget;
            public float Swim, Scale, LifeYears, BirthYear; public School School; public int Kind;
            public bool Lone; public Vector3 LonePos, LoneDir; public float TurnCd;
        }
        class School
        {
            public readonly List<Fish> Fishes=new();
            public FishFormation Form; public FishPath Path; public FishDepth Depth;
            public Vector3 Center; public float Radius, Y, Speed, Phase;
        public float YOff;   // V6.1.9(i) 相对水面的泳层偏移（随潮汐连续升降）
            public Vector3 Corner; public float PingT, PingSpeed=0.05f; public bool PingForward=true;
            public Vector3 RandCenter, RandTarget; public float RandCd;
        }

        public const int MaxFish = 300;    // V6.2.2 上限上调到300（最近30条完全体，其余简化体）
        const float FormationSwitchSeconds = 50f;   // 现实时间：编队切换
        const float PathSwitchSeconds = 300f;       // 现实时间：路线 5 分钟重选
        const float JoinRange = 16f;
        const int SchoolCap = 5;

        readonly List<School> _schools=new();
        readonly List<Fish> _all=new();
        readonly List<Vector3> _waters=new();
        Transform _root;
        WorldGenerator _terrain;
        Vector3 _home;
        System.Random _rng;
        float _formTimer,_pathTimer;
        readonly Dictionary<int,Material> _matCache=new();
        static float WaterY => GameConstants.WaterLevel;
        // V6.1.9(i) 潮汐水面高度 / 洋流洄游漂移
        float SurfY(float x,float z){ var t=PixelToCivilization.Systems.TideSystem.Instance; return t!=null?t.SurfaceY(x,z):GameConstants.WaterLevel; }
        Vector2 CurrentDrift(float x,float z,float dt){ var c=PixelToCivilization.Systems.OceanCurrentSystem.Instance; return c!=null?c.Drift(x,z,dt,1.6f):Vector2.zero; }

        public void Init(Vector3 villageCenter, WorldGenerator terrain, int schools=5)
        {
            Clear();
            _home=villageCenter; _terrain=terrain;
            _rng=new System.Random(System.Environment.TickCount ^ 0xF15);
            _root=new GameObject("Fish").transform; _root.SetParent(transform);
            _formTimer=0f; _pathTimer=0f;
            CollectWaterAnchors();
            int sc=Mathf.Clamp(schools<=0?22:schools,16,32);
            for(int i=0;i<sc;i++) SpawnSchool();
            int lone=2+_rng.Next(3);
            for(int i=0;i<lone && _all.Count<MaxFish;i++) SpawnLone();
        }

        // 在村址周边 / 全图采样一批真实水格作为鱼群活动锚点
        void CollectWaterAnchors()
        {
            _waters.Clear();
            int tries=0;
            while(_waters.Count<10 && tries++<400)
            {
                float ang=(float)_rng.NextDouble()*Mathf.PI*2f;
                float rr = _waters.Count<6 ? 28f+(float)_rng.NextDouble()*95f : (float)_rng.NextDouble()*210f;
                float x=_home.x+Mathf.Cos(ang)*rr, z=_home.z+Mathf.Sin(ang)*rr;
                if(_terrain!=null && _terrain.IsWater(x,z)) _waters.Add(new Vector3(x,0,z));
            }
            if(_waters.Count==0) _waters.Add(new Vector3(_home.x+30f,0,_home.z+30f)); // 兜底
        }
        Vector3 PickWater()
        {
            // 优先从锚点附近扰动出一个水点
            for(int t=0;t<12;t++)
            {
                var b=_waters[_rng.Next(_waters.Count)];
                float x=b.x+((float)_rng.NextDouble()-0.5f)*30f, z=b.z+((float)_rng.NextDouble()-0.5f)*30f;
                if(_terrain==null||_terrain.IsWater(x,z)) return new Vector3(x,0,z);
            }
            return _waters[_rng.Next(_waters.Count)];
        }

        // ---------- 生成 ----------
        void SpawnSchool()
        {
            if(_all.Count>=MaxFish)return;
            var s=new School{
                Center=PickWater(),
                Speed=0.05f+(float)_rng.NextDouble()*0.05f,
                Phase=(float)_rng.NextDouble()*Mathf.PI*2f,
                Form=(FishFormation)_rng.Next(4),
            };
            RollPath(s);
            int n=2+_rng.Next(4); // 2~5
            for(int i=0;i<n && _all.Count<MaxFish;i++)
            {
                var f=MakeFish(); f.School=s; f.FormTarget=FormationOffset(s.Form,i,n); f.Form=f.FormTarget;
                s.Fishes.Add(f); _all.Add(f);
            }
            _schools.Add(s);
        }

        void RollPath(School s)
        {
            s.Center=PickWater();
            s.Path=(FishPath)_rng.Next(3);
            s.Depth=(FishDepth)_rng.Next(3);
            s.Y = s.Depth==FishDepth.Surface ? WaterY+0.10f
                : s.Depth==FishDepth.Shallow ? WaterY-0.42f : WaterY-1.25f;
            s.YOff = s.Depth==FishDepth.Surface ? 0.10f : s.Depth==FishDepth.Shallow ? -0.42f : -1.25f;
            if(s.Path==FishPath.Circle)
            {   // 小圈 6~12 / 中圈 13~20 / 大圈 21~30
                int size=_rng.Next(3);
                s.Radius = size==0? 6f+(float)_rng.NextDouble()*6f
                         : size==1? 13f+(float)_rng.NextDouble()*7f
                         :         21f+(float)_rng.NextDouble()*9f;
            }
            else if(s.Path==FishPath.Diagonal)
            {
                int corner=_rng.Next(4);
                Vector3 dir = corner==0?new Vector3( 1,0, 1):corner==1?new Vector3(-1,0, 1)
                            :corner==2?new Vector3( 1,0,-1):new Vector3(-1,0,-1);
                float range=16f+(float)_rng.NextDouble()*26f;
                s.Corner=s.Center+dir.normalized*range;
                s.PingT=(float)_rng.NextDouble(); s.PingForward=true;
                s.PingSpeed=0.035f+(float)_rng.NextDouble()*0.03f;
                s.Y=WaterY-0.42f; s.YOff=-0.42f; // 对角统一浅游层
            }
            else { s.RandCenter=s.Center; s.RandTarget=PickWater(); s.RandCd=0f; s.Y=WaterY-0.5f; s.YOff=-0.5f; }
        }

        Vector3 RandomWaypoint()
        {
            Vector3 p=PickWater();
            // 控制在中心周边，避免离群太远
            Vector3 c=_schools.Count>0?_schools[0].Center:_home;
            return Vector3.MoveTowards(c,p,46f);
        }

        void SpawnLone()
        {
            var f=MakeFish(); f.Lone=true; f.LonePos=PickWater(); f.LonePos.y=WaterY-0.5f;
            f.LoneDir=new Vector3((float)_rng.NextDouble()-0.5f,0,(float)_rng.NextDouble()-0.5f).normalized;
            _all.Add(f);
        }

        Fish MakeFish()
        {
            var go=new GameObject("Fish"); go.transform.SetParent(_root);
            int kind=_rng.Next(8);   // V6.3.1 八品种
            RollScaleColor(kind,out Color body,out Color fin);
            Material bm=MatOf(body), fm=MatOf(fin), dm=MatOf(new Color(0.05f,0.05f,0.06f));
            var (bs,ts)=KindShape(kind);
            // —— LV2：旧精模下沉为中景 ——
            var lv2=new GameObject("LV2");lv2.transform.SetParent(go.transform,false);
            Transform tail2=BuildFishSimple(lv2.transform,kind,bs,ts,bm,fm);
            // —— LV3：品种化近景完全体 ——
            var lv3=new GameObject("LV3");lv3.transform.SetParent(go.transform,false);
            Transform tail=BuildFishDetailed(lv3.transform,kind,bs,ts,bm,fm,dm,fin);
            float scale=0.5f+(float)_rng.NextDouble()*2.5f;
            go.transform.localScale=Vector3.one*scale;
            var lod=LODKit.Attach(go,0.6f*scale,2,0.01f,0.001f,"fish",30); // Lv3≥0.01且最近30条；Lv2 0.001-0.01；再远剔除
            return new Fish{
                Lod=lod,Root=go.transform,Tail=tail,Tail2=tail2,Kind=kind,Scale=scale,
                LifeYears=5f+(float)_rng.NextDouble()*15f, BirthYear=CurrentYear(),
                Swim=(float)_rng.NextDouble()*Mathf.PI*2f,
            };
        }
        // LV2（=V6.2.2 精模）
        Transform BuildFishSimple(Transform host,int kind,Vector3 bs,Vector3 ts,Material bm,Material fm)
        {
            FPart(host,PrimitiveType.Sphere,Vector3.zero,bs,bm);
            var tp=new GameObject("TailPivot");tp.transform.SetParent(host,false);
            tp.transform.localPosition=new Vector3(0,0,-bs.z*0.95f);
            FPart(tp.transform,PrimitiveType.Cube,new Vector3(0,0,-ts.z*0.5f),ts,fm);
            FPart(host,PrimitiveType.Cube,new Vector3(0,bs.y*0.9f,0),new Vector3(0.04f,bs.y*0.7f,bs.z*0.5f),fm);
            if(kind==3||kind==0)
            for(int sd=-1;sd<=1;sd+=2) FPart(host,PrimitiveType.Cube,new Vector3(bs.x*0.9f*sd,-bs.y*0.2f,bs.z*0.2f),new Vector3(bs.x*0.8f,0.02f,bs.z*0.4f),fm).transform.localRotation=Quaternion.Euler(0,0,sd*24f);
            return tp.transform;
        }
        // LV3：双眼/鳃盖/鳞纹/双层尾/胸鳍腹鳍臀鳍，按品种增减
        Transform BuildFishDetailed(Transform host,int kind,Vector3 bs,Vector3 ts,Material bm,Material fm,Material dm,Color finC)
        {
            FPart(host,PrimitiveType.Sphere,Vector3.zero,bs,bm);
            var tp=new GameObject("TailPivot");tp.transform.SetParent(host,false);
            tp.transform.localPosition=new Vector3(0,0,-bs.z*0.95f);
            float lobe=kind==3?1.3f:kind==6?1.25f:1f;
            FPart(tp.transform,PrimitiveType.Cube,new Vector3(0, ts.z*0.28f,-ts.z*0.5f),new Vector3(ts.x,0.02f,ts.z*lobe),fm);
            FPart(tp.transform,PrimitiveType.Cube,new Vector3(0,-ts.z*0.28f,-ts.z*0.5f),new Vector3(ts.x*0.8f,0.02f,ts.z*0.8f*lobe),fm);
            float dLen=kind==2?bs.z*0.9f:bs.z*0.5f;
            FPart(host,PrimitiveType.Cube,new Vector3(0,bs.y*0.95f,0),new Vector3(0.04f,bs.y*0.8f,dLen),fm);
            FPart(host,PrimitiveType.Cube,new Vector3(0,-bs.y*0.9f,-bs.z*0.2f),new Vector3(0.04f,bs.y*0.4f,bs.z*0.3f),fm);
            for(int sd=-1;sd<=1;sd+=2){
                FPart(host,PrimitiveType.Cube,new Vector3(bs.x*0.95f*sd,-bs.y*0.15f,bs.z*0.25f),new Vector3(bs.x*0.85f,0.02f,bs.z*0.42f),fm).transform.localRotation=Quaternion.Euler(0,0,sd*26f);
                FPart(host,PrimitiveType.Cube,new Vector3(bs.x*0.55f*sd,-bs.y*0.85f,-bs.z*0.05f),new Vector3(bs.x*0.5f,0.02f,bs.z*0.28f),fm).transform.localRotation=Quaternion.Euler(0,0,sd*18f);
            }
            FPart(host,PrimitiveType.Sphere,new Vector3( bs.x*0.55f,bs.y*0.35f,bs.z*0.72f),Vector3.one*bs.x*0.16f,dm);
            FPart(host,PrimitiveType.Sphere,new Vector3(-bs.x*0.55f,bs.y*0.35f,bs.z*0.72f),Vector3.one*bs.x*0.16f,dm);
            for(int sd=-1;sd<=1;sd+=2) FPart(host,PrimitiveType.Cube,new Vector3(bs.x*1.0f*sd,0,bs.z*0.42f),new Vector3(0.02f,bs.y*1.1f,bs.z*0.05f),dm);
            Material sm=MatOf(Color.Lerp(finC,Color.black,0.25f));
            for(int i=0;i<2;i++){float zz=bs.z*(0.15f-i*0.35f);
                FPart(host,PrimitiveType.Cube,new Vector3(0,bs.y*0.98f,zz),new Vector3(bs.x*1.7f,0.02f,0.03f),sm);}
            if(kind==3)FPart(host,PrimitiveType.Sphere,new Vector3(0,bs.y*1.05f,bs.z*0.55f),Vector3.one*bs.x*0.5f,fm);
            if(kind==5)for(int i=0;i<5;i++){float a=i/5f*Mathf.PI*2f;FPart(host,PrimitiveType.Cube,new Vector3(Mathf.Cos(a)*bs.x*0.9f,bs.y*0.6f,Mathf.Sin(a)*bs.z*0.9f),Vector3.one*0.05f,dm);}
            return tp.transform;
        }
        GameObject FPart(Transform host,PrimitiveType pt,Vector3 pos,Vector3 scale,Material mat)
        {
            var g=GameObject.CreatePrimitive(pt);Kill(g);
            g.transform.SetParent(host,false);g.transform.localPosition=pos;g.transform.localScale=scale;
            g.GetComponent<Renderer>().sharedMaterial=mat;return g;
        }
        static (Vector3 body,Vector3 tail) KindShape(int kind)=>kind switch
        {   // body 椭球, tail 尾鳍（局部，朝向 +Z）
            0 => (new Vector3(0.22f,0.16f,0.42f), new Vector3(0.26f,0.02f,0.22f)), // 普通纺锤
            1 => (new Vector3(0.34f,0.30f,0.34f), new Vector3(0.22f,0.02f,0.18f)), // 扁鲳
            2 => (new Vector3(0.12f,0.12f,0.66f), new Vector3(0.14f,0.02f,0.16f)), // 长条/鳗
            3 => (new Vector3(0.26f,0.24f,0.34f), new Vector3(0.34f,0.02f,0.30f)), // 金鱼大尾
            4 => (new Vector3(0.28f,0.24f,0.40f), new Vector3(0.20f,0.02f,0.18f)), // 鲷（侧扁）
            5 => (new Vector3(0.30f,0.30f,0.30f), new Vector3(0.14f,0.02f,0.12f)), // 魨（近球）
            6 => (new Vector3(0.24f,0.20f,0.58f), new Vector3(0.30f,0.02f,0.30f)), // 鲨（大纺锤歪尾）
            _ => (new Vector3(0.42f,0.12f,0.40f), new Vector3(0.16f,0.02f,0.30f)), // 鳐（菱形扁体细尾）
        };

        // 自然鱼鳞色调色板；70% 纯色，30% 背腹/鳍身混色
        static readonly Color[] Scales = {
            new(0.80f,0.86f,0.92f), // V7.0.1 银亮
            new(0.20f,0.66f,0.88f), // 艳青蓝
            new(1.00f,0.74f,0.20f), // 金橙
            new(0.95f,0.42f,0.32f), // 珊瑚红
            new(0.66f,0.54f,0.38f), // 暖石斑
            new(0.20f,0.38f,0.60f), // 深海蓝（替墨黑）
            new(0.66f,0.84f,0.30f), // 黄绿
            new(0.94f,0.92f,0.84f), // 奶白
        };
        void RollScaleColor(out Color body,out Color fin){ RollScaleColor(-1,out body,out fin); }
        void RollScaleColor(int kind,out Color body,out Color fin)
        {   // 品种色域偏好：金鱼/珊瑚(2,3,7)、鲨/长条偏冷(0,1,5)、鲷金(2,4)、鳐偏褐(4,5,7)
            int[] pool=kind==3?new[]{2,3,7}:kind==6||kind==2?new[]{0,1,5}:kind==4?new[]{2,4,6}:kind==7?new[]{4,5,7}:null;
            Color Pick(){ Color c=pool!=null?Scales[pool[_rng.Next(pool.Length)]]:Scales[_rng.Next(Scales.Length)]; float v=0.9f+(float)_rng.NextDouble()*0.2f;
                return new Color(Mathf.Clamp01(c.r*v),Mathf.Clamp01(c.g*v),Mathf.Clamp01(c.b*v)); }
            body=Pick();
            if(_rng.NextDouble()<0.3f){ Color f=Pick(); fin=Color.Lerp(body,f,0.55f); }
            else fin=Color.Lerp(body,Color.white,0.25f);
        }
        Material MatOf(Color c)
        {
            int key=((Mathf.RoundToInt(c.r*31))<<10)|(Mathf.RoundToInt(c.g*31)<<5)|Mathf.RoundToInt(c.b*31);
            if(_matCache.TryGetValue(key,out var m))return m;
            var nm=ShaderHelper.Mat(c); _matCache[key]=nm; return nm;
        }
        static void Kill(GameObject g){var c=g.GetComponent<Collider>();if(c)Object.Destroy(c);}
        static float CurrentYear()=>GameManager.Instance!=null?GameManager.Instance.State.Year:0;

        // ---------- 队形（水平面 x-z）----------
        static Vector3 FormationOffset(FishFormation f,int slot,int total)
        {
            float t=total<=1?0f:slot/(float)(total-1);
            float mid=(total-1)*0.5f;
            switch(f)
            {
                case FishFormation.I:    return new Vector3(0,0,(slot-mid)*1.1f);
                case FishFormation.Line: return new Vector3((slot-mid)*1.1f,0,0);
                case FishFormation.V:
                    if(slot==0)return Vector3.zero;
                    {int row=(slot+1)/2;float side=(slot%2==1)?-1f:1f;return new Vector3(side*0.9f*row,0,1.0f*row);}
                case FishFormation.S: return new Vector3(Mathf.Sin(t*Mathf.PI*2f)*1.4f,0,(t-0.5f)*total*1.0f);
            }
            return Vector3.zero;
        }

        void Update()
        {
            if(_root==null)return;
            float dt=Mathf.Min(0.05f,Time.unscaledDeltaTime);
            float nowYear=CurrentYear();

            _formTimer+=dt;
            if(_formTimer>=FormationSwitchSeconds){_formTimer=0f;foreach(var s in _schools)s.Form=(FishFormation)Random.Range(0,4);}
            _pathTimer+=dt;
            if(_pathTimer>=PathSwitchSeconds){_pathTimer=0f;foreach(var s in _schools)RollPath(s);}

            foreach(var s in _schools)
            {
                // V6.1.9(i) 洄游本能：鱼群整体沿洋流缓慢漂移（只在水中漂，登陆则撤销）
                var dv=CurrentDrift(s.Center.x,s.Center.z,dt);
                float nx=s.Center.x+dv.x,nz=s.Center.z+dv.y;
                if(_terrain==null||_terrain.IsWater(nx,nz)){s.Center=new Vector3(nx,s.Center.y,nz);s.Corner+=new Vector3(dv.x,0,dv.y);s.RandCenter+=new Vector3(dv.x,0,dv.y);}
                s.Y=SurfY(s.Center.x,s.Center.z)+s.YOff;   // 泳层随潮汐连续升降
                Vector3 lead; float heading;
                if(s.Path==FishPath.Circle)
                {
                    s.Phase+=s.Speed*dt; float ang=s.Phase;
                    lead=new(s.Center.x+Mathf.Cos(ang)*s.Radius,
                             s.Y+Mathf.Sin(Time.unscaledTime*0.8f+s.Phase)*0.06f,
                             s.Center.z+Mathf.Sin(ang)*s.Radius);
                    heading=ang+Mathf.PI*0.5f;
                }
                else if(s.Path==FishPath.Diagonal)
                {
                    if(s.PingForward){s.PingT+=s.PingSpeed*dt;if(s.PingT>=1f){s.PingT=1f;s.PingForward=false;}}
                    else{s.PingT-=s.PingSpeed*dt;if(s.PingT<=0f){s.PingT=0f;s.PingForward=true;}}
                    float tt=s.PingT*s.PingT*(3f-2f*s.PingT);
                    var now=Vector3.Lerp(s.Center,s.Corner,tt);
                    lead=new(now.x,s.Y,now.z);
                    var dir=s.Corner-s.Center;
                    heading=Mathf.Atan2(s.PingForward?dir.z:-dir.z,s.PingForward?dir.x:-dir.x);
                }
                else
                {
                    s.RandCd-=dt; Vector3 to=s.RandTarget-s.RandCenter; float step=dt*5.5f;
                    if(to.sqrMagnitude<=step*step||s.RandCd<=0f){s.RandTarget=PickWater();s.RandTarget.y=0;s.RandCd=5f+(float)_rng.NextDouble()*7f;to=s.RandTarget-s.RandCenter;}
                    if(to.sqrMagnitude>1e-4f){to.Normalize();s.RandCenter+=to*step;heading=Mathf.Atan2(to.z,to.x);}else heading=0f;
                    lead=new(s.RandCenter.x,s.Y,s.RandCenter.z);
                }

                float cosA=Mathf.Cos(heading),sinA=Mathf.Sin(heading),yaw=heading*Mathf.Rad2Deg;
                for(int i=0;i<s.Fishes.Count;i++) s.Fishes[i].FormTarget=FormationOffset(s.Form,i,s.Fishes.Count);
                foreach(var f in s.Fishes)
                {
                    f.Form=Vector3.Lerp(f.Form,f.FormTarget,dt*3f);
                    var fm=f.Form;
                    Vector3 off=new(fm.x*cosA-fm.z*sinA,0,fm.x*sinA+fm.z*cosA);
                    f.Root.position=lead+off;
                    f.Root.rotation=Quaternion.Euler(0,yaw,0);
                    if(f.Lod==null||f.Lod.Tick(dt))Swim(f,dt);
                }
            }
            // 孤鱼漫游 + 视野内入群
            for(int i=_all.Count-1;i>=0;i--)
            {
                var f=_all[i]; if(!f.Lone)continue;
                f.TurnCd-=dt;
                if(f.TurnCd<=0){f.TurnCd=1.5f+Random.value*2f;f.LoneDir=Quaternion.Euler(0,Random.Range(-50f,50f),0)*f.LoneDir;}
                f.LonePos+=f.LoneDir*dt*4.5f;
                // 离开水或离锚点太远则调头
                bool onWater=_terrain==null||_terrain.IsWater(f.LonePos.x,f.LonePos.z);
                if(!onWater) f.LoneDir=-f.LoneDir;
                var anchor=_waters.Count>0?_waters[0]:_home;
                if((f.LonePos-anchor).sqrMagnitude>90f*90f) f.LoneDir=(anchor-f.LonePos).normalized;
                f.LonePos.y=Mathf.Lerp(f.LonePos.y,SurfY(f.LonePos.x,f.LonePos.z)-0.5f,dt*2f);
                f.Root.position=f.LonePos; f.Root.rotation=Quaternion.LookRotation(f.LoneDir);
                if(f.Lod==null||f.Lod.Tick(dt))Swim(f,dt); TryJoin(f);
            }
            // 寿命（游戏年份）
            for(int i=_all.Count-1;i>=0;i--)
            {
                var f=_all[i];
                if(nowYear-f.BirthYear>=f.LifeYears) RemoveFish(f,i);
            }
            // 缓慢维持种群
            if(_all.Count<MaxFish*0.7f && Random.value<dt*0.2f)
            {
                if(_schools.Count<58) SpawnSchool(); else SpawnLone();
            }
        }

        void Swim(Fish f,float dt)
        {
            f.Swim+=dt*9f; float w=Mathf.Sin(f.Swim);
            if(f.Tail!=null) f.Tail.localRotation=Quaternion.Euler(0,w*28f,0); // 尾鳍左右摆动
            if(f.Tail2!=null) f.Tail2.localRotation=Quaternion.Euler(0,w*28f,0);
        }

        void TryJoin(Fish f)
        {
            School best=null; float bestD=JoinRange;
            foreach(var s in _schools)
            {
                if(s.Fishes.Count>=SchoolCap)continue;
                foreach(var o in s.Fishes)
                {
                    float d=(o.Root.position-f.Root.position).sqrMagnitude;
                    if(d<bestD*bestD){bestD=Mathf.Sqrt(d);best=s;}
                }
            }
            if(best!=null)
            {
                f.Lone=false; f.School=best;
                f.FormTarget=FormationOffset(best.Form,best.Fishes.Count,best.Fishes.Count+1);
                best.Fishes.Add(f);
            }
        }

        void RemoveFish(Fish f,int idx)
        {
            if(f.School!=null)f.School.Fishes.Remove(f);
            if(f.Root!=null)Object.Destroy(f.Root.gameObject);
            _all.RemoveAt(idx);
            for(int i=_schools.Count-1;i>=0;i--)if(_schools[i].Fishes.Count==0)_schools.RemoveAt(i);
        }

        public void Clear()
        {
            if(_root!=null)Object.Destroy(_root.gameObject);
            _schools.Clear();_all.Clear();_waters.Clear();
            foreach(var m in _matCache.Values)if(m!=null)Object.Destroy(m);
            _matCache.Clear();
        }
    }
}

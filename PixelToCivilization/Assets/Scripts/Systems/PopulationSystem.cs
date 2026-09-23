using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.World;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// 人口系统 —— 对齐 v5.9.9：住房供给、出生/死亡、年龄结构(儿童/青年/中年/老年)、社会阶层、个体游走。
    /// </summary>
    public class PopulationSystem : GameSystemBase
    {
        public float HousingCapacity;
        private float _birthNotifyCd;
        private bool _visualDirty;   // V7.0.2 年龄/时代变化后待重建外观
        private WorldGenerator _terrain;
        const float WalkSpeed=1.2f;   // 平民陆地游走速度（世界单位/秒）

        public override void Init(GameManager gm)
        {
            base.Init(gm);
            _terrain=Object.FindObjectOfType<WorldGenerator>();
        }

        public override void Tick(float dt)
        {
            // 住房 = 所有居住建筑 housing 之和 * 等级系数 + 我方船只提供的船舱居住（V6.1.1 船只居住属性）
            float h = 0;
            foreach (var b in S.Buildings) h += b.Def != null ? b.Def.GetFunc("housing") * b.LevelMult : 0;
            foreach (var ship in S.Ships) h += ship != null ? ship.EffectiveHousing : 0;
            foreach (var fleet in S.OceanFleets) h += fleet != null ? fleet.EffectiveHousing : 0;
            if (GM.Wonder != null) h += GM.Wonder.HousingAdd;   // V6.8.0 紫禁城等奇观住房
            HousingCapacity = h; S.Housing = h;
            S.MaxPop = Mathf.Max(100, Mathf.RoundToInt(h));

            // 个体游走
            UpdateAgents(dt);
            // V7.0.2 外观按需重建（实时帧内每体最多一次；跳年/快进只改数据，不在补算中刷视图）
            if(_visualDirty)
            {
                _visualDirty=false;
                var ag=S.Agents;
                for(int i=0;i<ag.Count;i++) GM.Env.SyncAgentView(ag[i]);
            }
        }

        public override void OnYear(int year)
        {
            // ===== 出生 =====
            if (S.GetRes("food") > 50 && S.Pop < S.Housing && S.Pop < GameConstants.MaxPop)
            {
                int oldPop = S.Pop;
                int birth = 3 + Mathf.FloorToInt(UnityEngine.Random.value*4);
                S.Pop = Mathf.Min(GameConstants.MaxPop, Mathf.FloorToInt(S.Housing), S.Pop+birth);
                int real = S.Pop - oldPop;
                S.Children += real;
                if (real > 0)
                {
                    int oldM = Mathf.FloorToInt(oldPop/50f), newM = Mathf.FloorToInt(S.Pop/50f);
                    if (newM > oldM && newM > 0) GM.AddEvent("good","👥 人口达到"+(newM*50)+"人");
                }
            }
            // ===== 年龄增长（每年约2%）=====
            int ageUp = Mathf.FloorToInt(S.Pop*0.02f);
            float t;
            t = Mathf.Min(S.Children, ageUp); S.Children-=t; S.Young+=t;
            t = Mathf.Min(S.Young, Mathf.FloorToInt(ageUp*0.5f)); S.Young-=t; S.Middle+=t;
            t = Mathf.Min(S.Middle, Mathf.FloorToInt(ageUp*0.3f)); S.Middle-=t; S.Old+=t;
            // 老年死亡
            if (S.Old > 0 && UnityEngine.Random.value < 0.3f)
            {
                int deaths = Mathf.Min(Mathf.RoundToInt(S.Old), 1+Mathf.FloorToInt(UnityEngine.Random.value*3));
                S.Old -= deaths; S.Pop = Mathf.Max(10, S.Pop-deaths);
            }
            NormalizeAge();
            AgeAndGrow();
        }

        /// <summary>V7.0.2 个体逐年成长（只更新数据）：幼→壮→老，寿尽轮回为同户新生孩童；视觉置脏，由 Tick 按需重建</summary>
        void AgeAndGrow()
        {
            var agents=S.Agents;
            for(int i=0;i<agents.Count;i++)
            {
                var a=agents[i];
                a.Age++;
                int st=PixelToCivilization.Actors.HumanoidFactory.StageOf(a.Age);
                if(st!=a.LifeStage) a.LifeStage=st;
                if(a.Age>a.LifeSpan)
                {   // 寿尽：以同户新生孩童闭环（继承家门职业/阶层/家园，换新颜色个体）
                    a.Age=UnityEngine.Random.Range(0,3); a.LifeStage=0;
                    a.ColorSeed=UnityEngine.Random.Range(1,999999); a.LifeSpan=UnityEngine.Random.Range(60,89);
                }
            }
            _visualDirty=true;
        }

        /// <summary>V7.0.2 跨时代全员换装（置脏，Tick 内每体按签名最多重建一次，避免跳时代连环重建卡顿）</summary>
        public override void OnEra(int newEra,int oldEra)
        {
            if(newEra!=oldEra) _visualDirty=true;
        }

        /// <summary>建造居住建筑后提升社会阶层（对齐 commoner+2 / rich+1）</summary>
        public void OnResidenceBuilt(string type)
        {
            if (type == "rich_house") { ShiftClass("slave","commoner",2); ShiftClass("commoner","rich",1); }
            else if (type == "noble_palace") { ShiftClass("commoner","rich",2); ShiftClass("rich","noble",1); }
        }
        private void ShiftClass(string from, string to, float amt)
        {
            float v = Mathf.Min(S.SocialClasses[from], amt);
            S.SocialClasses[from]-=v; S.SocialClasses[to]+=v;
        }
        /// <summary>
        /// 年龄结构统一为「人数」口径并归一到当前总人口：出生计入儿童、衰老在四桶间流转、老年死亡扣减，
        /// 每游戏年末把四项之和缩放对齐 S.Pop，避免“初始百分比(和=100) + 人数增减”混用导致结构长期漂移。
        /// </summary>
        public void NormalizeAge()
        {
            float sum = S.Children+S.Young+S.Middle+S.Old;
            if (sum <= 0 || S.Pop <= 0)
            {
                S.Children=S.Pop*0.25f; S.Young=S.Pop*0.35f; S.Middle=S.Pop*0.30f; S.Old=S.Pop*0.10f;
                return;
            }
            float k = S.Pop/sum;
            S.Children*=k; S.Young*=k; S.Middle*=k; S.Old*=k;
        }

        private void UpdateAgents(float dt)
        {
            var agents = S.Agents;
            for (int i = agents.Count-1; i >= 0; i--)
            {
                var a = agents[i];
                if (a.Boarded) continue; // V6.3.7 已登乘车船者随载具移动，不再陆地游走
                a.WanderTimer -= dt;
                float distHome=Mathf.Sqrt((a.X-a.HomeX)*(a.X-a.HomeX)+(a.Z-a.HomeZ)*(a.Z-a.HomeZ));
                // 定期在村落周边选游走点；走出活动半径则回家；速度为 0（到站/被水挡住）也立即重选，避免原地呆立
                if (a.WanderTimer <= 0f || distHome > 20f || (a.Vx==0f && a.Vz==0f))
                {
                    float tx=a.HomeX, tz=a.HomeZ; bool have=false;
                    if (distHome <= 20f)
                    {
                        // 绕家园选一个距当前位置 >1.4 的可行走点，最多 10 次，保证真的会走起来
                        for(int t=0;t<10;t++)
                        {
                            float ang=UnityEngine.Random.value*Mathf.PI*2f, rr=2.5f+UnityEngine.Random.value*10.5f;
                            float cx=a.HomeX+Mathf.Cos(ang)*rr, cz=a.HomeZ+Mathf.Sin(ang)*rr;
                            if(Walkable(cx,cz) && (cx-a.X)*(cx-a.X)+(cz-a.Z)*(cz-a.Z)>1.96f){tx=cx;tz=cz;have=true;break;}
                        }
                        // 家园周边多水：改在当前位置附近找落点，贴着岸移动
                        if(!have) for(int t=0;t<8;t++)
                        {
                            float ang=UnityEngine.Random.value*Mathf.PI*2f, rr=1.2f+UnityEngine.Random.value*2.5f;
                            float cx=a.X+Mathf.Cos(ang)*rr, cz=a.Z+Mathf.Sin(ang)*rr;
                            if(Walkable(cx,cz)){tx=cx;tz=cz;have=true;break;}
                        }
                    }
                    float dx=tx-a.X,dz=tz-a.Z,dl=Mathf.Sqrt(dx*dx+dz*dz);
                    if(dl>0.05f){ a.Vx=dx/dl*WalkSpeed; a.Vz=dz/dl*WalkSpeed; }
                    a.WanderTimer = 2.0f + UnityEngine.Random.value*2.5f;
                }
                bool moved=false;
                float nx=a.X+a.Vx*dt, nz=a.Z+a.Vz*dt;
                if (Walkable(nx,nz)){ a.X=nx; a.Z=nz; moved=true; }
                else
                {
                    // 贴岸滑行：保持前进方向，依次向左右偏转找可行走格，而不是原地停下
                    float baseAng=Mathf.Atan2(a.Vx,a.Vz);
                    float[] turns={30f,-30f,60f,-60f,90f,-90f,120f,-120f,150f,-150f,180f};
                    foreach(var deg in turns)
                    {
                        float ang=baseAng+deg*Mathf.Deg2Rad;
                        float vx=Mathf.Sin(ang)*WalkSpeed, vz=Mathf.Cos(ang)*WalkSpeed;
                        float cx=a.X+vx*dt, cz=a.Z+vz*dt;
                        if(Walkable(cx,cz)){ a.Vx=vx;a.Vz=vz;a.X=cx;a.Z=cz;moved=true;break; }
                    }
                    if(!moved){ a.Vx=0f;a.Vz=0f;a.WanderTimer=Mathf.Min(a.WanderTimer,0.25f); }
                }
                if (a.View != null)
                {
                    float y=_terrain!=null?_terrain.HeightAt(a.X,a.Z):0f;
                    a.View.transform.position = new Vector3(a.X,y,a.Z);
                    // 朝向移动方向
                    if (moved)
                        a.View.transform.rotation=Quaternion.Slerp(a.View.transform.rotation,
                            Quaternion.Euler(0,Mathf.Atan2(a.Vx,a.Vz)*Mathf.Rad2Deg,0),0.2f);
                    // V6.8.1 迈腿动画：把世界速度喂给人形动画器（懒加载，读档视图重建后自动重取）
                    if(a.Anim==null) a.Anim=a.View.GetComponentInChildren<PixelToCivilization.Actors.HumanoidAnimator>();
                    if(a.Anim!=null) a.Anim.Velocity = moved ? new Vector3(a.Vx,0f,a.Vz) : Vector3.zero;
                }
            }
        }

        /// <summary>V6.5.6 人员可行走判定：非水面，或站在桥梁上；无地形数据时放行（兼容）</summary>
        bool Walkable(float x,float z)
        {
            if(_terrain==null)return true;
            if(!_terrain.IsWater(x,z))return true;
            return GM.Bridge!=null && GM.Bridge.IsBridgeAt(x,z);
        }
    }
}

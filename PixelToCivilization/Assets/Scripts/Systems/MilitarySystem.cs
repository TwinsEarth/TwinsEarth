using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.World;
using PixelToCivilization.Data;

namespace PixelToCivilization.Systems
{
    /// <summary>敌方作战单位（V6.1.4：Kind 0步兵 / 1骑兵）</summary>
    public class EnemyUnit
    {
        public float X, Z;
        public int Kind;                          // 0步兵 1骑兵
        public float Hp = 30, MaxHp = 30, Attack = 5, Speed = 1.5f;
        public float AtkCd;
        public GameObject View;
        public World.OverheadBillboard OH;   // V6.1.9(i) 头顶旗帜+同色血条
        public bool IsCavalry => Kind == 1;
        public bool Dead => Hp <= 0;
        public Vector3 Pos => new(X,0,Z);
    }

    /// <summary>敌方势力（东夷/西戎/南蛮/北狄/中原诸侯）</summary>
    public class Faction
    {
        public string Id, Name;
        public long ColorHex;
        public float X, Z;
        public int HomeContinent = 1;                 // V6.1.7 据点所在大陆（1=玩家主大陆），隔海大陆航海前各自独立
        public List<EnemyUnit> Army = new();
        public float Population = 30;
        public bool Destroyed;
        public float SpawnTimer;
        public GameObject BaseView;
        public float Power => Population + Army.Count * 8f;
    }

    /// <summary>
    /// 军事系统 —— V6.1.4 骑兵&amp;塔防&amp;群雄争霸：征兵/训练骑兵、我方步骑机动部队、五方势力互伐吞并、
    /// 玩家主动讨伐、城墙阻挡、火塔/炮塔 AOE、兵种相克（骑克步、箭塔/炮塔克骑）。1:1 继承 v5.9.9 征兵与塔防。
    /// </summary>
    public class MilitarySystem : GameSystemBase
    {
        public static readonly (string id,string name,long color)[] FactionDefs =
        {
            ("east","东夷",0xFF6347),("west","西戎",0x4169E1),("south","南蛮",0x32CD32),
            ("north","北狄",0x9370DB),("central","中原诸侯",0xFFD700),
        };
        public List<Faction> Factions = new();
        public bool FactionsInited;
        private float _warCheckCd, _pendingInf, _pendingCav;
        private Transform _root;
        private WorldGenerator _terrain;

        public override void Init(GameManager gm)
        {
            base.Init(gm);
            _terrain=Object.FindObjectOfType<WorldGenerator>();
            _root=EntityViewFactory.EnsureRoot("Military",gm.transform);
        }

        public override void OnEra(int newEra, int oldEra)
        {
            if (newEra >= 1 && !FactionsInited) { InitFactions(); return; }
            // 新时代边患更迭：旧势力已被全数剿灭且尚未达成征服胜利时，重新崛起一批
            if (newEra >= 1 && FactionsInited && Factions.Count > 0 && !S.Victory)
            {
                bool any=false; foreach (var f in Factions) if (!f.Destroyed) { any=true; break; }
                if (!any)
                {
                    foreach(var f in Factions) if(f.BaseView)Object.Destroy(f.BaseView);
                    Factions.Clear(); FactionsInited=false; InitFactions();
                }
            }
        }

        public void InitFactions()
        {
            Factions.Clear();
            int home = _terrain!=null ? _terrain.HomeContinent : 1;
            float hx = S.VillageX.Count>0 ? S.VillageX[0] : 0f;
            float hz = S.VillageZ.Count>0 ? S.VillageZ[0] : 0f;
            var used = new List<Vector2>();
            int fi=0;
            // 玩家主大陆布 3~4 个会直接交锋的势力（围绕玩家村、同大陆陆地）；
            // 隔海大陆初始不摆军事据点（其陆地隐于海图、航海前各自独立，发现后走殖民玩法），杜绝未显现区模型穿帮
            int homeN = 3+Mathf.FloorToInt(Random.value*2);
            for(int i=0;i<homeN && fi<FactionDefs.Length;i++)
                TryPlaceFaction(fi++, home, hx,hz,40f,88f,used);
            FactionsInited = true;
            GM.AddEvent("bad","⚔️ 周边敌对势力崛起，群雄逐鹿，整军备战！");
        }

        /// <summary>在指定大陆放置一个势力据点及 5 名守军；主大陆围绕玩家村环带，其他大陆在该大陆随机陆地</summary>
        bool TryPlaceFaction(int fi,int cid,float hx,float hz,float rMin,float rMax,List<Vector2> used)
        {
            var fd=FactionDefs[fi%FactionDefs.Length];
            bool aroundHome = rMax>0f;
            for(int att=0;att<50;att++)
            {
                float x,z;
                if(aroundHome)
                {
                    float ang=Random.value*Mathf.PI*2f, dist=rMin+Random.value*(rMax-rMin);
                    x=hx+Mathf.Cos(ang)*dist; z=hz+Mathf.Sin(ang)*dist;
                    if(_terrain!=null && _terrain.ContinentAt(x,z)!=cid) continue;
                    if(_terrain!=null && _terrain.IsWater(x,z)) continue;
                }
                else
                {
                    if(_terrain==null || !_terrain.RandomPointOnContinent(cid,out x,out z,60)) return false;
                }
                bool close=false;
                foreach(var p in used) if(Vector2.Distance(p,new Vector2(x,z))<46f){close=true;break;}
                if(close) continue;
                bool overseas = cid!=(_terrain!=null?_terrain.HomeContinent:1);
                var f=new Faction{
                    Id=fd.id+"_"+fi, Name=overseas?("海外"+fd.name):fd.name, ColorHex=fd.color,
                    X=x,Z=z, Population=20+Random.value*30, HomeContinent=cid };
                Factions.Add(f); used.Add(new Vector2(x,z));
                f.BaseView=EntityViewFactory.Spawn("Base_"+f.Name,_root,PrimitiveType.Cylinder,
                    EntityViewFactory.Hex(fd.color),2.2f);
                EntityViewFactory.Place(f.BaseView,_terrain,x,z,2f);
                // V6.1.9(i) 据点大旗（势力色，无血条）
                World.OverheadBillboard.Attach(f.BaseView,EntityViewFactory.Hex(fd.color),false,2.0f,3.4f)?.SetBarVisible(false);
                for(int k=0;k<5;k++) f.Army.Add(MakeUnit(x,z,fd.color));
                return true;
            }
            return false;
        }

        /// <summary>陆地单位能否进入(x,z)：大航海前开阔深水不可逾越（隔海独立），航海后可航渡但减速一半</summary>
        bool CanStepInto(float x,float z,out float mul)
        {
            mul=1f;
            if(_terrain==null) return true;
            if(_terrain.IsDeepWater(x,z))
            {
                if(!S.AgeOfSail) return false;
                mul=0.5f;
            }
            return true;
        }

        private EnemyUnit MakeUnit(float x, float z, long colorHex=0xFF6347, int forceKind=-1)
        {
            int kind = forceKind>=0 ? forceKind
                : (Random.value < Mathf.Clamp01(0.15f + S.Era*0.07f) ? 1 : 0); // 时代越高骑兵越多
            bool cav = kind==1;
            var u = new EnemyUnit
            {
                X=x+Random.Range(-4,4f), Z=z+Random.Range(-4,4f), Kind=kind,
                Hp=cav?45f:20+Random.value*20,
                Attack=cav?7f:3+Random.value*4,
                Speed=cav?2.8f:1.5f
            };
            u.MaxHp=u.Hp;
            u.View=EntityViewFactory.SpawnHumanoid("Enemy",_root,EntityViewFactory.Hex(colorHex),cav?1.12f:0.95f,cav);
            EntityViewFactory.Place(u.View,_terrain,u.X,u.Z,0f);
            // V6.1.9(i) 头顶旗帜+与旗帜同色血条
            u.OH=World.OverheadBillboard.Attach(u.View,EntityViewFactory.Hex(colorHex),true,cav?1.05f:0.92f,cav?2.6f:2.3f);
            u.OH?.SetHp(1f);
            return u;
        }

        // ---------- 训练 ----------
        /// <summary>征兵：耗粮20、人口5，+5兵（每5兵编1步兵队）</summary>
        public bool TrainSoldiers()
        {
            if (S.GetRes("food") < 20 || S.Pop < 10) { GM.AddEvent("bad","粮食或人口不足，无法征兵"); return false; }
            S.AddRes("food",-20); S.Pop -= 5; S.MilSoldiers += 5; _pendingInf += 5;
            RebuildAndPumpUnits(); // V6.1.9(i) 修复：征兵后立即编队（原仅依赖Tick，存在不出队风险）
            GM.AddEvent("good","⚔️ 训练5名士兵");
            return true;
        }

        /// <summary>V6.1.4 训练骑兵：需马厩，耗粮30金20人口4 → +4骑兵（每4骑编1骑兵队）</summary>
        public bool TrainCavalry()
        {
            bool stable=false; foreach (var b in S.Buildings) if (b.Type=="stable") { stable=true; break; }
            if (!stable) { GM.AddEvent("bad","需先建造马厩才能训练骑兵"); return false; }
            if (S.GetRes("food")<30 || S.GetRes("gold")<20 || S.Pop<8)
            { GM.AddEvent("bad","粮食/金币/人口不足，无法训练骑兵"); return false; }
            S.AddRes("food",-30); S.AddRes("gold",-20); S.Pop-=4; S.MilCavalry+=4; _pendingCav+=4;
            RebuildAndPumpUnits(); // V6.1.9(i) 立即编队
            GM.AddEvent("good","🐎 训练4名骑兵");
            return true;
        }

        public override void Tick(float dt)
        {
            if (!FactionsInited && S.Era >= 1) InitFactions();
            RebuildAndPumpUnits();
            UpdateTowers(dt);
            UpdateProjectiles(dt);
            UpdateFriendly(dt);
            UpdateEnemyArmy(dt);
            UpdateWarState(dt);
        }

        /// <summary>训练队列转实体队；读档后重建丢失的我方单位视图</summary>
        private void RebuildAndPumpUnits()
        {
            while (_pendingInf >= 5) { S.FriendlyUnits.Add(MakeFriendly(0)); _pendingInf-=5; }
            while (_pendingCav >= 4) { S.FriendlyUnits.Add(MakeFriendly(1)); _pendingCav-=4; }
            foreach (var fu in S.FriendlyUnits)
                if (fu.View==null)
                {
                    fu.MaxHp=fu.IsCavalry?60:50; if (fu.Hp<=0) fu.Hp=fu.MaxHp;
                    fu.View=EntityViewFactory.SpawnHumanoid(fu.IsCavalry?"OurCav":"OurInf",_root,
                        fu.IsCavalry?new Color(0.33f,0.8f,1f):EntityViewFactory.Hex(0xFFD700),
                        fu.IsCavalry?1.12f:1f,fu.IsCavalry);
                    EntityViewFactory.Place(fu.View,_terrain,fu.X,fu.Z,0f);
                    fu.OH=World.OverheadBillboard.Attach(fu.View,fu.IsCavalry?new Color(0.33f,0.8f,1f):EntityViewFactory.Hex(0xFFD700),true,fu.IsCavalry?1.05f:0.92f,fu.IsCavalry?2.6f:2.3f);
                    fu.OH?.SetHp(fu.Hp/fu.MaxHp);
                }
        }

        private Vector2 RallyPoint()
        {
            if (S.Buildings.Count==0) return Vector2.zero;
            Vector2 s=Vector2.zero; foreach (var b in S.Buildings) s+=new Vector2(b.X,b.Z);
            return s/S.Buildings.Count;
        }

        private FriendlyUnit MakeFriendly(int kind)
        {
            bool cav=kind==1; var rp=RallyPoint();
            var fu=new FriendlyUnit
            {
                Kind=kind, HomeX=rp.x, HomeZ=rp.y,
                X=rp.x+Random.Range(-3,3f), Z=rp.y+Random.Range(-3,3f),
                Hp=cav?60:50, MaxHp=cav?60:50, Attack=cav?9:6, Speed=cav?3.2f:1.6f, State=0
            };
            fu.View=EntityViewFactory.SpawnHumanoid(cav?"OurCav":"OurInf",_root,
                cav?new Color(0.33f,0.8f,1f):EntityViewFactory.Hex(0xFFD700),cav?1.12f:1f,cav);
            EntityViewFactory.Place(fu.View,_terrain,fu.X,fu.Z,0f);
            fu.OH=World.OverheadBillboard.Attach(fu.View,cav?new Color(0.33f,0.8f,1f):EntityViewFactory.Hex(0xFFD700),true,cav?1.05f:0.92f,cav?2.6f:2.3f);
            fu.OH?.SetHp(1f);
            return fu;
        }

        // ---------- 塔防 ----------
        private void UpdateTowers(float dt)
        {
            foreach (var b in S.Buildings)
            {
                if (b.Def==null || b.Def.GetFunc("attack")<=0) continue;
                int lv=Mathf.Max(1,b.Level);
                float range = b.Def.GetFunc("range")*(1+(lv-1)*0.08f);
                EnemyUnit target=null; float nearest=range;
                foreach (var f in Factions)
                    if (!f.Destroyed)
                        foreach (var u in f.Army)
                        {
                            float d=Vector2.Distance(new Vector2(u.X,u.Z),new Vector2(b.X,b.Z));
                            if (d<nearest){nearest=d;target=u;}
                        }
                if (target!=null)
                {
                    b.AttackCooldown = Mathf.Max(0,b.AttackCooldown-dt);
                    if (b.AttackCooldown<=0)
                    {
                        float dmg=b.Def.GetFunc("attack")*(1+(lv-1)*0.3f);
                        // 箭塔/炮塔克制骑兵 ×1.5
                        if (target.IsCavalry && b.Type!="fire_tower") dmg*=1.5f;
                        string kind = b.Type=="fire_tower"?"fire" : b.Type=="cannon_tower"?"cannonball":"arrow";
                        FireProjectile(b.X,b.Z,target,dmg,kind);
                        b.AttackCooldown = b.Type=="bunker"?0.6f:1.5f;   // 碉堡机枪速射
                    }
                }
            }
        }

        public void FireProjectile(float x, float z, EnemyUnit target, float damage, string kind="arrow")
        {
            var p = new ProjectileEntity
            {
                Pos=new Vector3(x,2,z), Vel=(target.Pos-new Vector3(x,0,z)).normalized*18f,
                Damage=damage, Life=2f, Kind=kind, Target=target
            };
            Color c = kind=="cannonball"?new Color(0.2f,0.2f,0.2f)
                    : kind=="fire"?new Color(1f,0.45f,0.1f):new Color(0.9f,0.8f,0.4f);
            p.View=EntityViewFactory.Spawn("Projectile",_root,PrimitiveType.Sphere,c,kind=="arrow"?0.22f:0.34f);
            S.Projectiles.Add(p);
        }

        private void UpdateProjectiles(float dt)
        {
            for (int i=S.Projectiles.Count-1;i>=0;i--)
            {
                var p=S.Projectiles[i];
                p.Life-=dt;
                if (p.Target is EnemyUnit u && !u.Dead) p.Vel=(u.Pos-p.Pos).normalized*18f;
                p.Pos += p.Vel*dt;
                if (p.View) p.View.transform.position=p.Pos;
                if (p.Target is EnemyUnit t && !t.Dead && Vector3.Distance(p.Pos,t.Pos)<1.6f)
                {
                    if (p.Kind=="fire") { AoeDamage(p.Pos,4f,p.Damage,1f); p.Life=0; }
                    else if (p.Kind=="cannonball") { AoeDamage(p.Pos,5f,p.Damage,0.5f,t); p.Life=0; }
                    else { t.Hp-=p.Damage; p.Life=0; }
                }
                if (p.Life<=0){ if(p.View)Object.Destroy(p.View); S.Projectiles.RemoveAt(i); }
            }
        }

        /// <summary>范围伤害：center 半径内全部敌军；main 吃全额，其余吃 splash 比例</summary>
        private void AoeDamage(Vector3 center,float radius,float dmg,float splash,EnemyUnit main=null)
        {
            foreach (var f in Factions)
                if (!f.Destroyed)
                    foreach (var u in f.Army)
                    {
                        if (u.Dead) continue;
                        float d=Vector3.Distance(center,u.Pos);
                        if (d>radius) continue;
                        u.Hp -= (main==u)?dmg:dmg*splash;
                    }
        }

        // ---------- 我方步骑机动部队 ----------
        private EnemyUnit NearestEnemy(FriendlyUnit fu,float view)
        {
            EnemyUnit best=null; float bd=view;
            foreach (var f in Factions)
                if (!f.Destroyed)
                    foreach (var u in f.Army)
                    {
                        float d=Vector2.Distance(new Vector2(u.X,u.Z),new Vector2(fu.X,fu.Z));
                        if (d<bd){bd=d;best=u;}
                    }
            return best;
        }

        private void UpdateFriendly(float dt)
        {
            for (int i=S.FriendlyUnits.Count-1;i>=0;i--)
            {
                var fu=S.FriendlyUnits[i];
                if (fu.Hp<=0)
                {
                    if(fu.View)Object.Destroy(fu.View);
                    // V6.1.4 队覆灭同步账面兵力（1 步兵队=5 兵、1 骑兵队=4 骑），保持面板兵力与实际队数一致
                    if(fu.IsCavalry) S.MilCavalry=Mathf.Max(0,S.MilCavalry-4);
                    else S.MilSoldiers=Mathf.Max(0,S.MilSoldiers-5);
                    S.FriendlyUnits.RemoveAt(i); continue;
                }
                EnemyUnit foe = NearestEnemy(fu, fu.IsCavalry?28:22);
                Vector3 aim=fu.Pos; bool move=false; Vector3 vel=Vector3.zero;

                if (fu.State==2) // 讨伐行军/围攻据点
                {
                    var fac = Factions.Find(x=>x.Id==fu.CampaignId && !x.Destroyed);
                    if (fac==null){ fu.State=3; }
                    else
                    {
                        Vector3 bp=new(fac.X,0,fac.Z);
                        if (foe!=null && Vector3.Distance(foe.Pos,fu.Pos)<7f){ aim=foe.Pos;move=true; }
                        else if (Vector3.Distance(bp,fu.Pos)>8f){ aim=bp;move=true; }
                        else { fu.AtkCd-=dt; if(fu.AtkCd<=0){ fu.AtkCd=1.2f; SiegeFaction(fac,fu.Attack);} continue; }
                    }
                }
                else if (foe!=null) { fu.State=1; aim=foe.Pos; move=true; }
                else
                {
                    Vector3 home=new(fu.HomeX,0,fu.HomeZ);
                    if (Vector3.Distance(home,fu.Pos)>2.2f){ fu.State=3; aim=home; move=true; }
                    else fu.State=0;
                }

                if (foe!=null && Vector3.Distance(foe.Pos,fu.Pos)<=2.6f)
                {
                    fu.AtkCd-=dt;
                    if (fu.AtkCd<=0)
                    {
                        fu.AtkCd=1.2f;
                        float d=fu.Attack*(fu.IsCavalry&&!foe.IsCavalry?1.5f:1f); // 骑克步
                        foe.Hp-=d;
                    }
                }
                else if (move)
                {
                    Vector3 dir=(aim-fu.Pos); dir.y=0;
                    if (dir.sqrMagnitude>0.001f)
                    {
                        dir.Normalize();
                        float nx=fu.X+dir.x*fu.Speed*dt, nz=fu.Z+dir.z*fu.Speed*dt;
                        // V6.1.7 大航海前陆地队不得踏入开阔深水（无法跨洋），航海后可航渡、速度减半
                        if(CanStepInto(nx,nz,out var sm)){ fu.X=nx; fu.Z=nz; vel=dir*fu.Speed*sm; }
                    }
                }
                if (fu.View)
                {
                    var ha=fu.View.GetComponentInChildren<Actors.HumanoidAnimator>();
                    if (ha) ha.Velocity=vel;
                    EntityViewFactory.Place(fu.View,_terrain,fu.X,fu.Z,0f);
                    fu.OH?.SetHp(fu.MaxHp>0?fu.Hp/fu.MaxHp:1f);
                }
            }
        }

        /// <summary>围攻势力据点：削减其人口（据点火力/耐久）并杀伤守军，清零即吞并</summary>
        private void SiegeFaction(Faction fac,float atk)
        {
            fac.Population -= atk*0.5f;
            if (fac.Army.Count>0) fac.Army[Random.Range(0,fac.Army.Count)].Hp-=atk;
            if (fac.Population<=0 && !fac.Destroyed) Annex(fac,true);
        }

        /// <summary>玩家主动出师讨伐某势力</summary>
        public bool LaunchCampaign(string factionId)
        {
            if (S.FriendlyUnits.Count==0){ GM.AddEvent("bad","尚无机动部队，请先征兵或训练骑兵"); return false; }
            var fac=Factions.Find(f=>f.Id==factionId && !f.Destroyed);
            if (fac==null){ GM.AddEvent("bad","目标势力已不存在"); return false; }
            int home=_terrain!=null?_terrain.HomeContinent:1;
            if (fac.HomeContinent!=home && !S.AgeOfSail)
            { GM.AddEvent("bad","🧭 大航海时代（公元1000年）前远隔重洋，陆地兵马无法跨洋远征"); return false; }
            foreach (var fu in S.FriendlyUnits){ fu.State=2; fu.CampaignId=factionId; }
            S.WarActive=true;
            GM.AddEvent("bad","⚔️ 出师讨伐："+fac.Name+"！");
            return true;
        }

        // ---------- 敌军推进（城墙阻挡 / 骑冲塔 / 与我军接战）----------
        private void UpdateEnemyArmy(float dt)
        {
            foreach (var f in Factions)
            {
                if (f.Destroyed) continue;
                for (int i=f.Army.Count-1;i>=0;i--)
                {
                    var u=f.Army[i];
                    if (u.Dead){ if(u.View)Object.Destroy(u.View); f.Army.RemoveAt(i); continue; }

                    // 1) 近身有我方单位则先交战
                    FriendlyUnit fu=NearestFriendly(u.X,u.Z,2.6f);
                    Vector3 moveVel=Vector3.zero;
                    if (fu!=null)
                    {
                        if (Vector3.Distance(fu.Pos,u.Pos)>1.8f)
                        {
                            Vector3 dir=(fu.Pos-u.Pos).normalized;
                            float nx=u.X+dir.x*u.Speed*dt, nz=u.Z+dir.z*u.Speed*dt;
                            if(CanStepInto(nx,nz,out var em)){ u.X=nx;u.Z=nz; moveVel=dir*u.Speed*em; }
                        }
                        else
                        {
                            u.AtkCd-=dt;
                            if (u.AtkCd<=0)
                            {
                                u.AtkCd=1.5f;
                                fu.Hp -= u.Attack*(u.IsCavalry&&!fu.IsCavalry?1.5f:1f); // 骑克步
                            }
                        }
                    }
                    else if (!S.WarActive)
                    {
                        // V6.1.4 平时驻防：群雄军队只在本势力据点 18 格内巡逻，不主动远征犯境（避免开局被持续平推）；离开即回防
                        float home=Vector2.Distance(new Vector2(f.X,f.Z),new Vector2(u.X,u.Z));
                        if (home>18f)
                        {
                            Vector3 dir=(new Vector3(f.X,0,f.Z)-u.Pos).normalized;
                            float nx=u.X+dir.x*u.Speed*dt, nz=u.Z+dir.z*u.Speed*dt;
                            if(CanStepInto(nx,nz,out var em)){ u.X=nx;u.Z=nz; moveVel=dir*u.Speed*em; }
                        }
                    }
                    else
                    {
                        BuildingEntity goal=ChooseGoal(u);
                        if (goal!=null)
                        {
                            float d=Vector2.Distance(new Vector2(u.X,u.Z),new Vector2(goal.X,goal.Z));
                            if (d>3f)
                            {
                                Vector3 dir=(goal.Pos-u.Pos).normalized;
                                float nx=u.X+dir.x*u.Speed*dt, nz=u.Z+dir.z*u.Speed*dt;
                                if(CanStepInto(nx,nz,out var em)){ u.X=nx;u.Z=nz; moveVel=dir*u.Speed*em; }
                            }
                            else
                            {
                                u.AtkCd-=dt;
                                if (u.AtkCd<=0){ u.AtkCd=1.5f; goal.Hp-=u.Attack; if(goal.Hp<=0) GM.Building.DestroyByEnemy(goal); }
                            }
                        }
                    }
                    if (u.View)
                    {
                        var ha=u.View.GetComponentInChildren<Actors.HumanoidAnimator>();
                        if (ha) ha.Velocity=moveVel;
                        EntityViewFactory.Place(u.View,_terrain,u.X,u.Z,0f);
                        u.OH?.SetHp(u.MaxHp>0?u.Hp/u.MaxHp:1f);
                    }
                }
                f.SpawnTimer-=dt;
                // V6.1.4 平时按时代维持常备军（4+Era*2，8s 慢补，让群雄持续陈兵边境）；战争期上限 12、5s 快补
                int troopCap = S.WarActive?12:Mathf.Min(12,4+S.Era*2);
                if (f.SpawnTimer<=0 && f.Army.Count<troopCap)
                { f.Army.Add(MakeUnit(f.X,f.Z,f.ColorHex)); f.SpawnTimer=S.WarActive?5f:8f; }
                if (f.Army.Count==0 && f.Population<=0 && !f.Destroyed) Annex(f,true);
            }
        }

        private FriendlyUnit NearestFriendly(float x,float z,float within)
        {
            FriendlyUnit best=null; float bd=within;
            foreach (var fu in S.FriendlyUnits)
            {
                float d=Vector2.Distance(new Vector2(x,z),new Vector2(fu.X,fu.Z));
                if (d<bd){bd=d;best=fu;}
            }
            return best;
        }

        private static bool IsWall(BuildingEntity b)
            => b.Type=="wall"||b.Type=="great_wall"||b.Type=="watchtower";

        private BuildingEntity ChooseGoal(EnemyUnit u)
        {
            BuildingEntity direct=NearestBuilding(u.X,u.Z);
            // 骑兵优先穿插攻击远程塔
            if (u.IsCavalry)
            {
                var tower=NearestTower(u.X,u.Z,20f);
                if (tower!=null) direct=tower;
            }
            // 城墙阻挡：挡在前方的墙/烽火台先被攻击
            var wall=NearestWall(u.X,u.Z);
            if (wall!=null && direct!=null && !IsWall(direct))
            {
                float dw=Vector2.Distance(new Vector2(u.X,u.Z),new Vector2(wall.X,wall.Z));
                float dd=Vector2.Distance(new Vector2(u.X,u.Z),new Vector2(direct.X,direct.Z));
                if (dw < dd+4f) direct=wall;
            }
            return direct;
        }

        private BuildingEntity NearestBuilding(float x,float z)
        {
            BuildingEntity best=null; float bd=99999;
            foreach (var b in S.Buildings)
            { float d=Vector2.Distance(new Vector2(x,z),new Vector2(b.X,b.Z)); if (d<bd){bd=d;best=b;} }
            return best;
        }
        private BuildingEntity NearestWall(float x,float z)
        {
            BuildingEntity best=null; float bd=99999;
            foreach (var b in S.Buildings) if (IsWall(b))
            { float d=Vector2.Distance(new Vector2(x,z),new Vector2(b.X,b.Z)); if (d<bd){bd=d;best=b;} }
            return best;
        }
        private BuildingEntity NearestTower(float x,float z,float within)
        {
            BuildingEntity best=null; float bd=within;
            foreach (var b in S.Buildings)
                if (b.Def!=null && b.Def.GetFunc("attack")>0)
                { float d=Vector2.Distance(new Vector2(x,z),new Vector2(b.X,b.Z)); if (d<bd){bd=d;best=b;} }
            return best;
        }

        // ---------- 群雄争霸：势力间数值攻伐吞并（每20游戏年）----------
        public override void OnYear(int year)
        {
            if (S.FactionAnnexTimer<=0) S.FactionAnnexTimer=year;
            if (year-S.FactionAnnexTimer<20) return;
            S.FactionAnnexTimer=year;
            var alive=Factions.FindAll(f=>!f.Destroyed);
            if (alive.Count<2) return;
            var a=alive[Random.Range(0,alive.Count)];
            // V6.1.7 隔海大陆不互相攻伐：只在同一大陆的势力间配对（异大陆各自独立发展）
            var peers=alive.FindAll(x=>x!=a && x.HomeContinent==a.HomeContinent);
            if (peers.Count==0) return;
            Faction b=peers[Random.Range(0,peers.Count)];
            var (win,lose)=a.Power>=b.Power?(a,b):(b,a);
            lose.Population-=10+Random.value*10;
            win.Population=Mathf.Min(120,win.Population+6);
            // V6.1.4 沙场折损：败方此役被击溃 1-2 支军队（视图一并清除，由常备屯兵再补）
            int rout=Mathf.Min(lose.Army.Count,1+Mathf.FloorToInt(Random.value*2));
            for(int k=0;k<rout;k++)
            {
                var ru=lose.Army[lose.Army.Count-1];
                if(ru.View)Object.Destroy(ru.View);
                lose.Army.RemoveAt(lose.Army.Count-1);
            }
            if (lose.Population<=0) Annex(lose,false,win.Name);
            else GM.AddEvent("info","⚔️ 群雄攻伐："+win.Name+" 击破 "+lose.Name+(rout>0?"，歼其"+rout+"队":""));
        }

        /// <summary>吞并势力：byPlayer=玩家攻克给奖励；byName=被其他势力兼并</summary>
        private void Annex(Faction fac,bool byPlayer,string byName=null)
        {
            if (fac.Destroyed) return;
            fac.Destroyed=true;
            if (fac.BaseView) Object.Destroy(fac.BaseView);
            foreach (var u in fac.Army) if(u.View)Object.Destroy(u.View);
            fac.Army.Clear();
            if (byPlayer)
            {
                int g=80+Mathf.RoundToInt(Random.value*60f);
                S.AddRes("gold",g); S.AddRes("food",50);
                S.Pop=Mathf.Min(GameConstants.MaxPop,S.Pop+10);
                GM.AddEvent("good","🏳️ 攻克 "+fac.Name+"，兼并其地，获金"+g+"、粮50、民10");
            }
            else GM.AddEvent("info","🏴 "+fac.Name+" 为 "+(byName??"邻邦")+" 所并");
        }

        private void UpdateWarState(float dt)
        {
            _warCheckCd-=dt;
            if (_warCheckCd>0) return;
            _warCheckCd=15f;
            int home=_terrain!=null?_terrain.HomeContinent:1;
            // 大航海前只有同大陆势力能来犯；航海后远洋势力亦可跨海远征
            var threat=Factions.FindAll(f=>!f.Destroyed && (S.AgeOfSail || f.HomeContinent==home));
            if (!S.WarActive && S.Era>=1 && threat.Count>0 && Random.value<0.25f)
            {
                S.WarActive=true;
                var f=threat[Random.Range(0,threat.Count)];
                GM.AddEvent("bad","🔥 战争爆发："+f.Name+"来犯！");
            }
            if (S.WarActive)
            {
                bool any=false;
                foreach (var f in threat) if (f.Army.Count>0) any=true;
                if (!any){ S.WarActive=false; GM.AddEvent("good","🕊️ 敌军退去，战争结束"); }
            }
        }
    }
}

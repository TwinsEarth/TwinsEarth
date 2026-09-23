using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.World;
using PixelToCivilization.Data;
using PixelToCivilization.Rendering;

namespace PixelToCivilization.Systems
{
    /// <summary>船型静态定义（对齐 v5.9.9 SHIP_DEFS）</summary>
    public class ShipDef
    {
        public string Id, Name, Icon, AttackType;
        public int Capacity, Housing, Durability, Defense, Era;
        public float Speed, Attack, Range;
        public Dictionary<string,int> Cost;
        public bool Military;
        public long ColorHex;
    }

    /// <summary>
    /// 水军海战系统 —— 对齐 v5.9.9：8种船型、船员加成、建造宝船、敌方舰队、战船交火、随时代升级。
    /// </summary>
    public class NavalSystem : GameSystemBase
    {
        public readonly Dictionary<string,ShipDef> Defs = new();
        public List<ShipEntity> EnemyShips = new();
        private float _spawnCd;
        private bool _pirateEngaged;   // V6.1.5 本轮是否有敌舰/海盗，肃清后发护航赏金
        private Transform _root;
        private WorldGenerator _terrain;

        public override void Init(GameManager gm)
        {
            base.Init(gm);
            _root=EntityViewFactory.EnsureRoot("Navy",gm.transform);
            _terrain=Object.FindObjectOfType<WorldGenerator>();
            LoadDefs();
        }
        // V6.3.4：船只只能在水面（无地形数据时不阻拦，避免副本/异常空引用）
        private bool OnWater(float x,float z)=> _terrain==null || (_terrain.IsOceanWater(x,z) && _terrain.InsideFrontier(x,z)); // V6.8.3 船只只准在外海（排除内河+淡水湖）
        // V6.8.3 船只永留外海（每帧每船调用一次）：
        //  · 在外海：不干预；
        //  · 退潮露出的“外海潮滩”（基准水位下本是海水、当前临时干涸、群系仍为外海 Default）：原地坐滩，随涨潮自动复浮，绝不水平拖行；
        //  · 一旦出现在内河/淡水湖/陆地上（退潮被河道引入、地图扩展、强风、旧存档等任何成因）：螺旋搜索最近外海并一次性归位。
        private void KeepAtSea(ShipEntity s)
        {
            if(_terrain==null) return;
            if(_terrain.IsOceanWater(s.X,s.Z)) return;
            bool dry=!_terrain.IsWater(s.X,s.Z);
            bool seaBiome=_terrain.BiomeAt(s.X,s.Z)==BiomeKind.Default;
            bool wouldBeSea=_terrain.HeightAt(s.X,s.Z)<GameConstants.WaterLevel;
            if(dry && seaBiome && wouldBeSea) return; // 外海潮滩：坐滩等涨潮，不做水平移动
            float tile=GameConstants.Tile,bx=s.X,bz=s.Z,best=float.MaxValue; bool found=false;
            for(int ring=1;ring<=200 && !found;ring++)
            {
                float r=ring*tile;
                for(int a=0;a<24;a++)
                {
                    float ang=a/24f*Mathf.PI*2f;
                    float cx=s.X+Mathf.Cos(ang)*r, cz=s.Z+Mathf.Sin(ang)*r;
                    if(!_terrain.IsOceanWater(cx,cz)||!_terrain.InsideFrontier(cx,cz)) continue; // 河/湖一律不是归位点
                    if(r<best){best=r;bx=cx;bz=cz;found=true;}
                }
            }
            if(found){ s.X=bx; s.Z=bz; } // 一次性归位最近外海（自愈，杜绝滞留内河/湖泊）
        }

        /// <summary>船的贴合高度：在水里随潮位起伏；坐滩时托在滩面与水面的较高者，不悬空、不陷地、不被拖走。</summary>
        private float ShipRestY(ShipEntity s)
        {
            float surf=GM.Tide!=null?GM.Tide.SurfaceY(s.X,s.Z):GameConstants.WaterLevel;
            if(_terrain!=null && !_terrain.IsOceanWater(s.X,s.Z))
            {
                float ground=_terrain.HeightAt(s.X,s.Z);
                return Mathf.Max(surf,ground)+0.12f; // 退潮露出：稳坐滩面，潮涨水面没过即复浮
            }
            return surf+0.08f+Mathf.Sin(Time.time*1.6f+s.HomeX+s.HomeZ)*0.13f;
        }

        private GameObject ShipView(ShipEntity s, long colorHex, float scale)
        {
            // V6.1.1：方块替换为程序化舰船（民用帆船 / 军用战舰）
            var kind=s.Military?PixelToCivilization.Actors.VehicleKind.Warship:PixelToCivilization.Actors.VehicleKind.SailBoat;
            var v=EntityViewFactory.SpawnVehicle("Ship_"+s.Name,_root,kind,EntityViewFactory.Hex(colorHex),scale*0.85f,s.ShipTypeId);
            v.transform.position=new Vector3(s.X,0.1f,s.Z);
            // V6.1.1 船只可点击：载具工厂默认移除了碰撞体，这里在根节点补一个包围盒 + 点击桥（对齐建筑 BuildingClick）
            var col=v.AddComponent<BoxCollider>();
            bool huge=(s.ShipTypeId=="treasure_ship"||s.ShipTypeId=="treasure_warship");
            col.size=huge?new Vector3(9f,4.5f,8f):(s.Military?new Vector3(7f,3.2f,4.2f):new Vector3(5f,2.4f,3f)); col.center=new Vector3(0,1.2f,0);
            // V6.3.4 船只三级 LOD：占比>0.1%(0.001)显 LV3 高精且同屏最近≤30，0.01%~0.1% 为 LV2 简化，<0.01% 为 LV1 旧模
            PixelToCivilization.Rendering.LODKit.Attach(v, s.Military?7.5f:5.2f, 3, 0.001f, 0.00002f, "Ship", 30, 0.0001f);
            var click=v.AddComponent<ShipClick>(); click.Ship=s;
            click.OnClicked=ship=>PixelToCivilization.UI.UIManager.Instance?.ShowShip(ship);
            return v;
        }

        private void LoadDefs()
        {
            Add("small_boat","小木船","🛶",1,2,50,2,0.04f,0,0,false,0xB5743C,new(){{"wood",15}});
            Add("medium_boat","帆船","⛵",5,8,100,5,0.035f,0,0,false,0xEBCFA0,new(){{"wood",100},{"stone",30}});
            Add("large_boat","大船","🚢",20,20,200,10,0.025f,0,0,false,0xE05A4E,new(){{"wood",500},{"stone",200},{"gold",100}});
            Add("treasure_ship","宝船","🛳️",50,50,400,20,0.02f,0,0,false,0xFFC23D,new(){{"wood",3000},{"stone",500},{"iron",200},{"gold",1000}});
            // 军用船只：V6.1.1 按四级锚点补居住（运兵10/战船15/火炮20/火船5/宝船战舰50）
            Add("troop_boat","运兵船","🚣",10,10,80,3,0.035f,0,0,true,0x7FA04E,new(){{"wood",150},{"stone",50},{"food",30}},2);
            Add("war_junk","战船","⛵",15,15,150,8,0.04f,15,12,true,0xE05A4E,new(){{"wood",300},{"stone",100},{"iron",30},{"gold",50}},2,"arrow");
            Add("cannon_ship","火炮船","🚢",20,20,250,15,0.03f,40,18,true,0x4E8290,new(){{"wood",500},{"stone",150},{"iron",80},{"gold",100}},3,"cannon");
            Add("fire_ship","火船","🔥",5,5,60,2,0.05f,60,6,true,0xFF7A1E,new(){{"wood",100},{"stone",20},{"iron",10},{"gold",20}},3,"fire");
            Add("treasure_warship","宝船战舰","🛳️",50,50,500,25,0.025f,50,20,true,0xFFC23D,new(){{"wood",3000},{"stone",500},{"iron",300},{"gold",1000}},4,"cannon");
        }
        private void Add(string id,string name,string icon,int cap,int house,int dur,int def,float speed,
            float atk,float range,bool mil,long color,Dictionary<string,int> cost,int era=0,string atkType="")
        {
            Defs[id]=new ShipDef{Id=id,Name=name,Icon=icon,Capacity=cap,Housing=house,Durability=dur,Defense=def,
                Speed=speed,Attack=atk,Range=range,Military=mil,ColorHex=color,Cost=cost,Era=era,AttackType=atkType};
        }

        // ===== 属性公式（对齐源码）=====
        public int Capacity(ShipEntity s) => Mathf.FloorToInt((Defs.TryGetValue(s.ShipTypeId,out var d)?d.Capacity:1)*s.LevelMult);
        public int MaxDurability(ShipEntity s) => Mathf.FloorToInt((Defs.TryGetValue(s.ShipTypeId,out var d)?d.Durability:50)*s.LevelMult);
        public int AttackOf(ShipEntity s)
        {
            if (!Defs.TryGetValue(s.ShipTypeId,out var d)) return 0;
            float crewBonus = 1+s.Crew*0.1f, lvlBonus = 1+(s.Level-1)*0.25f;
            return Mathf.FloorToInt(d.Attack*crewBonus*lvlBonus);
        }
        public float SpeedOf(ShipEntity s)
        {
            if (!Defs.TryGetValue(s.ShipTypeId,out var d)) return 0;
            if (s.Crew==0) return 0;
            float ratio=Mathf.Min(1,s.Crew/(float)Capacity(s));
            return d.Speed*(0.5f+ratio*0.5f)*(1+(s.Level-1)*0.1f);
        }

        /// <summary>建造船只（通用）</summary>
        public bool BuildShip(string typeId, float x, float z)
        {
            if (!Defs.TryGetValue(typeId,out var d)) return false;
            if (!S.CanAfford(d.Cost)){ GM.AddEvent("bad","资源不足，无法建造"+d.Name); return false; }
            S.Pay(d.Cost);
            var s = new ShipEntity
            {
                ShipTypeId=typeId, Name=d.Name, Side="ours", X=x, Z=z, Level=1,
                Capacity=d.Capacity, Housing=d.Housing, MaxHp=d.Durability, Hp=d.Durability,
                BaseAttack=d.Attack, Range=d.Range, Military=d.Military, AttackType=d.AttackType
            };
            s.View=ShipView(s,d.ColorHex,d.Military?1.6f:1.2f);
            S.Ships.Add(s);
            GM.AddEvent("good",d.Icon+" 新"+d.Name+"建成！");
            return true;
        }

        /// <summary>开局村落免费赠船：不扣资源，停泊/巡游于指定水面点（带家园锚点供缓慢巡游）</summary>
        public ShipEntity SpawnInitialShip(string typeId,float x,float z)
        {
            if(!Defs.TryGetValue(typeId,out var d)) return null;
            var s=new ShipEntity{
                ShipTypeId=typeId,Name=d.Name,Side="ours",X=x,Z=z,HomeX=x,HomeZ=z,Level=1,
                Capacity=d.Capacity,Housing=d.Housing,MaxHp=d.Durability,Hp=d.Durability,
                BaseAttack=d.Attack,Range=d.Range,Military=d.Military,AttackType=d.AttackType,Crew=Mathf.Max(1,d.Capacity/2)};
            s.View=ShipView(s,d.ColorHex,d.Military?1.6f:1.35f);
            S.Ships.Add(s); return s;
        }

        /// <summary>读档恢复船只：不扣资源，按类型/坐标/等级重建（含居住与视图）</summary>
        public ShipEntity RestoreShip(string typeId,float x,float z,int level,bool military)        {
            if(!Defs.TryGetValue(typeId,out var d))return null;
            var s=new ShipEntity{
                ShipTypeId=typeId,Name=d.Name,Side="ours",X=x,Z=z,Level=Mathf.Clamp(level,1,3),
                Capacity=d.Capacity,Housing=d.Housing,Military=military,AttackType=d.AttackType,
                BaseAttack=d.Attack,Range=d.Range};
            s.MaxHp=MaxDurability(s);s.Hp=s.MaxHp;
            s.View=ShipView(s,d.ColorHex,d.Military?1.6f:1.2f);
            S.Ships.Add(s);return s;
        }

        /// <summary>建造宝船（对齐 buildTreasureShip，木100铁10的简化舰队版）</summary>
        public bool BuildTreasureShip()
        {
            if (S.GetRes("wood")<100 || S.GetRes("iron")<10) return false;
            S.AddRes("wood",-100); S.AddRes("iron",-10);
            S.OceanFleets.Add(new ShipEntity{ShipTypeId="treasure_ship",Name="宝船",Side="ours",X=20,Z=50,MaxHp=400,Hp=400,Capacity=50,Housing=50});
            GM.AddEvent("good","🚢 新宝船建成！");
            return true;
        }

        private ShipEntity SpawnEnemyShip()
        {
            string[] types={"war_junk","cannon_ship","fire_ship"};
            string t=types[Random.Range(0,types.Length)]; var d=Defs[t];
            float ex=0,ez=0; // V6.3.4：敌舰出生环上找水面点，避免直接刷在陆地
            for(int k=0;k<24;k++){float a=Random.value*Mathf.PI*2,rr=60f+Random.value*30f;ex=Mathf.Cos(a)*rr;ez=Mathf.Sin(a)*rr;
                if(_terrain==null||_terrain.IsOceanWater(ex,ez))break;} // V6.8.3：敌舰只刷在外海
            var s=new ShipEntity{ShipTypeId=t,Name="敌"+d.Name,Side="enemy",
                X=ex,Z=ez,Level=1,MaxHp=d.Durability,Hp=d.Durability,Housing=d.Housing,
                BaseAttack=d.Attack,Range=d.Range,Military=true,AttackType=d.AttackType,Capacity=d.Capacity,Crew=d.Capacity};
            s.View=ShipView(s,0xFF4500,1.6f);
            EnemyShips.Add(s); _pirateEngaged=true; return s;
        }

        /// <summary>V6.1.2 Debug：强制生成一艘敌方战船（海战演示）</summary>
        public ShipEntity DebugSpawnEnemy(){ S.NavyBattleActive=true; return SpawnEnemyShip(); }

        /// <summary>V6.1.2 Debug：在村址附近水域生成一艘我方战船（无消耗）</summary>
        public ShipEntity DebugSpawnOwnShip()
        {
            string t = S.Era>=4?"treasure_warship":S.Era>=3?"cannon_ship":"war_junk";
            if(!Defs.ContainsKey(t)) t="war_junk";
            for(int i=0;i<24;i++)
            {
                float ang=Random.value*Mathf.PI*2f, dist=12f+Random.value*24f;
                float x=Mathf.Cos(ang)*dist,z=Mathf.Sin(ang)*dist;
                var terrain=UnityEngine.Object.FindObjectOfType<WorldGenerator>();
                if(terrain!=null && !terrain.IsOceanWater(x,z)) continue; // V6.8.3：我方船只刷在外海
                var s=SpawnInitialShip(t,x,z);
                GM.AddEvent("good","🚢 Debug 生成我方"+Defs[t].Name);
                return s;
            }
            GM.AddEvent("warn","附近没有可停靠的水域");
            return null;
        }

        public override void Tick(float dt)
        {
            // 时代2以后、有我方战船时周期遭遇敌方舰队
            bool hasWarship=false;
            foreach (var s in S.Ships) if (s.Military) hasWarship=true;
            _spawnCd-=dt;
            if (S.Era>=2 && hasWarship && _spawnCd<=0 && EnemyShips.Count<6)
            {
                _spawnCd=20f;
                if (Random.value<0.6f){ SpawnEnemyShip(); S.NavyBattleActive=true; GM.AddEvent("bad","⚓ 敌方舰队出现！"); }
            }
            UpdateOurShips(dt);
            UpdateEnemyShips(dt);
        }

        /// <summary>船龄按「游戏年」增长并到寿退役（旧实现误放在每帧 Tick 的 Age++，60fps 下约 3.3 秒即到寿 200 全部沉没）</summary>
        public override void OnYear(int year)
        {
            for(int i=S.Ships.Count-1;i>=0;i--)
            {
                var s=S.Ships[i];
                s.Age++;
                if (s.Age>s.MaxAge)
                {
                    if(s.View!=null)Object.Destroy(s.View);
                    S.Ships.RemoveAt(i);
                    GM.AddEvent("bad","一艘"+s.Name+"超期服役，已退役（船龄 "+s.Age+" 年）");
                }
            }
        }

        private void UpdateOurShips(float dt)
        {
            foreach (var s in S.Ships)
            {
                float lookYaw=0f; bool hasLook=false;
                KeepAtSea(s); // V6.8.3 永留外海：退潮坐滩、误入内河/湖泊/陆地即归位最近外海
                if(!OnWater(s.X,s.Z))
                { // 退潮坐滩：仅垂直贴合潮位/滩面，不巡航、不追击、不漂移，涨潮自动复浮
                    if(s.View!=null) s.View.transform.position=new Vector3(s.X,ShipRestY(s),s.Z);
                    continue;
                }
                if (!s.Military)
                {
                    // 民用船：围绕家园锚点缓慢圆周巡游，让水面"活"起来
                    float phase=(s.HomeX*0.7f+s.HomeZ*0.5f)+Time.time*0.10f;
                    float rr=9f;
                    float tx=s.HomeX+Mathf.Cos(phase)*rr, tz=s.HomeZ+Mathf.Sin(phase)*rr;
                    float ox=s.X, oz=s.Z;
                    float nx=Mathf.Lerp(s.X,tx,dt*0.6f), nz=Mathf.Lerp(s.Z,tz,dt*0.6f);
                    float dvx=0f,dvz=0f;
                    if(GM.OceanFlow!=null){var dv=GM.OceanFlow.Drift(nx,nz,dt,1.2f);dvx=dv.x;dvz=dv.y;} // 洋流/海风漂流
                    // V6.3.4：巡游目标与洋流叠加后必须仍在水面，否则本帧不移动，杜绝被吹上陆地
                    if(OnWater(nx+dvx,nz+dvz)){ s.X=nx+dvx; s.Z=nz+dvz; }
                    float mvx=s.X-ox, mvz=s.Z-oz;
                    if (Mathf.Abs(mvx)+Mathf.Abs(mvz)>1e-4f){ lookYaw=Mathf.Atan2(mvx,mvz)*Mathf.Rad2Deg; hasLook=true; }
                }
                else
                {
                    s.AttackCd-=dt;
                    ShipEntity target=NearestEnemy(s);
                    if (target!=null)
                    {
                        float d=Vector2.Distance(new Vector2(s.X,s.Z),new Vector2(target.X,target.Z));
                        float ox=s.X,oz=s.Z;
                        bool fire=s.ShipTypeId=="fire_ship";
                        float engage=fire?3.2f:s.Range*0.6f;   // 火船贴近自爆
                        if (d>engage){ Vector3 dir=(target.Pos-s.Pos).normalized; float sp=SpeedOf(s);
                            // V6.1.9(i) 洋流海风：顺流顺风加速、逆流逆风减速
                            if(GM.OceanFlow!=null) sp*=GM.OceanFlow.SailFactor(s.X,s.Z,new Vector2(dir.x,dir.z));
                            float nx=s.X+dir.x*sp*30*dt, nz=s.Z+dir.z*sp*30*dt;
                            if(OnWater(nx,nz)){ s.X=nx; s.Z=nz; } // V6.3.4：军舰也不得登上陆地
                        }
                        else if (fire){ DetonateOurFireShip(s); }
                        else if (s.AttackCd<=0 && AttackOf(s)>0){ OurShipHit(s,target); s.AttackCd=2f; }
                        float mvx=s.X-ox,mvz=s.Z-oz;
                        if (Mathf.Abs(mvx)+Mathf.Abs(mvz)>1e-4f){ lookYaw=Mathf.Atan2(mvx,mvz)*Mathf.Rad2Deg; hasLook=true; }
                    }
                }
                if (s.View!=null)
                {
                    float bob=ShipRestY(s); // V6.8.2：随潮起伏，退潮坐滩时托在滩面
                    s.View.transform.position=new Vector3(s.X,bob,s.Z);
                    if (hasLook) s.View.transform.rotation=Quaternion.Slerp(s.View.transform.rotation,Quaternion.Euler(0,lookYaw,0),0.12f);
                }
            }
        }

        private void UpdateEnemyShips(float dt)
        {
            for (int i=EnemyShips.Count-1;i>=0;i--)
            {
                var e=EnemyShips[i];
                KeepAtSea(e); // V6.8.3 敌舰同样永留外海
                ShipEntity target=NearestOurs(e);
                if (target!=null)
                {
                    float d=Vector2.Distance(new Vector2(e.X,e.Z),new Vector2(target.X,target.Z));
                    bool eFire=e.ShipTypeId=="fire_ship";
                    float engage=eFire?3.2f:e.Range*0.6f;
                    if (d>engage){ Vector3 dir=(target.Pos-e.Pos).normalized;
                        float nx=e.X+dir.x*1.2f*dt, nz=e.Z+dir.z*1.2f*dt;
                        if(OnWater(nx,nz)){e.X=nx;e.Z=nz;} } // V6.8.3：敌舰只在外海移动
                    else if (eFire){ DetonateEnemyFireShip(e); }
                    else { e.AttackCd-=dt; if(e.AttackCd<=0){ EnemyShipHit(e,target);e.AttackCd=2.5f;} }
                }
                if (e.View!=null) e.View.transform.position=new Vector3(e.X,ShipRestY(e),e.Z);
                if (e.Hp<=0){ if(e.View!=null)Object.Destroy(e.View); EnemyShips.RemoveAt(i); GM.AddEvent("good","💥 击沉一艘敌舰！"); }
            }
            // 清理我方沉舰
            S.Ships.RemoveAll(s=>{ if(s.Hp<=0){if(s.View!=null)Object.Destroy(s.View);return true;} return false; });
            if (EnemyShips.Count==0)
            {
                S.NavyBattleActive=false;
                // V6.1.5 肃清海盗/敌舰：护航赏金 + 一段平静期
                if (_pirateEngaged)
                {
                    _pirateEngaged=false;
                    int gold=40+Random.Range(0,41);
                    S.AddRes("gold",gold); _spawnCd=Mathf.Max(_spawnCd,60f);
                    GM.AddEvent("good","🏴‍☠️ 肃清当前海域敌舰，护航赏金 "+gold+" 金，海疆暂宁");
                }
            }
        }

        private ShipEntity NearestEnemy(ShipEntity s){ ShipEntity best=null;float bd=99999;foreach(var e in EnemyShips){float d=Vector2.Distance(new Vector2(s.X,s.Z),new Vector2(e.X,e.Z));if(d<bd){bd=d;best=e;}}return best; }
        private ShipEntity NearestOurs(ShipEntity e){ ShipEntity best=null;float bd=99999;foreach(var s in S.Ships){if(!s.Military)continue;float d=Vector2.Distance(new Vector2(e.X,e.Z),new Vector2(s.X,s.Z));if(d<bd){bd=d;best=s;}}return best; }

        // ===== V6.1.5 海战分型：弓箭拦截 / 火炮溅射 / 火船自爆 =====
        private void OurShipHit(ShipEntity s, ShipEntity target)
        {
            int atk=AttackOf(s);
            if (s.ShipTypeId=="war_junk" && (target.ShipTypeId=="fire_ship"||target.ShipTypeId=="troop_boat"))
                atk=Mathf.RoundToInt(atk*1.5f);   // 弓箭战船快速拦截火船/运兵
            target.Hp-=atk;
            if (s.ShipTypeId=="cannon_ship"||s.ShipTypeId=="treasure_warship")
                foreach (var e in EnemyShips)
                    if (e!=target && Vector2.Distance(new Vector2(s.X,s.Z),new Vector2(e.X,e.Z))<=4.5f) e.Hp-=atk*0.5f;
        }
        private void EnemyShipHit(ShipEntity e, ShipEntity target)
        {
            int atk=AttackOf(e);
            target.Hp-=atk;
            if (e.ShipTypeId=="cannon_ship")
                foreach (var s in S.Ships)
                    if (s.Military && s!=target && Vector2.Distance(new Vector2(e.X,e.Z),new Vector2(s.X,s.Z))<=4.5f) s.Hp-=atk*0.5f;
        }
        private void DetonateOurFireShip(ShipEntity s)
        {
            float boom=Mathf.Max(60,AttackOf(s)*3f);
            foreach (var e in EnemyShips)
                if (Vector2.Distance(new Vector2(s.X,s.Z),new Vector2(e.X,e.Z))<=5f) e.Hp-=boom;
            GM.AddEvent("bad","🔥 我军火船冲撞自爆，烈焰覆盖敌舰！");
            s.Hp=0;
        }
        private void DetonateEnemyFireShip(ShipEntity e)
        {
            float boom=Mathf.Max(60,AttackOf(e)*3f);
            foreach (var s in S.Ships)
                if (s.Military && Vector2.Distance(new Vector2(e.X,e.Z),new Vector2(s.X,s.Z))<=5f) s.Hp-=boom;
            GM.AddEvent("bad","🔥 敌方火船贴舷自爆，冲撞我舰队！");
            e.Hp=0;
        }

        /// <summary>时代切换：我方所有船只升1级（对齐 upgradeShipsByEra）</summary>
        public void UpgradeShipsByEra(int newEra)
        {
            foreach (var s in S.Ships) if (s.Level<3) { s.Level++; s.MaxHp=MaxDurability(s); s.Hp=s.MaxHp; }
        }

        public static readonly string[] LevelNames={"普通","精良","传奇"};
        public string LevelName(ShipEntity s)=>LevelNames[Mathf.Clamp(s.Level-1,0,2)];

        /// <summary>升1级花费（对齐 getShipUpgradeCost：基础cost * 当前等级 * 1.5），满级返回 null</summary>
        public Dictionary<string,int> UpgradeCost(ShipEntity s)
        {
            if (!Defs.TryGetValue(s.ShipTypeId,out var d) || s.Level>=3) return null;
            var cost=new Dictionary<string,int>();
            foreach (var kv in d.Cost) cost[kv.Key]=Mathf.FloorToInt(kv.Value*s.Level*1.5f);
            return cost;
        }

        /// <summary>手动升级单船：普通→精良→传奇，升级后耐久/居住随等级倍率提升（住房由 PopulationSystem 自动重算）</summary>
        public bool UpgradeShip(ShipEntity s)
        {
            var cost=UpgradeCost(s);
            if (cost==null){ GM.AddEvent("bad","该船已达传奇级"); return false; }
            if (!S.CanAfford(cost)){ GM.AddEvent("bad","资源不足，无法升级"+s.Name); return false; }
            S.Pay(cost); s.Level++; s.MaxHp=MaxDurability(s); s.Hp=s.MaxHp;
            GM.AddEvent("good",$"⬆ {s.Name}升级为{LevelName(s)}（居住{s.EffectiveHousing}人）");
            return true;
        }

        /// <summary>解散/凿沉一艘我方船（视图销毁、住房自动减少）</summary>
        public void ScuttleShip(ShipEntity s)
        {
            if (s.View!=null) Object.Destroy(s.View);
            S.Ships.Remove(s); S.OceanFleets.Remove(s);
            GM.AddEvent("info","已解散一艘"+s.Name);
        }
    }

    /// <summary>船只点击桥（对齐建筑 BuildingClick，依赖根节点 Collider）</summary>
    public class ShipClick : MonoBehaviour
    {
        public ShipEntity Ship;
        public System.Action<ShipEntity> OnClicked;
        private void OnMouseDown()=>OnClicked?.Invoke(Ship);
    }
}

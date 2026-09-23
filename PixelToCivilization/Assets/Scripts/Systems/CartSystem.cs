using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.World;
using PixelToCivilization.Actors;

namespace PixelToCivilization.Systems
{
    /// <summary>车辆静态定义（对齐 v5.9.9 CART_DEFS，纳入四级锚点 T1-T3）</summary>
    public class CartDef
    {
        public string Id, Name, Icon;
        public int Capacity, Durability;
        public float Speed;
        public Dictionary<string,int> Cost;
        public long ColorHex;
    }

    /// <summary>
    /// 马车/车辆系统 —— 对齐 v5.9.9：小车/马车/大马车三型，四级锚点定价，只能在陆地，
    /// 无驾驶员停靠最近建筑、有驾驶员在建筑间运输；自动建造 AI 按人口补车。
    /// </summary>
    public class CartSystem : GameSystemBase
    {
        public readonly Dictionary<string,CartDef> Defs = new();
        private Transform _root;
        private WorldGenerator _terrain;
        private float _autoCd;

        public override void Init(GameManager gm)
        {
            base.Init(gm);
            _root=EntityViewFactory.EnsureRoot("Carts",gm.transform);
            _terrain=Object.FindObjectOfType<WorldGenerator>();
            // T1 小车15木 / T2 马车100木30石 / T3 大马车500木200石100金
            Add("small_cart","小车","🛒",1,80,0.05f,0x8B4513,new(){{"wood",15}});
            Add("medium_cart","马车","🐴",4,150,0.045f,0xC0814A,new(){{"wood",100},{"stone",30}});
            Add("large_cart","大马车","🚃",10,250,0.035f,0x9A541E,new(){{"wood",500},{"stone",200},{"gold",100}});
        }
        private void Add(string id,string name,string icon,int cap,int dur,float speed,long color,Dictionary<string,int> cost)
        => Defs[id]=new CartDef{Id=id,Name=name,Icon=icon,Capacity=cap,Durability=dur,Speed=speed,ColorHex=color,Cost=cost};

        /// <summary>按时代返回自动购车类型（古代小车→马车→大马车，工业后由载具体系承接）</summary>
        public string AutoCartTypeForEra(int era) => era<=1?"small_cart":era<=3?"medium_cart":"large_cart";

        public bool CanBuild(string type,float x,float z,out string reason)
        {
            reason=null;
            if(!Defs.ContainsKey(type)){reason="无此车型";return false;}
            if(_terrain!=null && _terrain.IsWater(x,z)){reason="车辆只能建在陆地";return false;}
            return true;
        }

        public bool BuildCart(string type,float x,float z)
        {
            if(!Defs.TryGetValue(type,out var d)) return false;
            if(_terrain!=null && _terrain.IsWater(x,z)){GM.AddEvent("bad","⚠ 车辆只能建在陆地");return false;}
            if(!S.CanAfford(d.Cost)){GM.AddEvent("bad","资源不足，无法建造"+d.Name);return false;}
            S.Pay(d.Cost);
            SpawnCart(type,x,z);
            GM.AddEvent("good",d.Icon+" 建造了"+d.Name);
            return true;
        }

        public CartEntity SpawnCart(string type,float x,float z,int level=1)
        {
            if(!Defs.TryGetValue(type,out var d)) return null;
            float h=_terrain!=null?_terrain.HeightAt(x,z):0f;
            var c=new CartEntity{CartTypeId=type,Name=d.Name,X=x,Z=z,H=h,TargetX=x,TargetZ=z,
                Capacity=d.Capacity,Durability=d.Durability,MaxDurability=d.Durability,HasDriver=true};
            // V6.1.3 恢复/指定等级（普通1/精良2/传奇3），数值随等级放大，与 UpgradeCart 口径一致
            c.Level=Mathf.Clamp(level,1,3);
            if(c.Level>=2){ float mult=c.Level==2?1.5f:2.25f;
                c.Capacity=Mathf.RoundToInt(c.Capacity*mult);
                c.MaxDurability=Mathf.RoundToInt(c.MaxDurability*mult); c.Durability=c.MaxDurability; }
            var v=EntityViewFactory.SpawnVehicle("Cart_"+d.Name,_root,VehicleKind.Cart,EntityViewFactory.Hex(d.ColorHex),
                type=="large_cart"?1.5f:type=="medium_cart"?1.15f:0.85f,type);
            v.transform.position=new Vector3(x,h,z);
            // V6.1.3 车辆可点击（对齐船只），补包围盒 + 点击桥
            var cc=v.AddComponent<BoxCollider>();cc.center=new Vector3(0,0.85f,0);
            cc.size=type=="large_cart"?new Vector3(2.6f,2.0f,4.6f):new Vector3(2.2f,1.8f,3.2f);
            var click=v.AddComponent<CartClick>();click.Cart=c;
            click.OnClicked=cart=>PixelToCivilization.UI.UIManager.Instance?.ShowCart(cart);
            c.View=v;
            S.Carts.Add(c);
            return c;
        }

        public override void Tick(float dt)
        {
            AutoPurchase(dt);
            var carts=S.Carts;
            for(int i=carts.Count-1;i>=0;i--)
            {
                var c=carts[i];
                if(c.Durability<=0){ if(c.View!=null)Object.Destroy(c.View); carts.RemoveAt(i); continue; }
                MoveCart(c,dt);
            }
        }

        private void MoveCart(CartEntity c,float dt)
        {
            if(!Defs.TryGetValue(c.CartTypeId,out var d)) return;
            float dx=c.TargetX-c.X, dz=c.TargetZ-c.Z;
            float dist=Mathf.Sqrt(dx*dx+dz*dz);
            if(dist<1.5f)
            {
                // 抵达：选下一个目标（最近的另一座建筑，无建筑则原地游走）
                PickNextTarget(c);
                return;
            }
            float sp=d.Speed*60f*(c.HasDriver?1f:0.4f);
            float nx=c.X+dx/dist*sp*dt, nz=c.Z+dz/dist*sp*dt;
            // V6.3.4：车辆只能在陆地，下一步若是水/海则放弃移动并重新选目标，绝不驶入水中
            bool onBridge=GM.Bridge!=null && GM.Bridge.IsBridgeAt(nx,nz); // V6.3.9 桥面可越水通行

            if(_terrain!=null && ((_terrain.IsWater(nx,nz)&&!onBridge)||!_terrain.InsideFrontier(nx,nz))){ PickNextTarget(c); return; } // 车不进水(桥除外)、不出疆域
            c.X=nx; c.Z=nz;
            if(onBridge) c.H=GM.Bridge.DeckHeightAt(c.X,c.Z); else if(_terrain!=null) c.H=_terrain.HeightAt(c.X,c.Z);
            if(c.View!=null)
            {
                c.View.transform.position=new Vector3(c.X,c.H,c.Z);
                float yaw=Mathf.Atan2(dx,dz)*Mathf.Rad2Deg;
                c.View.transform.rotation=Quaternion.Slerp(c.View.transform.rotation,Quaternion.Euler(0,yaw,0),0.2f);
            }
        }

        private void PickNextTarget(CartEntity c)
        {
            var bs=S.Buildings;
            if(bs.Count==0){ c.WanderTimer-=1f; if(c.WanderTimer<=0){c.WanderTimer=3f;var a=Random.value*Mathf.PI*2;float r=Random.Range(8f,40f);c.TargetX=c.X+Mathf.Cos(a)*r;c.TargetZ=c.Z+Mathf.Sin(a)*r;} return; }
            // 找与当前目标不同的最近建筑
            BuildingEntity best=null;float bd=99999;
            foreach(var b in bs){float dd=(b.X-c.X)*(b.X-c.X)+(b.Z-c.Z)*(b.Z-c.Z);if(dd>4&&dd<bd){bd=dd;best=b;}}
            if(best!=null){c.TargetX=best.X;c.TargetZ=best.Z;c.HasDriver=true;}
        }

        // 对齐 v5.9.9 第七类自动建造：每80人1辆车，资源足够时概率补车
        private void AutoPurchase(float dt)
        {
            _autoCd-=dt; if(_autoCd>0) return; _autoCd=2f;
            int target=Mathf.CeilToInt(S.Pop/80f);
            if(S.Carts.Count>=target) return;
            var type=AutoCartTypeForEra(S.Era);
            if(!Defs.TryGetValue(type,out var d)) return;
            if(!S.CanAfford(d.Cost)) return;
            if(Random.value>0.3f) return;
            // 在既有建筑附近找落点
            if(S.Buildings.Count==0) return;
            var anchor=S.Buildings[Random.Range(0,S.Buildings.Count)];
            float x=anchor.X+Random.Range(-8f,8f), z=anchor.Z+Random.Range(-8f,8f);
            if(_terrain!=null && _terrain.IsWater(x,z)) return;
            S.Pay(d.Cost); SpawnCart(type,x,z);
            GM.AddEvent("good",d.Icon+" AI购置了"+d.Name);
        }

        // ===== V6.1.3 车辆等级（普通/精良/传奇），浮窗实拍图随等级切换 =====
        public static readonly string[] CartLevelNames={"普通","精良","传奇"};
        public string CartLevelName(CartEntity c)=>CartLevelNames[Mathf.Clamp(c.Level-1,0,2)];
        public Dictionary<string,int> CartUpgradeCost(CartEntity c)
        {
            if(c.Level>=3) return null;
            return c.Level==1
                ? new Dictionary<string,int>{{"wood",100},{"gold",50}}
                : new Dictionary<string,int>{{"wood",300},{"stone",100},{"gold",200}};
        }
        public bool UpgradeCart(CartEntity c)
        {
            var cost=CartUpgradeCost(c);
            if(cost==null){ GM.AddEvent("bad","该车辆已达传奇级"); return false; }
            if(!S.CanAfford(cost)){ GM.AddEvent("bad","资源不足，无法升级"+c.Name); return false; }
            S.Pay(cost); c.Level++;
            c.Capacity=Mathf.RoundToInt(c.Capacity*1.5f);
            c.MaxDurability=Mathf.RoundToInt(c.MaxDurability*1.5f); c.Durability=c.MaxDurability;
            GM.AddEvent("good",$"⬆ {c.Name}升级为{CartLevelName(c)}");
            return true;
        }
    }

    /// <summary>车辆点击桥（对齐 ShipClick/BuildingClick，依赖根节点 Collider）</summary>
    public class CartClick : MonoBehaviour
    {
        public CartEntity Cart;
        public System.Action<CartEntity> OnClicked;
        private void OnMouseDown()=>OnClicked?.Invoke(Cart);
    }
}

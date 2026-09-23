using System.Collections.Generic;
using UnityEngine;

namespace PixelToCivilization.Actors
{
    public enum VehicleKind { Cart, SailBoat, Car, Truck, Tank, Airplane, Warship, Jet, Rocket, Hover }

    /// <summary>载具运动件引用（轮子/螺旋桨/旋翼）</summary>
    public class VehicleRig : MonoBehaviour
    {
        public readonly List<Transform> Wheels = new();
        public Transform Propeller, Rotor;
        public VehicleKind Kind;
        public Vector3 Velocity;
    }

    /// <summary>
    /// V6.1.3 程序化载具工厂：3 种车(小车/马车/大马车)、9 种船(小木船→宝船战舰)按子类型精细建模，
    /// 并保留现代/未来载具(车/坦克/飞机/火箭/悬浮)。运动件分组供 VehicleMotion 驱动。
    /// </summary>
    public static class VehicleFactory
    {
        static Material _hull, _metal, _dark, _sail, _glass, _glow, _gold, _redsail, _fire, _rope;
        static Material MatHull(Color c)=> World.ShaderHelper.Pbr(c,0f,0.22f,Mathf.RoundToInt(c.r*88+c.g*66+c.b*44),1.2f);
        static Material Metal => _metal ??= World.ShaderHelper.Pbr(new Color(0.66f,0.70f,0.76f),0.6f,0.58f,810,0.6f); // V7.0.1
        static Material Dark => _dark ??= World.ShaderHelper.Pbr(new Color(0.30f,0.28f,0.30f),0.1f,0.36f,811,0.8f);
        static Material Sail => _sail ??= World.ShaderHelper.Pbr(new Color(0.99f,0.97f,0.91f),0f,0.14f,812,0.5f);
        static Material RedSail => _redsail ??= World.ShaderHelper.Pbr(new Color(0.72f,0.18f,0.14f),0f,0.1f,814,0.7f);
        static Material Glass => _glass ??= World.ShaderHelper.Pbr(new Color(0.40f,0.72f,0.88f),0.1f,0.88f,813,0.4f);
        static Material Glow => _glow ??= World.ShaderHelper.Emissive(new Color(0.1f,0.12f,0.2f),new Color(0.3f,0.8f,1f));
        static Material Gold => _gold ??= World.ShaderHelper.Pbr(new Color(0.96f,0.78f,0.28f),0.9f,0.74f,815,0.6f);
        static Material Fire => _fire ??= World.ShaderHelper.Emissive(new Color(0.3f,0.1f,0.03f),new Color(1f,0.45f,0.1f));
        static Material Horse => _horse ??= World.ShaderHelper.Pbr(new Color(0.34f,0.23f,0.15f),0f,0.18f,816,1.2f);
        static Material _horse;
        static Material _lacquer;
        static Material Lacquer => _lacquer ??= World.ShaderHelper.Pbr(new Color(0.55f,0.12f,0.10f),0f,0.32f,817,1.0f); // 大马车朱漆华盖

        // ============ V6.7.0 CC0 真实模型映射 ============
        // 船型→Kenney Watercraft 真实模型（带帆/木质者用于古代，索引对应包内 watercraftPack_###）
        static string RealShipModel(bool war,string sub)
        {
            switch(sub)
            {
                case "small_boat": return "Ships/watercraftPack_028";  // 木质小舢板
                case "large_boat": return "Ships/watercraftPack_006";  // 长体帆船
                case "treasure_ship": return "Ships/watercraftPack_007"; // 高桅巨帆（宝船）
                case "troop_boat": return "Ships/watercraftPack_025";  // 运兵帆船
                case "fire_ship": return "Ships/watercraftPack_029";   // 木质火船
                case "cannon_ship": return "Ships/watercraftPack_006"; // 炮舰
                case "treasure_warship": return "Ships/watercraftPack_007";
                default: return war ? "Ships/watercraftPack_002" : "Ships/watercraftPack_001"; // 战船/普通帆船
            }
        }
        // 陆地载具→真实模型与目标足迹（长,宽）
        static bool GroundModel(VehicleKind kind,string sub,out string res,out float len,out float wid)
        {
            res=null;len=2f;wid=1.3f;
            switch(kind)
            {
                case VehicleKind.Cart:
                    // V6.8.1 大马车保留程序化“双马四轮华盖大车”（与中车形态明确区分、且吃阵营色），不替换为真实车模
                    if(sub=="large_cart"){ return false; }
                    if(sub=="medium_cart"){res="Cars/cart_high";len=2.4f;wid=1.6f;}
                    else {res="Cars/cart";len=1.8f;wid=1.3f;}
                    return true;
                case VehicleKind.Car: res="Cars/sedan";len=2.2f;wid=1.2f;return true;
                case VehicleKind.Truck: res="Cars/truck";len=3.2f;wid=1.4f;return true;
                default: return false; // 坦克/飞机/火箭/悬浮等保留程序化
            }
        }
        static void TryReplaceGround(GameObject root,VehicleKind kind,string sub,Color hullColor)
        {
            if(!GroundModel(kind,sub,out var res,out var len,out var wid)) return;
            var real=PixelToCivilization.Art.AssetModelLibrary.PlaceReal(root.transform,res,wid,len,0f,false,0f,"RealGround",hullColor,0.5f);
            if(real==null) return; // 缺资源：保留程序化
            // 隐藏既有程序化直子体（运动件引用仍在，旋转被停用的 Transform 无副作用）
            for(int i=root.transform.childCount-1;i>=0;i--)
            {
                var ch=root.transform.GetChild(i);
                if(ch!=real.transform) ch.gameObject.SetActive(false);
            }
        }

        public static VehicleRig Build(GameObject root, VehicleKind kind, Color hullColor, float scale=1f, string sub=null)
        {
            var rig=root.AddComponent<VehicleRig>(); rig.Kind=kind;
            _hull=MatHull(hullColor);
            root.transform.localScale=Vector3.one*scale;
            switch(kind)
            {
                case VehicleKind.Cart: BuildCart(root,rig,sub); break;
                case VehicleKind.SailBoat: BuildShipTiers(root,rig,false,sub); break;
                case VehicleKind.Car: BuildWheelVehicle(root,rig,2.2f,0.7f,0.55f,true); break;
                case VehicleKind.Truck: BuildWheelVehicle(root,rig,3.4f,1.1f,0.7f,false); break;
                case VehicleKind.Tank: BuildTank(root,rig); break;
                case VehicleKind.Airplane: BuildAirplane(root,rig,false); break;
                case VehicleKind.Jet: BuildAirplane(root,rig,true); break;
                case VehicleKind.Warship: BuildShipTiers(root,rig,true,sub); break;
                case VehicleKind.Rocket: BuildRocket(root,rig); break;
                case VehicleKind.Hover: BuildHover(root,rig); break;
            }
            // V6.7.0 陆地载具（古代马车/现代汽车/卡车）：用 CC0 真实整车模型替换程序化外观
            TryReplaceGround(root,kind,sub,hullColor);
            var mv=root.AddComponent<VehicleMotion>(); mv.Rig=rig;
            return rig;
        }

        // ================= 古代三种车 =================
        static void BuildCart(GameObject root,VehicleRig rig,string sub)
        {
            if(sub=="medium_cart") BuildHorseCart(root,rig,false);
            else if(sub=="large_cart") BuildHorseCart(root,rig,true);
            else BuildHandCart(root,rig);
        }
        // 小车：平板 + 两轮 + 车辕（人力）
        static void BuildHandCart(GameObject t,VehicleRig rig)
        {
            Part(t,"Bed",PrimitiveType.Cube,_hull,new Vector3(0,0.7f,0.1f),new Vector3(1.2f,0.18f,1.5f));
            Part(t,"SideL",PrimitiveType.Cube,_hull,new Vector3(-0.6f,0.9f,0.1f),new Vector3(0.1f,0.4f,1.5f));
            Part(t,"SideR",PrimitiveType.Cube,_hull,new Vector3( 0.6f,0.9f,0.1f),new Vector3(0.1f,0.4f,1.5f));
            rig.Wheels.Add(Wheel(t,new Vector3(-0.66f,0.4f,0.1f),0.42f));
            rig.Wheels.Add(Wheel(t,new Vector3( 0.66f,0.4f,0.1f),0.42f));
            Part(t,"ShaftL",PrimitiveType.Cube,Dark,new Vector3(-0.25f,0.65f,-1.0f),new Vector3(0.06f,0.06f,1.4f));
            Part(t,"ShaftR",PrimitiveType.Cube,Dark,new Vector3( 0.25f,0.65f,-1.0f),new Vector3(0.06f,0.06f,1.4f));
        }
        // 马车/大马车：车厢 + 拱篷 + 马，large 四轮双马加长
        static void BuildHorseCart(GameObject t,VehicleRig rig,bool large)
        {
            float len=large?2.0f:1.3f;
            float bodyW=large?1.5f:1.1f;
            Part(t,"Carriage",PrimitiveType.Cube,_hull,new Vector3(0,0.85f,0.3f),new Vector3(bodyW,0.7f,len));
            // 拱篷（半圆柱）：大马车用朱漆华盖并镶金，普通马车用素色布篷
            var canopy=Part(t,"Canopy",PrimitiveType.Cylinder,large?Lacquer:Sail,new Vector3(0,1.35f,0.3f),new Vector3(large?0.82f:0.62f,0.5f,len*0.55f));
            canopy.transform.localRotation=Quaternion.Euler(0,0,90f);
            Part(t,"Seat",PrimitiveType.Cube,Dark,new Vector3(0,1.0f,-len*0.45f),new Vector3(large?1.3f:0.9f,0.12f,0.4f));
            // 轮子（大马车四轮、加大）
            if(large){ foreach(var sx in new[]{-0.78f,0.78f})foreach(var sz in new[]{-0.6f,0.9f})rig.Wheels.Add(Wheel(t,new Vector3(sx,0.42f,sz),0.48f)); }
            else { rig.Wheels.Add(Wheel(t,new Vector3(-0.62f,0.42f,0.2f),0.46f));rig.Wheels.Add(Wheel(t,new Vector3(0.62f,0.42f,0.2f),0.46f)); }
            // 马（large 双马并驾）
            int horses=large?2:1;
            for(int i=0;i<horses;i++){ float ox=(i==0?-0.28f:0.28f); BuildHorse(t,ox,-len-0.5f); }
            Part(t,"Shaft",PrimitiveType.Cube,Dark,new Vector3(0,0.7f,-0.7f),new Vector3(0.07f,0.07f,1.6f));
            if(large)
            {
                // 大马车专属：金边华盖、四角金饰、前车灯、朱漆镶金货箱——形态/颜色/材质明确区别于中车
                Part(t,"CanopyGold",PrimitiveType.Cube,Gold,new Vector3(0,1.62f,0.3f),new Vector3(1.7f,0.08f,len*1.12f));
                foreach(var sx in new[]{-0.72f,0.72f}){ Part(t,"CornerGold",PrimitiveType.Sphere,Gold,new Vector3(sx,1.05f,-len*0.42f),Vector3.one*0.09f);
                                                        Part(t,"Lantern",PrimitiveType.Sphere,Fire,new Vector3(sx,1.25f,-len*0.55f),Vector3.one*0.12f); }
                Part(t,"Cargo",PrimitiveType.Cube,Lacquer,new Vector3(0,1.28f,0.95f),new Vector3(1.3f,0.62f,0.8f));
                Part(t,"CargoGold",PrimitiveType.Cube,Gold,new Vector3(0,1.32f,0.95f),new Vector3(1.34f,0.08f,0.84f));
            }
        }
        static void BuildHorse(GameObject t,float ox,float oz)
        {
            Part(t,"HorseBody",PrimitiveType.Capsule,Horse,new Vector3(ox,0.85f,oz),new Vector3(0.34f,0.48f,0.72f));
            Part(t,"HorseNeck",PrimitiveType.Cube,Horse,new Vector3(ox,1.05f,oz-0.55f),new Vector3(0.2f,0.5f,0.2f)).transform.localRotation=Quaternion.Euler(35,0,0);
            Part(t,"HorseHead",PrimitiveType.Cube,Horse,new Vector3(ox,1.2f,oz-0.85f),new Vector3(0.22f,0.26f,0.32f));
            Part(t,"Mane",PrimitiveType.Cube,Dark,new Vector3(ox,1.18f,oz-0.5f),new Vector3(0.08f,0.4f,0.3f));
            Part(t,"Tail",PrimitiveType.Cube,Dark,new Vector3(ox,1.0f,oz+0.6f),new Vector3(0.06f,0.4f,0.06f));
            foreach(var sx in new[]{-0.16f,0.16f})foreach(var sz in new[]{-0.25f,0.25f})
                Part(t,"HorseLeg",PrimitiveType.Capsule,Horse,new Vector3(ox+sx,0.38f,oz+sz),new Vector3(0.07f,0.32f,0.07f));
        }

        // ================= V6.3.4 船只三级 LOD =================
        // LV1=6.3.1 旧模型（屏幕占比<0.01% 远景）；LV2=新简化低模（0.01%~0.1%）；LV3=新增高精（>0.1%，同屏≤30）
        static void BuildShipTiers(GameObject root,VehicleRig rig,bool war,string sub)
        {
            // LV1：旧精模整体下沉为远景层
            var lv1=new GameObject("LV1"); lv1.transform.SetParent(root.transform,false);
            if(war) BuildWarship(lv1,rig,sub); else BuildSailBoat(lv1,rig,sub);
            // LV2：简化低模（单船体 + 单帆），中景省算力
            var lv2=new GameObject("LV2"); lv2.transform.SetParent(root.transform,false);
            BuildSimpleShip(lv2,war,sub);
            // LV3：V6.7.0 优先用 CC0 真实船模（按船型映射），缺失再回退"克隆精模+细节"
            var lv3=new GameObject("LV3"); lv3.transform.SetParent(root.transform,false);
            string shipRes=RealShipModel(war,sub);
            int sCls=ShipSizeClass(sub,war);
            float sW=new[]{0.9f,1.3f,1.7f,2.2f}[sCls], sLen=new[]{2.2f,3.4f,4.6f,6f}[sCls];
            var realShip=PixelToCivilization.Art.AssetModelLibrary.PlaceReal(lv3.transform,shipRes,sW,sLen,0f,false,0f,"RealShip");
            if(realShip==null)
            {
                var clone=Object.Instantiate(lv1,lv3.transform); clone.name="Base";
                AddShipDetail(lv3,war,sub);
            }
        }
        // 尺寸档：0 小 / 1 中 / 2 大 / 3 巨（宝船级）
        static int ShipSizeClass(string sub,bool war)
        {
            switch(sub){
                case "small_boat": case "fire_ship": return 0;
                case "treasure_ship": case "treasure_warship": return 3;
                case "large_boat": case "cannon_ship": return 2;
                default: return 1;
            }
        }
        static void BuildSimpleShip(GameObject t,bool war,string sub)
        {
            int cls=ShipSizeClass(sub,war);
            float w=new[]{0.9f,1.3f,1.7f,2.2f}[cls], h=new[]{0.4f,0.55f,0.7f,0.9f}[cls], len=new[]{2.2f,3.4f,4.6f,6f}[cls];
            HullBase(t,w,h,len,_hull,cls>=1);
            if(cls>=1) MakeMastSail(t,0f,len*0.62f,len*0.42f,war?RedSail:Sail); // 单帆
            else Part(t,"Oar",PrimitiveType.Cube,Dark,new Vector3(0.5f,0.5f,0),new Vector3(0.05f,0.05f,1.4f));
        }
        // LV3 高精细节：栏杆立柱、舷窗/炮门、甲板货桶、斜缆、船首像、阵营旗饰
        static void AddShipDetail(GameObject t,bool war,string sub)
        {
            int cls=ShipSizeClass(sub,war);
            float len=new[]{2.2f,3.4f,4.6f,6f}[cls], w=new[]{0.9f,1.3f,1.7f,2.2f}[cls], h=new[]{0.4f,0.55f,0.7f,0.9f}[cls];
            int posts=cls==0?3:cls==1?5:cls==2?7:9;
            // 两舷栏杆立柱 + 横栏
            for(int i=0;i<posts;i++){ float zz=-len*0.42f+i*(len*0.84f/Mathf.Max(1,posts-1));
                foreach(var sx in new[]{-1f,1f}) Part(t,"RailPost",PrimitiveType.Cube,Dark,new Vector3(sx*w*0.5f,h+0.34f,zz),new Vector3(0.07f,0.42f,0.07f)); }
            foreach(var sx in new[]{-1f,1f}) Part(t,"Rail",PrimitiveType.Cube,Dark,new Vector3(sx*w*0.5f,h+0.5f,0),new Vector3(0.06f,0.06f,len*0.86f));
            // 舷窗（民用圆窗 / 军用炮门），数量随尺寸
            int ports=cls+2;
            for(int i=0;i<ports;i++){ float zz=-len*0.3f+i*(len*0.6f/Mathf.Max(1,ports-1));
                foreach(var sx in new[]{-1f,1f}){
                    Part(t,war?"GunPort2":"Porthole",PrimitiveType.Cube,war?Dark:Metal,new Vector3(sx*w*0.52f,h*0.55f,zz),new Vector3(0.09f,0.16f,0.2f));
                    if(war){ var bar=Part(t,"GunBarrel",PrimitiveType.Cylinder,Metal,new Vector3(sx*w*0.6f,h*0.55f,zz),new Vector3(0.06f,0.28f,0.06f)); bar.transform.localRotation=Quaternion.Euler(0,0,90f); }
                } }
            // 甲板货桶 / 补给箱（中大型）
            if(cls>=1){ int crates=cls; for(int i=0;i<crates;i++){ float ox=(i%2==0?-0.3f:0.3f)*w;
                Part(t,"Barrel",PrimitiveType.Cylinder,Metal,new Vector3(ox,h+0.25f,-len*0.1f-i*0.5f),new Vector3(0.22f,0.3f,0.22f));
                Part(t,"Crate",PrimitiveType.Cube,Sail,new Vector3(-ox,h+0.2f,len*0.15f+i*0.4f),Vector3.one*0.34f); } }
            // 斜桁缆绳（前后各一，细圆柱斜拉），宝船级加到 4 根
            int ropes=cls>=3?4:cls>=1?2:0;
            for(int i=0;i<ropes;i++){ float sx=i%2==0?-0.4f:0.4f; float fz=i<2?-len*0.3f:len*0.3f;
                var rope=Part(t,"Rigging",PrimitiveType.Cylinder,Dark,new Vector3(sx*w*0.3f,len*0.34f,fz),new Vector3(0.03f,len*0.5f,0.03f));
                rope.transform.localRotation=Quaternion.Euler(fz<0?22f:-22f,0,fz<0?-8f:8f); }
            // 船首像（中大型：金色兽首）+ 尾楼金饰
            if(cls>=2){ Part(t,"Figurehead",PrimitiveType.Sphere,Gold,new Vector3(0,h*0.7f,-len*0.5f-0.32f),Vector3.one*0.3f);
                        Part(t,"SternGold",PrimitiveType.Cube,Gold,new Vector3(0,h+0.3f,len*0.46f),new Vector3(w*0.7f,0.12f,0.12f)); }
            // 宝船级：双层飞檐楼阁顶 + 灯笼
            if(cls==3){ Part(t,"LanternL",PrimitiveType.Sphere,Fire,new Vector3(-w*0.4f,h+0.7f,len*0.3f),Vector3.one*0.16f);
                        Part(t,"LanternR",PrimitiveType.Sphere,Fire,new Vector3( w*0.4f,h+0.7f,len*0.3f),Vector3.one*0.16f);
                        Part(t,"Eave",PrimitiveType.Cube,war?RedSail:Gold,new Vector3(0,h+1.5f,1.0f),new Vector3(w*1.05f,0.14f,2.2f)); }
            // 阵营色小燕尾旗（舰艏）
            Part(t,"Pennant",PrimitiveType.Cube,war?RedSail:Gold,new Vector3(0,h+0.2f,-len*0.5f),new Vector3(0.04f,0.16f,0.4f));
        }

        // ================= 民用船（4 种） =================
        static void BuildSailBoat(GameObject t,VehicleRig rig,string sub)
        {
            switch(sub)
            {
                case "small_boat": BuildRowboat(t); break;
                case "large_boat": BuildMerchant(t,rig,false); break;
                case "treasure_ship": BuildTreasure(t,rig,false); break;
                case "troop_boat": BuildTroopBoat(t,rig); break;
                default: BuildJunk(t,rig,1,false); break; // medium_boat 帆船
            }
        }
        // 小木船：舢板 + 两桨
        static void BuildRowboat(GameObject t)
        {
            HullBase(t,0.9f,0.4f,2.2f,_hull,false);
            Part(t,"OarL",PrimitiveType.Cube,Dark,new Vector3(-0.55f,0.55f,0),new Vector3(0.05f,0.05f,1.6f)).transform.localRotation=Quaternion.Euler(0,0,12);
            Part(t,"OarR",PrimitiveType.Cube,Dark,new Vector3( 0.55f,0.55f,0),new Vector3(0.05f,0.05f,1.6f)).transform.localRotation=Quaternion.Euler(0,0,-12);
        }
        // 帆船：1 桅大帆 + 小尾楼
        static void BuildJunk(GameObject t,VehicleRig rig,int masts,bool war)
        {
            HullBase(t,1.3f,0.55f,3.4f,_hull,true);
            MakeMastSail(t,0f,2.2f,1.5f,war?RedSail:Sail);
            Part(t,"Stern",PrimitiveType.Cube,_hull,new Vector3(0,0.85f,1.25f),new Vector3(1.0f,0.6f,0.8f));
            MakeFlag(t,0f,2.4f,0f,war);
        }
        // 大船：两桅 + 多舱 + 船舷
        static void BuildMerchant(GameObject t,VehicleRig rig,bool war)
        {
            HullBase(t,1.7f,0.7f,4.6f,_hull,true);
            MakeMastSail(t,-0.7f,2.6f,1.7f,Sail);MakeMastSail(t,0.7f,2.3f,1.4f,Sail);
            Part(t,"CabinA",PrimitiveType.Cube,Sail,new Vector3(-0.45f,0.95f,0.6f),new Vector3(0.8f,0.7f,1.2f));
            Part(t,"CabinB",PrimitiveType.Cube,Sail,new Vector3(0.45f,0.95f,0.6f),new Vector3(0.8f,0.7f,1.2f));
            Part(t,"Stern",PrimitiveType.Cube,_hull,new Vector3(0,1.0f,1.7f),new Vector3(1.3f,0.8f,1.0f));
            if(war) GunRow(t,1.7f,4.6f,2);
            MakeFlag(t,0f,2.8f,0f,war);
        }
        // 运兵船：宽舱遮棚 + 单帆 + 排桨
        static void BuildTroopBoat(GameObject t,VehicleRig rig)
        {
            HullBase(t,1.6f,0.6f,3.8f,_hull,true);
            Part(t,"TroopCover",PrimitiveType.Cube,Sail,new Vector3(0,1.0f,0.3f),new Vector3(1.5f,0.5f,2.2f));
            MakeMastSail(t,0f,2.3f,1.4f,Sail);
            for(int i=-1;i<=1;i++){ Part(t,"OarL",PrimitiveType.Cube,Dark,new Vector3(-0.85f,0.5f,i*0.9f),new Vector3(0.05f,0.05f,1.4f));Part(t,"OarR",PrimitiveType.Cube,Dark,new Vector3(0.85f,0.5f,i*0.9f),new Vector3(0.05f,0.05f,1.4f)); }
        }
        // 宝船：三桅巨帆 + 多层楼阁 + 金龙首 + 金饰
        static void BuildTreasure(GameObject t,VehicleRig rig,bool war)
        {
            HullBase(t,2.2f,0.9f,6.0f,_hull,true);
            // 龙首
            var dragon=Part(t,"DragonHead",PrimitiveType.Cube,Gold,new Vector3(0,0.8f,-3.1f),new Vector3(0.5f,0.5f,0.6f));
            MakeMastSail(t,-1.0f,3.2f,2.2f,war?RedSail:Sail);
            MakeMastSail(t,0f,3.6f,2.5f,war?RedSail:Sail);
            MakeMastSail(t,1.0f,3.0f,1.9f,war?RedSail:Sail);
            // 多层楼阁
            Part(t,"Pavilion1",PrimitiveType.Cube,Gold,new Vector3(0,1.1f,1.0f),new Vector3(1.8f,0.6f,2.0f));
            Part(t,"Pavilion2",PrimitiveType.Cube,Sail,new Vector3(0,1.7f,1.0f),new Vector3(1.3f,0.6f,1.4f));
            var roof=Part(t,"PavRoof",PrimitiveType.Cube,RedSail,new Vector3(0,2.2f,1.0f),new Vector3(1.6f,0.18f,1.7f));
            Part(t,"SternTower",PrimitiveType.Cube,Gold,new Vector3(0,1.3f,2.4f),new Vector3(1.5f,1.2f,1.0f));
            if(war) GunRow(t,2.2f,6.0f,4);
            MakeFlag(t,0f,3.9f,0f,true);
        }

        // ================= 军用船（4 种） =================
        static void BuildWarship(GameObject t,VehicleRig rig,string sub)
        {
            switch(sub)
            {
                case "fire_ship": BuildFireShip(t); break;
                case "cannon_ship": BuildCannonShip(t,rig); break;
                case "treasure_warship": BuildTreasure(t,rig,true); break;
                default: BuildWarJunk(t,rig); break; // war_junk
            }
        }
        static void BuildWarJunk(GameObject t,VehicleRig rig)
        {
            HullBase(t,1.5f,0.6f,4.0f,_hull,true);
            MakeMastSail(t,-0.5f,2.4f,1.6f,RedSail);MakeMastSail(t,0.6f,2.1f,1.3f,RedSail);
            // 女墙挡板 + 撞角
            Part(t,"Bulwark",PrimitiveType.Cube,Dark,new Vector3(0,0.95f,0),new Vector3(1.55f,0.4f,3.6f));
            Part(t,"Ram",PrimitiveType.Cube,Metal,new Vector3(0,0.4f,-2.3f),new Vector3(0.3f,0.3f,0.9f));
            GunRow(t,1.5f,4.0f,2);
            MakeFlag(t,0f,2.6f,0f,true);
        }
        static void BuildCannonShip(GameObject t,VehicleRig rig)
        {
            HullBase(t,1.9f,0.75f,5.0f,_hull,true);
            MakeMastSail(t,-0.7f,2.7f,1.8f,RedSail);MakeMastSail(t,0.7f,2.4f,1.5f,RedSail);
            Part(t,"Stern",PrimitiveType.Cube,_hull,new Vector3(0,1.2f,1.8f),new Vector3(1.4f,1.0f,1.2f));
            GunRow(t,1.9f,5.0f,3); // 三层炮门
            MakeFlag(t,0f,2.9f,0f,true);
        }
        static void BuildFireShip(GameObject t)
        {
            HullBase(t,1.1f,0.45f,2.8f,_hull,true);
            Part(t,"Faggot",PrimitiveType.Cube,Dark,new Vector3(0,0.85f,0),new Vector3(1.0f,0.7f,1.6f));
            for(int i=0;i<3;i++){var f=Part(t,"Flame",PrimitiveType.Sphere,Fire,new Vector3((i-1)*0.35f,1.3f+(i%2)*0.2f,0),Vector3.one*(0.4f+i*0.05f));}
            MakeMastSail(t,0f,1.8f,1.0f,RedSail);
        }

        // ---- 船体通用件 ----
        static void HullBase(GameObject t,float w,float h,float len,Material mat,bool pointed)
        {
            Part(t,"Hull",PrimitiveType.Cube,mat,new Vector3(0,h*0.5f,0),new Vector3(w,h,len));
            Part(t,"Deck",PrimitiveType.Cube,Sail,new Vector3(0,h+0.04f,0),new Vector3(w*0.92f,0.08f,len*0.92f));
            // 船舷
            Part(t,"GunwaleL",PrimitiveType.Cube,mat,new Vector3(-w*0.5f,h+0.12f,0),new Vector3(0.12f,0.3f,len*0.96f));
            Part(t,"GunwaleR",PrimitiveType.Cube,mat,new Vector3( w*0.5f,h+0.12f,0),new Vector3(0.12f,0.3f,len*0.96f));
            // V6.6.1 参考图木板船体：两舷各两条深色板缝横纹
            for(int pi=0;pi<2;pi++){ float py=h*(0.34f+pi*0.34f);
                Part(t,"PlankL",PrimitiveType.Cube,Dark,new Vector3(-w*0.505f,py,0),new Vector3(0.05f,0.05f,len*0.88f));
                Part(t,"PlankR",PrimitiveType.Cube,Dark,new Vector3( w*0.505f,py,0),new Vector3(0.05f,0.05f,len*0.88f)); }
            if(pointed){ // 尖头船首（旋转块）+ 上翘尾 + 前伸船首斜桅
                var bow=Part(t,"Bow",PrimitiveType.Cube,mat,new Vector3(0,h*0.45f,-len*0.5f-0.25f),new Vector3(w*0.7f,h*0.8f,0.6f));
                bow.transform.localRotation=Quaternion.Euler(-18,0,0);
                var sprite=Part(t,"Bowsprit",PrimitiveType.Cube,Dark,new Vector3(0,h*0.9f,-len*0.5f-0.55f),new Vector3(0.07f,0.07f,1.15f));
                sprite.transform.localRotation=Quaternion.Euler(-12,0,0);
            }
        }
        static void MakeMastSail(GameObject t,float ox,float mastH,float sailH,Material sailMat)
        {
            Part(t,"Mast",PrimitiveType.Cylinder,Dark,new Vector3(ox,mastH*0.5f,0),new Vector3(0.08f,mastH,0.08f));
            Part(t,"Yard",PrimitiveType.Cube,Dark,new Vector3(ox,mastH*0.75f,0),new Vector3(sailH*1.15f,0.07f,0.07f));
            var sail=Part(t,"Sail",PrimitiveType.Cube,sailMat,new Vector3(ox,mastH*0.42f,0.02f),new Vector3(sailH,sailH*0.85f,0.05f));
            Part(t,"SailStripe",PrimitiveType.Cube,Dark,new Vector3(ox,mastH*0.42f,0.05f),new Vector3(sailH*1.02f,0.06f,0.06f));
        }
        static void GunRow(GameObject t,float w,float len,int rows)
        {
            int perRow=Mathf.Max(2,(int)(len/1.4f));
            for(int r=0;r<rows;r++)
            for(int i=0;i<perRow;i++){
                float z=-len*0.3f+i*(len*0.6f/Mathf.Max(1,perRow-1)); float yy=0.55f+r*0.28f;
                foreach(var sx in new[]{-1f,1f}){ var g=Part(t,"GunPort",PrimitiveType.Cube,Dark,new Vector3(sx*w*0.52f,yy,z),new Vector3(0.1f,0.18f,0.22f));
                    var bar=Part(t,"Barrel",PrimitiveType.Cylinder,Metal,new Vector3(sx*w*0.58f,yy,z),new Vector3(0.07f,0.3f,0.07f));bar.transform.localRotation=Quaternion.Euler(0,0,90f); }
            }
        }
        static void MakeFlag(GameObject t,float x,float y,float z,bool red)
        {
            Part(t,"Pole",PrimitiveType.Cube,Dark,new Vector3(x,y,z),new Vector3(0.05f,0.5f,0.05f));
            Part(t,"Flag",PrimitiveType.Cube,red?RedSail:Gold,new Vector3(x+0.22f,y+0.18f,z),new Vector3(0.4f,0.24f,0.03f));
        }

        // ================= 现代/未来载具（保留） =================
        static void BuildWheelVehicle(GameObject root,VehicleRig rig,float len,float bodyH,float bodyW,bool hasCab)
        {
            Part(root,"Body",PrimitiveType.Cube,_hull,new Vector3(0,0.55f,0),new Vector3(bodyW,bodyH,len));
            if(hasCab) Part(root,"Cab",PrimitiveType.Cube,Glass,new Vector3(0,1.05f,-len*0.12f),new Vector3(bodyW*0.9f,0.5f,len*0.4f));
            else Part(root,"Cargo",PrimitiveType.Cube,Metal,new Vector3(0,1.0f,len*0.12f),new Vector3(bodyW*0.98f,0.9f,len*0.6f));
            float wx=bodyW*0.52f, zs=len*0.34f;
            foreach(var sx in new[]{-1f,1f})foreach(var sz in new[]{-zs,zs})
                rig.Wheels.Add(Wheel(root,new Vector3(sx*wx,0.32f,sz),0.34f));
            Part(root,"BumperF",PrimitiveType.Cube,Dark,new Vector3(0,0.5f,-len*0.52f),new Vector3(bodyW,0.25f,0.12f));
        }
        static void BuildTank(GameObject root,VehicleRig rig)
        {
            Part(root,"Hull",PrimitiveType.Cube,_hull,new Vector3(0,0.5f,0),new Vector3(1.5f,0.6f,3.0f));
            Part(root,"Turret",PrimitiveType.Cylinder,Metal,new Vector3(0,1.0f,0.2f),new Vector3(0.7f,0.4f,0.7f));
            var barrel=Part(root,"Barrel",PrimitiveType.Cylinder,Dark,new Vector3(0,1.0f,-1.6f),new Vector3(0.08f,1.4f,0.08f));
            barrel.transform.localRotation=Quaternion.Euler(0,0,90f);
            foreach(var sx in new[]{-1f,1f}) Part(root,"Track"+sx,PrimitiveType.Cube,Dark,new Vector3(sx*0.82f,0.35f,0),new Vector3(0.3f,0.5f,3.2f));
        }
        static void BuildAirplane(GameObject root,VehicleRig rig,bool jet)
        {
            Part(root,"Fuselage",PrimitiveType.Cylinder,_hull,new Vector3(0,0.6f,0),jet?new Vector3(0.45f,2.4f,0.45f):new Vector3(0.55f,2.0f,0.55f)).transform.localRotation=Quaternion.Euler(90,0,0);
            Part(root,"Nose",PrimitiveType.Sphere,Glass,new Vector3(0,0.6f,-(jet?2.4f:2.0f)),Vector3.one*0.42f);
            Part(root,"WingL",PrimitiveType.Cube,Metal,new Vector3(-1.3f,0.6f,0.2f),new Vector3(2.2f,0.08f,0.9f));
            Part(root,"WingR",PrimitiveType.Cube,Metal,new Vector3( 1.3f,0.6f,0.2f),new Vector3(2.2f,0.08f,0.9f));
            Part(root,"TailV",PrimitiveType.Cube,Metal,new Vector3(0,1.1f,jet?2.2f:1.8f),new Vector3(0.08f,0.9f,0.7f));
            Part(root,"TailH",PrimitiveType.Cube,Metal,new Vector3(0,0.75f,jet?2.3f:1.9f),new Vector3(1.4f,0.07f,0.4f));
            if(jet){ Part(root,"EngineL",PrimitiveType.Cylinder,Dark,new Vector3(-0.9f,0.55f,0.3f),new Vector3(0.22f,0.7f,0.22f)).transform.localRotation=Quaternion.Euler(90,0,0);
                     Part(root,"EngineR",PrimitiveType.Cube,Dark,new Vector3( 0.9f,0.55f,0.3f),new Vector3(0.22f,0.7f,0.22f)).transform.localRotation=Quaternion.Euler(90,0,0); }
            else rig.Propeller=MakePropeller(root,new Vector3(0,0.6f,-2.05f));
        }
        static Transform MakePropeller(GameObject root,Vector3 pos){
            var hub=new GameObject("Propeller");hub.transform.SetParent(root.transform);hub.transform.localPosition=pos;
            Part(hub,"BladeA",PrimitiveType.Cube,Dark,Vector3.zero,new Vector3(1.6f,0.08f,0.1f));
            Part(hub,"BladeB",PrimitiveType.Cube,Dark,Vector3.zero,new Vector3(0.1f,1.6f,0.08f));
            Part(hub,"Nose",PrimitiveType.Sphere,Dark,Vector3.zero,Vector3.one*0.12f);
            return hub.transform;
        }
        static void BuildRocket(GameObject root,VehicleRig rig)
        {
            Part(root,"Body",PrimitiveType.Cylinder,Glow,new Vector3(0,1.6f,0),new Vector3(0.5f,1.6f,0.5f));
            Part(root,"Nose",PrimitiveType.Cylinder,_hull,new Vector3(0,3.4f,0),new Vector3(0.5f,0.3f,0.5f));
            foreach(var sx in new[]{-1f,1f}) Part(root,"Fin"+sx,PrimitiveType.Cube,Metal,new Vector3(sx*0.5f,0.4f,0),new Vector3(0.1f,0.7f,0.6f));
            Part(root,"Engine",PrimitiveType.Cylinder,Dark,new Vector3(0,0.1f,0),new Vector3(0.35f,0.2f,0.35f));
        }
        static void BuildHover(GameObject root,VehicleRig rig)
        {
            Part(root,"Cabin",PrimitiveType.Sphere,Glass,new Vector3(0,0.7f,0),new Vector3(1.0f,0.55f,1.4f));
            Part(root,"Ring",PrimitiveType.Cylinder,Glow,new Vector3(0,0.35f,0),new Vector3(1.3f,0.12f,1.3f));
            Part(root,"Core",PrimitiveType.Sphere,Glow,new Vector3(0,0.3f,0),Vector3.one*0.3f);
        }

        // ---- 基础件 ----
        static Transform Wheel(GameObject root,Vector3 pos,float r)
        {
            var pivot=new GameObject("WheelPivot");pivot.transform.SetParent(root.transform);
            pivot.transform.localPosition=pos;
            Part(pivot,"Wheel",PrimitiveType.Cylinder,Dark,Vector3.zero,new Vector3(r,0.18f,r));
            pivot.transform.localRotation=Quaternion.Euler(0,0,90f);
            return pivot.transform;
        }
        static GameObject Part(GameObject root,string name,PrimitiveType type,Material mat,Vector3 local,Vector3 scale)
        { return Part(root.transform,name,type,mat,local,scale); }
        static GameObject Part(Transform parent,string name,PrimitiveType type,Material mat,Vector3 local,Vector3 scale)
        {
            var go=GameObject.CreatePrimitive(type);
            var c=go.GetComponent<Collider>(); if(c)Object.Destroy(c);
            go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=local;go.transform.localScale=scale;
            var r=go.GetComponent<Renderer>();r.sharedMaterial=mat;
            r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;r.receiveShadows=true;
            return go;
        }
    }
}

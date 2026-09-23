using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.World;

namespace PixelToCivilization.Buildings
{
    /// <summary>
    /// V6.3.7 功能建筑独立轮廓（BuildingMeshFactory 的 partial）。
    /// 原则：功能/生产类建筑不再套统一"四面墙+坡屋顶房屋主体"，而是以其现实作业形态作为
    /// 第一轮廓（井台/硐口井架/露天原木场/开放摊位/高架仓/三层坛/围栏马厩/大水车/高炉/
    /// 馒头窑/船台起重机），并随时代切换木石→砖铁→钢玻材质与形制，Lv2/Lv3 保留等级饰件。
    /// </summary>
    public partial class BuildingMeshFactory
    {
        // 返回 true 表示该类型由独立形制接管（不再走通用房屋主体）
        private bool BuildSpecial(GameObject host, BuildingEntity b, BuildingStyle s, int era)
        {
            Transform t = host.transform;
            float sc = s.BuildingScale;
            switch (b.Type)
            {
                case "well":            BuildWell(t,s,era,sc); break;
                case "granary":         BuildGranary(t,s,era,sc); break;
                case "altar":           BuildAltar(t,s,era,sc); break;
                case "lumbermill":      BuildLumberMill(t,s,era,sc); break;
                case "mine":            BuildMine(t,s,era,sc); break;
                case "market": case "caravanserai": case "tea_house": BuildMarket(t,b.Type,s,era,sc); break;
                case "stable":          BuildStable(t,s,era,sc); break;
                case "water_mill":      BuildWaterMill(t,s,era,sc); break;
                case "bronze_forge": case "iron_smelter": case "workshop":
                case "gunpowder_mill": case "arsenal": case "modern_arsenal": case "factory_pre": case "factory_modern":
                                        BuildForge(t,b.Type,s,era,sc); break;
                case "porcelain_kiln": case "brick_works": BuildKiln(t,b.Type,s,era,sc); break;
                case "shipyard_pre": case "treasure_shipyard": case "dockyard_modern":
                                        BuildShipyard(t,b.Type,s,era,sc); break;
                case "sea_port": case "customs_house":
                                        BuildPort(t,b.Type,s,era,sc); break;
                default: return false;
            }
            SpecialTrim(t,b,s,era);
            return true;
        }

        // 统一台基 + 等级饰件（独立形制也保留 Lv2 银边 / Lv3 金顶识别）
        private void SpecialTrim(Transform t, BuildingEntity b, BuildingStyle s, int era)
        {
            Box("Base", new Vector3(0,-0.18f,0), new Vector3(3.6f*s.BuildingScale,0.36f,3.6f*s.BuildingScale), Stone, t);
            AddLevelTrim(t, b.Level, 3f*s.BuildingScale, 3f*s.BuildingScale, TrimHeight(b.Type,s,era));
            if (CurHigh && era<=4) // LV3 近景：台基角石
            {
                Box("Rubble",new Vector3(-1.35f,0.05f,1.35f),Vector3.one*0.22f,Stone,t);
                Box("Rubble",new Vector3(1.35f,0.05f,-1.35f),Vector3.one*0.22f,Stone,t);
            }
        }
        private float TrimHeight(string id,BuildingStyle s,int era)
        {
            if(id=="mine"||id=="shipyard_pre"||id=="treasure_shipyard"||id=="sea_port") return 2.6f;
            if(id=="water_mill") return 2.2f;
            return 1.8f;
        }

        private GameObject Sph(string name, Vector3 pos, Vector3 scale, Material mat, Transform p)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name=name; g.transform.SetParent(p,false);
            g.transform.localPosition=pos; g.transform.localScale=scale;
            DestroyCol(g); g.GetComponent<Renderer>().material=mat; return g;
        }

        // ---------------- 水井：露天圆形井台 + 双柱辘轳 + 小坡顶（近代改压水井） ----------------
        private void BuildWell(Transform t, BuildingStyle s,int era,float sc)
        {
            var wall=WallMat(s,era);
            // 井圈（圆）
            Cyl("WellRing",new Vector3(0,0.32f*sc,0),new Vector3(0.72f*sc,0.42f*sc,0.72f*sc),Stone,t);
            Cyl("WellWater",new Vector3(0,0.5f*sc,0),new Vector3(0.56f*sc,0.06f,0.56f*sc),Mat(new Color(0.12f,0.28f,0.42f)),t);
            if(era>=5)
            {   // 近代压水井：金属立管 + 压柄 + 出水嘴
                Cyl("Pump",new Vector3(0,1.0f*sc,0),new Vector3(0.1f,1.0f*sc,0.1f),Metal,t);
                Box("PumpHead",new Vector3(0,1.55f*sc,0),new Vector3(0.34f,0.3f,0.3f),Metal,t);
                Box("PumpLever",new Vector3(0.32f,1.62f*sc,0),new Vector3(0.7f,0.07f,0.07f),Metal,t);
                Box("Spout",new Vector3(0,1.3f,0.3f),new Vector3(0.1f,0.1f,0.5f),Metal,t);
            }
            else
            {
                Box("PostL",new Vector3(-0.55f*sc,1.05f*sc,0),new Vector3(0.12f,1.5f*sc,0.12f),Wood,t);
                Box("PostR",new Vector3( 0.55f*sc,1.05f*sc,0),new Vector3(0.12f,1.5f*sc,0.12f),Wood,t);
                Cyl("Axle",new Vector3(0,1.75f*sc,0),new Vector3(0.07f,1.3f*sc,0.07f),Wood,t).transform.localRotation=Quaternion.Euler(0,0,90);
                var roofMat=RoofMat(s,era);
                var l=Box("RoofL",new Vector3(-0.42f*sc,2.05f*sc,0),new Vector3(0.95f*sc,0.1f,1.1f*sc),roofMat,t);l.transform.localRotation=Quaternion.Euler(0,0,22);
                var r=Box("RoofR",new Vector3( 0.42f*sc,2.05f*sc,0),new Vector3(0.95f*sc,0.1f,1.1f*sc),roofMat,t);r.transform.localRotation=Quaternion.Euler(0,0,-22);
                Box("Bucket",new Vector3(0,1.25f*sc,0),new Vector3(0.22f,0.28f,0.22f),Dark,t);
                Box("Rope",new Vector3(0,1.5f*sc,0),new Vector3(0.03f,0.5f,0.03f),Dark,t);
            }
            if(CurHigh){ Box("StoneSlab",new Vector3(0.95f,0.06f,0.7f),new Vector3(0.7f,0.12f,0.6f),Stone,t);}
        }

        // ---------------- 粮仓：四柱高架仓 + 锥形茅顶（近代改金属圆筒仓） ----------------
        private void BuildGranary(Transform t,BuildingStyle s,int era,float sc)
        {
            if(era>=5)
            {
                Cyl("Silo",new Vector3(0,1.15f*sc,0),new Vector3(0.95f*sc,1.15f*sc,0.95f*sc),Metal,t);
                Sph("SiloCap",new Vector3(0,2.32f*sc,0),new Vector3(0.98f*sc,0.5f,0.98f*sc),Metal,t);
                Box("SiloDoor",new Vector3(0,0.45f,0.9f),new Vector3(0.5f,0.8f,0.06f),Dark,t);
                if(CurHigh) Cyl("Vent",new Vector3(0,2.75f,0),new Vector3(0.12f,0.4f,0.12f),Metal,t);
                return;
            }
            var wm=Mat(new Color(0.78f,0.68f,0.42f));
            foreach(var p in new[]{new Vector3(-0.85f,0.5f,-0.85f),new Vector3(0.85f,0.5f,-0.85f),new Vector3(0.85f,0.5f,0.85f),new Vector3(-0.85f,0.5f,0.85f)})
                Box("Stilt",p*sc+new Vector3(0,0,0),new Vector3(0.14f,1.0f*sc,0.14f),Wood,t);
            Cyl("Bin",new Vector3(0,1.35f*sc,0),new Vector3(1.0f*sc,0.75f*sc,1.0f*sc),wm,t);
            Box("BinBand",new Vector3(0,1.55f*sc,0),new Vector3(2.0f*sc,0.08f,2.0f*sc),Wood,t);
            PitchedRoof(t,2.0f*sc,2.0f*sc,1.75f*sc,0.7f*sc,RoofMat(s,era),true);
            if(CurHigh){ Box("Sack1",new Vector3(-1.0f,0.3f,1.0f),Vector3.one*0.34f,Cloth(new Color(0.72f,0.62f,0.3f)),t);
                         Box("Sack2",new Vector3(1.05f,0.28f,0.95f),Vector3.one*0.3f,Cloth(new Color(0.72f,0.62f,0.3f)),t);}
        }

        // ---------------- 祭坛：三层收分露天石台 + 鼎炉（无墙无屋盖） ----------------
        private void BuildAltar(Transform t,BuildingStyle s,int era,float sc)
        {
            Box("Tier1",new Vector3(0,0.15f,0),new Vector3(3.0f*sc,0.3f,3.0f*sc),Stone,t);
            Box("Tier2",new Vector3(0,0.45f,0),new Vector3(2.2f*sc,0.3f,2.2f*sc),Stone,t);
            Box("Tier3",new Vector3(0,0.75f,0),new Vector3(1.4f*sc,0.3f,1.4f*sc),Stone,t);
            // 鼎/香炉
            Cyl("Ding",new Vector3(0,1.05f,0),new Vector3(0.34f,0.3f,0.34f),era>=4?Metal:Dark,t);
            Sph("Incense",new Vector3(0,1.28f,0),new Vector3(0.22f,0.18f,0.22f),Fire,t);
            foreach(var p in new[]{new Vector3(-0.22f,0.86f,0),new Vector3(0.22f,0.86f,0),new Vector3(0,0.86f,-0.22f)})
                Box("DingLeg",p,new Vector3(0.06f,0.28f,0.06f),Dark,t);
            // 两侧幡杆
            Box("PoleL",new Vector3(-1.25f,1.2f,0),new Vector3(0.07f,2.2f,0.07f),Wood,t);
            Box("PoleR",new Vector3( 1.25f,1.2f,0),new Vector3(0.07f,2.2f,0.07f),Wood,t);
            Box("BannerL",new Vector3(-1.25f,1.9f,0.18f),new Vector3(0.4f,0.9f,0.04f),Cloth(s.AccentColor),t);
            Box("BannerR",new Vector3( 1.25f,1.9f,0.18f),new Vector3(0.4f,0.9f,0.04f),Cloth(s.AccentColor),t);
            if(CurHigh) foreach(var p in new[]{new Vector3(-0.6f,0.95f,0.6f),new Vector3(0.6f,0.95f,-0.6f)})
                Sph("Candle",p,Vector3.one*0.12f,Fire,t);
        }

        // ---------------- 伐木场：露天原木堆场 + 三角锯架 + 单面斜棚（无围墙） ----------------
        private void BuildLumberMill(Transform t,BuildingStyle s,int era,float sc)
        {
            // 斜棚：两后柱 + 单坡顶
            Material post=era>=5?Metal:Wood;
            Box("PostL",new Vector3(-1.0f,1.0f*sc,-0.9f),new Vector3(0.12f,2.0f*sc,0.12f),post,t);
            Box("PostR",new Vector3( 1.0f,1.0f*sc,-0.9f),new Vector3(0.12f,2.0f*sc,0.12f),post,t);
            var lean=Box("LeanRoof",new Vector3(0,1.9f*sc,0.1f),new Vector3(2.4f*sc,0.12f,1.8f*sc),RoofMat(s,era),t);
            lean.transform.localRotation=Quaternion.Euler(-14,0,0);
            Box("WorkBench",new Vector3(0,0.6f,-0.5f),new Vector3(1.6f,0.16f,0.6f),Wood,t);
            // 三角锯架（A 架）
            var a=Box("SawA",new Vector3(-0.35f,0.55f,0.55f),new Vector3(0.08f,1.1f,0.08f),Wood,t);a.transform.localRotation=Quaternion.Euler(0,0,18);
            var b2=Box("SawB",new Vector3(0.35f,0.55f,0.55f),new Vector3(0.08f,1.1f,0.08f),Wood,t);b2.transform.localRotation=Quaternion.Euler(0,0,-18);
            Box("SawBlade",new Vector3(0,1.0f,0.55f),new Vector3(0.9f,0.05f,0.03f),Metal,t);
            // 三摞横放原木
            for(int row=0;row<3;row++)
            {
                var lg=Cyl("Log",new Vector3(-1.05f,0.22f+row*0.24f,1.05f),new Vector3(0.12f,0.85f,0.12f),Wood,t);
                lg.transform.localRotation=Quaternion.Euler(0,0,90);
            }
            for(int row=0;row<2;row++){ var lg=Cyl("Log2",new Vector3(1.05f,0.22f+row*0.24f,1.0f),new Vector3(0.12f,0.7f,0.12f),Wood,t);lg.transform.localRotation=Quaternion.Euler(0,0,90);}
            // 劈柴墩 + 斧
            Box("Stump",new Vector3(1.15f,0.28f,0.2f),new Vector3(0.4f,0.56f,0.4f),Wood,t);
            if(CurHigh) Box("PlankStack",new Vector3(-0.2f,0.2f,1.25f),new Vector3(0.8f,0.4f,0.5f),Wood,t);
        }

        // ---------------- 矿场：岩堆硐口 + A 字井架 + 矿车轨道 ----------------
        private void BuildMine(Transform t,BuildingStyle s,int era,float sc)
        {
            Material frame=era>=4?Metal:Wood;
            // 后方岩堆
            Sph("Rock1",new Vector3(-0.9f,0.55f,-0.7f),new Vector3(1.1f,0.9f,1.0f),Stone,t);
            Sph("Rock2",new Vector3(0.85f,0.45f,-0.9f),new Vector3(0.9f,0.7f,0.9f),Stone,t);
            Sph("Rock3",new Vector3(0f,0.7f,-1.05f),new Vector3(1.2f,0.9f,0.9f),Stone,t);
            // 硐口（黑）+ 木支护
            Cyl("Adit",new Vector3(0,0.75f,-0.7f),new Vector3(0.7f,0.75f,0.5f),Dark,t).transform.localRotation=Quaternion.Euler(90,0,0);
            Box("AditL",new Vector3(-0.62f,0.75f,-0.55f),new Vector3(0.14f,1.5f,0.14f),frame,t);
            Box("AditR",new Vector3( 0.62f,0.75f,-0.55f),new Vector3(0.14f,1.5f,0.14f),frame,t);
            Box("AditTop",new Vector3(0,1.5f,-0.55f),new Vector3(1.4f,0.14f,0.14f),frame,t);
            // A 字井架 + 天轮
            var hl=Box("HeadL",new Vector3(-0.32f,1.2f,0.35f),new Vector3(0.12f,2.4f,0.12f),frame,t);hl.transform.localRotation=Quaternion.Euler(0,0,12);
            var hr=Box("HeadR",new Vector3( 0.32f,1.2f,0.35f),new Vector3(0.12f,2.4f,0.12f),frame,t);hr.transform.localRotation=Quaternion.Euler(0,0,-12);
            Cyl("Wheel",new Vector3(0,2.35f,0.35f),new Vector3(0.22f,0.1f,0.22f),Metal,t).transform.localRotation=Quaternion.Euler(90,0,0);
            // 轨道 + 矿车
            Box("RailL",new Vector3(-0.3f,0.1f,1.0f),new Vector3(0.08f,0.08f,1.6f),Metal,t);
            Box("RailR",new Vector3( 0.3f,0.1f,1.0f),new Vector3(0.08f,0.08f,1.6f),Metal,t);
            Box("OreCart",new Vector3(0,0.4f,1.35f),new Vector3(0.7f,0.5f,0.55f),era>=4?Metal:Wood,t);
            Sph("OrePile",new Vector3(0,0.72f,1.35f),new Vector3(0.3f,0.2f,0.24f),Mat(new Color(0.34f,0.3f,0.28f)),t);
            if(CurHigh){ Sph("Ore1",new Vector3(1.0f,0.16f,0.6f),Vector3.one*0.22f,Mat(new Color(0.4f,0.36f,0.32f)),t);
                         Sph("Ore2",new Vector3(1.2f,0.14f,0.9f),Vector3.one*0.18f,Mat(new Color(0.3f,0.33f,0.36f)),t);}
        }

        // ---------------- 市场：三开间开放式摊位（条布棚+柜台+货筐），无围墙 ----------------
        private void BuildMarket(Transform t,string id,BuildingStyle s,int era,float sc)
        {
            Material awning = era>=5?Metal:Cloth(new Color(0.78f,0.32f,0.26f));
            float[] xs={-1.05f,0f,1.05f};
            for(int i=0;i<3;i++)
            {
                float x=xs[i]*sc;
                // 四柱
                foreach(var p in new[]{new Vector2(-0.42f,-0.42f),new Vector2(0.42f,-0.42f),new Vector2(0.42f,0.42f),new Vector2(-0.42f,0.42f)})
                    Box("Pole",new Vector3(x+p.x*sc,0.8f*sc,p.y*sc),new Vector3(0.07f,1.6f*sc,0.07f),era>=5?Metal:Wood,t);
                var roof=Box("Awning",new Vector3(x,1.65f*sc,0),new Vector3(1.0f*sc,0.08f,1.0f*sc),awning,t);
                roof.transform.localRotation=Quaternion.Euler(6,0,0);
                Box("Counter",new Vector3(x,0.55f,0.25f*sc),new Vector3(0.85f*sc,0.18f,0.5f),Wood,t);
                Sph("Goods",new Vector3(x,0.72f,0.25f*sc),new Vector3(0.4f,0.22f,0.3f),Cloth(new Color(0.85f,0.72f,0.35f)),t);
            }
            // 招牌旗
            Box("SignPole",new Vector3(-1.4f,1.3f,-0.9f),new Vector3(0.06f,2.6f,0.06f),Wood,t);
            Box("Sign",new Vector3(-1.4f,2.3f,-0.7f),new Vector3(0.7f,0.4f,0.04f),Gold,t);
            if(id=="tea_house"){ Cyl("Teapot",new Vector3(1.05f,0.8f,-0.2f),Vector3.one*0.18f,Dark,t); }
            if(CurHigh){ Box("Crate1",new Vector3(-1.2f,0.25f,0.9f),Vector3.one*0.34f,Wood,t);Box("Crate2",new Vector3(1.2f,0.25f,0.9f),Vector3.one*0.3f,Wood,t);}
        }

        // ---------------- 马厩：三面围栏马场 + 后斜棚 + 一匹马的剪影 ----------------
        private void BuildStable(Transform t,BuildingStyle s,int era,float sc)
        {
            Material rail=era>=5?Metal:Wood;
            // 三面围栏（左/右/前）双横杆
            for(int h=0;h<2;h++)
            {
                float yy=(0.5f+h*0.45f)*sc;
                Box("Rail",new Vector3(-1.35f,yy,0),new Vector3(0.1f,0.1f,2.6f*sc),rail,t);
                Box("Rail",new Vector3( 1.35f,yy,0),new Vector3(0.1f,0.1f,2.6f*sc),rail,t);
                Box("Rail",new Vector3(0,yy,1.35f),new Vector3(2.8f*sc,0.1f,0.1f),rail,t);
            }
            for(int i=-1;i<=1;i++){ Box("Post",new Vector3(-1.35f,0.45f,i*0.7f),new Vector3(0.1f,0.9f,0.1f),rail,t);Box("Post",new Vector3(1.35f,0.45f,i*0.7f),new Vector3(0.1f,0.9f,0.1f),rail,t);}
            // 后墙斜棚
            Box("ShelterL",new Vector3(-1.0f,1.0f,-1.05f),new Vector3(0.1f,2.0f,0.1f),Wood,t);
            Box("ShelterR",new Vector3( 1.0f,1.0f,-1.05f),new Vector3(0.1f,2.0f,0.1f),Wood,t);
            var roof=Box("StableRoof",new Vector3(0,1.7f,-0.7f),new Vector3(2.6f,0.1f,1.2f),RoofMat(s,era),t);roof.transform.localRotation=Quaternion.Euler(-12,0,0);
            // 马：身/脖/头/四腿/尾
            var horse=Mat(new Color(0.45f,0.3f,0.18f));
            Box("HorseBody",new Vector3(0,0.72f,0.3f),new Vector3(0.9f,0.42f,0.34f),horse,t);
            Box("HorseNeck",new Vector3(0.38f,1.0f,0.3f),new Vector3(0.18f,0.55f,0.2f),horse,t).transform.localRotation=Quaternion.Euler(0,0,-20);
            Box("HorseHead",new Vector3(0.5f,1.28f,0.3f),new Vector3(0.22f,0.26f,0.24f),horse,t);
            foreach(var lx in new[]{-0.3f,0.3f})foreach(var lz in new[]{-0.1f,0.1f}) Box("HorseLeg",new Vector3(lx,0.3f,0.3f+lz),new Vector3(0.09f,0.6f,0.09f),Dark,t);
            Box("Hay",new Vector3(-0.9f,0.28f,-0.5f),new Vector3(0.5f,0.4f,0.4f),Mat(new Color(0.8f,0.72f,0.3f)),t);
            if(CurHigh) Box("Trough",new Vector3(0.9f,0.3f,-0.4f),new Vector3(0.7f,0.25f,0.35f),Wood,t);
        }

        // ---------------- 水磨坊：小屋 + 巨型水车轮（第一识别特征） ----------------
        private void BuildWaterMill(Transform t,BuildingStyle s,int era,float sc)
        {
            var wall=WallMat(s,era);
            Box("MillBody",new Vector3(-0.2f,0.9f*sc,-0.2f),new Vector3(1.6f,1.8f*sc,1.6f),wall,t);
            PitchedRoof(t,1.9f,1.9f,1.8f*sc,0.7f,RoofMat(s,era),true);
            Box("MillDoor",new Vector3(-0.2f,0.6f,0.62f),new Vector3(0.6f,1.1f,0.06f),Dark,t);
            // 巨型水车（轴沿 X，盘面朝 +z）
            var wheel=Cyl("WaterWheel",new Vector3(1.15f,1.0f,0.9f),new Vector3(1.05f,0.12f,1.05f),Wood,t);wheel.transform.localRotation=Quaternion.Euler(0,0,90);
            Cyl("WheelHub",new Vector3(1.15f,1.0f,0.9f),new Vector3(0.16f,0.16f,0.16f),Metal,t).transform.localRotation=Quaternion.Euler(0,0,90);
            for(int k=0;k<8;k++)
            {
                float a=k*Mathf.PI/4f;
                var spoke=Box("Paddle",new Vector3(1.15f,1.0f+Mathf.Sin(a)*0.95f,0.9f+Mathf.Cos(a)*0.95f),new Vector3(0.1f,0.34f,0.12f),Wood,t);
                spoke.transform.localRotation=Quaternion.Euler(a*Mathf.Rad2Deg,0,0);
            }
            // 引水渠
            Box("Race",new Vector3(1.15f,0.08f,1.9f),new Vector3(1.0f,0.1f,1.6f),Mat(new Color(0.2f,0.45f,0.7f)),t);
        }

        // ---------------- 冶炼/作坊/工厂：开放工棚 + 发光高炉（第一特征），近代加高烟囱 ----------------
        private void BuildForge(Transform t,string id,BuildingStyle s,int era,float sc)
        {
            bool modern = id=="factory_pre"||id=="modern_arsenal"||id=="factory_modern";
            Material post=era>=4?Metal:Wood;
            // 仅后墙 + 四柱开放工棚（不是封闭房屋）
            Box("BackWall",new Vector3(0,0.9f*sc,-1.0f),new Vector3(2.4f*sc,1.8f*sc,0.14f),WallMat(s,era),t);
            foreach(var p in new[]{new Vector3(-1.1f,0.9f,1.0f),new Vector3(1.1f,0.9f,1.0f),new Vector3(-1.1f,0.9f,-1.0f),new Vector3(1.1f,0.9f,-1.0f)})
                Box("Post",p,new Vector3(0.12f,1.8f*sc,0.12f),post,t);
            var roof=Box("ForgeRoof",new Vector3(0,1.85f*sc,0),new Vector3(2.6f*sc,0.12f,2.4f*sc),RoofMat(s,era),t);
            if(era<=3) roof.transform.localRotation=Quaternion.Euler(0,0,0);
            // 高炉（石/铁塔身 + 发光炉口 + 上收烟囱）
            float fh=modern?2.2f:1.5f;
            Cyl("Furnace",new Vector3(-0.6f,fh*0.5f*sc+0.1f,0.2f),new Vector3(0.42f,fh*sc*0.5f,0.42f),era>=4?Metal:Brick(),t);
            Cyl("FurnaceStack",new Vector3(-0.6f,(fh+0.7f)*sc,0.2f),new Vector3(0.22f,0.7f*sc,0.22f),era>=4?Metal:Brick(),t);
            Box("FireMouth",new Vector3(-0.6f,0.45f,0.58f),new Vector3(0.4f,0.4f,0.06f),Fire,t);
            Sph("Ember",new Vector3(-0.6f,0.5f,0.6f),new Vector3(0.26f,0.2f,0.1f),Fire,t);
            // 砧台 / 工作台 / 锭堆
            Box("Anvil",new Vector3(0.55f,0.4f,0.5f),new Vector3(0.4f,0.28f,0.28f),Dark,t);
            Box("AnvilStand",new Vector3(0.55f,0.2f,0.5f),new Vector3(0.16f,0.4f,0.16f),Wood,t);
            Box("Ingot1",new Vector3(0.95f,0.18f,-0.3f),new Vector3(0.3f,0.12f,0.16f),Metal,t);
            Box("Ingot2",new Vector3(0.95f,0.3f,-0.3f),new Vector3(0.3f,0.12f,0.16f),Metal,t);
            if(modern){ // 近代厂房：再加一根高烟囱 + 采光锯齿顶
                Cyl("BigChimney",new Vector3(1.0f,1.9f,-0.7f),new Vector3(0.26f,1.9f,0.26f),Metal,t);
                var smk=Sph("Smoke",new Vector3(1.0f,3.0f,-0.7f),Vector3.one*0.4f,ShaderHelper.Trans(new Color(0.8f,0.8f,0.82f,0.5f)),t);
            }
            if(id=="gunpowder_mill"||id=="arsenal"||id=="modern_arsenal") RoofFinial(t,2.0f,Gold);
        }

        // ---------------- 瓷窑/砖窑：双馒头穹窑 + 矮烟囱 + 坯件堆 ----------------
        private void BuildKiln(Transform t,string id,BuildingStyle s,int era,float sc)
        {
            Material kilnMat = id=="porcelain_kiln"?Mat(new Color(0.82f,0.8f,0.76f)):Brick();
            Sph("Kiln1",new Vector3(-0.55f,0.55f,0.1f),new Vector3(1.0f,0.7f,1.0f),kilnMat,t);
            Sph("Kiln2",new Vector3(0.6f,0.45f,-0.2f),new Vector3(0.8f,0.55f,0.8f),kilnMat,t);
            Box("KilnMouth",new Vector3(-0.55f,0.4f,0.85f),new Vector3(0.5f,0.5f,0.06f),Fire,t);
            Cyl("KilnStack",new Vector3(-0.55f,1.35f,0.1f),new Vector3(0.16f,0.6f,0.16f),Brick(),t);
            // 坯件 / 砖垛
            if(id=="porcelain_kiln")
            {
                Cyl("Vase1",new Vector3(1.05f,0.28f,0.8f),Vector3.one*0.22f,Mat(new Color(0.7f,0.85f,0.9f)),t);
                Cyl("Pot1",new Vector3(0.7f,0.2f,1.0f),new Vector3(0.18f,0.2f,0.18f),Mat(new Color(0.85f,0.85f,0.8f)),t);
                Sph("Bowl",new Vector3(1.3f,0.16f,0.55f),new Vector3(0.2f,0.1f,0.2f),Mat(new Color(0.8f,0.85f,0.92f)),t);
            }
            else
            { for(int r=0;r<3;r++)Box("BrickStack",new Vector3(1.05f,0.12f+r*0.16f,0.8f),new Vector3(0.6f,0.14f,0.4f),Brick(),t); }
        }

        // ---------------- 造船坊：斜向船台滑轨 + 船体骨架 + 木制起重机 ----------------
        private void BuildShipyard(Transform t,string id,BuildingStyle s,int era,float sc)
        {
            Material frame=era>=5?Metal:Wood;
            // 两条斜伸入水(+z)的船台滑轨
            var r1=Box("SlipL",new Vector3(-0.5f,0.25f,0.9f),new Vector3(0.12f,0.1f,2.6f),frame,t);r1.transform.localRotation=Quaternion.Euler(-8,0,0);
            var r2=Box("SlipR",new Vector3( 0.5f,0.25f,0.9f),new Vector3(0.12f,0.1f,2.6f),frame,t);r2.transform.localRotation=Quaternion.Euler(-8,0,0);
            // 在建船体骨架（肋材）：船底 + 几道弧形肋
            Box("HullKeel",new Vector3(0,0.55f,0.4f),new Vector3(0.3f,0.2f,1.8f),Wood,t);
            for(int k=-1;k<=1;k++){ var rib=Box("Rib",new Vector3(0,0.85f,0.4f+k*0.5f),new Vector3(1.1f,0.5f,0.08f),Wood,t);rib.transform.localRotation=Quaternion.Euler(0,0,0);}
            // 起重机：立柱 + 斜吊臂 + 滑轮
            Cyl("CraneMast",new Vector3(-1.15f,1.2f,-0.8f),new Vector3(0.12f,2.4f,0.12f),frame,t);
            var boom=Box("CraneBoom",new Vector3(-0.6f,2.0f,-0.3f),new Vector3(0.1f,1.6f,0.1f),frame,t);boom.transform.localRotation=Quaternion.Euler(0,0,-35);
            Cyl("Pulley",new Vector3(-0.2f,2.55f,0.05f),new Vector3(0.12f,0.08f,0.12f),Metal,t);
            Box("Rope",new Vector3(-0.2f,1.9f,0.05f),new Vector3(0.04f,1.3f,0.04f),Dark,t);
            // 木料堆
            for(int row=0;row<2;row++){var lg=Cyl("Timber",new Vector3(1.15f,0.2f+row*0.22f,-0.7f),new Vector3(0.12f,1.0f,0.12f),Wood,t);lg.transform.localRotation=Quaternion.Euler(0,0,90);}
            if(id=="treasure_shipyard"){ Box("YardFlag",new Vector3(-1.15f,2.6f,-0.8f),new Vector3(0.6f,0.34f,0.04f),Gold,t); }
        }

        // ---------------- 海港/市舶司：石砌码头 + 起重机 + 系缆桩/集装箱 ----------------
        private void BuildPort(Transform t,string id,BuildingStyle s,int era,float sc)
        {
            // 码头甲板向 +z 水面延伸
            Box("Quay",new Vector3(0,0.18f,0.9f),new Vector3(3.2f,0.36f,2.2f),Stone,t);
            Box("QuayEdge",new Vector3(0,0.42f,1.95f),new Vector3(3.2f,0.2f,0.2f),Stone,t);
            foreach(var bx in new[]{-1.3f,1.3f}) Cyl("Bollard",new Vector3(bx,0.55f,1.4f),new Vector3(0.14f,0.4f,0.14f),era>=5?Metal:Wood,t);
            // 码头吊机
            Material frame=era>=5?Metal:Wood;
            Cyl("CraneMast",new Vector3(-1.0f,1.3f,-0.3f),new Vector3(0.14f,2.6f,0.14f),frame,t);
            var boom=Box("CraneBoom",new Vector3(-0.3f,2.2f,0.3f),new Vector3(0.12f,1.8f,0.12f),frame,t);boom.transform.localRotation=Quaternion.Euler(0,0,-40);
            Cyl("Pulley",new Vector3(0.05f,2.9f,0.7f),new Vector3(0.14f,0.1f,0.14f),Metal,t);
            // 仓棚（小型，不抢吊机轮廓）
            Box("Warehouse",new Vector3(0.9f,0.7f,-0.6f),new Vector3(1.2f,1.4f,1.0f),WallMat(s,era),t);
            PitchedRoof(t,1.4f,1.2f,1.4f,0.5f,RoofMat(s,era),true);
            if(era>=5){ Box("Container1",new Vector3(1.0f,0.45f,0.9f),new Vector3(0.8f,0.5f,0.5f),Cloth(new Color(0.3f,0.5f,0.7f)),t);
                         Box("Container2",new Vector3(0.2f,0.45f,0.9f),new Vector3(0.8f,0.5f,0.5f),Cloth(new Color(0.7f,0.4f,0.3f)),t);}
            else { Box("Crate1",new Vector3(1.0f,0.35f,0.9f),Vector3.one*0.5f,Wood,t);Box("Crate2",new Vector3(0.4f,0.3f,1.1f),Vector3.one*0.4f,Wood,t);Box("Barrel",new Vector3(-0.4f,0.4f,1.0f),new Vector3(0.26f,0.4f,0.26f),Wood,t);}
            if(id=="customs_house") Box("CustomSign",new Vector3(0.9f,1.5f,-0.05f),new Vector3(0.8f,0.36f,0.05f),Gold,t);
        }
    }
}

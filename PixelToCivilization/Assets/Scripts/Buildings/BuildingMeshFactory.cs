using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.World;

namespace PixelToCivilization.Buildings
{
    /// <summary>建筑点击交互组件</summary>
    public class BuildingClick : MonoBehaviour
    {
        public BuildingEntity Entity;
        public System.Action<BuildingEntity> OnClicked;
        private void OnMouseDown() => OnClicked?.Invoke(Entity);
    }

    /// <summary>
    /// V6.1.3 程序化精细建筑工厂（需求1·路径C）：
    /// 在 v6.1.2 时代风格/PBR 基础上，升级为——真坡屋顶(人字/四坡/重檐)、台基台阶、
    /// 门与规律窗格、按12分类的识别特征件(军事垛口炮管/工业烟囱/文化宝顶飞檐/资源井架/
    /// 海洋码头/现代幕墙/太空能量核)、三等级装饰(铜→银→金)。签名保持不变。
    /// </summary>
    public partial class BuildingMeshFactory : MonoBehaviour
    {
        public System.Action<BuildingEntity> ClickHandler;
        private readonly Dictionary<int,Material> _wallMats=new(), _roofMats=new(), _accMats=new();
        private Material _wood, _stone, _dark, _metal, _gold, _glass, _fire, _cloth;
        private Material Wood => _wood ??= ShaderHelper.Pbr(new Color(0.62f,0.44f,0.26f),0f,0.30f,901,1.0f); // V7.0.1
        private Material Stone => _stone ??= ShaderHelper.Pbr(new Color(0.74f,0.73f,0.70f),0f,0.24f,902,1.1f);
        private Material Dark => _dark ??= ShaderHelper.Pbr(new Color(0.30f,0.26f,0.22f),0f,0.32f,903,1f);
        private Material Metal => _metal ??= ShaderHelper.Pbr(new Color(0.62f,0.68f,0.74f),0.6f,0.62f,904,0.7f);
        private Material Gold => _gold ??= ShaderHelper.Pbr(new Color(0.96f,0.78f,0.28f),0.9f,0.74f,905,0.6f);
        private Material Glass => _glass ??= ShaderHelper.Pbr(new Color(0.40f,0.74f,0.90f),0.1f,0.92f,906,0.4f);
        private Material Fire => _fire ??= ShaderHelper.Emissive(new Color(0.3f,0.12f,0.04f),new Color(1f,0.5f,0.12f));
        private Material Cloth(Color c)=>_cloth=ShaderHelper.Pbr(c,0f,0.26f,Mathf.RoundToInt(c.r*131+c.g*97+c.b*61),1.1f);

        public GameObject Create(BuildingEntity b, EraDefinition era, Vector3 pos, Transform parent)
        {
            var go = new GameObject("B_"+b.Type);
            go.transform.SetParent(parent,false);
            go.transform.position=pos;
            // V6.3.1：旧精模整体下沉为 LV2（中景），LV3 为叠加朝代/功能细节的近景完全体
            var lv2=new GameObject("LV2"); lv2.transform.SetParent(go.transform,false);
            var lv3=new GameObject("LV3"); lv3.transform.SetParent(go.transform,false);
            CurHigh=false; Assemble(lv2,b,era);
            CurHigh=true;  Assemble(lv3,b,era);
            return Finalize(go,b);
        }

        private bool CurHigh;
        private void Assemble(GameObject host, BuildingEntity b, EraDefinition era)
        {
            var style = era != null ? era.Style : new BuildingStyle();
            int eraId = era != null ? era.Id : -1;
            Transform tr=host.transform;

            float w=3f*style.BuildingScale, d=3f*style.BuildingScale, wallH=2.5f*style.BuildingHeight*style.BuildingScale;
            string cat=b.Def!=null?b.Def.Cat:"";
            // 特殊形制尺寸
            switch (b.Type)
            {
                case "palace": case "grand_hall": w*=2.4f;d*=2.4f;wallH*=1.8f; break;
                case "great_wall": case "wall": w*=4f;d*=0.5f;wallH*=1.4f; break;
                case "pagoda": case "water_clock": w*=0.7f;d*=0.7f;wallH*=3.2f; break;
                case "arrow_tower": case "fire_tower": case "cannon_tower":
                case "watchtower": case "bunker": w*=0.9f;d*=0.9f;wallH*=2.2f; break;
                case "skyscraper": w*=0.6f;d*=0.6f;wallH*=7f; break;
                case "space_elevator": w*=0.4f;d*=0.4f;wallH*=12f; break;
                case "hut": w*=0.7f;d*=0.7f;wallH*=0.55f; break;
                case "farm": BuildFarm(host,b); return;
                case "road": case "highway": case "highway_modern": case "railway_pre": case "high_speed_rail":
                    BuildRoad(host,b,style); return;
                case "canal": BuildCanal(host,b); return;
            }

            // V6.3.7：功能建筑独立轮廓（不再套统一房屋主体，避免"千屋一面"）
            if (BuildSpecial(host,b,style,eraId)) return;

            // V6.7.0 LV3 近景完全体：前工业时代(era<=4)的民居/市集/磨坊/伐木场改用 CC0 真实模型；
            // 资源缺失时 PlaceReal 返回 null，自动回退下方程序化建模，绝不影响出图。
            if (CurHigh && eraId<=4)
            {
                string realRes = RealBuildingModel(b.Type);
                if (!string.IsNullOrEmpty(realRes) &&
                    PixelToCivilization.Art.AssetModelLibrary.PlaceReal(host.transform, realRes, w, d, 0f, false, 0f, "RealHouse") != null)
                    return;
            }

            var wallMat = WallMat(style,eraId);
            // —— 分层台基（接地、层次） ——
            Box("Base",new Vector3(0,-0.18f,0),new Vector3(w+0.6f,0.36f,d+0.6f),Stone,tr);
            Box("Step",new Vector3(0,0.02f,d*0.5f+0.35f),new Vector3(w*0.5f,0.18f,0.5f),Stone,tr);
            if(CurHigh) // LV3：第二级踏跺
                Box("Step2",new Vector3(0,-0.08f,d*0.5f+0.72f),new Vector3(w*0.72f,0.16f,0.5f),Stone,tr);
            // —— 墙身 ——
            var wall=Box("Wall",new Vector3(0,wallH/2,0),new Vector3(w,wallH,d),wallMat,tr);
            // 四角立柱（木构时代增强骨架感）
            if(eraId<=4)
            {
                float px=w/2-0.12f, pz=d/2-0.12f;
                foreach(var p in new[]{new Vector3(-px,wallH/2,-pz),new Vector3(px,wallH/2,-pz),new Vector3(px,wallH/2,pz),new Vector3(-px,wallH/2,pz)})
                    Box("Corner",p,new Vector3(0.22f,wallH+0.1f,0.22f),Wood,tr);
            }
            if (style.HasPillars)
            {
                var acc=AccMat(style,eraId);
                float po=w/2-0.3f;
                Vector3[] ps={new(-po,wallH/2,-po),new(po,wallH/2,-po),new(po,wallH/2,po),new(-po,wallH/2,po)};
                foreach(var p in ps) Box("Pillar",p,new Vector3(0.25f,wallH,0.25f),acc,tr);
            }
            // —— 门 + 窗 ——
            AddDoorWindows(tr,w,d,wallH,style,eraId);
            // 现代/未来发光幕墙
            if (eraId>=5) AddFacadeWindows(tr,w,d,wallH,style);
            // —— 屋顶（真坡顶） ——
            // V6.6.1 参考图：木骨架抹灰半木构民居（era0-3 居住类）+ 石砌烟囱
            if(eraId<=3 && cat=="居住"){ AddHalfTimber(tr,w,d,wallH); AddChimney(tr,w,d,wallH); }
            BuildRoof(tr,b.Type,style,w,d,wallH,eraId);
            // —— 分类特征件 ——
            AddCategoryFeatures(tr,b,cat,w,d,wallH,style,eraId);
            // 发光带 / 能量罩
            if (style.IsEmissive) Box("Glow",new Vector3(0,wallH*0.55f,0),new Vector3(w+0.05f,0.2f,d+0.05f),Emissive(style),tr);
            if (style.HasForceField)
            {
                var dome=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dome.name="Field";dome.transform.SetParent(tr);
                dome.transform.localPosition=new Vector3(0,wallH*0.5f,0);
                dome.transform.localScale=new Vector3(w*1.6f,wallH*1.1f,d*1.6f);
                DestroyCol(dome); dome.GetComponent<Renderer>().material=ShaderHelper.Trans(new Color(0f,0.6f,1f,0.18f));
            }
            // —— 三等级装饰（铜→银→金：灯笼/金边/宝顶珠） ——
            AddLevelTrim(tr,b.Level,w,d,wallH);
            // —— V6.3.1 LV3 近景专属：斗拱/彩绘/朝代与功能细节 ——
            if(CurHigh) AddHighDetail(tr,b,cat,w,d,wallH,style,eraId);
        }

        // ============ V6.7.0 CC0 真实建筑模型映射（前工业时代） ============
        private static string RealBuildingModel(string type)
        {
            switch (type)
            {
                case "hut": return "Buildings/House_3";            // 棚屋→小石木农舍
                case "rich_house": return "Buildings/House_1";     // 富人宅院→抹灰瓦顶宅
                case "market": case "tea_house": case "caravanserai": return "Buildings/Inn"; // 市集/茶馆/驿站→旅馆
                case "water_mill": return "Buildings/Mill";        // 水磨坊→风车磨坊
                case "lumbermill": return "Buildings/Sawmill";     // 伐木场→锯木坊
                default: return null;                              // 其余（宫殿/塔/现代/太空等）保留程序化
            }
        }

        // ============ 门 / 窗 ============
        private void AddDoorWindows(Transform root,float w,float d,float wallH,BuildingStyle s,int eraId)
        {
            // 正门（朝南 +z）：深色门洞 + 门框
            Box("Door",new Vector3(0,0.7f,d/2+0.02f),new Vector3(0.9f,1.4f,0.08f),Dark,root);
            Box("DoorFrameL",new Vector3(-0.5f,0.7f,d/2+0.03f),new Vector3(0.12f,1.5f,0.1f),Wood,root);
            Box("DoorFrameR",new Vector3( 0.5f,0.7f,d/2+0.03f),new Vector3(0.12f,1.5f,0.1f),Wood,root);
            // 窗格（古代木格窗 / 近代玻璃窗），四面各 1-2 扇
            var winMat = eraId>=4?Glass:Cloth(s.WindowColor);
            int n = wallH>4f?2:1;
            float yy = wallH*0.62f;
            for(int i=0;i<n;i++){
                float ox = n==1?0f:(i==0?-w*0.3f:w*0.3f);
                Box("Win",new Vector3(ox,yy,d/2+0.02f),new Vector3(0.7f,0.6f,0.06f),winMat,root);
                Box("Win",new Vector3(ox,yy,-d/2-0.02f),new Vector3(0.7f,0.6f,0.06f),winMat,root);
            }
            if(w>=2.6f){
                Box("Win",new Vector3(w/2+0.02f,yy,0),new Vector3(0.06f,0.6f,0.7f),winMat,root);
                Box("Win",new Vector3(-w/2-0.02f,yy,0),new Vector3(0.06f,0.6f,0.7f),winMat,root);
            }
        }

        /// <summary>现代/未来发光幕墙</summary>
        private void AddFacadeWindows(Transform root,float w,float d,float wallH,BuildingStyle s)
        {
            var winMat=ShaderHelper.Emissive(new Color(0.05f,0.08f,0.12f),
                s.EmissiveColor.maxColorComponent>0?s.EmissiveColor:new Color(0.2f,0.55f,0.9f));
            int floors=Mathf.Clamp(Mathf.FloorToInt(wallH/0.9f),2,14);
            int cols=Mathf.Clamp(Mathf.FloorToInt(w/0.8f),2,6);
            float fh=wallH/(floors+1);
            for(int f=1;f<=floors;f++){
                float y=fh*f;
                for(int c=0;c<cols;c++){
                    float cx=-w*0.5f+(c+0.5f)*(w/cols);
                    Box("Win",new Vector3(cx,y,d*0.5f+0.01f),new Vector3(w/cols*0.55f,fh*0.5f,0.04f),winMat,root);
                    Box("Win",new Vector3(cx,y,-d*0.5f-0.01f),new Vector3(w/cols*0.55f,fh*0.5f,0.04f),winMat,root);
                }
            }
        }

        // ============ 真坡屋顶（旋转斜面拼合，零手写法线风险） ============
        private void BuildRoof(Transform parent,string type,BuildingStyle s,float w,float d,float wallH,int eraId)
        {
            var roofMat=RoofMat(s,eraId);
            float baseY=wallH;
            switch(s.RoofType)
            {
                case RoofType.Flat:
                    Box("Roof",new Vector3(0,baseY+0.12f,0),new Vector3(w+0.3f,0.24f,d+0.3f),roofMat,parent);
                    Box("Parapet",new Vector3(0,baseY+0.34f,d/2),new Vector3(w+0.3f,0.3f,0.16f),roofMat,parent);
                    Box("Parapet",new Vector3(0,baseY+0.34f,-d/2),new Vector3(w+0.3f,0.3f,0.16f),roofMat,parent);
                    break;
                case RoofType.Energy:
                {
                    var cap=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    cap.name="Roof";cap.transform.SetParent(parent);
                    cap.transform.localPosition=new Vector3(0,baseY+0.3f,0);
                    cap.transform.localScale=new Vector3(w*0.8f,0.5f,d*0.8f);
                    DestroyCol(cap);cap.GetComponent<Renderer>().material=Emissive(s); break;
                }
                case RoofType.Thatched:
                    PitchedRoof(parent,w,d,baseY,0.9f,roofMat,false);
                    break;
                case RoofType.Imperial: // 重檐：上下两圈四坡 + 飞檐
                    PitchedRoof(parent,w+0.6f,d+0.6f,baseY,1.15f,roofMat,true);
                    PitchedRoof(parent,w*0.52f,d*0.52f,baseY+1.5f,0.8f,roofMat,true);
                    AddUpturnedEaves(parent,w,d,wallH,s,eraId);
                    RoofFinial(parent,baseY+2.5f,Gold);
                    break;
                case RoofType.Curved:
                    PitchedRoof(parent,w+0.4f,d+0.4f,baseY,1.05f,roofMat,true);
                    AddUpturnedEaves(parent,w,d,wallH,s,eraId);
                    RoofFinial(parent,baseY+1.25f,roofMat);
                    break;
                case RoofType.Dome:
                {
                    var dome=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    dome.name="Roof";dome.transform.SetParent(parent);
                    dome.transform.localPosition=new Vector3(0,baseY+0.55f,0);
                    dome.transform.localScale=new Vector3(w*0.8f,1.1f,d*0.8f);
                    DestroyCol(dome);dome.GetComponent<Renderer>().material=roofMat;
                    RoofFinial(parent,baseY+1.5f,Gold); break;
                }
                default: // Hip / Gable / Sawtooth / None：四坡顶
                    PitchedRoof(parent,w+0.3f,d+0.3f,baseY,1.0f,roofMat,true);
                    if(type=="pagoda") BuildPagodaTiers(parent,w,d,baseY,roofMat);
                    break;
            }
        }

        /// <summary>用旋转斜面拼出人字/四坡顶。hip=true 加两端坡面成四坡，false 为人字硬山。</summary>
        private void PitchedRoof(Transform parent,float w,float d,float baseY,float rh,Material mat,bool hip)
        {
            float over=0.28f;
            float hd=d/2+over, hw=w/2+over;
            float slopeD=Mathf.Sqrt(hd*hd+rh*rh), angD=Mathf.Atan2(rh,hd)*Mathf.Rad2Deg;
            // 前后两坡（绕 X）
            var f=Box("RoofSlope",new Vector3(0,baseY+rh/2f,hd/2f),new Vector3(w+0.5f,0.16f,slopeD),mat,parent);
            f.transform.localRotation=Quaternion.Euler(-angD,0,0);
            var bk=Box("RoofSlope",new Vector3(0,baseY+rh/2f,-hd/2f),new Vector3(w+0.5f,0.16f,slopeD),mat,parent);
            bk.transform.localRotation=Quaternion.Euler(angD,0,0);
            if(hip)
            {
                float slopeW=Mathf.Sqrt(hw*hw+rh*rh), angW=Mathf.Atan2(rh,hw)*Mathf.Rad2Deg;
                var l=Box("RoofSlope",new Vector3(-hw/2f,baseY+rh/2f,0),new Vector3(slopeW,0.16f,d+0.5f),mat,parent);
                l.transform.localRotation=Quaternion.Euler(0,0,angW);
                var r=Box("RoofSlope",new Vector3(hw/2f,baseY+rh/2f,0),new Vector3(slopeW,0.16f,d+0.5f),mat,parent);
                r.transform.localRotation=Quaternion.Euler(0,0,-angW);
                // 短屋脊
                Box("Ridge",new Vector3(0,baseY+rh,0),new Vector3(w*0.32f,0.14f,0.2f),mat,parent);
            }
            else
            {   // 人字顶通长屋脊 + 山墙封板
                Box("Ridge",new Vector3(0,baseY+rh,0),new Vector3(w+0.5f,0.14f,0.22f),mat,parent);
                Box("Gable",new Vector3(-hw+0.05f,baseY+rh*0.4f,0),new Vector3(0.12f,rh*0.8f,d),WallLike(mat),parent);
                Box("Gable",new Vector3( hw-0.05f,baseY+rh*0.4f,0),new Vector3(0.12f,rh*0.8f,d),WallLike(mat),parent);
            }
        }
        private Material WallLike(Material roof){return roof;}

        /// <summary>宝塔多层密檐</summary>
        private void BuildPagodaTiers(Transform parent,float w,float d,float baseY,Material mat)
        {
            for(int i=1;i<=3;i++)
            {
                float s=1f-i*0.22f, yy=baseY+i*0.85f;
                var tier=Box("Tier",new Vector3(0,yy,0),new Vector3(w*s,0.5f,d*s),WallMat(new BuildingStyle{RoofColor=new Color(0.7f,0.66f,0.5f)},2),parent);
                PitchedRoof(parent,w*s+0.2f,d*s+0.2f,yy+0.25f,0.4f,mat,true);
            }
            RoofFinial(parent,baseY+3.6f,Gold);
        }

        /// <summary>屋顶宝顶/塔刹</summary>
        private void RoofFinial(Transform parent,float y,Material mat)
        {
            var sp=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sp.name="Finial";sp.transform.SetParent(parent);sp.transform.localPosition=new Vector3(0,y,0);
            sp.transform.localScale=Vector3.one*0.3f;DestroyCol(sp);sp.GetComponent<Renderer>().material=mat;
            var pin=Box("FinialPin",new Vector3(0,y+0.28f,0),new Vector3(0.08f,0.5f,0.08f),mat,parent);
        }

        /// <summary>中式飞檐翘角</summary>
        private void AddUpturnedEaves(Transform root,float w,float d,float wallH,BuildingStyle s,int eraId)
        {
            var mat=RoofMat(s,eraId);
            float ey=wallH+1.0f, ex=w*0.46f, ez=d*0.46f;
            foreach(var sx in new[]{-1f,1f})foreach(var sz in new[]{-1f,1f})
            {
                var tip=Box("EaveTip",new Vector3(sx*ex,ey+0.22f,sz*ez),new Vector3(0.55f,0.12f,0.55f),mat,root);
                tip.transform.localRotation=Quaternion.Euler(sz*12f,0,sx*-12f);
            }
        }

        // ============ 分类识别特征件 ============
        private void AddCategoryFeatures(Transform root,BuildingEntity b,string cat,float w,float d,float wallH,BuildingStyle s,int eraId)
        {
            float top=wallH+1.1f;
            switch(cat)
            {
                case "军事": AddMilitary(root,b.Type,w,d,wallH,top); break;
                case "工业": AddIndustrial(root,b.Type,w,d,wallH,eraId); break;
                case "经济": AddEconomy(root,w,d,wallH); break;
                case "文化": AddCulture(root,b.Type,w,d,wallH,top); break;
                case "资源": AddResource(root,b.Type,w,d,wallH); break;
                case "海洋": AddNaval(root,b.Type,w,d,wallH); break;
                case "能源": AddEnergy(root,b.Type,w,d,wallH); break;
                case "科技": AddSci(root,w,d,wallH); break;
                case "太空": AddSpace(root,b.Type,w,d,wallH); break;
                case "食物": AddFood(root,w,d); break;
                case "基础": AddInfra(root,b.Type,w,d,wallH); break;
                case "居住": AddResidence(root,b.Type,w,d,wallH); break;
            }
        }
        private void AddMilitary(Transform t,string id,float w,float d,float wh,float top)
        {
            // V6.6.1 参考图石砌城堡：军营/城墙/碉堡四座角楼 + 四面雉堞
            bool keep = id=="barracks"||id=="new_army"||id=="great_wall"||id=="wall"||id=="bunker";
            if(keep) AddCastleTurrets(t,id,w,d,wh);
            // 墙顶垛口（前后两面）
            if(id=="great_wall"||id=="wall"||id=="bunker")
            { for(int i=-2;i<=2;i++){ Box("Crenel",new Vector3(i*w*0.18f,wh+0.25f,d/2),new Vector3(0.3f,0.4f,0.2f),Stone,t);
                                       Box("Crenel",new Vector3(i*w*0.18f,wh+0.25f,-d/2),new Vector3(0.3f,0.4f,0.2f),Stone,t); } }
            // 塔类：射击孔 + 炮管/箭垛
            if(id.Contains("tower")||id=="watchtower"||id=="cannon_tower"||id=="bunker")
            {
                Box("Embrasure",new Vector3(0,wh*0.7f,d/2+0.03f),new Vector3(w*0.5f,0.4f,0.08f),Dark,t);
                if(id=="cannon_tower"||id=="bunker")
                { var gun=Cyl("Cannon",new Vector3(0,wh*0.72f,d/2+0.45f),new Vector3(0.12f,0.7f,0.12f),Metal,t);gun.transform.localRotation=Quaternion.Euler(90,0,0); }
                Box("Railing",new Vector3(0,top,0),new Vector3(w+0.2f,0.12f,d+0.2f),Wood,t);
            }
            // 军营/新军：旗杆 + 红旗
            if(id=="barracks"||id=="new_army")
            { Box("Pole",new Vector3(w*0.35f,top+0.6f,-d*0.35f),new Vector3(0.08f,1.4f,0.08f),Wood,t);
              Box("Flag",new Vector3(w*0.35f+0.3f,top+1.05f,-d*0.35f),new Vector3(0.6f,0.36f,0.04f),Cloth(new Color(0.72f,0.14f,0.12f)),t); }
            // 烽火台火盆
            if(id=="watchtower"){ var fire=GameObject.CreatePrimitive(PrimitiveType.Sphere);fire.name="Beacon";fire.transform.SetParent(t);
                fire.transform.localPosition=new Vector3(0,top+0.2f,0);fire.transform.localScale=Vector3.one*0.5f;DestroyCol(fire);fire.GetComponent<Renderer>().material=Fire; }
        }
        private void AddIndustrial(Transform t,string id,float w,float d,float wh,int era)
        {
            // 烟囱（工业/近代更高）
            float ch = era>=4?wh*0.9f:wh*0.55f;
            Cyl("Chimney",new Vector3(w*0.32f,wh+ch/2f,-d*0.3f),new Vector3(0.28f,ch,0.28f),era>=4?Metal:Brick(),t);
            var smk=GameObject.CreatePrimitive(PrimitiveType.Sphere);smk.name="Smoke";smk.transform.SetParent(t);
            smk.transform.localPosition=new Vector3(w*0.32f,wh+ch+0.2f,-d*0.3f);smk.transform.localScale=Vector3.one*0.4f;DestroyCol(smk);
            smk.GetComponent<Renderer>().material=ShaderHelper.Trans(new Color(0.8f,0.8f,0.82f,0.5f));
            // 火药坊/军器局警示铜顶
            if(id=="gunpowder_mill"||id=="arsenal"||id=="modern_arsenal") RoofFinial(t,wh+0.4f,Gold);
        }
        private Material _brick; private Material Brick()=>_brick ??= ShaderHelper.Pbr(new Color(0.48f,0.34f,0.28f),0f,0.2f,907,1.2f);
        private void AddEconomy(Transform t,float w,float d,float wh)
        {   // 市场遮阳布棚 + 招牌旗
            Box("Awning",new Vector3(0,wh+0.15f,d*0.55f),new Vector3(w+0.5f,0.1f,0.7f),Cloth(new Color(0.78f,0.32f,0.28f)),t);
            Box("SignPole",new Vector3(-w*0.4f,wh+0.7f,d*0.4f),new Vector3(0.06f,1.0f,0.06f),Wood,t);
            Box("Sign",new Vector3(-w*0.4f,wh+1.0f,d*0.4f),new Vector3(0.5f,0.3f,0.04f),Gold,t);
        }
        private void AddCulture(Transform t,string id,float w,float d,float wh,float top)
        {   // 匾额 + 两侧旗幡
            Box("Plaque",new Vector3(0,wh*0.82f,d/2+0.03f),new Vector3(w*0.6f,0.4f,0.08f),Gold,t);
            Box("BannerL",new Vector3(-w*0.55f,wh*0.6f,d*0.3f),new Vector3(0.08f,wh*0.9f,0.04f),Cloth(new Color(0.7f,0.16f,0.16f)),t);
            Box("BannerR",new Vector3( w*0.55f,wh*0.6f,d*0.3f),new Vector3(0.08f,wh*0.9f,0.04f),Cloth(new Color(0.7f,0.16f,0.16f)),t);
            if(id=="temple") RoofFinial(t,top+0.2f,Gold);
        }
        private void AddResource(Transform t,string id,float w,float d,float wh)
        {
            if(id=="lumbermill"){ // 原木堆 + 支架
                Cyl("Log",new Vector3(-w*0.4f,0.4f,d*0.55f),new Vector3(0.22f,0.7f,0.22f),Wood,t).transform.localRotation=Quaternion.Euler(0,0,90);
                Box("Frame",new Vector3(w*0.3f,wh*0.6f,-d*0.3f),new Vector3(0.1f,wh*0.9f,0.1f),Wood,t); }
            else if(id=="mine"){ // 矿井架（A 架）+ 矿车
                Box("HeadframeL",new Vector3(-0.3f,wh*0.5f,0),new Vector3(0.12f,wh,0.12f),Wood,t).transform.localRotation=Quaternion.Euler(0,0,12);
                Box("HeadframeR",new Vector3( 0.3f,wh*0.5f,0),new Vector3(0.12f,wh,0.12f),Wood,t).transform.localRotation=Quaternion.Euler(0,0,-12);
                Box("OreCart",new Vector3(0,0.35f,d*0.6f),new Vector3(0.7f,0.5f,0.5f),Metal,t); }
        }
        private void AddNaval(Transform t,string id,float w,float d,float wh)
        {   // 系缆桩、小桅杆、码头甲板延伸
            for(int i=-1;i<=1;i+=2) Cyl("Bollard",new Vector3(i*w*0.4f,0.4f,d*0.6f),new Vector3(0.12f,0.5f,0.12f),Wood,t);
            Box("Dock",new Vector3(0,0.15f,d*0.85f),new Vector3(w*0.9f,0.16f,1.2f),Wood,t);
            if(id=="treasure_shipyard"||id=="sea_port"){ Cyl("YardMast",new Vector3(0,wh+0.8f,-d*0.2f),new Vector3(0.1f,2.0f,0.1f),Wood,t);
                Box("YardFlag",new Vector3(0.35f,wh+1.4f,-d*0.2f),new Vector3(0.6f,0.34f,0.04f),Gold,t); }
        }
        private void AddEnergy(Transform t,string id,float w,float d,float wh)
        {   // 冷却塔（双曲线近似用圆柱）/ 发电站
            if(id=="power_plant"||id=="fusion_plant"){ Cyl("CoolTower",new Vector3(-w*0.35f,wh*0.6f,-d*0.3f),new Vector3(0.5f,wh*0.9f,0.5f),Metal,t);
                var core=GameObject.CreatePrimitive(PrimitiveType.Sphere);core.name="Core";core.transform.SetParent(t);
                core.transform.localPosition=new Vector3(w*0.3f,wh*0.6f,d*0.2f);core.transform.localScale=Vector3.one*0.7f;DestroyCol(core);
                core.GetComponent<Renderer>().material=Fire; }
        }
        private void AddSci(Transform t,float w,float d,float wh)
        {   // 天线/雷达球
            Cyl("Antenna",new Vector3(0,wh+0.7f,0),new Vector3(0.06f,1.4f,0.06f),Metal,t);
            var dome=GameObject.CreatePrimitive(PrimitiveType.Sphere);dome.name="Radome";dome.transform.SetParent(t);
            dome.transform.localPosition=new Vector3(0,wh+1.5f,0);dome.transform.localScale=Vector3.one*0.5f;DestroyCol(dome);dome.GetComponent<Renderer>().material=Glass;
        }
        private void AddSpace(Transform t,string id,float w,float d,float wh)
        {
            var glow=ShaderHelper.Emissive(new Color(0.08f,0.1f,0.18f),new Color(0.35f,0.8f,1f));
            if(id=="space_elevator"){ Box("TetherGlow",new Vector3(0,wh*0.5f,0),new Vector3(0.18f,wh,0.18f),glow,t); }
            else if(id=="dyson_swarm"){ // 环绕光环
                var ring=Cyl("Ring",new Vector3(0,wh*0.5f,0),new Vector3(w*1.4f,0.06f,w*1.4f),glow,t);
                var core=GameObject.CreatePrimitive(PrimitiveType.Sphere);core.name="SunCore";core.transform.SetParent(t);
                core.transform.localPosition=new Vector3(0,wh*0.5f,0);core.transform.localScale=Vector3.one*1.1f;DestroyCol(core);core.GetComponent<Renderer>().material=Fire; }
            else { // 基地/空间站：发光舱段 + 太阳能板
                Box("Module",new Vector3(0,wh*0.5f,0),new Vector3(w*0.7f,wh*0.7f,d*0.7f),glow,t);
                Box("SolarL",new Vector3(-w*0.7f,wh*0.5f,0),new Vector3(w*0.7f,0.05f,d*0.9f),Metal,t);
                Box("SolarR",new Vector3( w*0.7f,wh*0.5f,0),new Vector3(w*0.7f,0.05f,d*0.9f),Metal,t);
            }
        }
        private void AddFood(Transform t,float w,float d){ Box("CropSack",new Vector3(-0.8f,0.4f,d*0.4f),new Vector3(0.5f,0.6f,0.5f),Cloth(new Color(0.72f,0.62f,0.3f)),t); }
        private void AddInfra(Transform t,string id,float w,float d,float wh)
        {
            if(id=="well"){ Cyl("WellRing",new Vector3(0,0.4f,0),new Vector3(0.7f,0.5f,0.7f),Stone,t);
                Box("WellPostL",new Vector3(-0.5f,1.1f,0),new Vector3(0.1f,1.4f,0.1f),Wood,t);Box("WellPostR",new Vector3(0.5f,1.1f,0),new Vector3(0.1f,1.4f,0.1f),Wood,t);
                Box("WellRoof",new Vector3(0,1.8f,0),new Vector3(1.3f,0.12f,1.3f),Wood,t); }
            else if(id=="granary"){ Box("GranaryLid",new Vector3(0,wh+0.2f,0),new Vector3(w+0.2f,0.3f,d+0.2f),Wood,t); }
        }
        // V6.6.1 半木构（fachwerk）：抹灰墙面上叠加深色木骨架——横向槛/楣板、竖向立柱、斜撑，四面闭合
        private void AddHalfTimber(Transform t,float w,float d,float wh)
        {
            float yLo=0.42f, yHi=wh-0.34f;
            foreach(var sz in new[]{1f,-1f}){ float z=sz*(d/2+0.03f);
                Box("TimberSill",new Vector3(0,yLo,z),new Vector3(w+0.02f,0.14f,0.08f),Dark,t);
                Box("TimberLintel",new Vector3(0,yHi,z),new Vector3(w+0.02f,0.14f,0.08f),Dark,t);
                foreach(var fx in new[]{-w*0.3f,0f,w*0.3f}) Box("TimberStud",new Vector3(fx,(yLo+yHi)/2,z),new Vector3(0.13f,yHi-yLo,0.08f),Dark,t);
                var b1=Box("TimberBrace",new Vector3(-w*0.16f,(yLo+yHi)/2,z),new Vector3(0.1f,yHi-yLo,0.08f),Dark,t); b1.transform.localRotation=Quaternion.Euler(0,0,28f);
                var b2=Box("TimberBrace",new Vector3( w*0.16f,(yLo+yHi)/2,z),new Vector3(0.1f,yHi-yLo,0.08f),Dark,t); b2.transform.localRotation=Quaternion.Euler(0,0,-28f);
            }
            foreach(var sx in new[]{1f,-1f}){ float x=sx*(w/2+0.03f);
                Box("TimberSill",new Vector3(x,yLo,0),new Vector3(0.08f,0.14f,d+0.02f),Dark,t);
                Box("TimberLintel",new Vector3(x,yHi,0),new Vector3(0.08f,0.14f,d+0.02f),Dark,t);
                Box("TimberStud",new Vector3(x,(yLo+yHi)/2,0),new Vector3(0.08f,yHi-yLo,0.13f),Dark,t);
            }
        }
        // 石砌烟囱：屋脊一侧毛石烟囱体 + 压顶
        private void AddChimney(Transform t,float w,float d,float wh)
        {
            Box("Chimney",new Vector3(w*0.3f,wh+0.55f,-d*0.22f),new Vector3(0.38f,1.1f,0.38f),Stone,t);
            Box("ChimneyCap",new Vector3(w*0.3f,wh+1.12f,-d*0.22f),new Vector3(0.5f,0.14f,0.5f),Dark,t);
        }
        // 石砌城堡角楼：四角圆柱石塔 + 顶部雉堞块 + 陶瓦尖帽
        private void AddCastleTurrets(Transform t,string id,float w,float d,float wh)
        {
            var terra=Cloth(new Color(0.55f,0.30f,0.17f));
            float th=wh*0.5f+0.5f, px=w/2-0.05f, pz=d/2-0.05f;
            foreach(var p in new[]{new Vector3(-px,th/2,-pz),new Vector3(px,th/2,-pz),new Vector3(px,th/2,pz),new Vector3(-px,th/2,pz)})
            {
                Cyl("Turret",p,new Vector3(0.42f,th/2,0.42f),Stone,t);
                for(int k=0;k<4;k++){ float a=k/4f*Mathf.PI*2f; Box("Merlon",p+new Vector3(Mathf.Cos(a)*0.36f,th/2+0.05f,Mathf.Sin(a)*0.36f),new Vector3(0.16f,0.28f,0.16f),Stone,t); }
                var cap=Cyl("TurretCap",p+new Vector3(0,th/2+0.32f,0),new Vector3(0.5f,0.4f,0.5f),terra,t);
            }
        }
        private void AddResidence(Transform t,string id,float w,float d,float wh)
        {
            if(id=="rich_house"){ // 院落围墙
                Box("YardL",new Vector3(-w*0.75f,0.4f,0),new Vector3(0.12f,0.8f,d+0.6f),Stone,t);Box("YardR",new Vector3(w*0.75f,0.4f,0),new Vector3(0.12f,0.8f,d+0.6f),Stone,t); }
            if(id=="noble_palace"){ Box("LanternL",new Vector3(-w*0.5f,1.4f,d*0.5f),Vector3.one*0.22f,Fire,t);Box("LanternR",new Vector3(w*0.5f,1.4f,d*0.5f),Vector3.one*0.22f,Fire,t); }
        }

        private GameObject Cyl(string name,Vector3 pos,Vector3 scale,Material mat,Transform parent)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;
            DestroyCol(go);go.GetComponent<Renderer>().material=mat;return go;
        }

        /// <summary>三等级装饰：Lv1 铜边/无饰，Lv2 银檐+灯笼，Lv3 金顶+双旗</summary>
        private void AddLevelTrim(Transform root,int level,float w,float d,float wh)
        {
            if(level<=1) return;
            Material trim = level>=3?Gold:Metal;
            // 檐口镶边
            Box("Trim",new Vector3(0,wh+0.05f,d/2+0.02f),new Vector3(w+0.4f,0.12f,0.1f),trim,root);
            Box("Trim",new Vector3(0,wh+0.05f,-d/2-0.02f),new Vector3(w+0.4f,0.12f,0.1f),trim,root);
            if(level>=3)
            {
                Box("TrimL",new Vector3(w/2+0.02f,wh+0.05f,0),new Vector3(0.1f,0.12f,d+0.4f),trim,root);
                Box("TrimR",new Vector3(-w/2-0.02f,wh+0.05f,0),new Vector3(0.1f,0.12f,d+0.4f),trim,root);
                RoofFinial(root,wh+1.7f,Gold);
            }
        }

        // ============ V6.3.1 LV3 近景专属细节：斗拱/彩绘/朝代形制/功能小件 ============
        private void AddHighDetail(Transform t,BuildingEntity b,string cat,float w,float d,float wh,BuildingStyle s,int eraId)
        {
            var acc=AccMat(s,eraId);
            // ① 上层加密窗格（较高建筑近景多一排格扇/玻窗）
            if(wh>3.2f)
            {
                var winMat=eraId>=4?Glass:Cloth(s.WindowColor);
                int cols=Mathf.Clamp(Mathf.FloorToInt(w/0.9f),2,5);
                for(int c=0;c<cols;c++){
                    float cx=-w*0.5f+(c+0.5f)*(w/cols);
                    Box("WinHi",new Vector3(cx,wh*0.40f,d/2+0.02f),new Vector3(w/cols*0.5f,0.42f,0.05f),winMat,t);
                    Box("WinHi",new Vector3(cx,wh*0.40f,-d/2-0.02f),new Vector3(w/cols*0.5f,0.42f,0.05f),winMat,t);
                }
            }
            // ② 古代木构：檐下斗拱（沿前后墙顶均布托块）+ 彩绘梁枋条带
            if(eraId<=4)
            {
                int bk=Mathf.Clamp(Mathf.FloorToInt(w/0.7f),2,6);
                for(int i=0;i<bk;i++){
                    float bx=-w*0.5f+(i+0.5f)*(w/bk);
                    Box("Bracket",new Vector3(bx,wh-0.06f,d/2+0.05f),new Vector3(0.18f,0.16f,0.18f),acc,t);
                    Box("Bracket",new Vector3(bx,wh-0.06f,-d/2-0.05f),new Vector3(0.18f,0.16f,0.18f),acc,t);
                }
                Box("PaintedBeam",new Vector3(0,wh-0.22f,d/2+0.03f),new Vector3(w+0.05f,0.10f,0.06f),Cloth(s.AccentColor),t);
                Box("PaintedBeam",new Vector3(0,wh-0.22f,-d/2-0.03f),new Vector3(w+0.05f,0.10f,0.06f),Cloth(s.AccentColor),t);
            }
            // ③ 朝代形制特征
            switch(eraId)
            {
                case 0: // 三皇~商周：茅草脊束、夯土护边石
                    Box("ThatchRidge",new Vector3(0,wh+0.95f,0),new Vector3(w*0.7f,0.14f,0.22f),Mat(new Color(0.52f,0.44f,0.28f)),t);
                    Box("Rubble",new Vector3(-w*0.42f,0.05f,d*0.42f),Vector3.one*0.22f,Stone,t);
                    Box("Rubble",new Vector3(w*0.42f,0.05f,-d*0.42f),Vector3.one*0.22f,Stone,t);
                    break;
                case 1: // 东周~南北朝：青瓦当、双阙小观（礼制/宫室类）
                    for(int sx=-1;sx<=1;sx+=2) Box("EaveTile",new Vector3(sx*w*0.42f,wh+0.5f,d*0.42f),new Vector3(0.16f,0.1f,0.16f),RoofMat(s,eraId),t);
                    if(cat=="文化"||b.Type=="palace"){ Cyl("QueL",new Vector3(-w*0.72f,1.1f,d*0.5f),new Vector3(0.16f,1.1f,0.16f),Stone,t);Cyl("QueR",new Vector3(w*0.72f,1.1f,d*0.5f),new Vector3(0.16f,1.1f,0.16f),Stone,t);}
                    break;
                case 2: // 隋唐：朱红立柱加粗感 + 鸱吻脊兽
                    Box("ChiwenL",new Vector3(-w*0.46f,wh+1.0f,0),new Vector3(0.16f,0.34f,0.16f),RoofMat(s,eraId),t);
                    Box("ChiwenR",new Vector3(w*0.46f,wh+1.0f,0),new Vector3(0.16f,0.34f,0.16f),RoofMat(s,eraId),t);
                    break;
                case 3: // 宋元：素墙月梁、山面悬鱼
                    Box("Xuanyu",new Vector3(-w/2-0.03f,wh+0.55f,0),new Vector3(0.05f,0.4f,0.28f),Cloth(s.AccentColor),t);
                    Box("Xuanyu",new Vector3(w/2+0.03f,wh+0.55f,0),new Vector3(0.05f,0.4f,0.28f),Cloth(s.AccentColor),t);
                    break;
                case 4: // 明清：琉璃鎏金宝顶已在重檐；宫室加铜缸/石狮
                    if(b.Type=="palace"||b.Type=="noble_palace"||cat=="文化"){
                        Cyl("BronzeVat",new Vector3(-w*0.42f,0.4f,d*0.5f),new Vector3(0.22f,0.3f,0.22f),Metal,t);
                        Cyl("BronzeVat",new Vector3(w*0.42f,0.4f,d*0.5f),new Vector3(0.22f,0.3f,0.22f),Metal,t);}
                    break;
                case 5: // 民国：青砖拱券门楣、山花
                    Box("ArchLintel",new Vector3(0,1.55f,d/2+0.04f),new Vector3(1.1f,0.18f,0.08f),Brick(),t);
                    break;
                case 6: // 新中国：楼顶红旗 + 预制板分格线
                    Box("RoofPole",new Vector3(0,wh+0.7f,0),new Vector3(0.05f,1.0f,0.05f),Metal,t);
                    Box("RoofFlag",new Vector3(0.28f,wh+1.0f,0),new Vector3(0.5f,0.3f,0.03f),Cloth(new Color(0.78f,0.12f,0.12f)),t);
                    break;
                case 7: // 地球联盟：能量管线环 + 悬浮指示灯
                    Box("PipeGlow",new Vector3(0,wh*0.3f,d/2+0.03f),new Vector3(w*0.9f,0.06f,0.04f),ShaderHelper.Emissive(new Color(0.05f,0.1f,0.16f),s.EmissiveColor.maxColorComponent>0?s.EmissiveColor:new Color(0.3f,0.8f,1f)),t);
                    break;
            }
            // ④ 功能小件（近景才看得清的生活/作业细节）
            switch(cat)
            {
                case "居住":
                    Box("StoneLantern",new Vector3(w*0.55f,0.5f,d*0.55f),new Vector3(0.14f,0.7f,0.14f),Stone,t);
                    Box("WaterJar",new Vector3(-w*0.55f,0.35f,d*0.5f),Vector3.one*0.3f,Mat(new Color(0.45f,0.5f,0.55f)),t);
                    break;
                case "经济":
                    Box("Crate1",new Vector3(-w*0.45f,0.3f,d*0.62f),new Vector3(0.4f,0.35f,0.4f),Wood,t);
                    Box("Crate2",new Vector3(w*0.4f,0.28f,d*0.66f),new Vector3(0.34f,0.3f,0.34f),Wood,t);
                    break;
                case "文化":
                    Cyl("Incense",new Vector3(0,0.5f,d*0.55f),new Vector3(0.16f,0.28f,0.16f),Dark,t);
                    break;
                case "军事": // 阵营色军旗（与五方势力同色系识别）
                    Box("CampPole",new Vector3(w*0.42f,wh+0.5f,-d*0.42f),new Vector3(0.06f,1.2f,0.06f),Wood,t);
                    Box("CampFlag",new Vector3(w*0.42f+0.3f,wh+0.95f,-d*0.42f),new Vector3(0.56f,0.36f,0.04f),Cloth(s.AccentColor),t);
                    break;
                case "工业":
                    Cyl("Pipe",new Vector3(-w*0.2f,wh*0.55f,d*0.5f),new Vector3(0.1f,wh*0.7f,0.1f),Metal,t).transform.localRotation=Quaternion.Euler(90,0,0);
                    break;
            }
        }

        // ============ 农田 / 道路 / 运河 ============
        private void BuildFarm(GameObject root,BuildingEntity b)
        {
            Box("Soil",new Vector3(0,0.05f,0),new Vector3(3.4f,0.1f,3.4f),Mat(new Color(0.36f,0.27f,0.16f)),root.transform);
            Color[] stages={new(0.36f,0.27f,0.16f),new(0.56f,0.93f,0.56f),new(0.2f,0.8f,0.2f),new(1f,0.84f,0f)};
            var cropMat=Mat(stages[Mathf.Clamp(b.FarmStage,0,3)]);
            for (int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
            {
                Box("Crop",new Vector3(x*1f,0.25f,z*1f),new Vector3(0.7f,0.4f,0.7f),cropMat,root.transform);
                if(b.FarmStage>=2) Cyl("Stalk",new Vector3(x*1f,0.55f,z*1f),new Vector3(0.05f,0.5f,0.05f),Mat(new Color(0.35f,0.55f,0.2f)),root.transform);
            }
        }
        public void UpdateFarmStage(BuildingEntity b)
        {
            if (b.View==null) return;
            Color[] stages={new(0.36f,0.27f,0.16f),new(0.56f,0.93f,0.56f),new(0.2f,0.8f,0.2f),new(1f,0.84f,0f)};
            foreach (var t in b.View.GetComponentsInChildren<Transform>())
                if (t.name=="Crop" && t.GetComponent<Renderer>())
                    t.GetComponent<Renderer>().material.color=stages[Mathf.Clamp(b.FarmStage,0,3)];
        }
        private void BuildRoad(GameObject root,BuildingEntity b,BuildingStyle s)
        {
            Box("Road",new Vector3(0,0.05f,0),new Vector3(4.5f,0.1f,2f),Mat(s.RoadColor),root.transform);
            // 车道线 / 铁轨
            if(b.Type=="railway_pre"||b.Type=="high_speed_rail"){ Box("RailL",new Vector3(0,0.12f,0.45f),new Vector3(4.5f,0.08f,0.12f),Metal,root.transform);Box("RailR",new Vector3(0,0.12f,-0.45f),new Vector3(4.5f,0.08f,0.12f),Metal,root.transform); }
            else Box("Lane",new Vector3(0,0.12f,0),new Vector3(4.2f,0.04f,0.08f),Mat(new Color(0.9f,0.86f,0.5f)),root.transform);
        }
        private void BuildCanal(GameObject root,BuildingEntity b)
        {
            Box("CanalWater",new Vector3(0,0.02f,0),new Vector3(4.5f,0.12f,2.4f),Mat(new Color(0.2f,0.45f,0.7f)),root.transform);
            Box("BankL",new Vector3(0,0.08f,1.3f),new Vector3(4.5f,0.16f,0.3f),Mat(new Color(0.5f,0.45f,0.35f)),root.transform);
            Box("BankR",new Vector3(0,0.08f,-1.3f),new Vector3(4.5f,0.16f,0.3f),Mat(new Color(0.5f,0.45f,0.35f)),root.transform);
        }

        // ============ 工具 ============
        private GameObject Box(string name,Vector3 localPos,Vector3 scale,Material mat,Transform parent)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=localPos;go.transform.localScale=scale;
            go.GetComponent<Renderer>().material=mat;
            var r=go.GetComponent<Renderer>();r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;r.receiveShadows=true;
            DestroyCol(go);
            return go;
        }
        private GameObject Finalize(GameObject go,BuildingEntity b)
        {
            var renderers=go.GetComponentsInChildren<Renderer>();
            if (renderers.Length>0)
            {
                Bounds bd=renderers[0].bounds;
                for(int i=1;i<renderers.Length;i++)bd.Encapsulate(renderers[i].bounds);
                var col=go.AddComponent<BoxCollider>();
                col.center=go.transform.InverseTransformPoint(bd.center);
                col.size=go.transform.InverseTransformVector(bd.size);
            }
            var click=go.AddComponent<BuildingClick>();
            click.Entity=b; click.OnClicked=ClickHandler;
            return go;
        }
        private void DestroyCol(GameObject g){var c=g.GetComponent<Collider>();if(c)Destroy(c);}
        private Material Mat(Color c)=>ShaderHelper.Mat(c);
        private Material Emissive(BuildingStyle s)=>ShaderHelper.Emissive(s.RoofColor,s.EmissiveColor.maxColorComponent>0?s.EmissiveColor:new Color(0.1f,0.5f,0.9f));
        private static (float metal,float smooth) EraSurface(int eraId)=> eraId switch
        {
            7 => (0.45f,0.72f),
            6 => (0.18f,0.58f),
            5 => (0.02f,0.32f),
            _ => (0.0f,0.16f),
        };
        private Material WallMat(BuildingStyle s,int eraId)
        {
            int k=CombineKey(s.WallColor,eraId,1);
            if(!_wallMats.TryGetValue(k,out var m)){var (mt,sm)=EraSurface(eraId);m=ShaderHelper.Pbr(s.WallColor,mt,sm,s.WallColor.GetHashCode()+eraId);_wallMats[k]=m;}
            return m;
        }
        private Material RoofMat(BuildingStyle s,int eraId)
        {
            int k=CombineKey(s.RoofColor,eraId,2);
            if(!_roofMats.TryGetValue(k,out var m)){
                if(s.IsEmissive) m=ShaderHelper.Emissive(s.RoofColor,s.EmissiveColor.maxColorComponent>0?s.EmissiveColor:new Color(0.1f,0.5f,0.9f));
                else { var (mt,sm)=EraSurface(eraId); m=ShaderHelper.Pbr(s.RoofColor,mt,Mathf.Clamp(sm+0.1f,0.1f,0.8f),s.RoofColor.GetHashCode()+eraId+7); }
                _roofMats[k]=m;
            }
            return m;
        }
        private Material AccMat(BuildingStyle s,int eraId)
        {
            int k=CombineKey(s.AccentColor,eraId,3);
            if(!_accMats.TryGetValue(k,out var m)){var (mt,sm)=EraSurface(eraId);m=ShaderHelper.Pbr(s.AccentColor,mt*0.5f,sm+0.05f,s.AccentColor.GetHashCode()+eraId+3);_accMats[k]=m;}
            return m;
        }
        private static int CombineKey(Color c,int era,int salt)
        {
            int h=Mathf.RoundToInt(c.r*255)*65536+Mathf.RoundToInt(c.g*255)*256+Mathf.RoundToInt(c.b*255);
            return (h^era*397^salt*17)&0x7fffffff;
        }
    }
}

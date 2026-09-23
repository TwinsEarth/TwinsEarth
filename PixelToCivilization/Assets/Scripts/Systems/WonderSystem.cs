using System.Collections.Generic;
using System.Text;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.World;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// V6.8.0 世界奇观 · 文明丰碑：每时代一座、全世界唯一、不可拆除的国家工程。
    /// 职责：建造校验/扣费、永久全局 Buff 汇总（供经济/人口系统读取）、文明成就自动判定、
    /// 九神自动援建、宏伟程序化 3D 丰碑（视图按状态自愈，读档后自动重建）。
    /// </summary>
    public class WonderSystem : GameSystemBase
    {
        public List<WonderDefinition> Defs;
        private readonly Dictionary<string, WonderDefinition> _byId = new();
        private Transform _root;
        private WorldGenerator _terrain;
        private float _syncCd;
        private int _lastEraAch = -1;

        // —— 汇总缓存 ——
        private float _resMul=1f,_goldMul=1f,_culMul=1f,_foodMul=1f,_goodsMul=1f,_housing,_fireMul=1f;
        private bool _forcePower,_dirty=true;

        public override void Init(GameManager gm)
        {
            base.Init(gm);
            Defs = WonderDatabase.CreateAll();
            _byId.Clear();
            foreach (var d in Defs) _byId[d.Id] = d;
            Recompute();
        }

        public WonderDefinition Def(string id) => id != null && _byId.TryGetValue(id, out var d) ? d : null;
        public bool Built(string id) { foreach (var w in S.Wonders) if (w.Id == id) return true; return false; }
        public bool IsAvailable(WonderDefinition d) => d != null && S.Era >= d.Era;

        public float ResearchMul { get { if (_dirty) Recompute(); return _resMul; } }
        public float GoldMul { get { if (_dirty) Recompute(); return _goldMul; } }
        public float CultureMul { get { if (_dirty) Recompute(); return _culMul; } }
        public float FoodMul { get { if (_dirty) Recompute(); return _foodMul; } }
        public float GoodsMul { get { if (_dirty) Recompute(); return _goodsMul; } }
        public float HousingAdd { get { if (_dirty) Recompute(); return _housing; } }
        public float FireCapMul { get { if (_dirty) Recompute(); return _fireMul; } }
        public bool ForcePower { get { if (_dirty) Recompute(); return _forcePower; } }

        private void Recompute()
        {
            float r=0,g=0,c=0,f=0,go=0,h=0; float fire=1f; bool fp=false;
            foreach (var w in S.Wonders)
            {
                var d = Def(w.Id); if (d == null) continue;
                r+=d.ResearchAdd; g+=d.GoldAdd; c+=d.CultureAdd; f+=d.FoodAdd; go+=d.GoodsAdd;
                h+=d.HousingAdd; fire*=d.FireCapMul; if (d.ForcePower) fp=true;
            }
            _resMul=1f+r; _goldMul=1f+g; _culMul=1f+c; _foodMul=1f+f; _goodsMul=1f+go;
            _housing=h; _fireMul=fire; _forcePower=fp; _dirty=false;
        }

        /// <summary>建造结果：ok / 失败原因（供 UI 提示）</summary>
        public (bool ok, string msg) TryBuild(string id)
        {
            var d = Def(id);
            if (d == null) return (false, "无此奇观");
            if (Built(id)) return (false, "奇观已建成");
            if (!IsAvailable(d)) return (false, $"需进入「{Eras[d.Era].Name}」时代");
            if (!S.CanAfford(d.Cost)) return (false, "资源不足：" + d.CostText());

            var site = FindSite();
            S.Pay(d.Cost);
            var rt = new WonderRuntime { Id=id, BuiltYear=S.Year, X=site.x, Z=site.z };
            S.Wonders.Add(rt);
            _dirty = true;
            BuildView(d, rt);
            GM.AddEvent("good", $"{d.Icon} 世界奇观「{d.Name}」落成 —— {d.Desc}");
            Grant("wonder_first", S.Year, "建成第一座世界奇观");
            if (S.Wonders.Count >= Defs.Count) Grant("wonder_all", S.Year, "集齐八座世界奇观");
            return (true, d.Name);
        }

        public override void Tick(float dt)
        {
            _syncCd -= dt;
            if (_syncCd > 0f) return;
            _syncCd = 1f;
            SyncViews();
        }

        public override void OnYear(int year)
        {
            // —— 文明成就（幂等）——
            if (S.Pop >= 100) Grant("pop100", year, "人口突破 100");
            if (S.Pop >= 500) Grant("pop500", year, "人口突破 500");
            if (S.Pop >= 1000) Grant("pop1000", year, "人口突破 1000");
            if (S.Era != _lastEraAch) { _lastEraAch = S.Era; Grant("era"+S.Era, year, $"进入「{Eras[S.Era].Name}」时代"); }
            if (S.Colonies != null && S.Colonies.Count >= 3) Grant("col3", year, "开辟 3 处海外殖民地");
            if (S.AgeOfSail) Grant("sail", year, "开启大航海时代");
            if (S.AgeOfSpace || S.SpaceUnlocked) Grant("space", year, "开启宇宙大开发时代");
            if (S.WorldPhase == "unify") Grant("unify", year, "天下大一统");
            if (S.Victory || !string.IsNullOrEmpty(S.VictoryType)) Grant("victory", year, "达成文明胜利");

            // —— 九神自动援建：选已达时代、未建、资源够的最早一座 ——
            if (!S.WonderAuto) return;
            foreach (var d in Defs)
            {
                if (!IsAvailable(d) || Built(d.Id)) continue;
                if (S.CanAfford(d.Cost)) { TryBuild(d.Id); break; }
            }
        }

        private void Grant(string id, int year, string text)
        {
            string key = id + "|";
            foreach (var a in S.Achievements) if (a.StartsWith(key)) return;
            S.Achievements.Add($"{id}|{year}|{text}");
        }

        // ================= 视图（自愈） =================
        private Transform Root
        {
            get
            {
                if (_root != null) return _root;
                _root = EntityViewFactory.EnsureRoot("Wonders", GM.transform);
                return _root;
            }
        }

        private void SyncViews()
        {
            if (_terrain == null) _terrain = Object.FindObjectOfType<WorldGenerator>();
            var have = new HashSet<string>();
            foreach (var w in S.Wonders)
            {
                have.Add(w.Id);
                string vn = "Wonder_" + w.Id;
                if (Root.Find(vn) == null)
                {
                    var d = Def(w.Id);
                    if (d != null) BuildView(d, w);
                }
            }
            // 清理数据已不存在的孤儿视图
            for (int i = Root.childCount - 1; i >= 0; i--)
            {
                var ch = Root.GetChild(i);
                var id = ch.name.Replace("Wonder_", "");
                if (!have.Contains(id)) Object.Destroy(ch.gameObject);
            }
        }

        private (float x, float z) FindSite()
        {
            float cx = 0, cz = 0;
            if (S.VillageX != null && S.VillageX.Count > 0) { cx = S.VillageX[0]; cz = S.VillageZ[0]; }
            for (float r = 26f; r <= 64f; r += 4f)
            {
                for (int k = 0; k < 16; k++)
                {
                    float ang = k * Mathf.PI * 2f / 16f + r;
                    float wx = cx + Mathf.Cos(ang) * r, wz = cz + Mathf.Sin(ang) * r;
                    if (_terrain != null && _terrain.IsWater(wx, wz)) continue;
                    if (_terrain != null)
                    {   // 要求相对平坦
                        float h0=_terrain.HeightAt(wx,wz);
                        if (Mathf.Abs(_terrain.HeightAt(wx+3,wz)-h0)>0.8f ||
                            Mathf.Abs(_terrain.HeightAt(wx-3,wz)-h0)>0.8f ||
                            Mathf.Abs(_terrain.HeightAt(wx,wz+3)-h0)>0.8f ||
                            Mathf.Abs(_terrain.HeightAt(wx,wz-3)-h0)>0.8f) continue;
                    }
                    bool tooClose=false;
                    foreach (var w in S.Wonders) { float dx=w.X-wx,dz=w.Z-wz; if(dx*dx+dz*dz<18f*18f){tooClose=true;break;} }
                    if (!tooClose) return (wx, wz);
                }
            }
            return (cx + 30f, cz + 30f);
        }

        private void BuildView(WonderDefinition d, WonderRuntime w)
        {
            if (_terrain == null) _terrain = Object.FindObjectOfType<WorldGenerator>();
            var old = Root.Find("Wonder_"+d.Id);
            if (old != null) Object.Destroy(old.gameObject);
            float y = _terrain != null ? _terrain.HeightAt(w.X,w.Z) : 0f;
            var go = new GameObject("Wonder_"+d.Id);
            go.transform.SetParent(Root,false);
            go.transform.position = new Vector3(w.X,y,w.Z);

            Color main = Hex(d.ColorHex), trim = Hex(d.TrimHex);
            var mMain = ShaderHelper.Mat(main);
            var mTrim = ShaderHelper.Pbr(trim,0.1f,0.55f,Mathf.RoundToInt(trim.r*99+trim.g*71));
            var mStone= ShaderHelper.Mat(new Color(0.78f,0.76f,0.70f));
            var mDark = ShaderHelper.Mat(new Color(0.32f,0.28f,0.24f));

            switch (d.Shape)
            {
                case "observatory": ShapeObservatory(go,mMain,mTrim,mStone,mDark); break;
                case "army": ShapeArmy(go,mMain,mTrim,mDark); break;
                case "hall": ShapeHall(go,mMain,mTrim,mStone); break;
                case "tower": ShapeTower(go,mMain,mTrim,mDark); break;
                case "palace": ShapePalace(go,mMain,mTrim,mStone); break;
                case "rail": ShapeRail(go,mMain,mTrim,mDark); break;
                case "dam": ShapeDam(go,mMain,mTrim); break;
                case "dyson": ShapeDyson(go,mMain,mTrim); break;
                default: ShapeHall(go,mMain,mTrim,mStone); break;
            }
            AddLabel(go, d.Icon+" "+d.Name, trim);
        }

        // ---------- 各造型（局部坐标，底座在 y=0） ----------
        private GameObject P(GameObject parent, PrimitiveType t, Material m, Vector3 pos, Vector3 scale)
        {
            var g = GameObject.CreatePrimitive(t);
            var col=g.GetComponent<Collider>(); if(col) Object.Destroy(col);
            g.transform.SetParent(parent.transform,false);
            g.transform.localPosition=pos; g.transform.localScale=scale;
            var r=g.GetComponent<Renderer>(); if(r&&m) r.sharedMaterial=m;
            return g;
        }

        private void ShapeObservatory(GameObject o,Material main,Material trim,Material stone,Material dark)
        {
            P(o,PrimitiveType.Cylinder,stone,new Vector3(0,0.3f,0),new Vector3(10f,0.6f,10f));
            P(o,PrimitiveType.Cylinder,dark,new Vector3(0,1.1f,0),new Vector3(8f,0.8f,8f));
            P(o,PrimitiveType.Cylinder,main,new Vector3(0,1.9f,0),new Vector3(6f,0.8f,6f));
            P(o,PrimitiveType.Cylinder,dark,new Vector3(0,2.7f,0),new Vector3(4f,0.8f,4f));
            P(o,PrimitiveType.Cylinder,trim,new Vector3(0,3.5f,0),new Vector3(2.6f,0.2f,2.6f));
            P(o,PrimitiveType.Sphere,trim,new Vector3(0,4.2f,0),Vector3.one*1.8f);
        }
        private void ShapeArmy(GameObject o,Material main,Material trim,Material dark)
        {
            P(o,PrimitiveType.Cube,main,new Vector3(0,0.6f,0),new Vector3(12f,1.2f,6f));
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
                P(o,PrimitiveType.Cube,dark,new Vector3(x*3f,1.5f,z*1.8f),new Vector3(0.7f,1.4f,0.7f));
            foreach(var sx in new[]{-5.6f,5.6f})foreach(var sz in new[]{-2.6f,2.6f})
                P(o,PrimitiveType.Cube,trim,new Vector3(sx,2.0f,sz),new Vector3(0.9f,3f,0.9f));
        }
        private void ShapeHall(GameObject o,Material main,Material trim,Material stone)
        {
            P(o,PrimitiveType.Cube,stone,new Vector3(0,0.4f,0),new Vector3(10f,0.8f,7f));
            P(o,PrimitiveType.Cube,main,new Vector3(0,2.3f,0),new Vector3(7f,3f,4.5f));
            var r1=P(o,PrimitiveType.Cube,trim,new Vector3(0,4.1f,0),new Vector3(9f,0.5f,6f));r1.transform.localEulerAngles=new Vector3(0,8f,0);
            var r2=P(o,PrimitiveType.Cube,trim,new Vector3(0,4.7f,0),new Vector3(6.4f,0.5f,4.4f));r2.transform.localEulerAngles=new Vector3(0,-8f,0);
            P(o,PrimitiveType.Cylinder,trim,new Vector3(0,5.3f,0),new Vector3(0.3f,0.8f,0.3f));
        }
        private void ShapeTower(GameObject o,Material main,Material trim,Material dark)
        {
            P(o,PrimitiveType.Cube,dark,new Vector3(0,0.3f,0),new Vector3(4f,0.6f,4f));
            float w=3.4f;
            for(int i=0;i<4;i++){ P(o,PrimitiveType.Cube,main,new Vector3(0,1.1f+i*1.5f,0),new Vector3(w,1.5f,w)); w-=0.35f; }
            P(o,PrimitiveType.Sphere,trim,new Vector3(0,7.4f,0),Vector3.one*1.4f);
            var wheel=P(o,PrimitiveType.Cylinder,dark,new Vector3(2.2f,3f,0),new Vector3(1.6f,0.3f,1.6f));wheel.transform.localEulerAngles=new Vector3(0,0,90);
        }
        private void ShapePalace(GameObject o,Material main,Material trim,Material stone)
        {
            P(o,PrimitiveType.Cube,stone,new Vector3(0,0.25f,0),new Vector3(13f,0.5f,9f));
            P(o,PrimitiveType.Cube,stone,new Vector3(0,0.75f,0),new Vector3(11f,0.5f,7.5f));
            P(o,PrimitiveType.Cube,stone,new Vector3(0,1.25f,0),new Vector3(9f,0.5f,6f));
            P(o,PrimitiveType.Cube,main,new Vector3(0,3f,0),new Vector3(8f,3.5f,4.2f));
            P(o,PrimitiveType.Cube,trim,new Vector3(0,5.1f,0),new Vector3(10f,0.7f,6f));
            P(o,PrimitiveType.Cube,trim,new Vector3(0,5.6f,0),new Vector3(11.4f,0.3f,7f));
            foreach(var sx in new[]{-4.6f,4.6f})foreach(var sz in new[]{-2.6f,2.6f})
                P(o,PrimitiveType.Cylinder,trim,new Vector3(sx,6.1f,sz),new Vector3(0.25f,0.7f,0.25f));
        }
        private void ShapeRail(GameObject o,Material main,Material trim,Material dark)
        {
            P(o,PrimitiveType.Cube,main,new Vector3(0,1.2f,0),new Vector3(14f,0.5f,2.2f));
            P(o,PrimitiveType.Cube,dark,new Vector3(0,1.7f,0.9f),new Vector3(14f,0.18f,0.18f));
            P(o,PrimitiveType.Cube,dark,new Vector3(0,1.7f,-0.9f),new Vector3(14f,0.18f,0.18f));
            for(int i=-3;i<=3;i++) P(o,PrimitiveType.Cube,main,new Vector3(i*2f,0.5f,0),new Vector3(0.18f,1f,2f));
            // 机车
            P(o,PrimitiveType.Cube,trim,new Vector3(-2f,2.3f,0),new Vector3(2.4f,1.6f,1.6f));
            P(o,PrimitiveType.Cube,main,new Vector3(-3.4f,2.1f,0),new Vector3(1.2f,1.2f,1.4f));
            var b=P(o,PrimitiveType.Cylinder,dark,new Vector3(-1.6f,1.5f,0.85f),new Vector3(0.7f,0.7f,0.4f));b.transform.localEulerAngles=new Vector3(90,0,0);
            var b2=P(o,PrimitiveType.Cylinder,dark,new Vector3(-2.6f,1.5f,0.85f),new Vector3(0.7f,0.7f,0.4f));b2.transform.localEulerAngles=new Vector3(90,0,0);
        }
        private void ShapeDam(GameObject o,Material main,Material trim)
        {
            var water=ShaderHelper.Mat(new Color(0.2f,0.45f,0.7f));
            P(o,PrimitiveType.Cube,water,new Vector3(0,3.2f,-2.5f),new Vector3(16f,5f,5f));
            P(o,PrimitiveType.Cube,main,new Vector3(0,3f,0),new Vector3(16f,6f,3f));
            P(o,PrimitiveType.Cube,ShaderHelper.Mat(new Color(0.25f,0.28f,0.32f)),new Vector3(0,2.2f,1.55f),new Vector3(3f,3f,0.4f));
            foreach(var sx in new[]{-5f,5f})
            {
                P(o,PrimitiveType.Cube,trim,new Vector3(sx,6.5f,0),new Vector3(0.4f,5f,0.4f));
                P(o,PrimitiveType.Cube,trim,new Vector3(sx,9f,0),new Vector3(3f,0.4f,0.4f));
            }
        }
        private void ShapeDyson(GameObject o,Material main,Material trim)
        {
            var em = ShaderHelper.Emissive(new Color(0.05f,0.3f,0.45f),new Color(0.2f,0.8f,1f));
            P(o,PrimitiveType.Cylinder,ShaderHelper.Mat(new Color(0.3f,0.32f,0.36f)),new Vector3(0,0.3f,0),new Vector3(6f,0.6f,6f));
            P(o,PrimitiveType.Sphere,em,new Vector3(0,4f,0),Vector3.one*4.4f);
            var ring1=P(o,PrimitiveType.Cylinder,trim,new Vector3(0,4f,0),new Vector3(7f,0.12f,7f));
            var ring2=P(o,PrimitiveType.Cylinder,trim,new Vector3(0,4f,0),new Vector3(8.4f,0.1f,8.4f));ring2.transform.localEulerAngles=new Vector3(90,0,0);
            var ring3=P(o,PrimitiveType.Cylinder,trim,new Vector3(0,4f,0),new Vector3(9.6f,0.08f,9.6f));ring3.transform.localEulerAngles=new Vector3(0,90,0);
            for(int i=0;i<6;i++){float a=i*Mathf.PI/3f;P(o,PrimitiveType.Sphere,em,new Vector3(Mathf.Cos(a)*4.2f,4f,Mathf.Sin(a)*4.2f),Vector3.one*0.7f);}
        }

        private void AddLabel(GameObject host,string text,Color c)
        {
            var go=new GameObject("Label");
            go.transform.SetParent(host.transform,false);
            go.transform.localPosition=new Vector3(0,8.5f,0);
            var tm=go.AddComponent<TextMesh>();
            tm.text=text; tm.anchor=TextAnchor.MiddleCenter; tm.alignment=TextAlignment.Center;
            tm.fontSize=48; tm.characterSize=0.28f; tm.color=c;
            var mr=go.GetComponent<MeshRenderer>();
            if(mr!=null && tm.font!=null) mr.sharedMaterial=tm.font.material;
        }

        private static Color Hex(long h) => new(((h>>16)&255)/255f,((h>>8)&255)/255f,(h&255)/255f);
    }
}

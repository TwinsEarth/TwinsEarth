using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.World;
using PixelToCivilization.Rendering;
using PixelToCivilization.Actors;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// 环境系统 —— 对齐 v5.9.9：初始森林、树木生长/枯荣、人口小人（按社会阶层着色）、实时昼夜光照、环境生物。
    /// </summary>
    public class EnvironmentSystem : GameSystemBase
    {
        public Transform TreeRoot, AgentRoot;
        public Light Sun;
        public int VisualTreeCap = 400;
        private WorldGenerator _terrain;
        private Material _trunkMat, _leafMat, _groundMat;
        private readonly Dictionary<string,Material> _jobMats=new();
        private Vector3 _village;
        private WildlifeBirds _birds;
        private WildlifeFish _fish;
        private GameObject _fallbackSun;

        public override void Init(GameManager gm)
        {
            base.Init(gm);
            _terrain = Object.FindObjectOfType<WorldGenerator>();
            var roots=new GameObject("Environment").transform;
            roots.SetParent(gm.transform);
            TreeRoot=new GameObject("Trees").transform; TreeRoot.SetParent(roots);
            AgentRoot=new GameObject("Agents").transform; AgentRoot.SetParent(roots);
            _trunkMat=Mat(new Color(0.47f,0.32f,0.19f)); // V7.0.1 暖棕
            _leafMat=Mat(new Color(0.32f,0.72f,0.30f)); // V7.0.1 亮松绿
            AdoptSun();
        }

        private Material Mat(Color c)=>ShaderHelper.Mat(c);

        /// <summary>
        /// 统一光源：优先复用 EnvironmentDirector 的唯一主方向光。子系统 Init 早于导演创建，
        /// 此时先放一个不覆盖环境光/雾的保底光；PopulateInitial（点开始、导演已就绪）时再次调用，
        /// 销毁保底光并改用导演主光，确保场景全程只有一个方向光，杜绝双光叠加把地形冲白。
        /// </summary>
        private void AdoptSun()
        {
            var director = Object.FindObjectOfType<EnvironmentDirector>();
            if (director != null && director.Sun != null)
            {
                if (_fallbackSun != null) { Object.Destroy(_fallbackSun); _fallbackSun = null; }
                Sun = director.Sun;
                return;
            }
            if (Sun == null)
            {
                _fallbackSun = new GameObject("Sun");
                var l = _fallbackSun.AddComponent<Light>();
                l.type=LightType.Directional; l.intensity=1.0f;
                l.color=new Color(1f,0.95f,0.82f);
                _fallbackSun.transform.rotation=Quaternion.Euler(52f,35f,0);
                Sun=l;
            }
        }

        /// <summary>新游戏开局：玩家主村(全套) + 随机1~4个AI邻村(共2~5聚落) + 聚集人口(职业) + 飞鸟鱼群 + 万邦方国</summary>
        public void PopulateInitial()
        {
            if (_terrain==null) return;
            AdoptSun(); // 此时 EnvironmentDirector 已就绪，销毁保底光、统一为导演主光
            _village=_terrain.SettlementCenter;
            var centers=new List<Vector3>{_village};

            // 1) 玩家主村（朱红、祭坛/棚屋/农田/道路/旗/码头渔船/小车全套）
            InitialSettlementBuilder.Build(GM,_terrain,_village,"d9402f",true,0);
            for (int i=0;i<GameConstants.StartPop && i<120;i++)
            {
                var p=VillagePoint(15f);
                SpawnAgent(p.x,p.y,_village.x,_village.z);
            }

            // 2) V6.3.6 初始村落分布：
            //    主大陆共 2~5 村（1 玩家主村 + 1~4 AI 邻村），邻村规模在中型/小型间随机、大小各不相同；
            //    次大陆共 1~2 个小型初始村落（隔海独立发展，大航海前无法跨洋）；无人山地岛不立村。
            int mainTotal=Random.Range(2,6);          // 主大陆村落总数 2~5
            int extraMain=mainTotal-1;                // 主大陆还需补的 AI 邻村 1~4
            var palette=NationSystem.FlagPalette;
            int made=0;
            int attempts=0;
            while (made<extraMain && attempts<280)
            {
                attempts++;
                float ang=Random.value*Mathf.PI*2f;
                float dist=Random.Range(48f,Mathf.Max(64f,_terrain.LandRadius*0.78f));
                float x=_village.x+Mathf.Cos(ang)*dist, z=_village.z+Mathf.Sin(ang)*dist;
                if (_terrain.ContinentAt(x,z)!=_terrain.HomeContinent) continue;   // 只落在玩家主大陆
                if (_terrain.IsWater(x,z)||_terrain.IsBeach(x,z)) continue;
                if (!_terrain.IsFlatAt(x,z)) continue;                              // 只在平地
                if (_terrain.BiomeAt(x,z)==BiomeKind.Desert) continue;
                bool far=true;
                foreach (var c in centers)
                    if ((c.x-x)*(c.x-x)+(c.z-z)*(c.z-z)<52f*52f){far=false;break;}
                if (!far) continue;
                var vc=new Vector3(x,0,z);
                int tier=Random.value<0.5f?1:2;                                    // 中型 / 小型随机，规模不同
                InitialSettlementBuilder.Build(GM,_terrain,vc,palette[made%palette.Length],false,made+1,tier);
                int n=tier==1?Random.Range(16,27):Random.Range(8,17);              // 中型人口更多
                for (int k=0;k<n;k++){ var p=PointAround(x,z,11f); SpawnAgent(p.x,p.y,x,z); }
                centers.Add(vc); made++;
            }
            // 2b) 次大陆：共 1~2 个小型村，每个次大陆至多 1 个（打乱顺序挑选）
            int secTotal=Random.Range(1,3);
            var secIds=new List<int>();
            foreach (var L in _terrain.Landmasses)
                if (L.Id!=_terrain.HomeContinent && L.Kind==0) secIds.Add(L.Id);   // Kind=0 才是次大陆(平地)，排除无人山地岛
            for (int i=0;i<secIds.Count;i++){ int j=Random.Range(i,secIds.Count); (secIds[i],secIds[j])=(secIds[j],secIds[i]); }
            int secMade=0;
            foreach (var cid in secIds)
            {
                if (secMade>=secTotal) break;
                if (_terrain.RandomPointOnContinent(cid,out float sx,out float sz,90))
                {
                    var sc=new Vector3(sx,0,sz);
                    InitialSettlementBuilder.Build(GM,_terrain,sc,palette[made%palette.Length],false,made+1,2); // 次大陆一律小型
                    int n=Random.Range(6,13);
                    for (int k=0;k<n;k++){ var p=PointAround(sx,sz,10f); SpawnAgent(p.x,p.y,sx,sz); }
                    centers.Add(sc); made++; secMade++;
                }
            }

            // 3) 飞鸟群 / 鱼群（纯视觉，绕玩家主村）
            if (_birds==null)
            {
                _birds=GM.gameObject.GetComponent<WildlifeBirds>();
                if (_birds==null) _birds=GM.gameObject.AddComponent<WildlifeBirds>();
            }
            _birds.Init(_village);
            if (_fish==null)
            {
                _fish=GM.gameObject.GetComponent<WildlifeFish>();
                if (_fish==null) _fish=GM.gameObject.AddComponent<WildlifeFish>();
            }
            _fish.Init(_village,_terrain);

            // 4) 建立方国（玩家「华夏」+ AI 邻邦），进入万邦并立
            GM.Nation?.InitNations(centers);
            Debug.Log($"[PopulateInitial] 共生成 {centers.Count} 处聚落（主大陆{mainTotal}含1玩家主村 + 次大陆{secMade}）");
        }

        /// <summary>围绕指定村中心取一个陆地落点（邻村人口用）</summary>
        private Vector2 PointAround(float cx,float cz,float radius)
        {
            // V6.5.6 人口出生点必须在陆地（多次重选，避免人一出生就站水里）
            for(int i=0;i<8;i++)
            {
                float ang=Random.value*Mathf.PI*2f, rr=Random.value*radius;
                float x=cx+Mathf.Cos(ang)*rr, z=cz+Mathf.Sin(ang)*rr;
                if(_terrain==null||!_terrain.IsWater(x,z))return new Vector2(x,z);
            }
            return new Vector2(cx,cz);
        }

        /// <summary>V6.1.3 读档后：按恢复后的村址/地形重新生成飞鸟与鱼群（纯视觉，不写入存档）</summary>
        public void ReinitWildlife()
        {
            if(_terrain==null) _terrain=Object.FindObjectOfType<WorldGenerator>();
            _village=_terrain!=null?_terrain.SettlementCenter:Vector3.zero;
            _birds?.Init(_village);
            _fish?.Init(_village,_terrain);
        }

        /// <summary>取村落周边一个陆地落点</summary>
        private Vector2 VillagePoint(float radius)
        {
            float ang=Random.value*Mathf.PI*2f, rr=Random.value*radius;
            float x=_village.x+Mathf.Cos(ang)*rr, z=_village.z+Mathf.Sin(ang)*rr;
            if (_terrain!=null && _terrain.IsWater(x,z)){ x=_village.x+Random.Range(-6f,6f); z=_village.z+Random.Range(-6f,6f); }
            return new Vector2(x,z);
        }

        public void SpawnTree(float x,float z,int stage)
        {
            if (TreeRoot.childCount>=VisualTreeCap) return;
            var t=new TreeEntity{X=x,Z=z,Stage=stage,Age=stage*30};
            var go=new GameObject("Tree");
            go.transform.SetParent(TreeRoot);
            float y=_terrain!=null?_terrain.HeightAt(x,z):0;
            go.transform.position=new Vector3(x,y,z);
            // V6.1.3：多树种（0阔叶/1针叶塔松/2果树/3垂柳），坐标哈希稳定选种
            int _kh=Mathf.Abs(Mathf.RoundToInt(x*13.7f+z*7.3f))%100; // V7.0.1 以塔松为主
            int kind=_kh<60?1:_kh<85?0:_kh<95?2:3;                    // 60%塔松/25%阔叶/10%果树/5%垂柳
            EnsureTreeMats();
            float s=stage==0?0.45f:stage==1?0.78f:1f;
            // 树干（直接挂 go，保证枯树销毁时移除整棵）
            var trunk=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(go.transform);
            trunk.transform.localScale=new Vector3(0.26f*s,1.05f*s,0.26f*s);
            trunk.transform.localPosition=new Vector3(0,1.05f*s,0);
            DestroyCollider(trunk); trunk.GetComponent<Renderer>().sharedMaterial=_trunkMats[kind];
            // 树冠组（t.Leaves，成长期整体缩放兼容旧逻辑）
            var canopy=new GameObject("Canopy");canopy.transform.SetParent(go.transform);
            canopy.transform.localPosition=new Vector3(0,2.15f*s,0);
            var leaf=_leafMats[kind];
            System.Action<Vector3,Vector3> ball=(p,sc)=>{
                var b=GameObject.CreatePrimitive(PrimitiveType.Sphere);b.transform.SetParent(canopy.transform);
                b.transform.localPosition=p;b.transform.localScale=sc;DestroyCollider(b);b.GetComponent<Renderer>().sharedMaterial=leaf; };
            System.Action<Vector3,Vector3> coneAt=(p,sc)=>{ // V7.0.1 圆锥树冠
                var c=new GameObject("pineCone");c.transform.SetParent(canopy.transform);
                var mf=c.AddComponent<MeshFilter>();mf.sharedMesh=ConeMesh;
                c.AddComponent<MeshRenderer>().sharedMaterial=leaf;
                c.transform.localPosition=p;c.transform.localScale=sc; };
            switch(kind)
            {
                case 1: // 针叶塔松：3 层递减真圆锥（V7.0.1 玩具尖塔松）
                    coneAt(new Vector3(0,-0.55f*s,0),new Vector3(2.0f*s,1.8f*s,2.0f*s));
                    coneAt(new Vector3(0,0.35f*s,0),new Vector3(1.45f*s,1.6f*s,1.45f*s));
                    coneAt(new Vector3(0,1.10f*s,0),new Vector3(0.92f*s,1.4f*s,0.92f*s));
                    break;
                case 2: // 果树：两团叶 + 彩色果实
                    ball(new Vector3(0,0.2f*s,0),Vector3.one*1.3f*s);ball(new Vector3(0.5f*s,0,0.2f),Vector3.one*0.8f*s);
                    for(int i=0;i<5;i++){var f=GameObject.CreatePrimitive(PrimitiveType.Sphere);f.transform.SetParent(canopy.transform);
                        float a=i*1.26f; f.transform.localPosition=new Vector3(Mathf.Cos(a)*0.9f*s,0.35f*s+(i%2)*0.3f,Mathf.Sin(a)*0.9f*s);
                        f.transform.localScale=Vector3.one*0.22f*s;DestroyCollider(f);f.GetComponent<Renderer>().sharedMaterial=_fruitMat;}
                    break;
                case 3: // 垂柳：高干 + 下垂宽冠
                    ball(Vector3.zero,new Vector3(1.5f*s,1.0f*s,1.5f*s));
                    ball(new Vector3(0,-0.45f*s,0),new Vector3(1.7f*s,0.7f*s,1.7f*s));
                    break;
                default: // 阔叶：顶+三向团簇
                    ball(new Vector3(0,0.25f*s,0),Vector3.one*1.25f*s);
                    ball(new Vector3(0.7f*s,0f,0.1f),Vector3.one*0.85f*s);
                    ball(new Vector3(-0.6f*s,0.05f,-0.3f),Vector3.one*0.8f*s);
                    ball(new Vector3(0.1f*s,0.55f*s,-0.5f),Vector3.one*0.7f*s);
                    break;
            }
            t.Trunk=trunk;t.Leaves=canopy;
            S.Trees.Add(t);
        }
        private Material[] _leafMats,_trunkMats; private Material _fruitMat;
        static Mesh _coneMesh; // V7.0.1
        static Mesh ConeMesh{ get{ if(_coneMesh!=null)return _coneMesh; _coneMesh=BuildConeMesh(); return _coneMesh; } }
        static Mesh BuildConeMesh(int seg=14)
        {
            var m=new Mesh();var v=new System.Collections.Generic.List<Vector3>();var t=new System.Collections.Generic.List<int>();
            v.Add(new Vector3(0,0.5f,0));
            for(int i=0;i<=seg;i++){float a=(float)i/seg*Mathf.PI*2f;v.Add(new Vector3(Mathf.Cos(a)*0.5f,-0.5f,Mathf.Sin(a)*0.5f));}
            for(int i=0;i<seg;i++){t.Add(0);t.Add(i+1);t.Add(i+2);}
            int bc=v.Count; v.Add(new Vector3(0,-0.5f,0));
            for(int i=0;i<seg;i++){t.Add(bc);t.Add(i+2);t.Add(i+1);}
            m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();return m;
        }
        private void EnsureTreeMats()
        {
            if(_leafMats!=null)return;
            _leafMats=new[]{
                Mat(new Color(0.30f,0.70f,0.28f)),  // V7.0.1 阔叶 亮绿
                Mat(new Color(0.22f,0.60f,0.30f)),  // 针叶 松绿
                Mat(new Color(0.42f,0.78f,0.32f)),  // 果树 嫩绿
                Mat(new Color(0.48f,0.78f,0.38f))}; // 垂柳 柳绿
            _trunkMats=new[]{
                Mat(new Color(0.47f,0.32f,0.19f)),Mat(new Color(0.40f,0.28f,0.17f)),
                Mat(new Color(0.50f,0.35f,0.21f)),Mat(new Color(0.53f,0.39f,0.24f))};
            _fruitMat=Mat(new Color(0.85f,0.25f,0.18f));
        }
        private void DestroyCollider(GameObject g){var c=g.GetComponent<Collider>();if(c)Object.Destroy(c);}

        public void SpawnAgent(float x,float z,float homeX,float homeZ)
        {
            var cls=PickClass();
            SpawnAgent(x,z,homeX,homeZ,cls,PickJob(cls));
        }

        /// <summary>V6.1.2 读档恢复：按指定阶层/职业重建村民（不再随机）</summary>
        public void SpawnAgent(float x,float z,float homeX,float homeZ,string socialClass,string job)
            => SpawnAgent(x,z,homeX,homeZ,socialClass,job,-1,0);

        /// <summary>V7.0.2 读档恢复：携带年龄 / 颜色种子</summary>
        public void SpawnAgent(float x,float z,float homeX,float homeZ,string socialClass,string job,int age,int colorSeed)
        {
            var cls=string.IsNullOrEmpty(socialClass)?"commoner":socialClass;
            int rollAge;
            if(age>=0) rollAge=age;
            else { float rr=Random.value;                          // 开局即可见幼 / 壮 / 老
                   rollAge = rr<0.25f?Random.Range(0,14) : rr<0.85f?Random.Range(14,56) : Random.Range(56,76); }
            var a=new AgentEntity{X=x,Z=z,HomeX=homeX,HomeZ=homeZ,SocialClass=cls,
                                  Job=string.IsNullOrEmpty(job)?PickJob(cls):job,
                                  Age=rollAge,WanderTimer=Random.value*3f,
                                  ColorSeed=colorSeed>0?colorSeed:Random.Range(1,999999),
                                  LifeSpan=Random.Range(60,89)};
            a.LifeStage=HumanoidFactory.StageOf(a.Age);
            BuildAgentView(a,x,z);
            S.Agents.Add(a);
        }

        /// <summary>V7.0.2 按个体数据搭建人形外观（年龄/时代/职业/阶层/颜色）</summary>
        public void BuildAgentView(AgentEntity a,float x,float z)
        {
            var go=new GameObject("Agent");
            go.transform.SetParent(AgentRoot);
            float y=_terrain!=null?_terrain.HeightAt(x,z):0;
            go.transform.position=new Vector3(x,y,z);
            HumanoidFactory.Build(go,JobColor(a.Job),0.8f,a.Job=="soldier",a.Job,a.SocialClass,
                                  a.LifeStage,S.Era,a.ColorSeed,a.Age);
            a.View=go; a.Anim=null; a.ViewSig=a.LifeStage*100+S.Era;
        }

        /// <summary>V7.0.2 仅当外观签名不符（成长/跨时代）时重建，跳年补算后每体最多重建一次</summary>
        public void SyncAgentView(AgentEntity a)
        {
            int want=a.LifeStage*100+S.Era;
            if(a.View==null || a.ViewSig!=want) RebuildAgentView(a);
            a.ViewSig=want;
        }

        /// <summary>V7.0.2 成长 / 跨时代换装：原地重建外观，保留位置与朝向</summary>
        public void RebuildAgentView(AgentEntity a)
        {
            Vector3 pos=a.View!=null?a.View.transform.position
                       :new Vector3(a.X,_terrain!=null?_terrain.HeightAt(a.X,a.Z):0f,a.Z);
            Quaternion rot=a.View!=null?a.View.transform.rotation:Quaternion.identity;
            if(a.View!=null) Object.Destroy(a.View);
            BuildAgentView(a,a.X,a.Z);
            if(a.View!=null){ a.View.transform.position=pos; a.View.transform.rotation=rot; }
        }

        /// <summary>V6.1.2 Debug：清除全部树木（视觉与数据）</summary>
        public void ClearTrees()
        {
            for(int i=TreeRoot.childCount-1;i>=0;i--) Object.Destroy(TreeRoot.GetChild(i).gameObject);
            S.Trees.Clear();
        }

        // V6.1.2 底部工具：在指定落点种树（水里/沙滩不种）
        public void PlantTreeAt(float x,float z)
        {
            if (_terrain==null) return;
            if (_terrain.IsWater(x,z)||_terrain.IsBeach(x,z)) return;
            SpawnTree(x,z,1);
        }
        // V6.1.2 底部工具：在指定落点招募一名村民（家园锚点即落点，人口+1，受上限保护）
        public void RecruitAt(float x,float z)
        {
            if (S.Pop>=GameConstants.MaxPop) return;
            S.Pop++;
            SpawnAgent(x,z,x,z);
        }

        // 职业分配（农耕为主，辅以劳工/伐木/采矿/兵/商/官吏）
        private static string PickJob(string cls)
        {
            float r=Random.value;
            if (cls=="noble") return r<0.5f?"official":"merchant";
            if (cls=="rich")  return r<0.5f?"merchant":"official";
            if (r<0.40f) return "farmer";
            if (r<0.60f) return "worker";
            if (r<0.75f) return "woodcutter";
            if (r<0.87f) return "miner";
            if (r<0.97f) return "soldier";
            return "merchant";
        }
        // V6.1.3 职业服装色（骨骼人形 outfit）
        private static Color JobColor(string job)=>job switch
        { "farmer"=>new Color(0.86f,0.27f,0.18f),"woodcutter"=>new Color(0.30f,0.62f,0.30f),  // V7.0.1 红衣工人玩具色
          "miner"=>new Color(0.36f,0.58f,0.68f),"worker"=>new Color(0.96f,0.55f,0.14f),
          "soldier"=>new Color(0.78f,0.20f,0.18f),"merchant"=>new Color(0.16f,0.66f,0.64f),
          "official"=>new Color(0.22f,0.42f,0.82f),_=>new Color(0.86f,0.30f,0.18f) };
        private Material JobMat(string job)
        {
            if (_jobMats.TryGetValue(job,out var m)) return m;
            Color c=JobColor(job);
            var nm=Mat(c); _jobMats[job]=nm; return nm;
        }

        private string PickClass()
        {
            float r=Random.value*100;
            if (r<S.SocialClasses.Or("slave")) return "slave";
            r-=S.SocialClasses.Or("slave");
            if (r<S.SocialClasses.Or("commoner")) return "commoner";
            r-=S.SocialClasses.Or("commoner");
            if (r<S.SocialClasses.Or("rich")) return "rich";
            return "noble";
        }

        public override void OnYear(int year)
        {
            // 既有散树（如读档恢复）的生长与枯荣；视觉森林主体由 VegetationSystem 静态合批，不再随机散植独立树
            for (int i=S.Trees.Count-1;i>=0;i--)
            {
                var t=S.Trees[i]; t.Age++;
                if (t.Stage<2 && t.Age%30==0){ t.Stage++; if(t.Leaves)t.Leaves.transform.localScale=Vector3.one*(1.6f*(t.Stage==0?0.5f:1f)); }
                if (t.Age>100 && Random.value<0.05f)
                {
                    if (t.Trunk)Object.Destroy(t.Trunk.transform.parent.gameObject);
                    S.Trees.RemoveAt(i);
                }
            }

            // 同步人口视觉数量（最多显示150个小人），新增人口围绕村落聚集
            int target=Mathf.Min(150,S.Pop);
            while (S.Agents.Count<target && _terrain!=null)
            {
                var p=VillagePoint(18f);
                SpawnAgent(p.x,p.y,_village.x,_village.z);
            }
        }

        public override void Tick(float dt)
        {
            // 光照/昼夜统一由 EnvironmentDirector 管理（固定美观正午）；此处不再重复驱动复用的主光，避免互相覆盖。
        }
    }
}

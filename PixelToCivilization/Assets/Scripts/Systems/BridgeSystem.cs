using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.World;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// V6.3.9 桥梁系统（建筑-交通）。
    /// 规则：随时代材料进化 木→石→钢铁→混凝土，建桥技术（跨距/宽度/高度/结构）同步升级；
    /// 同一阵营（玩家）在相邻两块【预期陆地】（主大陆/次大陆/岛，以 WorldGenerator.Landmass 为准，
    /// 不使用浅水连通分量编号，避免近岸浅滩把两块陆地误判为同一块）都有据点时，工程司按【最短跨距】自动建桥，
    /// 仅当当前时代技术跨距足以跨越才执行，否则不建；造价随实际跨距与材料增长。桥面可供车辆越水通行。
    /// </summary>
    public class BridgeSystem : GameSystemBase
    {
        private WorldGenerator _w;
        private Transform _root;
        private float _scanTimer;
        private const float ScanInterval = 3f;     // 现实秒：周期性评估（也在每个游戏年补一次）
        private const int MaxBridges = 350;        // 性能上限（普通300+跨海50）
        // V6.5.7 岸线全量参与配对（原 ShoreCap=120 截断会漏掉真正最近点，已移除）
        private int[] _landGrid;                   // 预期陆地网格（按 Landmass 圆盘栅格化，独立于浅水连通分量）
        private int _landBuilt=-1;                 // 已栅格化的陆地数量（变化则重建）

        // 时代 → 技术档：0木 / 1石 / 2钢铁 / 3混凝土
        private static readonly float[] Span = { 28f, 50f, 75f, 100f };     // 各档最大跨距；V6.5.4 硬上限100
        private const float HardMaxSpan = 100f;   // 大陆之间桥最大跨距，超过一律不建
        private const float GrandSpan = 50f;      // 跨距>50 记为跨海大桥
        private const int MainCap = 5, SecCap = 3, IslandCap = 2;  // 单块陆地接桥数：主大陆/次大陆/岛
        // V6.5.5 时间额度 + 寿命：普通桥每10年获1座额度、上限300、寿命10~30年；跨海大桥每50年获1座额度、上限50、寿命50~100年
        private const int NormalEveryYears=10, NormalCapTotal=300, NormalLifeMin=10, NormalLifeMax=30;
        private const int GrandEveryYears=50, GrandCapTotal=50, GrandLifeMin=50, GrandLifeMax=100;
        private const int RunStride=8;   // 每桥8整数：ax,az,bx,bz,tier,span,birthYear,lifeSpan
        private readonly System.Random _rng=new System.Random();
        private static readonly float[] Width = { 4f, 6f, 8f, 12f };         // 桥宽
        private static readonly float[] DeckY = { 0.75f, 1.15f, 1.5f, 1.35f };// 桥面高
        private static readonly string[] TName = { "木桥", "石拱桥", "钢铁桁架桥", "混凝土大桥" };
        private static readonly Color[] DeckColor = {
            new(0.45f,0.30f,0.16f), new(0.62f,0.60f,0.56f),
            new(0.42f,0.46f,0.52f), new(0.72f,0.72f,0.74f) };
        private Material[] _deckMat;
        private readonly Dictionary<int,int> _cellTier = new();   // 桥面格 → 技术档（供车辆取桥面高度）

        private int G => _w!=null?_w.G:GameConstants.MaxMapSize;
        private int Idx(int gx,int gz)=>gz*G+gx;

        public override void Init(GameManager gm)
        {
            base.Init(gm);
            _w=Object.FindObjectOfType<WorldGenerator>();
            _root=EntityViewFactory.EnsureRoot("Bridges",gm.transform);
            _deckMat=new Material[4];
            for(int i=0;i<4;i++) _deckMat[i]=ShaderHelper.Pbr(DeckColor[i], i==2?0.6f:0f, i>=2?0.5f:0.25f, 700+i);
        }

        public override void Tick(float dt)
        {
            if(S.CurrentMap!="home") return;
            _scanTimer-=Time.unscaledDeltaTime;
            if(_scanTimer>0f) return;
            _scanTimer=ScanInterval;
            AutoBuildScan();
        }
        public override void OnYear(int year){ if(S.CurrentMap!="home")return; DecaySweep(year); AutoBuildScan(); }
        public override void OnEra(int n,int o){ _scanTimer=0f; }

        /// <summary>V6.5.5 寿命到期拆除：普通桥寿命10~30年、跨海大桥50~100年，到期清除并释放额度</summary>
        void DecaySweep(int year)
        {
            if(S.BridgeRuns.Count<RunStride)return;
            bool changed=false;
            var keep=new List<int>();
            for(int i=0;i+RunStride-1<S.BridgeRuns.Count;i+=RunStride)
            {
                int birth=S.BridgeRuns[i+6], life=S.BridgeRuns[i+7];
                int span=S.BridgeRuns[i+5];
                if(life>0 && year-birth>=life)
                {
                    changed=true;
                    GM.AddEvent("info",$"🌉 一座{(span>GrandSpan?"跨海大桥":"桥梁")}已达{life}年使用年限，老化拆除（额度释放）");
                    continue;
                }
                for(int k=0;k<RunStride;k++)keep.Add(S.BridgeRuns[i+k]);
            }
            if(changed){ S.BridgeRuns=keep; RebuildViews(); }
        }

        private static int TierOf(int era){ if(era<=0)return 0; if(era<=2)return 1; if(era<=4)return 2; return 3; }

        // 按 Landmass 圆盘把"预期陆地"栅格化（与浅水连通分量解耦），陆地数量变化时重建
        private void EnsureLandGrid()
        {
            if(_w==null)return;
            int lm=_w.Landmasses.Count;
            if(_landGrid!=null && _landBuilt==lm)return;
            // V6.5.8 直接复制权威 ContinentMap（已按 X/Z 轴正确栅格化，且含运行时增长/读档恢复的陆地）
            _landGrid=new int[G*G];
            var cm=_w.ContinentMap;
            for(int gz=0;gz<G;gz++)for(int gx=0;gx<G;gx++)_landGrid[gz*G+gx]=cm[gz,gx];
            _landBuilt=lm;
        }
        private int LandOfCell(int gx,int gz)
        {
            if(gx<0||gx>=G||gz<0||gz>=G)return 0;
            return _landGrid[gz*G+gx];
        }

        // ============ 自动建桥扫描 ============
        private void AutoBuildScan()
        {
            if(_w==null||S.BridgeRuns.Count/RunStride>=MaxBridges) return;
            EnsureLandGrid();
            int tier=TierOf(S.Era);
            float maxSpan=Mathf.Min(Span[tier],HardMaxSpan);
            var occupied=new HashSet<int>();
            foreach(var b in S.Buildings){int lid=LandOfWorld(b.X,b.Z);if(lid>0)occupied.Add(lid);}
            if(occupied.Count<2) return;
            var shore=CollectShores(occupied);
            if(shore.Count<2) return;
            // 既有桥统计：陆地接桥数、普通/跨海现存数
            var incident=new Dictionary<int,int>();
            int usedNormal=0,usedGrand=0;
            for(int i=0;i+RunStride-1<S.BridgeRuns.Count;i+=RunStride)
            {
                int a=LandOfWorld(CellX(S.BridgeRuns[i]),CellZ(S.BridgeRuns[i+1]));
                int b2=LandOfWorld(CellX(S.BridgeRuns[i+2]),CellZ(S.BridgeRuns[i+3]));
                if(a>0)incident[a]=incident.TryGetValue(a,out var ia)?ia+1:1;
                if(b2>0)incident[b2]=incident.TryGetValue(b2,out var ib)?ib+1:1;
                if(S.BridgeRuns[i+5]>GrandSpan)usedGrand++;else usedNormal++;
            }
            // V6.5.5 时间累积额度：每10年1普通(上限300)、每50年1跨海(上限50)；现存数低于已获额度才可建
            int capNormal=Mathf.Min(NormalCapTotal,S.Year/NormalEveryYears);
            int capGrand =Mathf.Min(GrandCapTotal,S.Year/GrandEveryYears);
            var paired=new HashSet<long>();
            for(int i=0;i+RunStride-1<S.BridgeRuns.Count;i+=RunStride)
            {
                int a=LandOfWorld(CellX(S.BridgeRuns[i]),CellZ(S.BridgeRuns[i+1]));
                int b2=LandOfWorld(CellX(S.BridgeRuns[i+2]),CellZ(S.BridgeRuns[i+3]));
                if(a>0&&b2>0) paired.Add(PairKey(a,b2));
            }
            var ids=new List<int>(shore.Keys);
            int bestA=-1,bestB=-1,bax=0,baz=0,bbx=0,bbz=0; float bestD=float.MaxValue;
            Dictionary<string,int> bestCost=null; bool bestGrand=false;
            for(int i=0;i<ids.Count;i++)
                for(int j=i+1;j<ids.Count;j++)
                {
                    int A=ids[i],B=ids[j];
                    if(paired.Contains(PairKey(A,B)))continue;
                    if(!LandHasQuota(A,incident)||!LandHasQuota(B,incident))continue;   // 单陆接桥上限
                    if(ClosestShore(shore[A],shore[B],out int ax,out int az,out int bx,out int bz,out float dw))
                    {
                        if(dw<GameConstants.Tile*2f||dw>maxSpan) continue;
                        bool grand=dw>GrandSpan;
                        if(grand){if(usedGrand>=capGrand)continue;}else if(usedNormal>=capNormal)continue; // 时间额度
                        var c=CostOf(tier,dw);
                        if(!CanAfford(c)) continue;
                        if(dw<bestD){bestD=dw;bestA=A;bestB=B;bax=ax;baz=az;bbx=bx;bbz=bz;bestCost=c;bestGrand=grand;}
                    }
                }
            if(bestA<0) return;
            BuildBridge(bax,baz,bbx,bbz,tier,bestCost);
        }

        /// <summary>单块陆地接桥数上限：主大陆(Br≥130)5、次大陆3、无人岛2</summary>
        private bool LandHasQuota(int lid,Dictionary<int,int> incident)
        {
            int cap=IslandCap;
            foreach(var L in _w.Landmasses) if(L.Id==lid){ cap = L.Kind==1?IslandCap : (L.Br>=130f?MainCap:SecCap); break; }
            int used=incident.TryGetValue(lid,out var u)?u:0;
            return used<cap;
        }

        private int LandOfWorld(float wx,float wz)
        {
            int gx=_w.W2CX(wx),gz=_w.W2CZ(wz);
            return LandOfCell(gx,gz);
        }

        /// <summary>对 occupied 每块预期陆地，沿其圆盘包围盒在【全图】取岸线格（陆地且四邻有真海），
        /// 不依赖初始活动疆域——次大陆/岛可能落在尚未展开的外圈。</summary>
        private Dictionary<int,List<Vector2Int>> CollectShores(HashSet<int> occupied)
        {
            var res=new Dictionary<int,List<Vector2Int>>();
            foreach(var L in _w.Landmasses)
            {
                if(!occupied.Contains(L.Id))continue;
                int cx=_w.W2CX(L.Cx),cz=_w.W2CZ(L.Cz);
                int rcx=Mathf.CeilToInt(L.Br*1.15f/GameConstants.Tile),rcz=Mathf.CeilToInt(L.Br*1.15f/_w.TZ);
                var list=new List<Vector2Int>();
                int x0=Mathf.Max(1,cx-rcx),x1=Mathf.Min(G-2,cx+rcx),z0=Mathf.Max(1,cz-rcz),z1=Mathf.Min(G-2,cz+rcz);
                // V6.6.0 端点必须是【真实陆地】：ContinentMap 归属该陆 + 高度高于水面；四邻至少一格真海（纯高度判定，不受圆盘预期陆地干扰）
                for(int gz=z0;gz<=z1;gz++)
                    for(int gx=x0;gx<=x1;gx++)
                    {
                        if(_w.ContinentMap[gz,gx]!=L.Id)continue;
                        if(_w.HeightMap[gz,gx]<GameConstants.WaterLevel)continue;   // 端点绝不能是水面
                        if(!SeaCell(gx+1,gz)&&!SeaCell(gx-1,gz)&&!SeaCell(gx,gz+1)&&!SeaCell(gx,gz-1))continue;
                        list.Add(new Vector2Int(gx,gz));
                    }
                if(list.Count>0)res[L.Id]=list;
            }
            return res;
        }
        private bool IsOpenWater(int gx,int gz)
        {
            if(gx<0||gx>=G||gz<0||gz>=G)return false;
            if(_landGrid[gz*G+gx]!=0)return false;
            return _w.HeightMap[gz,gx]<GameConstants.WaterLevel;
        }
        // V6.6.0 纯高度海面判定（不看圆盘预期陆地），保证桥两端之间确实隔水、端点本身为陆
        private bool SeaCell(int gx,int gz){ if(gx<0||gx>=G||gz<0||gz>=G)return false; return _w.HeightMap[gz,gx]<GameConstants.WaterLevel; }
        private bool DryLand(int gx,int gz){ if(gx<0||gx>=G||gz<0||gz>=G)return false; return _w.HeightMap[gz,gx]>=GameConstants.WaterLevel; }
        private static long PairKey(int a,int b){if(a>b)(a,b)=(b,a);return (long)a*100000+b;}

        private bool ClosestShore(List<Vector2Int> A,List<Vector2Int> B,out int ax,out int az,out int bx,out int bz,out float worldDist)
        {
            ax=az=bx=bz=0;worldDist=float.MaxValue;bool any=false;
            // V6.5.7 对完整岸线做穷举精确最近点（不再跳点），保证选出两块陆地距离最短的连接两点
            for(int i=0;i<A.Count;i++)
                for(int j=0;j<B.Count;j++)
                {
                    float dx=A[i].x-B[j].x,dz=A[i].y-B[j].y;float d=dx*dx+dz*dz;
                    if(d<worldDist){worldDist=d;ax=A[i].x;az=A[i].y;bx=B[j].x;bz=B[j].y;any=true;}
                }
            worldDist=Mathf.Sqrt(worldDist)*GameConstants.Tile;
            return any;
        }

        // ============ 造价：随跨距与材料增长 ============
        private static Dictionary<string,int> CostOf(int tier,float L)
        {
            var c=new Dictionary<string,int>();
            switch(tier)
            {
                case 0: c["wood"]=Mathf.CeilToInt(L*1.2f); break;
                case 1: c["stone"]=Mathf.CeilToInt(L*1.5f); c["wood"]=Mathf.CeilToInt(L*0.4f); break;
                case 2: c["steel"]=Mathf.CeilToInt(L*0.6f); c["iron"]=Mathf.CeilToInt(L*0.8f); break;
                default: c["concrete"]=Mathf.CeilToInt(L*0.8f); c["steel"]=Mathf.CeilToInt(L*0.4f); break;
            }
            return c;
        }
        private bool CanAfford(Dictionary<string,int> c){foreach(var kv in c)if(S.GetRes(kv.Key)<kv.Value)return false;return true;}

        // ============ 建桥 ============
        private void BuildBridge(int ax,int az,int bx,int bz,int tier,Dictionary<string,int> cost)
        {
            // V6.6.0 最终保险：任一端点不是干燥陆地（水面）则放弃，绝不以水面为起终点
            if(!DryLand(ax,az)||!DryLand(bx,bz)){ Debug.Log("[Bridge] 端点含水，取消建桥"); return; }
            float sx=CellX(ax),sz=CellZ(az),ex=CellX(bx),ez=CellZ(bz);
            float dx=ex-sx,dz=ez-sz;float len=Mathf.Sqrt(dx*dx+dz*dz);
            float yaw=Mathf.Atan2(dx,dz)*Mathf.Rad2Deg;
            int landA=LandOfCell(ax,az),landB=LandOfCell(bx,bz);
            var marked=new List<int>();
            int steps=Mathf.CeilToInt(len/(GameConstants.Tile*0.5f));
            for(int k=0;k<=steps;k++)
            {
                float t=steps==0?0f:(float)k/steps;
                float wx=sx+dx*t,wz=sz+dz*t;
                int gx=_w.W2CX(wx),gz=_w.W2CZ(wz);
                if(gx<0||gx>=G||gz<0||gz>=G)continue;
                int lid=LandOfCell(gx,gz);
                if(lid>0){ if(lid!=landA&&lid!=landB){Rollback(marked);return;} continue; }
                int idx=Idx(gx,gz);
                if(S.BridgeCells.Add(idx)){marked.Add(idx);_cellTier[idx]=tier;}
            }
            if(marked.Count==0)return;
            foreach(var kv in cost) S.AddRes(kv.Key,-kv.Value);
            int spanI=Mathf.RoundToInt(len);
            bool grand=len>GrandSpan;
            int birth=S.Year;
            int life=grand?GrandLifeMin+_rng.Next(GrandLifeMax-GrandLifeMin+1)
                          :NormalLifeMin+_rng.Next(NormalLifeMax-NormalLifeMin+1);
            S.BridgeRuns.Add(ax);S.BridgeRuns.Add(az);S.BridgeRuns.Add(bx);S.BridgeRuns.Add(bz);
            S.BridgeRuns.Add(tier);S.BridgeRuns.Add(spanI);S.BridgeRuns.Add(birth);S.BridgeRuns.Add(life);
            foreach(var idx in marked) DeckView(idx,yaw,tier);
            var cs=new System.Text.StringBuilder();foreach(var kv in cost){if(cs.Length>0)cs.Append('、');cs.Append(kv.Value).Append(ResName(kv.Key));}
            string kind=grand?"跨海大桥":"桥梁";
            GM.AddEvent("good",$"🌉 建成{TName[tier]}{kind}，连接两块陆地（跨距{len:0}单位，寿命{life}年，耗{cs}）");
        }
        private void Rollback(List<int> marked){foreach(var i in marked){S.BridgeCells.Remove(i);_cellTier.Remove(i);}}
        private static string ResName(string k)=>k switch{"wood"=>"木","stone"=>"石","iron"=>"铁","steel"=>"钢","concrete"=>"水泥",_=>k};

        private float CellX(int gx)=>_w.C2WX(gx)+GameConstants.Tile*0.5f;
        private float CellZ(int gz)=>_w.C2WZ(gz)+_w.TZ*0.5f;

        // ============ 车辆通行查询 ============
        public bool IsBridgeAt(float wx,float wz)
        {
            int gx=_w.W2CX(wx),gz=_w.W2CZ(wz);
            if(gx<0||gx>=G||gz<0||gz>=G)return false;
            return S.BridgeCells.Contains(Idx(gx,gz));
        }
        public float DeckHeightAt(float wx,float wz)
        {
            int gx=_w.W2CX(wx),gz=_w.W2CZ(wz);
            return _cellTier.TryGetValue(Idx(gx,gz),out var t)?DeckY[t]:0f;
        }

        // ============ 视图：随技术档改变结构 ============
        private void DeckView(int idx,float yaw,int tier)
        {
            int gx=idx%G,gz=idx/G;
            var cell=new GameObject($"Bridge_{tier}_{gx}_{gz}");
            cell.transform.SetParent(_root);
            cell.transform.position=new Vector3(CellX(gx),DeckY[tier],CellZ(gz));
            cell.transform.rotation=Quaternion.Euler(0,yaw,0);
            Part(cell,PrimitiveType.Cube,new Vector3(0,0,0),new Vector3(Width[tier],0.4f,GameConstants.Tile*1.02f),_deckMat[tier]);
            float w=Width[tier];
            if(tier==0)
            {
                Part(cell,PrimitiveType.Cube,new Vector3(-w/2,0.45f,0),new Vector3(0.18f,0.9f,0.18f),_deckMat[0]);
                Part(cell,PrimitiveType.Cube,new Vector3( w/2,0.45f,0),new Vector3(0.18f,0.9f,0.18f),_deckMat[0]);
            }
            else if(tier==1)
            {
                Part(cell,PrimitiveType.Cylinder,new Vector3(0,-1.0f,0),new Vector3(0.9f,2.0f,0.9f),_deckMat[1]);
            }
            else if(tier==2)
            {
                Part(cell,PrimitiveType.Cube,new Vector3(-w/2,0.8f,0),new Vector3(0.12f,1.6f,0.12f),_deckMat[2]);
                Part(cell,PrimitiveType.Cube,new Vector3( w/2,0.8f,0),new Vector3(0.12f,1.6f,0.12f),_deckMat[2]);
                Part(cell,PrimitiveType.Cube,new Vector3(0,1.55f,0),new Vector3(w,0.1f,0.1f),_deckMat[2]);
            }
            else
            {
                Part(cell,PrimitiveType.Cube,new Vector3(0,0.21f,0),new Vector3(0.3f,0.04f,GameConstants.Tile),ShaderHelper.Mat(new Color(0.95f,0.82f,0.3f)));
                if(((gx+gz)&3)==0)
                {
                    Part(cell,PrimitiveType.Cylinder,new Vector3(-w/2,0.9f,0),new Vector3(0.12f,1.8f,0.12f),ShaderHelper.Mat(new Color(0.3f,0.3f,0.32f)));
                    Part(cell,PrimitiveType.Sphere,new Vector3(-w/2,1.85f,0),Vector3.one*0.22f,ShaderHelper.Emissive(new Color(1f,0.95f,0.7f),new Color(1f,0.9f,0.5f)));
                }
            }
        }
        private static GameObject Part(GameObject parent,PrimitiveType t,Vector3 localPos,Vector3 scale,Material mat)
        {
            var p=GameObject.CreatePrimitive(t);
            Object.Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(parent.transform,false);
            p.transform.localPosition=localPos;p.transform.localScale=scale;
            if(mat!=null)p.GetComponent<Renderer>().material=mat;
            return p;
        }

        /// <summary>Debug：无视跨距与资源，为最近两块有据点陆地建当前时代桥（回归验证用）</summary>
        public bool ForceNearest()
        {
            if(_w==null)return false;
            EnsureLandGrid();
            int tier=TierOf(S.Era);
            var occ=new HashSet<int>();
            foreach(var bld in S.Buildings){int l=LandOfWorld(bld.X,bld.Z);if(l>0)occ.Add(l);}
            var shore=CollectShores(occ);var ids=new List<int>(shore.Keys);
            int ba=-1,bb=-1;int ax=0,az=0,bx=0,bz=0;float bd=float.MaxValue;
            for(int i=0;i<ids.Count;i++)for(int j=i+1;j<ids.Count;j++)
                if(ClosestShore(shore[ids[i]],shore[ids[j]],out var x1,out var z1,out var x2,out var z2,out var dw)&&dw<bd)
                {bd=dw;ba=ids[i];bb=ids[j];ax=x1;az=z1;bx=x2;bz=z2;}
            if(ba<0){Debug.Log("[Bridge] ForceNearest 找不到两块有据点陆地");return false;}
            BuildBridge(ax,az,bx,bz,tier,CostOf(tier,bd));
            Debug.Log($"[Bridge] ForceNearest 已连接 陆{ba}-陆{bb} 跨距{bd:0} tier{tier}");
            return true;
        }

        /// <summary>浏览器自证：打印有据点陆地两两最短岸线跨距 vs 当前时代技术跨距</summary>
        public void DebugProbe()
        {
            if(_w==null){Debug.Log("[Bridge] world null");return;}
            EnsureLandGrid();
            var lm=new System.Text.StringBuilder("[Bridge] LANDS ");
            foreach(var L in _w.Landmasses) lm.Append($"#{L.Id}(k{L.Kind},{L.Cx:0},{L.Cz:0},r{L.Br:0}) ");
            Debug.Log(lm.ToString());
            var vc=new System.Text.StringBuilder("[Bridge] VILLAGES ");
            for(int i=0;i<S.VillageX.Count;i++)
            {
                float vx=S.VillageX[i],vz=S.VillageZ[i];
                vc.Append($"[{i}] comp={_w.ContinentAt(vx,vz)} disk={LandOfWorld(vx,vz)} ({vx:0},{vz:0}) ");
            }
            Debug.Log(vc.ToString());
            var hist=new Dictionary<int,int>();
            foreach(var b in S.Buildings){int l=LandOfWorld(b.X,b.Z);hist[l]=hist.TryGetValue(l,out var hh)?hh+1:1;}
            var hh2=new System.Text.StringBuilder("[Bridge] BLD-DISK ");
            foreach(var kv in hist)hh2.Append($"land{kv.Key}:{kv.Value} ");
            Debug.Log(hh2.ToString());
            int tier=TierOf(S.Era);
            var occ=new HashSet<int>();
            foreach(var bld in S.Buildings){int l=LandOfWorld(bld.X,bld.Z);if(l>0)occ.Add(l);}
            var shore=CollectShores(occ);
            var ids=new List<int>(shore.Keys);
            var sb=new System.Text.StringBuilder();
            sb.Append($"[Bridge] era={S.Era} tier{tier}({TName[tier]}) 技术跨距≤{Mathf.Min(Span[tier],HardMaxSpan):0} 已建桥={S.BridgeRuns.Count/RunStride} 年{S.Year}(普通额{Mathf.Min(NormalCapTotal,S.Year/NormalEveryYears)}/跨海额{Mathf.Min(GrandCapTotal,S.Year/GrandEveryYears)}) 有据点陆地={ids.Count}");
            for(int i=0;i<ids.Count;i++)for(int j=i+1;j<ids.Count;j++)
                if(ClosestShore(shore[ids[i]],shore[ids[j]],out _,out _,out _,out _,out var dw))
                    sb.Append($" | 陆{ids[i]}-陆{ids[j]} 跨距{dw:0}"+(dw<=Span[tier]?"[可建]":"[跨距不足]"));
            Debug.Log(sb.ToString());
        }

        /// <summary>读档后按 S.BridgeRuns / BridgeCells 重建全部桥体</summary>
        public void RebuildViews()
        {
            if(_w==null)return;
            EnsureLandGrid();
            for(int i=_root.childCount-1;i>=0;i--)Object.Destroy(_root.GetChild(i).gameObject);
            _cellTier.Clear();
            for(int r=0;r+RunStride-1<S.BridgeRuns.Count;r+=RunStride)
            {
                int ax=S.BridgeRuns[r],az=S.BridgeRuns[r+1],bx=S.BridgeRuns[r+2],bz=S.BridgeRuns[r+3],tier=S.BridgeRuns[r+4];
                float sx=CellX(ax),sz=CellZ(az),dx=CellX(bx)-sx,dz=CellZ(bz)-sz;
                float yaw=Mathf.Atan2(dx,dz)*Mathf.Rad2Deg;
                int steps=Mathf.CeilToInt(Mathf.Sqrt(dx*dx+dz*dz)/(GameConstants.Tile*0.5f));
                for(int k=0;k<=steps;k++)
                {
                    float tt=steps==0?0f:(float)k/steps;
                    float wx=sx+dx*tt,wz=sz+dz*tt;
                    int gx=_w.W2CX(wx),gz=_w.W2CZ(wz);
                    if(gx<0||gx>=G||gz<0||gz>=G)continue;
                    if(LandOfCell(gx,gz)>0)continue;
                    int idx=Idx(gx,gz);
                    S.BridgeCells.Add(idx);
                    if(!_cellTier.ContainsKey(idx)){_cellTier[idx]=tier;DeckView(idx,yaw,tier);}
                }
            }
        }
    }
}

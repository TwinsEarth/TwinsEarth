using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.World;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// 大运河 + 潮汐系统 —— 1:1 对齐 v5.9.9 updateCanalSystem/findCanalDigPoint：
    /// 研究「运河工程」后自动开凿，每 8 秒耗 金30/石20/木15，用 BFS 连通分量找出被陆地隔开的水域、
    /// 挖最短陆径（每次≤3格）；潮汐按正弦周期变化，涨潮(>0.45)运河成水可通航、退潮成浅滩。
    /// </summary>
    public class CanalSystem : GameSystemBase
    {
        public const float MaxBonus = 50f;
        public const float BonusPerSegment = 5f;
        private const float DigInterval = 8f;
        private const int MaxDigPerCycle = 3;

        private WorldGenerator _world;
        private Transform _root;
        private readonly Dictionary<int,GameObject> _views = new();
        private int N => GameConstants.MaxMapSize;   // V6.3.7(真扩展) 与全量地形网格一致
        private int Idx(int gx,int gz)=>gz*N+gx;

        public override void Init(GameManager gm)
        {
            base.Init(gm);
            _world=Object.FindObjectOfType<WorldGenerator>();
            _root=EntityViewFactory.EnsureRoot("Canals",gm.transform);
        }

        public override void Tick(float dt)
        {
            if (S.CurrentMap!="home") return;
            UpdateTide(dt);
            UpdateAutoDig(dt);
        }

        // ============ 潮汐 ============
        private void UpdateTide(float dt)
        {
            S.TidePhase += dt*0.08f;
            S.TideLevel=(Mathf.Sin(S.TidePhase)+1f)/2f;
            bool was=S.TideHigh;
            S.TideHigh=S.TideLevel>0.45f;
            if (was!=S.TideHigh && S.CanalCells.Count>0)
            {
                ApplyTideVisual();
                GM.AddEvent("info", S.TideHigh ? "🌊 涨潮！运河水位上升，船只可通航" : "🏜️ 退潮！运河水位下降，船只暂不可通行");
            }
        }

        // ============ 自动开凿 ============
        private void UpdateAutoDig(float dt)
        {
            if (!S.CanalAutoBuild) return;
            S.CanalBuildTimer += dt;
            if (S.CanalBuildTimer < DigInterval) return;
            S.CanalBuildTimer=0f;
            // 每周期消耗 金30/石20/木15，任一不足则等待
            if (S.GetRes("gold")<30||S.GetRes("stone")<20||S.GetRes("wood")<15) return;
            var path=FindCanalDigPoint();
            if (path==null) return;
            S.AddRes("gold",-30);S.AddRes("stone",-20);S.AddRes("wood",-15);
            int dug=0;
            foreach (var cell in path)
            {
                int idx=Idx(cell.x,cell.y);
                if (IsWaterCell(cell.x,cell.y)) continue;
                if (S.CanalCells.Contains(idx)) continue;
                if (HasBuildingAt(cell.x,cell.y)) continue;
                S.CanalCells.Add(idx);
                CreateCellView(cell.x,cell.y);
                dug++;
                if (dug>=MaxDigPerCycle) break;
            }
            if (dug>0)
            {
                S.CanalSegments+=dug;
                S.CanalBonus=Mathf.Min(MaxBonus,S.CanalSegments*BonusPerSegment);
                GM.AddEvent("good",$"⛏ 运河开凿+{dug}段（共{S.CanalSegments}段），贸易+{Mathf.RoundToInt(S.CanalBonus)}%");
            }
        }

        /// <summary>研究「运河工程」完成时调用（对齐 v5.9.9）</summary>
        public void StartAuto()
        {
            if (S.CanalAutoBuild) return;
            S.CanalAutoBuild=true; S.CanalBuildTimer=0f;
            GM.AddEvent("good","🌊 运河工程启动！自动消耗金/石/木开凿运河，涨潮时可通航！");
        }

        public void AddSegment(){ S.CanalSegments++; S.CanalBonus=Mathf.Min(MaxBonus,S.CanalSegments*BonusPerSegment); }
        public void SetAuto(bool on){ S.CanalAutoBuild=on; S.CanalBuildTimer=0f; }

        // ============ BFS：找两片被陆地隔开的水域之间最短陆径（1:1 移植 findCanalDigPoint）============
        private List<Vector2Int> FindCanalDigPoint()
        {
            if (_world==null) return null;
            var water=new List<Vector2Int>();
            for (int gz=2;gz<N-2;gz++)
                for (int gx=2;gx<N-2;gx++)
                    if (IsWaterCell(gx,gz)) water.Add(new Vector2Int(gx,gz));
            if (water.Count<10) return null;

            // 1) 水域四连通分量标号
            var component=new int[N*N]; for(int i=0;i<component.Length;i++)component[i]=-1;
            int compCount=0;
            var dirs=new (int,int)[]{(1,0),(-1,0),(0,1),(0,-1)};
            foreach (var start in water)
            {
                int sidx=Idx(start.x,start.y);
                if (component[sidx]!=-1) continue;
                int cid=compCount++;
                var q=new Queue<int>(); q.Enqueue(sidx); component[sidx]=cid;
                while(q.Count>0)
                {
                    int cur=q.Dequeue(); int cx=cur%N,cz=cur/N;
                    foreach(var (dx,dz) in dirs)
                    {
                        int nx=cx+dx,nz=cz+dz;
                        if(nx<0||nx>=N||nz<0||nz>=N)continue;
                        int ni=Idx(nx,nz);
                        if(component[ni]!=-1)continue;
                        if(IsWaterCell(nx,nz)){component[ni]=cid;q.Enqueue(ni);}
                    }
                }
            }
            if (compCount<=1) return null;

            // 2) 多源 BFS 从所有水域同时扩散，遇到来自不同分量的邻居即找到最短陆径
            var dist=new int[N*N]; for(int i=0;i<dist.Length;i++)dist[i]=-1;
            var fromComp=new int[N*N]; for(int i=0;i<fromComp.Length;i++)fromComp[i]=-1;
            var bfs=new Queue<int>();
            foreach (var wc in water){int idx=Idx(wc.x,wc.y);dist[idx]=0;fromComp[idx]=component[idx];bfs.Enqueue(idx);}
            List<Vector2Int> best=null; int bestDist=999;
            while(bfs.Count>0)
            {
                int cur=bfs.Dequeue(); int cx=cur%N,cz=cur/N,cd=dist[cur];
                if (cd>=10) break;
                foreach(var (dx,dz) in dirs)
                {
                    int nx=cx+dx,nz=cz+dz;
                    if(nx<1||nx>=N-1||nz<1||nz>=N-1)continue;
                    int ni=Idx(nx,nz);
                    bool isWater=IsWaterCell(nx,nz);
                    bool isCanal=S.CanalCells.Contains(ni);
                    if (isCanal){ if(dist[ni]==-1){dist[ni]=cd+1;fromComp[ni]=fromComp[cur];bfs.Enqueue(ni);} continue; }
                    if (isWater)
                    {
                        if(dist[ni]==-1){dist[ni]=0;fromComp[ni]=component[ni];bfs.Enqueue(ni);}
                        else if(fromComp[ni]!=fromComp[cur]&&fromComp[ni]!=-1)
                        {
                            var p=TracePath(nx,nz,cx,cz);
                            if(p!=null&&p.Count<bestDist){bestDist=p.Count;best=p;}
                        }
                    }
                    else
                    {
                        if(dist[ni]==-1){dist[ni]=cd+1;fromComp[ni]=fromComp[cur];bfs.Enqueue(ni);}
                        else if(fromComp[ni]!=fromComp[cur]&&fromComp[ni]!=-1&&cd>0)
                        {
                            var p=TracePath(nx,nz,cx,cz);
                            if(p!=null&&p.Count<bestDist){bestDist=p.Count;best=p;}
                        }
                    }
                }
            }
            return best;
        }

        // 对齐 traceCanalPath：从(x1,z1)朝(x2,z2)贪心走≤5步，遇水即停
        private List<Vector2Int> TracePath(int x1,int z1,int x2,int z2)
        {
            var path=new List<Vector2Int>{new(x1,z1)};
            int cx=x1,cz=z1;
            for(int i=0;i<5;i++)
            {
                int dx=System.Math.Sign(x2-cx),dz=System.Math.Sign(z2-cz);
                if(dx!=0)cx+=dx; else if(dz!=0)cz+=dz; else break;
                if(cx<1||cx>=N-1||cz<1||cz>=N-1)break;
                if(IsWaterCell(cx,cz))break;
                path.Add(new Vector2Int(cx,cz));
            }
            return path.Count>0?path:null;
        }

        private bool IsWaterCell(int gx,int gz)
        {
            if (_world==null) return false;
            return _world.WaterMap[gz,gx]>0.5f || _world.HeightMap[gz,gx]<GameConstants.WaterLevel;
        }
        private bool HasBuildingAt(int gx,int gz)
        {
            foreach(var b in S.Buildings)
            {
                int bgx=_world.W2CX(b.X);
                int bgz=_world.W2CZ(b.Z);
                if(bgx==gx&&bgz==gz)return true;
            }
            return false;
        }

        // ============ 视觉：每格一块贴片，涨潮蓝水/退潮浅滩 ============
        private Vector3 CellWorld(int gx,int gz)
        {
            float x=_world.C2WX(gx)+GameConstants.Tile*0.5f;
            float z=_world.C2WZ(gz)+_world.TZ*0.5f;
            return new Vector3(x,0,z);
        }
        private void CreateCellView(int gx,int gz)
        {
            int idx=Idx(gx,gz);
            if (_views.ContainsKey(idx)) return;
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name="Canal_"+gx+"_"+gz;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_root);
            go.transform.position=CellWorld(gx,gz);
            go.transform.rotation=Quaternion.Euler(90f,0f,0f);
            go.transform.localScale=new Vector3(GameConstants.Tile,GameConstants.Tile,1f);
            var rend=go.GetComponent<Renderer>();
            rend.material=ShaderHelper.Trans(new Color(0.55f,0.45f,0.3f,0.6f));
            _views[idx]=go;
            ApplyOne(go);
        }
        private void ApplyOne(GameObject go)
        {
            var p=go.transform.position;
            p.y=S.TideHigh?GameConstants.WaterLevel+0.06f:0.15f;
            go.transform.position=p;
            var rend=go.GetComponent<Renderer>();
            if(rend!=null) rend.material.color=S.TideHigh?new Color(0.2f,0.6f,0.9f,0.75f):new Color(0.55f,0.45f,0.3f,0.6f);
        }
        private void ApplyTideVisual(){ foreach(var go in _views.Values) if(go!=null)ApplyOne(go); }

        /// <summary>读档后按 S.CanalCells 重建全部视觉贴片</summary>
        public void RebuildViews()
        {
            foreach(var kv in _views) if(kv.Value!=null)Object.Destroy(kv.Value);
            _views.Clear();
            foreach(var idx in S.CanalCells)
            {
                int gx=idx%N,gz=idx/N; CreateCellView(gx,gz);
            }
            ApplyTideVisual();
        }
    }
}

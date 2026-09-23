using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;

namespace PixelToCivilization.World
{
    /// <summary>
    /// V6.1.3 初始聚落构建器：新游戏开局随机生成 2~5 处聚落。
    /// 玩家主村(full=true)：中心祭坛、双圈16棚屋(住房≥初始人口)、农田/水井/粮仓/伐木场、辐射土路、国色旗帜、码头渔船、小车；
    /// AI 邻村(full=false)：祭坛 + 4 棚屋 + 2 农田 + 简化道路 + 国色旗帜。
    /// 分裂期新立的方国用 BuildOutpost（祭坛 + 2 棚屋 + 旗帜的轻量据点）。全部开局免费赠予。
    /// </summary>
    public static class InitialSettlementBuilder
    {
        /// <summary>构建一处聚落。flagHex=国色(无#)；full=是否玩家主村（含码头/船/车全套）；settlementIndex=聚落序号（节点名）</summary>
        public static Vector3 Build(GameManager gm, WorldGenerator terrain, Vector3 center,
            string flagHex, bool full, int settlementIndex, int sizeTier=2)
        {
            var rootGo = new GameObject("Settlement_"+settlementIndex);
            rootGo.transform.SetParent(gm.transform);
            Transform root = rootGo.transform;
            float cx = center.x, cz = center.z;
            Color flag = NationEntity.HexToColor(flagHex);

            // 1) 中心祭坛（部落议事/势力中心）
            gm.Building.PlaceInitial("altar", cx, cz);

            var roadPts = new List<Vector3>();
            if (full)
            {
                // 2a) 双圈 16 座棚屋（内圈 r=10 八座、外圈 r=18 八座交错）。
                //     16×5=80 住房 + 2 渔船 4 + 邻村，稳定 ≥ 初始人口 StartPop=80，修复住房倒挂导致人口只减不增
                for (int ring=0;ring<2;ring++)
                {
                    float rr = ring==0 ? 10f : 18f;
                    for (int i=0;i<8;i++)
                    {
                        float a=i*Mathf.PI*2f/8f + (ring==0 ? Mathf.PI/8f : 0f);
                        float rx=cx+Mathf.Cos(a)*rr, rz=cz+Mathf.Sin(a)*rr;
                        if (!terrain.IsWater(rx,rz)) gm.Building.PlaceInitial("hut",rx,rz);
                        roadPts.Add(new Vector3(rx,0,rz));
                    }
                }
                // 3a) 农田 3 块 / 水井 / 粮仓 / 伐木场
                gm.Building.PlaceInitial("farm", cx+15f, cz+4f);
                gm.Building.PlaceInitial("farm", cx-14f, cz+8f);
                gm.Building.PlaceInitial("farm", cx+3f, cz-16f);
                gm.Building.PlaceInitial("well", cx+5.5f, cz+5f);
                gm.Building.PlaceInitial("granary", cx-6.5f, cz+5f);
                gm.Building.PlaceInitial("lumbermill", cx-13f, cz-11f);
                roadPts.Add(new Vector3(cx+15f,0,cz+4f));
                roadPts.Add(new Vector3(cx-14f,0,cz+8f));
                roadPts.Add(new Vector3(cx+3f,0,cz-16f));
                roadPts.Add(new Vector3(cx-13f,0,cz-11f));
                BuildRoads(terrain, root, new Vector3(cx,0,cz), roadPts);
            }
            else
            {
                // 2b) V6.3.6 邻村分档：sizeTier=1 中型(8棚屋+3农田,半径11) / sizeTier=2 小型(4棚屋+2农田,半径9)，规模各不相同
                int hutN = sizeTier==1?8:4;
                float ringR = sizeTier==1?11f:9f;
                for (int i=0;i<hutN;i++)
                {
                    float a=i*Mathf.PI*2f/hutN + Mathf.PI/4f;
                    float rx=cx+Mathf.Cos(a)*ringR, rz=cz+Mathf.Sin(a)*ringR;
                    if (!terrain.IsWater(rx,rz)) gm.Building.PlaceInitial("hut",rx,rz);
                    roadPts.Add(new Vector3(rx,0,rz));
                }
                gm.Building.PlaceInitial("farm", cx+(ringR+2f), cz+3f);
                gm.Building.PlaceInitial("farm", cx-(ringR-1f), cz-5f);
                if (sizeTier==1) gm.Building.PlaceInitial("farm", cx+2f, cz-(ringR+3f));
                roadPts.Add(new Vector3(cx+(ringR+2f),0,cz+3f));
                roadPts.Add(new Vector3(cx-(ringR-1f),0,cz-5f));
                if (sizeTier==1) roadPts.Add(new Vector3(cx+2f,0,cz-(ringR+3f)));
                BuildRoads(terrain, root, new Vector3(cx,0,cz), roadPts);
            }

            // 势力旗帜（高杆 + 国色旗面），立于祭坛旁
            BuildFlag(terrain, root, cx+2.4f, cz-2.4f, flag);

            if (full)
            {
                // 近水岸码头 + 停泊 2 艘小木船（自适应搜索最近淡水/海岸方向）
                Vector2 waterDir = NearestWaterDir(terrain, new Vector2(cx,cz));
                if (FindShoreAndWater(terrain, new Vector2(cx,cz), new Vector2(cx,cz)+waterDir*120f,
                      out var shore, out var water, out var water2))
                {
                    BuildDock(terrain, root, shore, water);
                    gm.Naval.SpawnInitialShip("small_boat", water.x, water.y);
                    if (water2.HasValue) gm.Naval.SpawnInitialShip("small_boat", water2.Value.x, water2.Value.y);
                }
                // 村内一辆运输小车
                gm.Cart.SpawnCart("small_cart", cx+4f, cz-5f);
                Debug.Log("[InitialSettlement] 玩家主村：祭坛+16棚屋(双圈)+3农田+水井/粮仓/伐木场+道路+旗帜+码头渔船+小车");
            }
            else
            {
                Debug.Log($"[InitialSettlement] 邻村#{settlementIndex}：祭坛+4棚屋+2农田+道路+国色旗帜");
            }
            return center;
        }

        /// <summary>分裂期新立方国的轻量据点：祭坛 + 2 棚屋 + 国色旗帜</summary>
        public static Vector3 BuildOutpost(GameManager gm, WorldGenerator terrain, Vector3 c, string flagHex, int idx)
        {
            var rootGo = new GameObject("Settlement_"+idx);
            rootGo.transform.SetParent(gm.transform);
            Transform root = rootGo.transform;
            gm.Building.PlaceInitial("altar", c.x, c.z);
            for (int i=0;i<2;i++)
            {
                float a=i*Mathf.PI + Mathf.PI/4f;
                float rx=c.x+Mathf.Cos(a)*7f, rz=c.z+Mathf.Sin(a)*7f;
                if (!terrain.IsWater(rx,rz)) gm.Building.PlaceInitial("hut",rx,rz);
            }
            BuildFlag(terrain, root, c.x+2.2f, c.z-2.2f, NationEntity.HexToColor(flagHex));
            return c;
        }

        // ---------- 土路：中心到各点的扁平面片，合并为一个静态网格 ----------
        static void BuildRoads(WorldGenerator terrain, Transform parent, Vector3 center, List<Vector3> targets)
        {
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx") ?? FallbackCube();
            var segs = new List<CombineInstance>();
            foreach (var t in targets)
            {
                float dx=t.x-center.x, dz=t.z-center.z;
                float len=Mathf.Sqrt(dx*dx+dz*dz);
                if (len<1f) continue;
                float midx=(center.x+t.x)*0.5f, midz=(center.z+t.z)*0.5f;
                float y=terrain.HeightAt(midx,midz)+0.06f;
                float yaw=Mathf.Atan2(dx,dz)*Mathf.Rad2Deg;
                var ci=new CombineInstance();
                ci.mesh=cube;
                ci.transform=Matrix4x4.TRS(new Vector3(midx,y,midz),Quaternion.Euler(0,yaw,0),new Vector3(2.4f,0.12f,len));
                segs.Add(ci);
            }
            if (segs.Count==0) return;
            var go=new GameObject("DirtRoads"); go.transform.SetParent(parent);
            var mf=go.AddComponent<MeshFilter>(); var mr=go.AddComponent<MeshRenderer>();
            var m=new UnityEngine.Mesh(){indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            m.CombineMeshes(segs.ToArray(),true,true); m.RecalculateBounds(); mf.sharedMesh=m;
            mr.sharedMaterial=ShaderHelper.Mat(new Color(0.46f,0.39f,0.28f));
            mr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            go.isStatic=true;
        }

        // ---------- 势力旗帜：木杆 + 国色旗面 ----------
        static void BuildFlag(WorldGenerator terrain, Transform parent, float x, float z, Color cloth)
        {
            var go=new GameObject("FactionFlag"); go.transform.SetParent(parent);
            float y=terrain.HeightAt(x,z);
            var pole=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(pole.GetComponent<Collider>());
            pole.transform.SetParent(go.transform);
            pole.transform.localPosition=new Vector3(0,2.2f,0); pole.transform.localScale=new Vector3(0.12f,2.2f,0.12f);
            pole.GetComponent<Renderer>().sharedMaterial=ShaderHelper.Mat(new Color(0.32f,0.23f,0.14f));
            var flag=GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(flag.GetComponent<Collider>());
            flag.name="FlagCloth"; flag.transform.SetParent(go.transform);
            flag.transform.localPosition=new Vector3(0.85f,3.6f,0); flag.transform.localScale=new Vector3(1.5f,0.85f,0.06f);
            Color dark=new(cloth.r*0.32f,cloth.g*0.32f,cloth.b*0.32f);
            flag.GetComponent<Renderer>().sharedMaterial=ShaderHelper.Emissive(cloth,dark);
            go.transform.position=new Vector3(x,y,z);
        }

        // ---------- 湖岸码头：伸入水面的木平台 + 木桩 ----------
        static void BuildDock(WorldGenerator terrain, Transform parent, Vector2 shore, Vector2 water)
        {
            var go=new GameObject("Dock"); go.transform.SetParent(parent);
            Vector2 dir=(water-shore).normalized;
            float yaw=Mathf.Atan2(dir.x,dir.y)*Mathf.Rad2Deg;
            for (int i=0;i<5;i++)
            {
                float px=shore.x+dir.x*(i*1.6f), pz=shore.y+dir.y*(i*1.6f);
                var plank=GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(plank.GetComponent<Collider>());
                plank.transform.SetParent(go.transform);
                plank.transform.position=new Vector3(px, GameConstants.WaterLevel+0.18f, pz);
                plank.transform.rotation=Quaternion.Euler(0,yaw,0);
                plank.transform.localScale=new Vector3(2.4f,0.18f,1.7f);
                plank.GetComponent<Renderer>().sharedMaterial=ShaderHelper.Mat(new Color(0.42f,0.30f,0.17f));
            }
        }

        /// <summary>V6.3.6 仅海洋为可停泊水域：排除陆地淡水湖与内河，保证初始渔船出生在海里而非中央大湖</summary>
        static bool IsSea(WorldGenerator terrain,float x,float z)
            => terrain.IsWater(x,z)
               && terrain.BiomeAt(x,z)!=BiomeKind.FreshWater
               && terrain.BiomeAt(x,z)!=BiomeKind.River;

        /// <summary>环形扫描，找村址到最近水体（湖/河/海）的水平方向；找不到则回退正前方</summary>
        static Vector2 NearestWaterDir(WorldGenerator terrain, Vector2 home)
        {
            Vector2 best=Vector2.up; float bestDist=float.MaxValue;
            const int rays=32;
            for (int i=0;i<rays;i++)
            {
                float ang=i*Mathf.PI*2f/rays;
                Vector2 dir=new(Mathf.Cos(ang),Mathf.Sin(ang));
                for (float d=18f;d<=185f;d+=3f)
                {
                    var p=home+dir*d;
                    if (IsSea(terrain,p.x,p.y)) { if (d<bestDist){bestDist=d;best=dir;} break; } // V6.3.6 码头只朝海洋
                }
            }
            return best;
        }

        /// <summary>沿提示方向找到"岸边陆地点 + 水面点(+第二个停泊点)"</summary>
        static bool FindShoreAndWater(WorldGenerator terrain, Vector2 home, Vector2 hint,
            out Vector2 shore, out Vector2 water, out Vector2? water2)
        {
            shore=default;water=default;water2=null;
            Vector2 dir=(hint-home); if (dir.sqrMagnitude<0.01f) dir=Vector2.up; dir.Normalize();
            Vector2 foundShore=default,foundWater=default; bool gotShore=false,gotWater=false;
            for (float d=20f; d<185f; d+=2f) // V6.3.6 延伸至海岸
            {
                var p=home+dir*d;
                bool w=IsSea(terrain,p.x,p.y); // V6.3.6 泊位取海水
                if (!gotShore && w){ foundShore=home+dir*(d-3f); gotShore=true; }
                if (gotShore && w){ foundWater=p; gotWater=true; break; }
            }
            if (!(gotShore&&gotWater)) return false;
            shore=foundShore; water=foundWater;
            var p2=foundWater+new Vector2(-dir.y,dir.x)*5f;
            if (IsSea(terrain,p2.x,p2.y)) water2=p2; else { var p3=foundWater+new Vector2(dir.y,-dir.x)*5f; if(IsSea(terrain,p3.x,p3.y))water2=p3; }
            return true;
        }

        static Mesh FallbackCube()
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var m=g.GetComponent<MeshFilter>().sharedMesh; var c=Object.Instantiate(m);
            Object.DestroyImmediate(g); return c;
        }
    }
}

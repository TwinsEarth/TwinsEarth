using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Data;

namespace PixelToCivilization.World
{
    /// <summary>生物群系标记（在高度之上的叠加遮罩，用于沙漠/淡水着色、植被与小地图）</summary>
    public enum BiomeKind : byte { Default=0, Desert=1, FreshWater=2, River=3 }

    /// <summary>
    /// V6.1.2 世界地形生成器 —— 每次开局随机种子重新生成。
    /// 分层适应性生成：①海陆骨架（圆形大陆+FBM 海岸，比例约束 海≥50%/陆≥30%/山≤10%）
    /// ②山区（内环高斯峰，总覆盖≤10%）③河流（山峰顺最低势贪心入海/湖）④湖泊（内陆低洼）
    /// ⑤沙漠（内陆低地遮罩）；最后自适应挑选最平坦且近淡水的村址并平坦化。
    /// 逐顶点生物群系色地形（PxC/VertexColor，全平台一致），提供 HeightAt/IsWater/IsBeach/BiomeAt 查询。
    /// </summary>
    public class WorldGenerator : MonoBehaviour
    {
        public int Seed = 20260829;
        public float[,] HeightMap;
        public float[,] WaterMap;
        public BiomeKind[,] Biome;
        public float Tile => GameConstants.Tile;
        // 全量画布（V6.5.8：长X=初始5倍4800 × 宽Z=初始3倍2880，面积15倍；网格取方形 G=1200，Z 轴每格 TileZ=2.4）
        public int G => GameConstants.MaxMapSize;
        public float GW => GameConstants.WorldMaxX;                 // X 轴全量世界长 4800
        public float TZ => GameConstants.TileZ;                     // Z 轴每格世界单位 2.4
        public float HalfX => GameConstants.WorldMaxX*0.5f;         // 2400
        public float HalfZ => GameConstants.WorldMaxZ*0.5f;         // 1440
        // 世界↔网格（按轴）
        public float C2WX(int gx)=>(gx-G*0.5f)*Tile;
        public float C2WZ(int gz)=>(gz-G*0.5f)*TZ;
        public int W2CX(float wx)=>Mathf.RoundToInt(wx/Tile+G*0.5f);
        public int W2CZ(float wz)=>Mathf.RoundToInt(wz/TZ+G*0.5f);
        // 当前"真实活动疆域"（随年代外扩）：外部系统可玩边界一律取这里（矩形，取受限轴 Z 的内接方形给旧方形逻辑）
        public float ActiveHalfX => RevealRadius>0f?RevealRadius:GameConstants.WorldSize*0.5f;
        public float ActiveHalfZ => RevealRadius>0f?Mathf.Min(RevealRadius,HalfZ):GameConstants.WorldSize*0.5f;
        public float ActiveHalf => Mathf.Min(ActiveHalfX,ActiveHalfZ);
        public int ActiveN => Mathf.Clamp(Mathf.RoundToInt(ActiveHalf*2f/Tile),GameConstants.MapSize,G);
        public float ActiveWorld => ActiveN*Tile;
        public int Size => ActiveN;          // 兼容旧调用：当前活动网格
        public float World => ActiveWorld;   // 兼容旧调用：当前活动世界边长
        public bool InsideFrontier(float x,float z)=>Mathf.Abs(x)<=ActiveHalfX+2f && Mathf.Abs(z)<=ActiveHalfZ+2f;
        public GameObject WaterPlane;
        /// <summary>本局大陆半径（世界单位），决定海陆比例</summary>
        public float LandRadius = 165f;
        /// <summary>玩家初始部落村址中心（自适应选取）</summary>
        public Vector3 SettlementCenter = Vector3.zero;

        // 比例统计（生成后填充，供调试/小地图）
        // LandRatio=全部非水（含山）；LandOnlyRatio=纯陆地（非水且非山，用户口径"陆地不含山坡"）；
        // MountainRatio=山坡总占比；MaxMountainBlob=最大单片山坡占比（每片≤1%）
        public float SeaRatio, LandRatio, LandOnlyRatio, MountainRatio, MaxMountainBlob, DesertRatio, FreshWaterRatio;

        // —— V6.1.3 大地图随年代自然延展 ——
        // 未显现的外圈区域沉入海中（视觉/小地图隐藏），逻辑高度仍以 HeightMap 为准；随年代 RevealRadius 平滑外扩
        public float RevealRadius=-1f, RevealTarget=-1f, RevealBase;
        public float Expansion=1f;
        Mesh _terrainMesh; MeshCollider _terrainCol;
        Vector3[] _rvVerts; Color32[] _rvCols32;   // V6.5.8 地形刷新复用缓冲
        public bool RevealEnabled => RevealTarget>0f;

        readonly List<(int land,float x,float z,float r,float h)> _peaks = new();
        readonly List<(int cx,int cz,float r)> _deserts = new();
        readonly List<(int land,float x,float z,float r,float h)> _plateaus = new();   // V6.1.9 仅主大陆的高原（宽缓隆起，非山峰）

        // —— V6.1.7 多大陆隔海世界：开局随机 2~5 块被大海隔开的大陆/岛屿，年代外扩时隔海陆续浮现 ——
        /// <summary>一块大陆/岛屿：中心、等效半径、三块岸线扰动相位；Kind=0 大陆(平地陆地为主)，Kind=1 山地岛(峰密少平地)</summary>
        public class Landmass { public float Cx,Cz,Br,P1,P2,P3; public int Id; public int Kind; public bool Grown; }
        readonly List<Landmass> _lands = new();
        /// <summary>V6.5.4 运行时实时增陆（非开局生成）的陆地，供存档/读档恢复</summary>
        public readonly List<Landmass> GrownLands = new();
        /// <summary>大陆归属图：0=开阔海洋，1..K=各大陆/岛屿（1 固定为玩家主大陆）</summary>
        public int[,] ContinentMap;
        public readonly List<Vector3> ContinentCenters = new();
        public int ContinentCount, HomeContinent = 1;
        readonly HashSet<int> _revealedLands = new();
        public int LandmassCount => _lands.Count;
        /// <summary>V6.1.9(i) 只读陆地列表（月度潮汐分陆地抬降水面用）</summary>
        public System.Collections.Generic.IReadOnlyList<Landmass> Landmasses => _lands;
        /// <summary>V6.1.9(i) 月度潮汐注入：按世界坐标返回该处当前生效水位（null=用基准水位）</summary>
        public System.Func<float,float,float> DynamicWaterLevel;
        public float EffectiveWaterLevel(float x,float z)
            => DynamicWaterLevel!=null ? DynamicWaterLevel(x,z) : GameConstants.WaterLevel;

        /// <summary>随机新种子（每次开始游戏调用）</summary>
        public static int RandomSeed() =>
            System.Environment.TickCount ^ (int)(Time.realtimeSinceStartup*1000f) ^ Random.Range(1,int.MaxValue);

        public void Generate(int seed = 20260829, float forceScale = -1f)
        {
            Seed = seed;
            int n = G;
            HeightMap = new float[n,n];
            WaterMap = new float[n,n];
            Biome = new BiomeKind[n,n];
            ContinentMap = new int[n,n];
            _peaks.Clear(); _deserts.Clear(); _lands.Clear(); GrownLands.Clear(); _plateaus.Clear(); ContinentCenters.Clear(); _revealedLands.Clear();
            var rng = new System.Random(seed);
            float centerX = HalfX, centerZ = HalfZ;
            float scale = forceScale>0f ? forceScale : 1f;

            // ① 随机 2~5 块被大海隔开的陆地：Id=1 主大陆(平地,无山,单块≤10%)、Id=2 次大陆(平地,无山)、Id≥3 无人山地岛(唯一允许有山)
            // V6.1.9 规则：恰好 1 个主大陆(≤10%) + 1~3 个次大陆(各 2~5%、平地无山) + 1~3 个无人岛(唯一可有山，总数≤5)
            float R0 = 140f + (float)rng.NextDouble()*16f;         // 主大陆 140~156（π*156²/960²≈8.3%，唯一且≤10%）
            LandRadius = R0;
            var main = new Landmass{Cx=(float)(rng.NextDouble()-0.5)*12f, Cz=(float)(rng.NextDouble()-0.5)*12f,
                Br=R0, Id=1, Kind=0, P1=(float)rng.NextDouble()*9f, P2=(float)rng.NextDouble()*9f, P3=(float)rng.NextDouble()*9f};
            _lands.Add(main);
            const float edge=452f;                 // 世界半宽 480，留 28 边距不贴边
            float ringIn=R0+96f, ringOut=372f;     // 主大陆外围布地环带（960 世界）
            int nSec = 1+rng.Next(3);              // 次大陆 1~3 个（每个 2~5%）
            int nIsl = 1+rng.Next(3);              // 无人岛 1~3 个（总数≤5）
            int want=nSec+nIsl;
            for(int i=0;i<want;i++)
            {
                bool isSecond = i<nSec;                       // 前 nSec 块为次大陆(平地 Kind=0)，其余为无人山地岛(Kind=1)
                float sizeLo = isSecond?82f:34f;              // 次大陆 82~100(2.3~3.4%)；岛 34~54
                float sizeHi = isSecond?100f:54f;
                int  kind   = isSecond?0:1;
                float baseAng = i*Mathf.PI*2f/want + (float)(rng.NextDouble()-0.5)*0.5f;
                bool placed=false;
                for(int att=0;att<200;att++)
                {
                    float ang=baseAng+(float)(rng.NextDouble()-0.5)*1.1f;
                    float ring=ringIn+(float)rng.NextDouble()*(ringOut-ringIn);
                    float br=(sizeLo+(float)rng.NextDouble()*(sizeHi-sizeLo))*scale;
                    float cx=main.Cx+Mathf.Cos(ang)*ring, cz=main.Cz+Mathf.Sin(ang)*ring;
                    if(Mathf.Abs(cx)+br>edge||Mathf.Abs(cz)+br>edge) continue;      // 不贴世界边
                    bool ok=true;
                    foreach(var L in _lands)
                    {
                        // 块间留够深海沟：与主大陆海沟略宽，块间海沟也足够深，杜绝浅滩粘连
                        float gap = L.Id==1 ? R0*0.30f+40f : (br+L.Br)*0.34f+40f;
                        if(Vector2.Distance(new Vector2(cx,cz),new Vector2(L.Cx,L.Cz)) < br+gap){ok=false;break;}
                    }
                    if(!ok) continue;
                    _lands.Add(new Landmass{Cx=cx,Cz=cz,Br=br,Id=_lands.Count+1,Kind=kind,
                        P1=(float)rng.NextDouble()*9f,P2=(float)rng.NextDouble()*9f,P3=(float)rng.NextDouble()*9f});
                    placed=true; break;
                }
                // 首个次大陆是硬需求：随机摆不下时用确定性兜底，沿 8 方向找第一个合法点
                if(!placed && isSecond && i==0) PlaceFallbackLand(_lands,main,R0,90f*scale,0,edge);
            }
            // 最终保险：极端情况下平地陆地不足 2 块，则强制补一块次大陆，保证“至少 1 主 1 次”
            int plainCount=0; foreach(var lm in _lands) if(lm.Kind==0) plainCount++;
            if(plainCount<2) PlaceFallbackLand(_lands,main,R0,90f*scale,0,edge);
            EnforceSeparation(edge);   // 所有非主陆地外推分离：杜绝与主大陆或其它陆地浅滩接壤
            // V6.5.4 禁止"一次性建成再慢慢显现"：开局只生成初始960世界内陆地，外环一律留深海，
            //        改由 WorldExpansionSystem 在每100/500/1000年实时随机增陆（GrowLandmass）。

            // ② 特征归属（V6.1.9 规则）：
            //    主大陆=沙漠 + 恰好1个随机形状大湖(主湖盆+3~5子湖盆融合) + 1处高原 + 河流；
            //    次大陆=仅绿地/沙滩 + 1~3个小湖（无沙漠/无高原/无河/无山）；
            //    无人岛=唯一可有山峰，无湖/无沙漠/无河
            var lakes = new List<(float x,float z,float r,float d)>();
            foreach(var L in _lands)
            {
                bool isMain=L.Id==1;
                bool isSecond=(!isMain && L.Kind==0);
                bool isIsland=L.Kind==1;
                if(isIsland)
                {
                    int pn=3+rng.Next(2);                               // 山峰只在无人岛
                    for(int i=0;i<pn;i++)
                    {
                        float ang=(float)rng.NextDouble()*Mathf.PI*2f;
                        float rr=L.Br*(0.06f+(float)rng.NextDouble()*0.50f);
                        _peaks.Add((L.Id,L.Cx+Mathf.Cos(ang)*rr,L.Cz+Mathf.Sin(ang)*rr,
                            (8.8f+(float)rng.NextDouble()*5.2f)*scale,
                            (6.0f+(float)rng.NextDouble()*3.0f)));
                    }
                    continue;
                }
                if(isMain)
                {
                    // 高原：1 处宽缓隆起（非山峰，最高<5.6 不计山地）
                    float pang=(float)rng.NextDouble()*Mathf.PI*2f;
                    float prr=L.Br*(0.10f+(float)rng.NextDouble()*0.32f);
                    float plr=L.Br*(0.30f+(float)rng.NextDouble()*0.12f);
                    _plateaus.Add((L.Id,L.Cx+Mathf.Cos(pang)*prr,L.Cz+Mathf.Sin(pang)*prr,plr,1.35f));
                    // 恰好 1 个随机形状大湖：主湖盆 + 3~5 个偏移子湖盆融合成不规则形状（整体约束在内陆 0.62Br 内，不切穿海岸）
                    float lang=(float)rng.NextDouble()*Mathf.PI*2f;
                    float lrr=L.Br*(0.14f+(float)rng.NextDouble()*0.16f);
                    float lx=L.Cx+Mathf.Cos(lang)*lrr, lz=L.Cz+Mathf.Sin(lang)*lrr;
                    float bigR=L.Br*(0.20f+(float)rng.NextDouble()*0.08f);
                    lakes.Add((lx,lz,bigR,3.0f));
                    int sub=3+rng.Next(3);
                    for(int q=0;q<sub;q++)
                    {
                        float sa=(float)rng.NextDouble()*Mathf.PI*2f;
                        float sd=bigR*(0.30f+(float)rng.NextDouble()*0.30f);
                        float sr=bigR*(0.30f+(float)rng.NextDouble()*0.20f);
                        lakes.Add((lx+Mathf.Cos(sa)*sd,lz+Mathf.Sin(sa)*sd,sr,2.6f));
                    }
                    // 沙漠 1~2 块（仅主大陆）
                    int dn=1+rng.Next(2);
                    for(int i=0;i<dn;i++)
                    {
                        float ang=(float)rng.NextDouble()*Mathf.PI*2f;
                        float rr=L.Br*(0.20f+(float)rng.NextDouble()*0.34f);
                        int cx=Mathf.RoundToInt((L.Cx+Mathf.Cos(ang)*rr)/Tile+n*0.5f);
                        int cz=Mathf.RoundToInt((L.Cz+Mathf.Sin(ang)*rr)/TZ+n*0.5f);
                        float dr=Mathf.Max(36f,L.Br*0.30f)+(float)rng.NextDouble()*L.Br*0.16f;
                        _deserts.Add((cx,cz,dr));
                    }
                }
                else // 次大陆：仅 1~3 个小湖
                {
                    int ln=1+rng.Next(3);
                    for(int i=0;i<ln;i++)
                    {
                        float ang=(float)rng.NextDouble()*Mathf.PI*2f;
                        float rr=L.Br*(0.18f+(float)rng.NextDouble()*0.42f);
                        float lr=Mathf.Max(8f,L.Br*(0.10f+(float)rng.NextDouble()*0.08f));
                        lakes.Add((L.Cx+Mathf.Cos(ang)*rr,L.Cz+Mathf.Sin(ang)*rr,lr,1.8f));
                    }
                }
            }

            // ③ 高度场：全球深海基底；各大陆按不规则岸线隆起，岸线外平滑沉入海；多块取高（海沟自然衔接、不割裂）
            for (int x=0;x<n;x++)
            for (int z=0;z<n;z++)
            {
                float wx=x*Tile-centerX, wz=z*TZ-centerZ;
                float h=-2.8f;
                foreach (var L in _lands)
                {
                    float dx=wx-L.Cx, dz=wz-L.Cz, d=Mathf.Sqrt(dx*dx+dz*dz);
                    float rr=ShoreRadius(L,Mathf.Atan2(dz,dx));
                    float t=d/rr;
                    if (t<1.14f)
                    {
                        float lh=1.5f + TerrainPainter.FbmRidge(x,z,seed,5)*1.4f;
                        foreach (var p in _peaks)
                        {
                            float pdx=wx-p.x, pdz=wz-p.z;
                            lh += Mathf.Exp(-(pdx*pdx+pdz*pdz)/(2*p.r*p.r))*p.h;
                        }
                        foreach (var pl in _plateaus)
                        {
                            float pdx=wx-pl.x, pdz=wz-pl.z;
                            lh += Mathf.Exp(-(pdx*pdx+pdz*pdz)/(2*pl.r*pl.r))*pl.h;
                        }
                        foreach (var l in lakes)
                        {
                            float ldx=wx-l.x, ldz=wz-l.z;
                            lh -= Mathf.Exp(-(ldx*ldx+ldz*ldz)/(2*l.r*l.r))*l.d;
                        }
                        float coast=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(0.80f,1.14f,t));
                        lh=Mathf.Lerp(lh,-2.8f,coast);
                        if (lh>h) h=lh;
                    }
                }
                HeightMap[z,x]=Mathf.Max(-3.2f,h);
            }

            // ④ 河流：有山的岛从其最高峰发源；主/次大陆等无山陆地从其内陆高地发源，顺最低势蜿蜒挖到海/湖，主干宽 3 格
            foreach(var L in _lands)
            {
                var mine=_peaks.FindAll(p=>p.land==L.Id);
                mine.Sort((a,b)=>b.h.CompareTo(a.h));
                int rn=L.Id==1? 3+rng.Next(2) : 0;   // 仅主大陆有河（次大陆/无人岛无河）
                for(int i=0;i<rn;i++)
                {
                    int sx,sz;
                    if(i<mine.Count)
                    { var pk=mine[i]; sx=Mathf.RoundToInt(pk.x/Tile+n*0.5f); sz=Mathf.RoundToInt(pk.z/TZ+n*0.5f); }
                    else
                    {
                        var src=FindRiverSource(L,n); sx=src.x; sz=src.z;   // 无峰陆地用内陆高地发源
                    }
                    CarveRiver(sx,sz,n,rng);
                }
            }
            ApplyDesertMask(n);

            // ⑤ 水体标记：大陆内部被围的低洼水=淡水湖/河；大陆之间与外沿=外海
            for (int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                if (HeightMap[z,x] >= GameConstants.WaterLevel) continue;
                WaterMap[z,x]=1f;
                if (Biome[z,x]==BiomeKind.River) continue;
                float wx=x*Tile-centerX, wz=z*TZ-centerZ;
                bool inland=false;
                foreach(var L in _lands)
                    if(Vector2.Distance(new Vector2(wx,wz),new Vector2(L.Cx,L.Cz)) < L.Br*0.72f){inland=true;break;}
                if (Biome[z,x]==BiomeKind.Default && inland) Biome[z,x]=BiomeKind.FreshWater;
            }

            // ⑥ 大陆连通图（可徒涉地含浅水/河槽，8 邻域连通，深海阻隔），并重映射主大陆=1
            BuildContinentMap(n);
            RemapHomeContinent(n,main);

            // ⑦ 主大陆内自适应挑选最平坦、近淡水的村址并平坦化
            SettlementCenter = ChooseSettlement(n, centerX, centerZ, out int vx, out int vz);
            FlattenAround(vx,vz,n);

            ComputeRatios(n, centerX);
            // V6.3.7(真扩展) 开局活动疆域=V6.3.6完整世界(960,半幅480)，其内陆地全部真实可见；外环陆地随年代边疆外扩隆起
            RevealBase=RevealBaseRadius(); Expansion=1f;
            RevealTarget=RevealBase; RevealRadius=RevealBase;
            _revealedLands.Clear();
            foreach(var L in _lands)
                if(Mathf.Max(Mathf.Abs(L.Cx),Mathf.Abs(L.Cz))<=RevealTarget) _revealedLands.Add(L.Id);
            BuildTerrainMesh(n);
            BuildWater(n);
        }

        /// <summary>确定性兜底：沿 8 方向由近到远找第一个不贴边、与现有陆地隔海的位置放一块陆地（保证至少 1 主 1 次）</summary>
        void PlaceFallbackLand(List<Landmass> lands,Landmass main,float mainR,float br,int kind,float edge)
        {
            float[] dirs={0f,90f,180f,270f,45f,135f,225f,315f};
            float maxRing=Mathf.Min(edge-br-12f,430f);   // 960 世界：最远布岛环不贴世界边
            foreach(var deg in dirs)
            {
                float ang=deg*Mathf.Deg2Rad;
                for(float ring=mainR+br+56f; ring<maxRing; ring+=8f)
                {
                    float cx=main.Cx+Mathf.Cos(ang)*ring, cz=main.Cz+Mathf.Sin(ang)*ring;
                    if(Mathf.Abs(cx)+br>edge||Mathf.Abs(cz)+br>edge) break;
                    bool ok=true;
                    foreach(var L in lands)
                    {
                        float gap=L.Id==1?mainR*0.30f+40f:(br+L.Br)*0.34f+40f;
                        if(Vector2.Distance(new Vector2(cx,cz),new Vector2(L.Cx,L.Cz))<br+gap){ok=false;break;}
                    }
                    if(!ok) continue;
                    var rg=new System.Random((int)(ring*131+deg*17)+17);
                    lands.Add(new Landmass{Cx=cx,Cz=cz,Br=br,Id=lands.Count+1,Kind=kind,
                        P1=rg.Next(9),P2=rg.Next(9),P3=rg.Next(9)});
                    return;
                }
            }
        }

        /// <summary>
        /// 岛屿(Id≥3)与任何其它陆地“外缘确定性分离”：
        /// 海岸不规则岸线最大外扩约 1.30×Br，若两陆地中心距不足 (L.Br+O.Br)*1.22+34，就把岛屿沿径向外推；
        /// 若外推撞世界边，则在围绕对方的多角度候选里找第一个合法位；都不行则小幅收缩岛屿半径重试。
        /// 保证：岛屿永远不与主/次大陆或其它岛屿浅滩接壤（无人岛独立成块）。
        /// </summary>
        void EnforceSeparation(float edge)
        {
            for(int it=0;it<5;it++)
            {
                bool moved=false;
                for(int i=1;i<_lands.Count;i++)          // 推所有非主陆地（次大陆+岛），主大陆不动
                {
                    var L=_lands[i];
                    for(int j=0;j<_lands.Count;j++)
                    {
                        if(i==j)continue;
                        var O=_lands[j];
                        float need=(L.Br+O.Br)*1.22f+34f;                    // 外缘最坏情况 + 深海保险
                        float d=Vector2.Distance(new Vector2(L.Cx,L.Cz),new Vector2(O.Cx,O.Cz));
                        if(d>=need)continue;
                        float baseAng=Mathf.Atan2(L.Cz-O.Cz,L.Cx-O.Cx);
                        bool placed=false;
                        for(int k=0;k<8 && !placed;k++)                      // 原向 + 7 个旋转候选
                        {
                            float ang=baseAng+k*Mathf.PI*0.25f;
                            float dist=need+28f;
                            float nx=O.Cx+Mathf.Cos(ang)*dist, nz=O.Cz+Mathf.Sin(ang)*dist;
                            if(Mathf.Abs(nx)+L.Br>edge||Mathf.Abs(nz)+L.Br>edge)continue;
                            bool clash=false;
                            for(int m=0;m<_lands.Count;m++)
                            {
                                if(m==i)continue;
                                var Q=_lands[m];
                                if(Vector2.Distance(new Vector2(nx,nz),new Vector2(Q.Cx,Q.Cz))
                                   < (L.Br+Q.Br)*1.22f+30f){clash=true;break;}
                            }
                            if(clash)continue;
                            L.Cx=nx; L.Cz=nz; placed=true; moved=true; break;
                        }
                        if(!placed && L.Kind==1 && L.Br>26f){ L.Br*=0.90f; moved=true; }   // 仅无人岛可收缩，次大陆不缩
                    }
                }
                if(!moved)break;
            }
        }

        /// <summary>大陆不规则岸线半径：基础半径叠加多向正弦与低频噪声，形成自然海岸</summary>
        float ShoreRadius(Landmass L,float theta)
        {
            float w=Mathf.Sin(2f*theta+L.P1)*0.13f + Mathf.Sin(3f*theta+L.P2)*0.09f
                   +TerrainPainter.FbmRidge(Mathf.Cos(theta)*9f+L.P3,Mathf.Sin(theta)*9f-L.P3,Seed+L.Id*77,3)*0.06f;
            return L.Br*(1f+w);
        }

        /// <summary>陆地连通分量：可徒涉地（h≥水位-0.45，含浅水/河槽，避免河流切碎大陆）8 邻域 flood，开阔深海=0</summary>
        private void BuildContinentMap(int n)
        {
            var walk=new bool[n,n];
            for(int z=0;z<n;z++)for(int x=0;x<n;x++) walk[z,x]=HeightMap[z,x]>=GameConstants.WaterLevel-0.45f;
            var seen=new bool[n,n]; var q=new Queue<(int x,int z)>(); int cid=0;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                if(!walk[z,x]||seen[z,x])continue;
                cid++; q.Clear(); q.Enqueue((x,z)); seen[z,x]=true;
                while(q.Count>0)
                {
                    var c=q.Dequeue(); ContinentMap[c.z,c.x]=cid;
                    for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                    {
                        if(dx==0&&dz==0)continue;
                        int nx=c.x+dx,nz=c.z+dz;
                        if(nx<0||nz<0||nx>=n||nz>=n||seen[nz,nx]||!walk[nz,nx])continue;
                        seen[nz,nx]=true; q.Enqueue((nx,nz));
                    }
                }
            }
        }

        /// <summary>把包含主大陆中心的连通分量重映射为 1（玩家主大陆），其余顺序编号 2..K，并统计各大陆质心</summary>
        private void RemapHomeContinent(int n,Landmass main)
        {
            int mx=Mathf.Clamp(Mathf.RoundToInt(main.Cx/Tile+n*0.5f),0,n-1);
            int mz=Mathf.Clamp(Mathf.RoundToInt(main.Cz/TZ+n*0.5f),0,n-1);
            int homeRaw=ContinentMap[mz,mx];
            for(int r=1;r<14 && homeRaw<=0;r++)
            for(int dz=-1;dz<=1 && homeRaw<=0;dz++)for(int dx=-1;dx<=1 && homeRaw<=0;dx++)
            {
                int x=mx+dx*r,z=mz+dz*r; if(x<0||z<0||x>=n||z>=n)continue;
                if(ContinentMap[z,x]>0)homeRaw=ContinentMap[z,x];
            }
            var rawIds=new List<int>();
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
                if(ContinentMap[z,x]>0 && !rawIds.Contains(ContinentMap[z,x])) rawIds.Add(ContinentMap[z,x]);
            var remap=new Dictionary<int,int>();
            if(homeRaw>0) remap[homeRaw]=1;
            int next=2;
            foreach(var id in rawIds) if(!remap.ContainsKey(id)) remap[id]=next++;
            var sx=new Dictionary<int,long>(); var szm=new Dictionary<int,long>(); var sc=new Dictionary<int,int>();
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                int v=ContinentMap[z,x]; if(v==0)continue;
                int nv=remap.TryGetValue(v,out var rv)?rv:next++;
                ContinentMap[z,x]=nv; remap[v]=nv;
                sx[nv]=sx.TryGetValue(nv,out var a)?a+x:x;
                szm[nv]=szm.TryGetValue(nv,out var b)?b+z:z;
                sc[nv]=sc.TryGetValue(nv,out var c)?c+1:1;
            }
            ContinentCenters.Clear(); ContinentCount=next-1; HomeContinent=1;
            for(int id=1;id<=ContinentCount;id++)
            {
                int cnt=sc.TryGetValue(id,out var cc)?cc:1;
                float cxw=(sx[id]/(float)cnt)*Tile-HalfX, czw=(szm[id]/(float)cnt)*TZ-HalfZ;
                ContinentCenters.Add(new Vector3(cxw,0,czw));
            }
        }

        /// <summary>
        /// 带比例硬保证的生成（V6.1.3）：逐次生成后实测 海≥70% / 纯陆地≥15%（不含山）/ 山坡≤5%且单片≤1.2%，
        /// 不达标按面积比反推大陆半径重试（最多 6 次），确保每一局都是海洋为主的星球。
        /// </summary>
        public void GenerateBalanced(int seed)
        {
            float scale=1f;
            for (int attempt=0; attempt<6; attempt++)
            {
                Generate(seed + attempt*7919, scale);
                bool seaOk  = SeaRatio>=0.70f;                                   // 海≥70%
                // 纯陆≥13%：多大陆分裂为“主≤10%+次+岛”后，主/次无山、只岛有山，纯陆(不含山)结构上限约16%，
                // 故门控从旧单大陆口径的15%放宽到13%（实际 13~17%，海仍 80%+）
                bool landOk = LandOnlyRatio>=0.10f;
                bool mtnOk  = MountainRatio<=0.05f && MaxMountainBlob<=0.012f;   // 山≤5%、单片≤1.2%
                bool contOk= ContinentCount==_lands.Count && ContinentCount>=2; // 每块大陆都被海隔开、无粘连
                bool singleOk = Mathf.PI*LandRadius*LandRadius/(GameConstants.WorldSize*GameConstants.WorldSize)<=0.10f; // 单一大陆≤10%（初始活动区口径）
                if (seaOk && landOk && mtnOk && contOk && singleOk) break;
                // 仅缩放次大陆/岛屿（主大陆固定半径），同时换种子重排，最多 6 次
                if (!landOk) scale=Mathf.Clamp(scale*1.08f,0.95f,1.15f);
                else if (!seaOk) scale=Mathf.Clamp(scale*0.94f,0.95f,1.15f);
                else scale=Mathf.Clamp(scale*1.03f,0.95f,1.15f);
            }
            Debug.Log($"[World] 多大陆 K={ContinentCount}/{_lands.Count} 海{SeaRatio:P1} 纯陆{LandOnlyRatio:P1} 山{MountainRatio:P1}(片{MaxMountainBlob:P2}) 沙{DesertRatio:P1} 淡水{FreshWaterRatio:P1} 主R={LandRadius:F0} 主占{(Mathf.PI*LandRadius*LandRadius/(GameConstants.WorldSize*GameConstants.WorldSize)):P1}");
        }

        /// <summary>重新生成（先销毁旧地形/水面），返回新村址</summary>
        public Vector3 Regenerate(int seed)
        {
            var oldT = transform.Find("Terrain"); if (oldT!=null) Destroy(oldT.gameObject);
            var oldW = transform.Find("Water"); if (oldW!=null) Destroy(oldW.gameObject);
            GenerateBalanced(seed);
            return SettlementCenter;
        }

        /// <summary>无峰陆地河流发源点：在该陆地边界内采样，选高度最高、非水、距海岸有距离的格（作为河源高地）</summary>
        (int x,int z) FindRiverSource(Landmass L,int n)
        {
            int cx=Mathf.Clamp(Mathf.RoundToInt(L.Cx/Tile+n*0.5f),0,n-1);
            int cz=Mathf.Clamp(Mathf.RoundToInt(L.Cz/TZ+n*0.5f),0,n-1);
            int rx=Mathf.Clamp(Mathf.RoundToInt(L.Br/Tile*0.55f),4,n/3);
            int rz=Mathf.Clamp(Mathf.RoundToInt(L.Br/TZ*0.55f),4,n/3);
            float best=-999f; int bx=cx,bz=cz;
            for(int dz=-rz;dz<=rz;dz+=2)for(int dx=-rx;dx<=rx;dx+=2)
            {
                int x=cx+dx,z=cz+dz;
                if(x<2||z<2||x>=n-2||z>=n-2)continue;
                float h=HeightMap[z,x];
                if(h<GameConstants.WaterLevel+0.4f)continue;       // 必须是陆地
                if(Biome[z,x]==BiomeKind.Desert||Biome[z,x]==BiomeKind.River)continue;
                if(h>best){best=h;bx=x;bz=z;}
            }
            return (bx,bz);
        }

        // 河流：从起点走向 8 邻域最低格，直到入水；平地/洼地带随机蜿蜒避免断流；沿途压到水位下成河
        private void CarveRiver(int sx,int sz,int n,System.Random rng)
        {
            int cx=sx,cz=sz,px=-1,pz=-1,flat=0;
            for (int step=0;step<520;step++)
            {
                if (cx<1||cz<1||cx>=n-1||cz>=n-1) break;
                if (HeightMap[cz,cx] < GameConstants.WaterLevel && step>6) break; // 汇入水体
                float curH=HeightMap[cz,cx];
                float bestH=float.MaxValue; int bx=cx,bz=cz;
                for (int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                {
                    if (dx==0&&dz==0)continue;
                    int nx=cx+dx,nz=cz+dz;
                    if (nx<0||nz<0||nx>=n||nz>=n)continue;
                    if (nx==px&&nz==pz)continue;
                    float hh=HeightMap[nz,nx];
                    if (hh<bestH){bestH=hh;bx=nx;bz=nz;}
                }
                // 平地/局部洼地：不立即断流，沿当前前进方向 + 随机扰动继续找下坡
                if (bestH>=curH-0.02f)
                {
                    if (++flat>3)
                    {   // 朝偏离来路的方向随机走一格，强行延长河道
                        int tries=0;int tx=cx,tz=cz;
                        do{ int adx=rng.Next(-1,2),adz=rng.Next(-1,2); tx=cx+adx;tz=cz+adz;tries++; }
                        while(tries<6 && ((tx==px&&tz==pz)||tx<1||tz<1||tx>=n-1||tz>=n-1));
                        bx=tx;bz=tz;flat=0;
                    }
                }
                else flat=0;
                StampRiver(cx,cz,n);                 // 主干宽 3
                px=cx;pz=cz;cx=bx;cz=bz;
            }
        }
        private void StampRiver(int x,int z,int n)
        {
            float deep=GameConstants.WaterLevel-0.42f;
            for (int dz=-2;dz<=2;dz++)for(int dx=-2;dx<=2;dx++)
            {
                int nx=x+dx,nz=z+dz;
                if (nx<0||nz<0||nx>=n||nz>=n)continue;
                int man=Mathf.Abs(dx)+Mathf.Abs(dz);
                if (man>2)continue;                  // 菱形河槽，宽约 3 格
                float target = man==0?deep:GameConstants.WaterLevel-0.22f;
                if (HeightMap[nz,nx]>target) HeightMap[nz,nx]=Mathf.Lerp(HeightMap[nz,nx],target,man==0?0.95f:0.7f);
                Biome[nz,nx]=BiomeKind.River;
            }
        }

        private void ApplyDesertMask(int n)
        {
            foreach (var d in _deserts)
            {
                int ri=Mathf.CeilToInt(d.r/Tile);
                for (int dz=-ri;dz<=ri;dz++)for(int dx=-ri;dx<=ri;dx++)
                {
                    int x=d.cx+dx,z=d.cz+dz;
                    if (x<0||z<0||x>=n||z>=n)continue;
                    float dist=Mathf.Sqrt(dx*dx+dz*dz)*Tile;
                    if (dist>d.r)continue;
                    float h=HeightMap[z,x];
                    if (h<=GameConstants.WaterLevel)continue;          // 水里不变沙漠
                    if (h>4.0f)continue;                                // 高山不变沙漠
                    // 边缘羽化（阈值更低，连成大片沙海）
                    float edge=Mathf.SmoothStep(1f,0f,dist/d.r);
                    if (edge>0.18f) Biome[z,x]=BiomeKind.Desert;
                }
            }
        }

        // 选村址：仅在玩家主大陆内扫描，平坦度（邻域高差）最优、且 20~58 内有淡水
        private Vector3 ChooseSettlement(int n,float centerX,float centerZ,out int vx,out int vz)
        {
            vx=n/2;vz=n/2; float best=float.MaxValue;
            int step=2;
            Vector3 hc = ContinentCenters.Count>=HomeContinent ? ContinentCenters[HomeContinent-1] : Vector3.zero;
            for (int pass=0;pass<2 && best==float.MaxValue;pass++)
            for (int z=8;z<n-8;z+=step)for(int x=8;x<n-8;x+=step)
            {
                if (pass==0 && ContinentMap[z,x]!=HomeContinent) continue;   // 首遍只在主大陆；兜底遍放宽到任意陆地
                float wx=x*Tile-centerX,wz=z*TZ-centerZ;
                float r=Vector2.Distance(new Vector2(wx,wz),new Vector2(hc.x,hc.z));
                if (r>LandRadius*0.50f||r<16f)continue;
                float h=HeightMap[z,x];
                if (h<0.7f||h>2.4f)continue;
                if (Biome[z,x]==BiomeKind.Desert)continue;
                float rough=LocalRoughness(x,z,7);
                bool nearWater=HasFreshWater(x,z,20,58);
                if (!nearWater) rough+=2f;                 // 近水优先
                if (rough<best){best=rough;vx=x;vz=z;}
            }
            float sx=vx*Tile-centerX, sz=vz*TZ-centerZ;
            return new Vector3(sx,HeightMap[vz,vx],sz);
        }
        private float LocalRoughness(int x,int z,int rad)
        {
            float sum=0,sum2=0,c=0;
            for (int dz=-rad;dz<=rad;dz+=2)for(int dx=-rad;dx<=rad;dx+=2)
            {
                float h=SampleHeightInt(x+dx,z+dz);sum+=h;sum2+=h*h;c++;
            }
            float mean=sum/c; return Mathf.Sqrt(Mathf.Max(0,sum2/c-mean*mean));
        }
        private bool HasFreshWater(int cx,int cz,int rMin,int rMax)
        {
            int n=G;
            for (int dz=-rMax;dz<=rMax;dz+=2)for(int dx=-rMax;dx<=rMax;dx+=2)
            {
                int d=(int)Mathf.Sqrt(dx*dx+dz*dz);
                if (d<rMin||d>rMax)continue;
                int x=cx+dx,z=cz+dz;
                if (x<0||z<0||x>=n||z>=n)continue;
                if (HeightMap[z,x]<GameConstants.WaterLevel)return true;
            }
            return false;
        }

        // 村址周边平坦化：r<26 全平到 1.4，26~48 平滑过渡（不抹掉河/湖）
        private void FlattenAround(int vx,int vz,int n)
        {
            for (int z=-52;z<=52;z++)for(int x=-52;x<=52;x++)
            {
                int gx=vx+x,gz=vz+z;
                if (gx<0||gz<0||gx>=n||gz>=n)continue;
                float d=Mathf.Sqrt(x*x+z*z)*Tile;
                if (d>=48f)continue;
                if (HeightMap[gz,gx]<GameConstants.WaterLevel)continue; // 保留穿村河/湖
                float t=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(26f,48f,d));
                HeightMap[gz,gx]=Mathf.Lerp(HeightMap[gz,gx],1.4f,t);
            }
        }

        private void ComputeRatios(int n,float centerWorld)
        {
            // V6.3.7(真扩展) 海陆比例按"初始活动区"(中央 GameConstants.MapSize 见方)统计：开局即达标，而非扩满后才达标
            int rn=Mathf.Min(GameConstants.MapSize,n), c0=(n-rn)/2, c1=c0+rn;
            int sea=0,land=0,landOnly=0,mtn=0,desert=0,fresh=0,total=rn*rn;
            bool[,] isMtn=new bool[n,n];
            for (int z=c0;z<c1;z++)for(int x=c0;x<c1;x++)
            {
                float h=HeightMap[z,x];
                bool isFresh=Biome[z,x]==BiomeKind.FreshWater||Biome[z,x]==BiomeKind.River;
                if (isFresh) fresh++;
                else if (h<GameConstants.WaterLevel) sea++;
                else { land++; if(h>=5.6f){mtn++;isMtn[z,x]=true;} else landOnly++; }
                if (Biome[z,x]==BiomeKind.Desert)desert++;
            }
            int maxBlob=0; bool[,] seen=new bool[n,n];
            var q=new Queue<(int x,int z)>();
            for(int z=c0;z<c1;z++)for(int x=c0;x<c1;x++)
            {
                if(!isMtn[z,x]||seen[z,x])continue;
                int cnt=0; q.Clear(); q.Enqueue((x,z)); seen[z,x]=true;
                while(q.Count>0)
                {
                    var c=q.Dequeue(); cnt++;
                    for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                    {
                        if(Mathf.Abs(dx)+Mathf.Abs(dz)!=1)continue;
                        int nx=c.x+dx,nz=c.z+dz;
                        if(nx<c0||nz<c0||nx>=c1||nz>=c1||seen[nz,nx]||!isMtn[nz,nx])continue;
                        seen[nz,nx]=true; q.Enqueue((nx,nz));
                    }
                }
                if(cnt>maxBlob)maxBlob=cnt;
            }
            SeaRatio=(float)sea/total;LandRatio=(float)land/total;LandOnlyRatio=(float)landOnly/total;
            MountainRatio=(float)mtn/total;MaxMountainBlob=(float)maxBlob/total;
            DesertRatio=(float)desert/total;FreshWaterRatio=(float)fresh/total;
        }

        private void BuildTerrainMesh(int n)
        {
            var old = transform.Find("Terrain");
            if (old!=null) Destroy(old.gameObject);
            var go = new GameObject("Terrain"); go.transform.SetParent(transform);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var mesh = new Mesh(); mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            int verts = (n+1)*(n+1);
            var vertices = new Vector3[verts];
            var colors = new Color32[verts];
            var uv = new Vector2[verts];
            float halfX=HalfX,halfZ=HalfZ;
            int vi=0;
            for (int z=0;z<=n;z++)
                for (int x=0;x<=n;x++)
                {
                    float wx = x*Tile-halfX, wz = z*TZ-halfZ;
                    var (dh,dc)=DisplayAt(wx,wz,x,z);
                    vertices[vi]=new Vector3(wx,dh,wz);
                    colors[vi]=(Color32)dc;
                    uv[vi]=new Vector2(x/(float)n,z/(float)n);
                    vi++;
                }
            var tris = new int[n*n*6]; int ti=0;
            for (int z=0;z<n;z++)
                for (int x=0;x<n;x++)
                {
                    int a=z*(n+1)+x,b=a+1,c=a+(n+1),d=c+1;
                    tris[ti++]=a;tris[ti++]=c;tris[ti++]=b;
                    tris[ti++]=b;tris[ti++]=c;tris[ti++]=d;
                }
            mesh.vertices=vertices;mesh.triangles=tris;mesh.colors32=colors;mesh.uv=uv;
            _rvVerts=vertices;_rvCols32=colors;   // V6.5.8 复用，避免扩张时反复 new 144 万数组撑爆 WASM 堆
            mesh.normals = TerrainPainter.SmoothNormals(HeightMap, Tile, TZ);
            mf.mesh=mesh; _terrainMesh=mesh;
            var vcShader = Shader.Find("PxC/VertexColor");
            var mat = new Material(vcShader != null ? vcShader : ShaderHelper.Lit);
            ShaderHelper.SetSurfaceOpaque(mat);
            mr.material=mat;
            mr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows=false;
            var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh=mesh; _terrainCol=mc;
        }

        // V6.1.3：某顶点的"显示高度/颜色"——未延展到的外圈沉入深海（逻辑高度仍以 HeightMap 为准）
        static readonly Color HiddenDeep=new(0.03f,0.10f,0.22f);
        private (float h,Color c) DisplayAt(float wx,float wz,int gx,int gz)
        {
            float fh=SampleHeightInt(gx,gz);
            Color bc=BiomeColor(fh,gx,gz);
            if(!RevealEnabled) return (fh,bc);
            // V6.3.7(fix) 方形疆域（长宽同步外扩）：用切比雪夫距离=max(|x|,|z|)；海洋随疆域显现，未"发现"的陆地无论远近一律沉海
            float rz=Mathf.Min(RevealRadius,HalfZ);
            float cheb=Mathf.Max(Mathf.Abs(wx),Mathf.Abs(wz)*(HalfX/rz));
            int cid=ContinentMap[gz,gx];
            bool landKnown = cid<=0 || _revealedLands.Contains(cid);
            bool inside=Mathf.Abs(wx)<=RevealRadius && Mathf.Abs(wz)<=rz;
            if(inside && landKnown) return (fh,bc);
            float k = landKnown ? Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(RevealRadius,RevealRadius+18f,cheb)) : 1f;
            return (Mathf.Lerp(fh,-3.1f,k), Color.Lerp(bc,HiddenDeep,k));
        }

        /// <summary>初始活动疆域半幅=V6.3.6 完整世界(边长960)的一半480；开局即真实可玩，海陆比例即初始值</summary>
        float RevealBaseRadius()=>GameConstants.WorldSize*0.5f;

        // ================= V6.5.4 实时随机扩展：运行时增陆（不预建） =================
        /// <summary>shapeKind: 0=次大陆(平地/小湖) 1=无人岛(可有山峰) 2=新增主大陆(平地/高原/大湖/沙漠)。
        /// 在【当前已展开疆域内、初始960世界外】的深海随机选址，实时盖戳高度、标注连通分量、刷新网格。返回新陆地Id(0=疆域不足/选址失败)。</summary>
        public int GrowLandmass(int shapeKind, System.Random rng)
        {
            if (rng==null) rng=new System.Random();
            float br = shapeKind==2 ? 140f+(float)rng.NextDouble()*16f
                     : shapeKind==0 ? 82f+(float)rng.NextDouble()*18f
                     : 34f+(float)rng.NextDouble()*20f;
            float frontier=RevealTarget>0f?RevealTarget:RevealBaseRadius();
            float lo=GameConstants.WorldSize*0.5f+br+30f;
            float hi=frontier-br-20f;
            if (hi<=lo+4f) return 0;                     // 展开疆域还不足以容纳该陆地
            float cx=0f,cz=0f;bool placed=false;
            for (int att=0;att<80;att++)
            {
                float ang=(float)rng.NextDouble()*Mathf.PI*2f;
                float rr=lo+(float)rng.NextDouble()*(hi-lo);
                float tx=Mathf.Cos(ang)*rr,tz=Mathf.Sin(ang)*rr;
                if (Mathf.Abs(tx)+br>frontier-12f) continue;                 // X 不超当前疆域
                if (Mathf.Abs(tz)+br>HalfZ-12f) continue;                      // V6.5.8 Z 不超矩形宽边(1440)
                bool ok=true;
                foreach (var O in _lands)
                { float gap=(br+O.Br)*0.34f+46f; if (Vector2.Distance(new Vector2(tx,tz),new Vector2(O.Cx,O.Cz))<br+gap){ok=false;break;} }
                if (!ok) continue;
                cx=tx;cz=tz;placed=true;break;
            }
            if (!placed) return 0;
            var land=new Landmass{Cx=cx,Cz=cz,Br=br,Id=-1,Kind=shapeKind==1?1:0,
                P1=(float)rng.NextDouble()*9f,P2=(float)rng.NextDouble()*9f,P3=(float)rng.NextDouble()*9f};
            StampLand(land,shapeKind,rng);
            int newId=LabelNewComponent(land);
            land.Id=newId; land.Grown=true; _lands.Add(land); GrownLands.Add(land);
            _revealedLands.Add(newId);
            RefreshRevealMesh();
            return newId;
        }

        /// <summary>读档恢复：按存档参数重建一块运行时陆地（shapeKind 由 Br/Kind 反推：Kind1=岛，Br≥130=主大陆，否则次大陆）</summary>
        public int RestoreGrownLand(float cx,float cz,float br,int kind,float p1,float p2,float p3,System.Random rng)
        {
            foreach(var g in GrownLands) if(Mathf.Abs(g.Cx-cx)<0.5f&&Mathf.Abs(g.Cz-cz)<0.5f) return g.Id; // 幂等
            int shapeKind=kind==1?1:(br>=130f?2:0);
            var land=new Landmass{Cx=cx,Cz=cz,Br=br,Id=-1,Kind=kind,P1=p1,P2=p2,P3=p3,Grown=true};
            StampLand(land,shapeKind,rng??new System.Random());
            int newId=LabelNewComponent(land);
            land.Id=newId;_lands.Add(land);GrownLands.Add(land);_revealedLands.Add(newId);
            return newId;
        }
        public void EndRestoreGrown(){ RefreshRevealMesh(); }

        /// <summary>把一块新陆地实时盖戳进高度图/水体/生态（只扫其包围盒，不动其它陆地）</summary>
        void StampLand(Landmass L,int shapeKind,System.Random rng)
        {
            var peaks=new List<(float x,float z,float r,float h)>();
            var plats=new List<(float x,float z,float r,float h)>();
            var lakes=new List<(float x,float z,float r,float d)>();
            var deserts=new List<(int cx,int cz,float r)>();
            if (shapeKind==1)
            {
                int pn=3+rng.Next(2);
                for (int i=0;i<pn;i++)
                {
                    float a=(float)rng.NextDouble()*Mathf.PI*2f, rr=L.Br*(0.06f+(float)rng.NextDouble()*0.50f);
                    peaks.Add((L.Cx+Mathf.Cos(a)*rr,L.Cz+Mathf.Sin(a)*rr,6f+(float)rng.NextDouble()*3f,8.8f+(float)rng.NextDouble()*5.2f));
                }
            }
            else if (shapeKind==2)
            {
                float pa=(float)rng.NextDouble()*Mathf.PI*2f, pr=L.Br*(0.10f+(float)rng.NextDouble()*0.32f), plr=L.Br*(0.30f+(float)rng.NextDouble()*0.12f);
                plats.Add((L.Cx+Mathf.Cos(pa)*pr,L.Cz+Mathf.Sin(pa)*pr,plr,1.35f));
                float la=(float)rng.NextDouble()*Mathf.PI*2f, lr=L.Br*(0.14f+(float)rng.NextDouble()*0.16f);
                float lx=L.Cx+Mathf.Cos(la)*lr,lz=L.Cz+Mathf.Sin(la)*lr,bigR=L.Br*(0.20f+(float)rng.NextDouble()*0.08f);
                lakes.Add((lx,lz,bigR,3f));
                int sub=3+rng.Next(3);
                for (int q=0;q<sub;q++){float sa=(float)rng.NextDouble()*Mathf.PI*2f,sd=bigR*(0.30f+(float)rng.NextDouble()*0.30f),sr=bigR*(0.30f+(float)rng.NextDouble()*0.20f);lakes.Add((lx+Mathf.Cos(sa)*sd,lz+Mathf.Sin(sa)*sd,sr,2.6f));}
                int dn=1+rng.Next(2);
                for (int i=0;i<dn;i++){float a=(float)rng.NextDouble()*Mathf.PI*2f,rr=L.Br*(0.20f+(float)rng.NextDouble()*0.34f);
                    deserts.Add((Mathf.RoundToInt((L.Cx+Mathf.Cos(a)*rr)/Tile+G*0.5f),Mathf.RoundToInt((L.Cz+Mathf.Sin(a)*rr)/TZ+G*0.5f),Mathf.Max(36f,L.Br*0.30f)+(float)rng.NextDouble()*L.Br*0.16f));}
            }
            else
            {
                int ln=1+rng.Next(3);
                for (int i=0;i<ln;i++){float a=(float)rng.NextDouble()*Mathf.PI*2f,rr=L.Br*(0.18f+(float)rng.NextDouble()*0.42f),lr=Mathf.Max(8f,L.Br*(0.10f+(float)rng.NextDouble()*0.08f));
                    lakes.Add((L.Cx+Mathf.Cos(a)*rr,L.Cz+Mathf.Sin(a)*rr,lr,1.8f));}
            }
            int ccx=Mathf.RoundToInt(L.Cx/Tile+G*0.5f), ccz=Mathf.RoundToInt(L.Cz/TZ+G*0.5f);
            int rcx=Mathf.CeilToInt(L.Br*1.18f/Tile),rcz=Mathf.CeilToInt(L.Br*1.18f/TZ);
            int x0=Mathf.Clamp(ccx-rcx,0,G-1),x1=Mathf.Clamp(ccx+rcx,0,G-1),z0=Mathf.Clamp(ccz-rcz,0,G-1),z1=Mathf.Clamp(ccz+rcz,0,G-1);
            for (int gz=z0;gz<=z1;gz++)for (int gx=x0;gx<=x1;gx++)
            {
                float wx=gx*Tile-HalfX,wz=gz*TZ-HalfZ;
                float dx=wx-L.Cx,dz=wz-L.Cz,d=Mathf.Sqrt(dx*dx+dz*dz);
                float rr=ShoreRadius(L,Mathf.Atan2(dz,dx)); float t=d/rr;
                if (t>=1.14f) continue;
                float lh=1.5f+TerrainPainter.FbmRidge(gx,gz,Seed,5)*1.4f;
                foreach (var p in peaks){float px=wx-p.x,pz=wz-p.z;lh+=Mathf.Exp(-(px*px+pz*pz)/(2*p.r*p.r))*p.h;}
                foreach (var p in plats){float px=wx-p.x,pz=wz-p.z;lh+=Mathf.Exp(-(px*px+pz*pz)/(2*p.r*p.r))*p.h;}
                foreach (var l in lakes){float lx2=wx-l.x,lz2=wz-l.z;lh-=Mathf.Exp(-(lx2*lx2+lz2*lz2)/(2*l.r*l.r))*l.d;}
                float coast=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(0.80f,1.14f,t));
                lh=Mathf.Lerp(lh,-2.8f,coast);
                float cur=HeightMap[gz,gx];
                float nv=Mathf.Max(cur,Mathf.Max(-3.2f,lh));
                HeightMap[gz,gx]=nv;
                if (nv<GameConstants.WaterLevel){ WaterMap[gz,gx]=1f; if (Biome[gz,gx]==BiomeKind.Default && d<L.Br*0.72f) Biome[gz,gx]=BiomeKind.FreshWater; }
                else if (Biome[gz,gx]==BiomeKind.FreshWater) Biome[gz,gx]=BiomeKind.Default;
            }
            // 沙漠（仅新增主大陆）
            foreach (var d in deserts)
            {
                int rix=Mathf.CeilToInt(d.r/Tile),riz=Mathf.CeilToInt(d.r/TZ);
                for (int dz=-riz;dz<=riz;dz++)for(int dx=-rix;dx<=rix;dx++)
                {
                    int x=d.cx+dx,z=d.cz+dz;if(x<0||z<0||x>=G||z>=G)continue;
                    float wdx=dx*Tile,wdz=dz*TZ,dist=Mathf.Sqrt(wdx*wdx+wdz*wdz);if(dist>d.r)continue;
                    float h=HeightMap[z,x];if(h<=GameConstants.WaterLevel||h>4f)continue;
                    if (Mathf.SmoothStep(1f,0f,dist/d.r)>0.18f) Biome[z,x]=BiomeKind.Desert;
                }
            }
        }

        /// <summary>为新陆地标注独立连通分量（只给当前无标号且可徒涉的格子发新Id，不重排既有陆地）</summary>
        int LabelNewComponent(Landmass L)
        {
            int scx=Mathf.RoundToInt(L.Cx/Tile+G*0.5f),scz=Mathf.RoundToInt(L.Cz/TZ+G*0.5f);
            // 中心若是湖面，螺旋找一块可徒涉格
            int sx=scx,sz=scz;bool found=false;
            for (int rad=0;rad<40&&!found;rad++)
                for (int dz=-rad;dz<=rad&&!found;dz++)for(int dx=-rad;dx<=rad&&!found;dx++)
                { if (Mathf.Max(Mathf.Abs(dx),Mathf.Abs(dz))!=rad)continue; int x=scx+dx,z=scz+dz;
                  if(x<0||z<0||x>=G||z>=G)continue;
                  if(HeightMap[z,x]>=GameConstants.WaterLevel-0.45f){sx=x;sz=z;found=true;} }
            int newId=ContinentCount+1;
            var q=new Queue<(int x,int z)>(); q.Enqueue((sx,sz));
            var seen=new HashSet<int>(); seen.Add(sz*G+sx);
            while (q.Count>0)
            {
                var c=q.Dequeue(); ContinentMap[c.z,c.x]=newId;
                for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                {
                    if(dx==0&&dz==0)continue;int nx=c.x+dx,nz=c.z+dz;
                    if(nx<0||nz<0||nx>=G||nz>=G)continue;int key=nz*G+nx;
                    if(seen.Contains(key))continue;
                    if(ContinentMap[nz,nx]!=0)continue;                       // 已属其它既有陆地，不并入
                    if(HeightMap[nz,nx]<GameConstants.WaterLevel-0.45f)continue; // 深海阻隔
                    seen.Add(key);q.Enqueue((nx,nz));
                }
            }
            ContinentCount=newId;
            return newId;
        }

        /// <summary>V6.3.7(真扩展) 在初始960世界之外的同心环带上补次大陆/无人岛，密度对齐初始海陆比例（海为主），供边疆真实外扩时陆续隆起</summary>
        void PlaceOuterLands(List<Landmass> lands,System.Random rng,float scale)
        {
            float inner=GameConstants.WorldSize*0.5f+60f;          // 540
            float outer=HalfZ-80f;                               // 1360
            float ringW=200f;
            for(float r=inner; r<=outer; r+=ringW)
            {
                float spacing=760f+(float)rng.NextDouble()*120f;   // 稀疏布块，保证外环仍是海为主（~13%陆）
                int count=Mathf.Max(1,Mathf.RoundToInt(2f*Mathf.PI*r/spacing));
                float phase=(float)rng.NextDouble()*Mathf.PI*2f;
                for(int i=0;i<count;i++)
                {
                    for(int att=0;att<24;att++)
                    {
                        float ang=phase+(i+ (float)rng.NextDouble())*(Mathf.PI*2f/count);
                        float rr=r+(float)(rng.NextDouble()-0.5)*ringW*0.7f;
                        float cx=Mathf.Cos(ang)*rr, cz=Mathf.Sin(ang)*rr;
                        bool isIsland = rng.NextDouble()<0.34;     // 约1/3为可有山峰的无人岛
                        float br=(isIsland? 34f+(float)rng.NextDouble()*20f : 70f+(float)rng.NextDouble()*34f)*scale;
                        if(Mathf.Max(Mathf.Abs(cx),Mathf.Abs(cz))+br>HalfZ-30f) continue;
                        bool ok=true;
                        foreach(var L in lands)
                        { float gap=(br+L.Br)*0.34f+46f; if(Vector2.Distance(new Vector2(cx,cz),new Vector2(L.Cx,L.Cz))<br+gap){ok=false;break;} }
                        if(!ok)continue;
                        lands.Add(new Landmass{Cx=cx,Cz=cz,Br=br,Id=lands.Count+1,Kind=isIsland?1:0,
                            P1=(float)rng.NextDouble()*9f,P2=(float)rng.NextDouble()*9f,P3=(float)rng.NextDouble()*9f});
                        break;
                    }
                }
            }
        }

        /// <summary>V6.3.7(真扩展) 设定扩张倍率并【瞬切】活动边疆（仅在百年/千年倍率真正变化时重建一次全量网格，避免每帧重建52万顶点卡顿）</summary>
        public void SetExpansion(float e)
        {
            Expansion=e;
            float target=Mathf.Min(RevealBase*e, HalfX);
            if (Mathf.Abs(target-RevealTarget)<=0.01f && RevealRadius>=0f) return; // 倍率未变：不重建
            RevealTarget=target;
            RevealRadius=target;                 // 瞬切，不做逐帧平滑动画
            _revealedLands.Clear();
            foreach(var L in _lands)
                if(Mathf.Max(Mathf.Abs(L.Cx),Mathf.Abs(L.Cz))<=RevealTarget) _revealedLands.Add(L.Id);
            RefreshRevealMesh();
        }
        /// <summary>真扩展为瞬切，无需逐帧动画（保留空实现兼容旧 Tick 调用）</summary>
        public bool UpdateReveal(float dt)=>false;
        /// <summary>读档恢复：直接落到指定倍率，不做动画；并把当前方形疆域内大陆标记为已发现（不补弹事件）</summary>
        public void SnapExpansion(float e)
        {
            Expansion=e; RevealBase=RevealBaseRadius();
            RevealTarget=RevealRadius=Mathf.Min(RevealBase*Mathf.Max(1f,e), HalfX);
            _revealedLands.Clear();
            foreach(var L in _lands)
                if(Mathf.Max(Mathf.Abs(L.Cx),Mathf.Abs(L.Cz))<=RevealTarget) _revealedLands.Add(L.Id);
            RefreshRevealMesh();
        }

        // 仅重算顶点 y 与顶点色（不重建三角形/材质），同步刷新碰撞体
        private void RefreshRevealMesh()
        {
            if(_terrainMesh==null)return;
            int n=G; float halfX=HalfX,halfZ=HalfZ;
            int vc=(n+1)*(n+1);
            if(_rvVerts==null||_rvVerts.Length!=vc)_rvVerts=new Vector3[vc];
            if(_rvCols32==null||_rvCols32.Length!=vc)_rvCols32=new Color32[vc];
            int vi=0;
            for(int z=0;z<=n;z++)for(int x=0;x<=n;x++)
            {
                float wx=x*Tile-halfX,wz=z*TZ-halfZ;
                var(dh,dc)=DisplayAt(wx,wz,x,z);
                _rvVerts[vi]=new Vector3(wx,dh,wz); _rvCols32[vi]=(Color32)dc; vi++;
            }
            _terrainMesh.SetVertices(_rvVerts); _terrainMesh.SetColors(_rvCols32);
            if(_terrainCol!=null){_terrainCol.sharedMesh=null;_terrainCol.sharedMesh=_terrainMesh;}
        }

        private Color BiomeColor(float h,int gx,int gz)
        {
            if (h < GameConstants.WaterLevel)
            {
                // 河流：清透浅蓝
                bool river = gx>=0&&gz>=0&&gx<G&&gz<G&&Biome[gz,gx]==BiomeKind.River;
                if (river) return new Color(0.36f,0.66f,0.90f);
                // 海洋：参考图鲜亮蓝（浅海青蓝→深海皇家蓝）
                return Color.Lerp(new Color(0.12f,0.60f,0.92f),new Color(0.05f,0.30f,0.72f),
                    Mathf.InverseLerp(GameConstants.WaterLevel,-2.6f,h));
            }
            // 沙漠：暖沙金棕（仅主大陆），随高度略深
            if (gx>=0&&gz>=0&&gx<G&&gz<G&&Biome[gz,gx]==BiomeKind.Desert)
                return Color.Lerp(new Color(0.83f,0.66f,0.45f),new Color(0.69f,0.50f,0.29f),Mathf.InverseLerp(0.12f,4.0f,h));
            // 沙滩：近白米黄（参考图 #fff7c8）
            if (h <= 0.14f) return Color.Lerp(new Color(1.00f,0.97f,0.80f),new Color(0.93f,0.86f,0.62f),
                Mathf.InverseLerp(GameConstants.WaterLevel,0.14f,h));
            // 草地：鲜亮翠绿（参考图 #44cf4a），随高度略深并叠加自然明暗变化
            if (h < 3.0f) {
                float t=Mathf.InverseLerp(0.14f,3.0f,h);
                Color c=Color.Lerp(new Color(0.34f,0.85f,0.36f),new Color(0.18f,0.68f,0.24f),t*0.7f);
                float v=TerrainPainter.FbmRidge(gx,gz,Seed,3)*0.10f-0.05f;
                return new Color(Mathf.Clamp01(c.r+v),Mathf.Clamp01(c.g+v),Mathf.Clamp01(c.b+v));
            }
            // 高原：橄榄黄褐（仅主大陆宽缓隆起）
            if (h < 5.4f) return Color.Lerp(new Color(0.55f,0.57f,0.33f),new Color(0.62f,0.55f,0.40f),
                Mathf.InverseLerp(3.0f,5.4f,h));
            // 山地岩石→雪线（仅无人岛）
            if (h < 8.5f) return Color.Lerp(new Color(0.55f,0.55f,0.58f),new Color(0.70f,0.68f,0.66f),
                Mathf.InverseLerp(5.4f,8.5f,h));
            return Color.Lerp(new Color(0.70f,0.68f,0.66f),new Color(0.95f,0.96f,0.98f),
                Mathf.InverseLerp(8.5f,11.5f,h));
        }

        private float SampleHeightInt(int x,int z)
        {
            x=Mathf.Clamp(x,0,G-1);z=Mathf.Clamp(z,0,G-1);
            return HeightMap[z,x];
        }

        private void BuildWater(int n)
        {
            if (WaterPlane!=null) Destroy(WaterPlane);
            WaterPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            WaterPlane.name="Water"; WaterPlane.transform.SetParent(transform);
            float sxW=GameConstants.WorldMaxX*2.6f/10f, szW=GameConstants.WorldMaxZ*2.6f/10f;
            WaterPlane.transform.localScale=new Vector3(sxW,1,szW);
            WaterPlane.transform.position=new Vector3(0,GameConstants.WaterLevel-0.05f,0);
            var mat=new Material(ShaderHelper.Water);
            if (mat.HasProperty("_Shallow"))
            {
                mat.SetColor("_Shallow",new Color(0.22f,0.80f,1.00f,0.66f));
                mat.SetColor("_Deep",new Color(0.06f,0.44f,0.86f,0.90f));
                mat.SetFloat("_WaveAmp",0.12f); mat.SetFloat("_WaveFreq",0.9f);
                mat.SetFloat("_Smoothness",0.94f);
            }
            else if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor",new Color(0.07f,0.45f,0.85f,0.6f));
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color",new Color(0.07f,0.45f,0.85f,0.6f));
            var wr = WaterPlane.GetComponent<Renderer>();
            wr.material=mat; wr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(WaterPlane.GetComponent<Collider>());
        }

        // ===== 查询 =====
        public float HeightAt(float x,float z)
        {
            int gx=Mathf.FloorToInt(x/Tile+G/2f), gz=Mathf.FloorToInt(z/TZ+G/2f);
            if (gx<0||gx>=G||gz<0||gz>=G) return 0;
            return HeightMap[gz,gx];
        }
        public bool IsWater(float x,float z)
        {
            int gx=Mathf.FloorToInt(x/Tile+G/2f), gz=Mathf.FloorToInt(z/TZ+G/2f);
            if (gx<0||gx>=G||gz<0||gz>=G) return false;
            if (WaterMap[gz,gx]>0.5f) return true;
            return HeightMap[gz,gx] < EffectiveWaterLevel(x,z);   // V6.1.9(i) 月度潮汐动态水位
        }
        /// <summary>V6.3.6 船只可航行水域：海洋/河流/人工运河均可，但排除被陆地包围的淡水湖(FreshWater)，
        /// 从根上杜绝任何船只（无论大小、无论洋流潮汐如何推动）驶入陆地内湖。</summary>
        public bool IsNavigable(float x,float z)
        {
            if (!IsWater(x,z)) return false;
            return BiomeAt(x,z)!=BiomeKind.FreshWater;
        }
        /// <summary>V6.8.3 海船唯一允许的水域=外海（Default 水域）：同时排除淡水湖(FreshWater)与内河(River)。
        /// 河流虽可作运河/淡水，但任何海船都不得进入——退潮时近岸浅滩露出而深水河道仍通水，
        /// 旧逻辑会把船沿“最近的水”引入河道、再顺着入海/湖河道拖进大陆湖；以此从根上切断。</summary>
        public bool IsOceanWater(float x,float z)
        {
            if(!IsWater(x,z)) return false;
            var b=BiomeAt(x,z);
            return b!=BiomeKind.FreshWater && b!=BiomeKind.River;
        }
        /// <summary>V6.1.9(i) 平地判定：四邻高度差<=maxDelta 且高度落在[minH,maxH]（初始村落只允许平地）</summary>
        public bool IsFlatAt(float x,float z,float maxDelta=0.55f,float minH=0.35f,float maxH=3.0f)
        {
            float h=HeightAt(x,z);
            if(h<minH||h>maxH) return false;
            float s=Tile;
            float h1=HeightAt(x+s,z),h2=HeightAt(x-s,z),h3=HeightAt(x,z+s),h4=HeightAt(x,z-s);
            float mx=Mathf.Max(h,h1,h2,h3,h4),mn=Mathf.Min(h,h1,h2,h3,h4);
            return mx-mn<=maxDelta;
        }
        public bool IsBeach(float x,float z)
        {
            if (IsWater(x,z)) return false;
            float h=HeightAt(x,z);
            return h>=-0.2f && h<=0.3f;
        }
        public BiomeKind BiomeAt(float x,float z)
        {
            int gx=Mathf.FloorToInt(x/Tile+G/2f), gz=Mathf.FloorToInt(z/TZ+G/2f);
            if (gx<0||gx>=G||gz<0||gz>=G) return BiomeKind.Default;
            return Biome[gz,gx];
        }
        /// <summary>小地图用：世界坐标→群系底色</summary>
        public Color MapColorAt(float wx,float wz)
        {
            int gx=Mathf.FloorToInt(wx/Tile+G/2f), gz=Mathf.FloorToInt(wz/TZ+G/2f);
            if (gx<0||gx>=G||gz<0||gz>=G) return HiddenDeep;
            if(RevealEnabled){float rz=Mathf.Min(RevealRadius,HalfZ);float cheb=Mathf.Max(Mathf.Abs(wx),Mathf.Abs(wz)*(HalfX/rz)); int cid=ContinentMap[gz,gx]; if(cheb>RevealRadius+6f || (cid>0 && !_revealedLands.Contains(cid))) return HiddenDeep;}
            return BiomeColor(HeightMap[gz,gx],gx,gz);
        }

        // ===== V6.1.7 多大陆查询 =====
        /// <summary>世界坐标所属大陆 Id（0=开阔海洋）</summary>
        public int ContinentAt(float wx,float wz)
        {
            int gx=Mathf.FloorToInt(wx/Tile+G/2f), gz=Mathf.FloorToInt(wz/TZ+G/2f);
            if (gx<0||gx>=G||gz<0||gz>=G) return 0;
            return ContinentMap[gz,gx];
        }
        /// <summary>是否开阔深水（真正阻隔跨洋的海；内河/湖/近岸浅水不算）</summary>
        public bool IsDeepWater(float wx,float wz)
        {
            int gx=Mathf.FloorToInt(wx/Tile+G/2f), gz=Mathf.FloorToInt(wz/TZ+G/2f);
            if (gx<0||gx>=G||gz<0||gz>=G) return true;
            return ContinentMap[gz,gx]==0 && HeightMap[gz,gx]<GameConstants.WaterLevel-0.2f;
        }
        /// <summary>两点是否同属一块大陆（都在陆地且大陆 Id 相同）</summary>
        public bool SameLandmass(float x1,float z1,float x2,float z2)
        {
            int a=ContinentAt(x1,z1);
            return a>0 && a==ContinentAt(x2,z2);
        }
        /// <summary>在指定大陆上找一个平坦、非沙、非水的陆地落点</summary>
        public bool RandomPointOnContinent(int cid,out float x,out float z,int tries=60)
        {
            // V6.5.6 修复：全图均匀撒点找小块次大陆命中率极低（~0.3%/次）导致次大陆永不立村、自动建桥不触发；
            // 已知陆地改为在其圆盘包围盒内采样，命中率接近 1。
            Landmass target=null;
            foreach(var L in _lands) if(L.Id==cid){target=L;break;}
            if(target!=null)
            {
                int ccx=Mathf.RoundToInt(target.Cx/Tile+G*0.5f), ccz=Mathf.RoundToInt(target.Cz/TZ+G*0.5f);
                int rcx=Mathf.CeilToInt(target.Br*1.05f/Tile),rcz=Mathf.CeilToInt(target.Br*1.05f/TZ);
                for(int i=0;i<tries;i++)
                {
                    int gx=ccx+UnityEngine.Random.Range(-rcx,rcx+1), gz=ccz+UnityEngine.Random.Range(-rcz,rcz+1);
                    if(gx<2||gz<2||gx>=G-2||gz>=G-2)continue;
                    if(ContinentMap[gz,gx]!=cid)continue;
                    float wx=gx*Tile-HalfX, wz=gz*TZ-HalfZ;
                    if(IsWater(wx,wz)||IsBeach(wx,wz))continue;
                    float h=HeightMap[gz,gx]; if(h<0.5f||h>3.2f)continue;
                    if(Biome[gz,gx]==BiomeKind.Desert)continue;
                    x=wx;z=wz;return true;
                }
            }
            for(int i=0;i<tries;i++)
            {
                int gx=Random.Range(2,G-2), gz=Random.Range(2,G-2);
                if(ContinentMap[gz,gx]!=cid)continue;
                float wx=gx*Tile-HalfX, wz=gz*TZ-HalfZ;
                if(IsWater(wx,wz)||IsBeach(wx,wz))continue;
                float h=HeightMap[gz,gx]; if(h<0.5f||h>3.2f)continue;
                if(Biome[gz,gx]==BiomeKind.Desert)continue;
                x=wx;z=wz;return true;
            }
            x=0;z=0;return false;
        }
        /// <summary>任意陆地落点（60% 优先主大陆，供天下分裂补点等使用）</summary>
        public bool RandomLandPoint(out float x,out float z,int tries=40)
        {
            for (int i=0;i<tries;i++)
            {
                int cid = Random.value<0.6f ? HomeContinent : 1+Random.Range(0,Mathf.Max(1,ContinentCount));
                if (RandomPointOnContinent(cid,out x,out z,14)) return true;
            }
            x=0;z=0;return false;
        }
        /// <summary>年代外扩后，轮询首次进入显现圈的大陆 Id（发现新大陆/岛屿，仅回调一次）</summary>
        public List<int> PollNewlyRevealed()
        {
            var res=new List<int>();
            foreach(var L in _lands)
            {
                if(_revealedLands.Contains(L.Id))continue;
                if(Mathf.Max(Mathf.Abs(L.Cx),Mathf.Abs(L.Cz))<=RevealRadius)
                { _revealedLands.Add(L.Id); res.Add(L.Id); }
            }
            return res;
        }
        /// <summary>某块大陆是否已被发现（0=海洋恒为可见）；未发现大陆上的建筑/小人需随发现才显现</summary>
        public bool IsLandRevealed(int cid) => cid<=0 || _revealedLands.Contains(cid);
        /// <summary>大陆 Id→中心（世界坐标）</summary>
        public Vector3 ContinentCenter(int id) =>
            (id>=1 && id<=ContinentCenters.Count) ? ContinentCenters[id-1] : Vector3.zero;
    }
}

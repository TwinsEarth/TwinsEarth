using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;
using PixelToCivilization.Rendering;

namespace PixelToCivilization.World
{
    /// <summary>
    /// V6.2.2 植被系统（四层垂直结构 + 彩色花草 + 村落大榕树）：
    /// 乔木层(高)→灌木层(中)→草本层(低)→地被层(贴地)，全部按材质 CombineMeshes 静态合批控 DrawCall；
    /// 草本/地被层混生不同颜色花朵；村落附近生成独立大榕树，随游戏年代长高变大、寿命 300~800 游戏年，枯死后原地萌发新株。
    /// </summary>
    public class VegetationSystem : MonoBehaviour
    {
        Mesh _cylinder, _sphere, _quad, _cone;
        Material _bark, _leafA, _leafB, _grass, _shrubA, _shrubB, _ground;
        Material _rockA, _rockB;
        Material[] _flower;
        // V6.3.4 四季变色：记录植被材质基色，按年内进度在春夏秋冬之间插值
        Color _cLeafA,_cLeafB,_cGrass,_cShrubA,_cShrubB,_cGround; float _seasonTick;
        public int TreeTarget = 900;       // 乔木层
        public int ShrubTarget = 700;      // 灌木层
        public int GrassTarget = 5000;     // 草本层
        public int GroundTarget = 3000;    // 地被层
        public int FlowerTarget = 2800;    // 花朵（草本+地被合计）
        const int FlowerKinds = 6;

        class VegSet
        {
            public List<CombineInstance> bark=new(), la=new(), lb=new(), grass=new(), shrub=new(), ground=new();
            public List<CombineInstance> rockA=new(), rockB=new();   // V6.6.1 山坡/岸线层叠岩石（主岩/暗岩）
            public List<CombineInstance>[] flwH, flwG;
            public VegSet(){ flwH=new List<CombineInstance>[FlowerKinds]; flwG=new List<CombineInstance>[FlowerKinds];
                for(int i=0;i<FlowerKinds;i++){flwH[i]=new();flwG[i]=new();} }
        }
        readonly Dictionary<int,VegSet> _sets=new();
        readonly Dictionary<int,VegSet> _sets3=new();   // V6.3.1 近景 LV3 精合并
        VegSet SetFor(int cid){ if(!_sets.TryGetValue(cid,out var v)){v=new VegSet();_sets[cid]=v;} return v; }
        VegSet SetFor3(int cid){ if(!_sets3.TryGetValue(cid,out var v)){v=new VegSet();_sets3[cid]=v;} return v; }

        public void Populate(WorldGenerator terrain, int seed)
        {
            _sets.Clear(); _sets3.Clear();
            LODManager.ClearCull();
            _cylinder = Builtin("Cylinder.fbx") ?? BuildCylinder();
            _sphere = Builtin("Sphere.fbx") ?? BuildSphere();
            _quad = BuildQuad();
            _cone = BuildCone(); // V6.3.4 三角形（锥形）树冠
                        _bark = ShaderHelper.Pbr(new Color(0.45f,0.31f,0.19f), 0f, 0.14f, seed+1, 1.4f);  // V7.0.1 暖棕干
            _leafA = ShaderHelper.Pbr(new Color(0.22f,0.64f,0.24f), 0f, 0.20f, seed+2, 1.6f);  // 饱和松绿
            _leafB = ShaderHelper.Pbr(new Color(0.32f,0.72f,0.30f), 0f, 0.22f, seed+3, 1.6f);  // 嫩绿
            _grass = ShaderHelper.Pbr(new Color(0.42f,0.74f,0.28f), 0f, 0.12f, seed+4, 1.0f);
            _shrubA= ShaderHelper.Pbr(new Color(0.30f,0.68f,0.28f), 0f, 0.18f, seed+5, 1.3f);
            _shrubB= ShaderHelper.Pbr(new Color(0.38f,0.74f,0.32f), 0f, 0.18f, seed+6, 1.3f);
            _ground=ShaderHelper.Pbr(new Color(0.46f,0.77f,0.31f), 0f, 0.10f, seed+7, 1.0f);
            _rockA =ShaderHelper.Pbr(new Color(0.66f,0.64f,0.60f), 0f, 0.32f, seed+30, 1.3f);  // 柔灰岩
            _rockB =ShaderHelper.Pbr(new Color(0.52f,0.50f,0.47f), 0f, 0.36f, seed+31, 1.3f);
            _cLeafA=new Color(0.22f,0.64f,0.24f);_cLeafB=new Color(0.32f,0.72f,0.30f);
            _cGrass=new Color(0.42f,0.74f,0.28f);_cShrubA=new Color(0.30f,0.68f,0.28f);
            _cShrubB=new Color(0.38f,0.74f,0.32f);_cGround=new Color(0.46f,0.77f,0.31f);
            // 花朵六色：红/黄/粉/白/紫/橙
            Color[] fc={new(0.90f,0.25f,0.30f),new(0.95f,0.82f,0.22f),new(0.95f,0.55f,0.75f),
                        new(0.96f,0.96f,0.92f),new(0.62f,0.40f,0.85f),new(0.95f,0.55f,0.18f)};
            _flower=new Material[FlowerKinds];
            for(int i=0;i<FlowerKinds;i++)_flower[i]=ShaderHelper.Pbr(fc[i],0f,0.25f,seed+20+i,1.0f);

            var rng = new System.Random(seed ^ 0x5A5A);
            float vcx=terrain.SettlementCenter.x, vcz=terrain.SettlementCenter.z;
            float worldX=GameConstants.WorldMaxX,worldZ=GameConstants.WorldMaxZ;   // V6.5.8 矩形全量画布 4800x2880
            int trees=0,shrub=0,grass=0,ground=0,flw=0,guard=0;
            int maxGuard=(TreeTarget+ShrubTarget+GrassTarget+GroundTarget+FlowerTarget)*5;
            while((trees<TreeTarget||shrub<ShrubTarget||grass<GrassTarget||ground<GroundTarget||flw<FlowerTarget) && guard++<maxGuard)
            {
                float x=(float)(rng.NextDouble()*2-1)*worldX*0.46f;
                float z=(float)(rng.NextDouble()*2-1)*worldZ*0.46f;
                int cid=terrain.ContinentAt(x,z);
                if(cid==0) continue;
                if (terrain.IsWater(x,z)||terrain.IsBeach(x,z)) continue;
                var bio=terrain.BiomeAt(x,z);
                if (bio==BiomeKind.Desert) continue;
                float h=terrain.HeightAt(x,z);
                if (h<0.55f) continue;
                float rVillage=Mathf.Sqrt((x-vcx)*(x-vcx)+(z-vcz)*(z-vcz));
                if (rVillage<24f) continue;
                float y=h-0.05f;
                var vs=SetFor(cid); var vs3=SetFor3(cid);
                float dens=TerrainPainter.FbmRidge(x*0.06f,z*0.06f,seed+99,3)*0.5f+0.5f;
                float ring = (cid==terrain.HomeContinent && rVillage<82f) ? 0.18f : 0f;
                float gate = 0.42f-ring;
                double rnd=rng.NextDouble();

                // ① 乔木层（最高）
                if (trees<TreeTarget && h<7.2f && dens>gate && rnd<Mathf.Clamp01(dens+ring))
                {
                    float tr=(float)rng.NextDouble(); bool con=h>3.6f || (float)rng.NextDouble()<0.45f; // V6.3.6 平地也有45%三角锥形树，高海拔全锥形
                    AddTree(vs.bark,vs.la,vs.lb,x,y,z,tr,con);
                    AddTree3(vs3.bark,vs3.la,vs3.lb,x,y,z,tr,con);
                    trees++;
                }
                // ② 灌木层（中，无主干团状）
                if (shrub<ShrubTarget && h<6.2f && dens>0.30f && rng.NextDouble()<0.5f)
                {
                    float q1=(float)rng.NextDouble(),q2=(float)rng.NextDouble(); float yy=terrain.HeightAt(x,z)-0.02f;
                    AddShrub(vs.shrub,x,yy,z,q1,q2); AddShrub3(vs3.shrub,x,yy,z,q1,q2); shrub++;
                }
                // ③ 草本层（草叶 + 草本花）
                if (h<4.2f && rVillage>16f)
                {
                    if (grass<GrassTarget && rng.NextDouble()<0.92)
                    { float g1=(float)rng.NextDouble(),g2=(float)rng.NextDouble(); float yy=terrain.HeightAt(x,z)-0.04f;
                      AddGrassBlade(vs.grass,x,yy,z,g1,g2); AddGrassBlade3(vs3.grass,x,yy,z,g1,g2); grass++; }
                    if (flw<FlowerTarget && rng.NextDouble()<0.30)
                    { int fk=rng.Next(FlowerKinds); float fr=(float)rng.NextDouble(); float yy=terrain.HeightAt(x,z)-0.03f;
                      AddFlower(vs.flwH[fk],x,yy,z,fr,true); AddFlower3(vs3.flwH[fk],vs3.flwG[fk],x,yy,z,fr,true); flw++; }
                }
                // ④ 地被层（贴地苔藓小叶 + 地被小花）
                if (h<4.6f && rVillage>15f)
                {
                    if (ground<GroundTarget && rng.NextDouble()<0.7)
                    { float gr=(float)rng.NextDouble(); float yy=terrain.HeightAt(x,z)-0.045f;
                      AddGroundTuft(vs.ground,x,yy,z,gr); AddGroundTuft3(vs3.ground,x,yy,z,gr); ground++; }
                    if (flw<FlowerTarget && rng.NextDouble()<0.18)
                    { int fk=rng.Next(FlowerKinds); float fr=(float)rng.NextDouble(); float yy=terrain.HeightAt(x,z)-0.04f;
                      AddFlower(vs.flwG[fk],x,yy,z,fr,false); AddFlower3(vs3.flwG[fk],vs3.flwH[fk],x,yy,z,fr,false); flw++; }
                }
            }

            // ============ V6.6.1 山坡/崖岸 3D 层叠岩石簇（陡坡成片、缓坡点缀、岸线零星） ============
            // 采样集中在当前已揭示疆域内（全画布大部分尚未生成，撒太稀会导致岩石只有个位数）
            int rocks=0, rguard=0; const int RockTarget=340; float ds=3.2f;
            float rockR=terrain.RevealBase*0.98f;
            while(rocks<RockTarget && rguard++<RockTarget*14)
            {
                float rx=(float)(rng.NextDouble()*2-1)*rockR;
                float rz=(float)(rng.NextDouble()*2-1)*rockR;
                int rcid=terrain.ContinentAt(rx,rz); if(rcid==0) continue;
                if(terrain.IsWater(rx,rz)) continue;
                float rh=terrain.HeightAt(rx,rz);
                float hL=terrain.HeightAt(rx-ds,rz), hR=terrain.HeightAt(rx+ds,rz);
                float hD=terrain.HeightAt(rx,rz-ds), hU=terrain.HeightAt(rx,rz+ds);
                float slope=Mathf.Max(Mathf.Abs(hL-hR),Mathf.Abs(hD-hU))/(2f*ds);
                bool beach=terrain.IsBeach(rx,rz);
                bool steep = rh>3.2f || slope>0.22f;       // 高山/陡坡：大簇层叠岩
                bool gentle = slope>0.09f;                  // 缓坡/岩丘：小簇点缀
                double rr=rng.NextDouble();
                if(!steep){
                    if(beach){ if(rr>=0.32) continue; }     // 岸线约 1/3 落点有散石
                    else if(gentle){ if(rr>=0.5) continue; }// 缓坡约一半落点
                    else continue;                          // 纯平地不放石
                }
                float ry=rh-0.18f; var rvs=SetFor(rcid); var rvs3=SetFor3(rcid);
                AddRockCluster(rvs.rockA,rvs.rockB,rx,ry,rz,rng,steep);
                AddRockCluster3(rvs3.rockA,rvs3.rockB,rx,ry,rz,rng,steep);
                rocks++;
            }

            var vegRoot=new GameObject("Vegetation"); vegRoot.transform.SetParent(transform);
            int FAR=(int)LodTier.Far, MID=(int)LodTier.Mid, NEAR=(int)LodTier.Near;
            foreach(var kv in _sets)
            {
                int cid=kv.Key; var vs=kv.Value; var q3=_sets3.TryGetValue(cid,out var w3)?w3:null;
                // —— LV2（旧精模下沉为中景）：乔木 Far+Mid，灌草花仅 Mid，Near 时整体让给 LV3 ——
                var landGo=new GameObject("LV2"); landGo.name="Veg_Land_"+cid; landGo.transform.SetParent(vegRoot.transform);
                BuildCombine(landGo,"TreeTrunks",vs.bark,_bark,FAR,MID);
                BuildCombine(landGo,"TreeCanopy_A",vs.la,_leafA,FAR,MID);
                BuildCombine(landGo,"TreeCanopy_B",vs.lb,_leafB,FAR,MID);
                BuildCombine(landGo,"Shrubs",vs.shrub,_shrubA,MID,MID);
                BuildCombine(landGo,"Grass",vs.grass,_grass,MID,MID);
                BuildCombine(landGo,"Ground",vs.ground,_ground,MID,MID);
                BuildCombine(landGo,"Rocks_A",vs.rockA,_rockA,FAR,MID);
                BuildCombine(landGo,"Rocks_B",vs.rockB,_rockB,FAR,MID);
                for(int i=0;i<FlowerKinds;i++)
                {
                    BuildCombine(landGo,"Flowers_H"+i,vs.flwH[i],_flower[i],MID,MID);
                    BuildCombine(landGo,"Flowers_G"+i,vs.flwG[i],_flower[i],MID,MID);
                }
                // —— LV3（V6.3.1 近景完全体，仅 Near 显示）——
                if(q3!=null)
                {
                    var hi=new GameObject("LV3"); hi.name="Veg3_Land_"+cid; hi.transform.SetParent(vegRoot.transform);
                    BuildCombine(hi,"TreeTrunks3",q3.bark,_bark,NEAR,NEAR);
                    BuildCombine(hi,"TreeCanopy3_A",q3.la,_leafA,NEAR,NEAR);
                    BuildCombine(hi,"TreeCanopy3_B",q3.lb,_leafB,NEAR,NEAR);
                    BuildCombine(hi,"Shrubs3",q3.shrub,_shrubB,NEAR,NEAR);
                    BuildCombine(hi,"Grass3",q3.grass,_grass,NEAR,NEAR);
                    BuildCombine(hi,"Ground3",q3.ground,_ground,NEAR,NEAR);
                    BuildCombine(hi,"Rocks3_A",q3.rockA,_rockA,NEAR,NEAR);
                    BuildCombine(hi,"Rocks3_B",q3.rockB,_rockB,NEAR,NEAR);
                    for(int i=0;i<FlowerKinds;i++)
                    {
                        BuildCombine(hi,"Flowers3_H"+i,q3.flwH[i],_flower[i],NEAR,NEAR);
                        BuildCombine(hi,"Flowers3_G"+i,q3.flwG[i],_flower[i],NEAR,NEAR);
                    }
                    var cc=terrain.ContinentCenter(cid);
                    bool sh3=new Vector2(cc.x,cc.z).magnitude<=terrain.RevealBase+2f;
                    hi.SetActive(sh3);
                }
                var c=terrain.ContinentCenter(cid);
                bool shown=new Vector2(c.x,c.z).magnitude<=terrain.RevealBase+2f;
                landGo.SetActive(shown);
            }
            BuildBanyans(terrain,seed,vegRoot.transform);
            Debug.Log($"[Vegetation] 乔木{trees} 灌木{shrub} 草{grass} 地被{ground} 花{flw} 岩{rocks}，{_sets.Count} 大陆分组静态合并 + 村落大榕树");
        }

        /// <summary>村落附近的大榕树：独立个体，随年代生长、寿命300-800游戏年</summary>
        void BuildBanyans(WorldGenerator terrain,int seed,Transform parent)
        {
            var rng=new System.Random(seed^0xB4A4);
            var root=new GameObject("Banyans"); root.transform.SetParent(parent);
            var barkM=ShaderHelper.Pbr(new Color(0.45f,0.31f,0.19f),0f,0.16f,seed+40,1.5f); // V7.0.1
            var canM =ShaderHelper.Pbr(new Color(0.30f,0.70f,0.30f),0f,0.24f,seed+41,1.8f); // V7.0.1
            var vc=terrain.SettlementCenter; int made=0, tries=0;
            while(made<8 && tries++<40)
            {
                float ang=(float)rng.NextDouble()*Mathf.PI*2f;
                float rr=28f+(float)rng.NextDouble()*46f;
                float x=vc.x+Mathf.Cos(ang)*rr, z=vc.z+Mathf.Sin(ang)*rr;
                if(terrain.ContinentAt(x,z)!=terrain.HomeContinent)continue;
                if(terrain.IsWater(x,z)||terrain.IsBeach(x,z))continue;
                float h=terrain.HeightAt(x,z); if(h<0.55f||h>5.5f)continue;
                var go=new GameObject("Banyan"); go.transform.SetParent(root.transform);
                go.transform.position=new Vector3(x,h-0.05f,z);
                // —— LV2：旧精模 ——
                var b2=new GameObject("LV2"); b2.transform.SetParent(go.transform,false);
                AddPart(b2,_cylinder,barkM,new Vector3(0,1.6f,0),new Vector3(0.7f,1.6f,0.7f));
                AddPart(b2,_sphere,canM,new Vector3(0,3.4f,0),new Vector3(3.4f,1.5f,3.4f));
                for(int i=0;i<6;i++)
                {
                    float a=i/6f*Mathf.PI*2f+(float)rng.NextDouble()*0.5f;
                    AddPart(b2,_sphere,canM,new Vector3(Mathf.Cos(a)*2.2f,3.1f+(float)rng.NextDouble()*0.4f,Mathf.Sin(a)*2.2f),
                        Vector3.one*(1.3f+(float)rng.NextDouble()*0.6f));
                }
                for(int i=0;i<4;i++)
                {
                    float a=(i+0.5f)/4f*Mathf.PI*2f;
                    AddPart(b2,_cylinder,barkM,new Vector3(Mathf.Cos(a)*2.4f,1.6f,Mathf.Sin(a)*2.4f),new Vector3(0.12f,1.6f,0.12f));
                }
                // —— LV3：V6.3.1 近景（更多气根、板根、分层叶团）——
                var b3=new GameObject("LV3"); b3.transform.SetParent(go.transform,false);
                AddPart(b3,_cylinder,barkM,new Vector3(0,1.6f,0),new Vector3(0.78f,1.65f,0.78f));
                AddPart(b3,_sphere,canM,new Vector3(0,3.5f,0),new Vector3(3.6f,1.6f,3.6f));
                for(int i=0;i<10;i++)
                {
                    float a=i/10f*Mathf.PI*2f+(float)rng.NextDouble()*0.4f;
                    float rr3=1.4f+(float)rng.NextDouble()*1.6f;
                    AddPart(b3,_sphere,canM,new Vector3(Mathf.Cos(a)*rr3,3.0f+(float)rng.NextDouble()*0.9f,Mathf.Sin(a)*rr3),
                        Vector3.one*(1.1f+(float)rng.NextDouble()*0.7f));
                }
                for(int i=0;i<8;i++)
                {
                    float a=i/8f*Mathf.PI*2f;
                    AddPart(b3,_cylinder,barkM,new Vector3(Mathf.Cos(a)*2.6f,1.5f,Mathf.Sin(a)*2.6f),new Vector3(0.1f,1.5f,0.1f));
                }
                for(int i=0;i<4;i++) // 板根
                {
                    float a=i/4f*Mathf.PI*2f;
                    AddPart(b3,_cylinder,barkM,new Vector3(Mathf.Cos(a)*0.55f,0.35f,Mathf.Sin(a)*0.55f),Quaternion.Euler(0,-a*Mathf.Rad2Deg,62f),new Vector3(0.18f,0.7f,0.5f));
                }
                LODKit.Attach(go,8f,3);
                var bt=go.AddComponent<BanyanTree>();
                bt.BirthYear = GameManager.Instance!=null?GameManager.Instance.State.Year:1;
                bt.LifeSpan = 300f+(float)rng.NextDouble()*500f;
                made++;
            }
        }
        void AddPart(GameObject parent,Mesh m,Material mat,Vector3 localPos,Vector3 localScale)
        { AddPart(parent,m,mat,localPos,Quaternion.identity,localScale); }
        void AddPart(GameObject parent,Mesh m,Material mat,Vector3 localPos,Quaternion localRot,Vector3 localScale)
        {
            var go=new GameObject("p");go.transform.SetParent(parent.transform,false);
            var mf=go.AddComponent<MeshFilter>();mf.sharedMesh=m;var mr=go.AddComponent<MeshRenderer>();mr.sharedMaterial=mat;
            mr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;mr.receiveShadows=true;
            go.transform.localPosition=localPos;go.transform.localRotation=localRot;go.transform.localScale=localScale;
        }

        public void ActivateLand(int cid)
        {
            var root=transform.Find("Vegetation");
            if(root==null)return;
            var t=root.Find("Veg_Land_"+cid);
            if(t!=null && !t.gameObject.activeSelf) t.gameObject.SetActive(true);
        }

        public void Regrow(WorldGenerator terrain, int seed)
        {
            var old=transform.Find("Vegetation");
            if (old!=null) Destroy(old.gameObject);
            Populate(terrain,seed);
        }

        static Mesh BuildCylinder()
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var m=go.GetComponent<MeshFilter>().sharedMesh; var copy=Object.Instantiate(m);
            Object.DestroyImmediate(go); return copy;
        }
        static Mesh BuildSphere()
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var m=go.GetComponent<MeshFilter>().sharedMesh; var copy=Object.Instantiate(m);
            Object.DestroyImmediate(go); return copy;
        }
        // V6.3.4 程序化圆锥（Unity 无 Cone primitive）：底圈 y=-0.5 半径0.5，锥尖 y=+0.5
        static Mesh BuildCone(int seg=14)
        {
            var verts=new System.Collections.Generic.List<Vector3>();
            var tris=new System.Collections.Generic.List<int>();
            Vector3 apex=new(0f,0.5f,0f); Vector3 center=new(0f,-0.5f,0f);
            verts.Add(apex); verts.Add(center);
            for(int i=0;i<seg;i++){float a=i/(float)seg*Mathf.PI*2f; verts.Add(new Vector3(Mathf.Cos(a)*0.5f,-0.5f,Mathf.Sin(a)*0.5f));}
            for(int i=0;i<seg;i++)
            {
                int r0=2+i, r1=2+((i+1)%seg);
                tris.Add(0);tris.Add(r1);tris.Add(r0);          // 侧面
                tris.Add(1);tris.Add(r0);tris.Add(r1);          // 底面
            }
            var m=new Mesh(){indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            m.SetVertices(verts); m.SetTriangles(tris,0); m.RecalculateNormals(); return m;
        }

        // V6.3.4 四季调色：春嫩绿→夏深绿→秋橙→冬苍灰，按 GameTime 年内进度平滑过渡
        void Update()
        {
            _seasonTick-=Time.deltaTime; if(_seasonTick>0f||_leafA==null)return; _seasonTick=0.4f;
            var gm=GameManager.Instance; float p=gm!=null?gm.Time.YearProgress:0.25f;
            // 四季锚点（对基色的乘性染色）
            Color spr=new(1.02f,1.08f,0.92f), sum=new(0.90f,1.02f,0.88f),
                  aut=new(1.06f,0.66f,0.30f), win=new(0.74f,0.80f,0.84f);
            Color tint=SeasonTint(p,spr,sum,aut,win);
            if(_leafA)_leafA.color=_cLeafA*tint;
            if(_leafB)_leafB.color=_cLeafB*tint;
            if(_shrubA)_shrubA.color=_cShrubA*tint;
            if(_shrubB)_shrubB.color=_cShrubB*tint;
            if(_grass)_grass.color=_cGrass*tint;
            if(_ground)_ground.color=_cGround*tint;
        }
        static Color SeasonTint(float p,Color spr,Color sum,Color aut,Color win)
        {
            p=Mathf.Repeat(p,1f); float q=p*4f; int i=Mathf.FloorToInt(q); float f=q-i;
            Color a,b;
            switch(i){case 0:a=win;b=spr;break;case 1:a=spr;b=sum;break;case 2:a=sum;b=aut;break;default:a=aut;b=win;break;}
            return Color.Lerp(a,b,Mathf.SmoothStep(0f,1f,f));
        }

        // ① 乔木
        void AddTree(List<CombineInstance> bark,List<CombineInstance> la,List<CombineInstance> lb,
            float x,float y,float z,float r,bool conifer)
        {
            float s=(0.8f+r*0.7f)*1.4f;
            bark.Add(CI(_cylinder,Matrix4x4.TRS(new Vector3(x,y+1.1f*s,z),Quaternion.identity,new Vector3(0.28f*s,1.1f*s,0.28f*s))));
            if (conifer)
            {   // V6.3.4 针叶树：两层三角锥（替代圆球），下宽上窄
                la.Add(CI(_cone,Matrix4x4.TRS(new Vector3(x,y+2.05f*s,z),Quaternion.identity,new Vector3(1.9f*s,2.1f*s,1.9f*s))));
                lb.Add(CI(_cone,Matrix4x4.TRS(new Vector3(x,y+3.15f*s,z),Quaternion.identity,new Vector3(1.25f*s,1.7f*s,1.25f*s))));
            }
            else
            {   // V6.6.1 阔叶：5 团花菜状分层叶簇（参考图浓密阔叶团）
                la.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x,y+2.35f*s,z),Quaternion.identity,Vector3.one*1.18f*s)));
                la.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x,y+3.05f*s,z),Quaternion.identity,Vector3.one*0.72f*s)));
                lb.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x+0.5f*s,y+2.05f*s,z+0.22f),Quaternion.identity,Vector3.one*0.72f*s)));
                lb.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x-0.46f*s,y+2.12f*s,z-0.26f),Quaternion.identity,Vector3.one*0.64f*s)));
                lb.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x+0.12f*s,y+2.55f*s,z-0.5f*s),Quaternion.identity,Vector3.one*0.55f*s)));
            }
        }
        // V6.6.1 岩石簇：1 块主岩（非均整、随机倾斜）+ 1~3 块更小暗岩，模拟层叠风化岩崖
        void AddRockCluster(List<CombineInstance> a,List<CombineInstance> b,float x,float y,float z,System.Random rng,bool steep)
        {
            float main=(steep?1.15f:0.8f)+(float)rng.NextDouble()*0.7f;
            var q=Quaternion.Euler((float)(rng.NextDouble()*16-8),(float)(rng.NextDouble()*360),(float)(rng.NextDouble()*16-8));
            a.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x,y+main*0.42f,z),q,new Vector3(main,main*(0.8f+(float)rng.NextDouble()*0.35f),main*(0.85f+(float)rng.NextDouble()*0.3f)))));
            int n=steep?3:2;
            for(int i=0;i<n;i++){ float ang=(float)rng.NextDouble()*Mathf.PI*2f, d=main*(0.55f+(float)rng.NextDouble()*0.6f);
                float sc=main*(0.28f+(float)rng.NextDouble()*0.3f);
                var qq=Quaternion.Euler((float)(rng.NextDouble()*20-10),(float)(rng.NextDouble()*360),(float)(rng.NextDouble()*20-10));
                b.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x+Mathf.Cos(ang)*d,y+sc*0.4f,z+Mathf.Sin(ang)*d),qq,new Vector3(sc,sc*0.8f,sc)))); }
        }
        void AddRockCluster3(List<CombineInstance> a,List<CombineInstance> b,float x,float y,float z,System.Random rng,bool steep)
        {
            float main=(steep?1.25f:0.85f)+(float)rng.NextDouble()*0.8f;
            var q=Quaternion.Euler((float)(rng.NextDouble()*16-8),(float)(rng.NextDouble()*360),(float)(rng.NextDouble()*16-8));
            a.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x,y+main*0.42f,z),q,new Vector3(main,main*0.95f,main))));
            // 顶部叠一块小岩形成层理
            a.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x+main*0.12f,y+main*0.95f,z-main*0.1f),Quaternion.Euler(0,(float)rng.NextDouble()*360,0),new Vector3(main*0.55f,main*0.4f,main*0.5f))));
            int n=steep?4:2;
            for(int i=0;i<n;i++){ float ang=i/(float)n*Mathf.PI*2f+(float)rng.NextDouble()*0.6f, d=main*(0.6f+(float)rng.NextDouble()*0.7f);
                float sc=main*(0.3f+(float)rng.NextDouble()*0.34f);
                b.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x+Mathf.Cos(ang)*d,y+sc*0.4f,z+Mathf.Sin(ang)*d),Quaternion.Euler((float)(rng.NextDouble()*20-10),(float)(rng.NextDouble()*360),(float)(rng.NextDouble()*20-10)),new Vector3(sc,sc*0.85f,sc)))); }
        }
        // ② 灌木（团状、无主干）
        void AddShrub(List<CombineInstance> list,float x,float y,float z,float r1,float r2)
        {
            float s=0.5f+r1*0.5f;
            list.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x,y+0.32f*s,z),Quaternion.identity,new Vector3(0.8f*s,0.55f*s,0.8f*s))));
            list.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x+(r2-0.5f)*0.6f,y+0.4f*s,z+(r1-0.5f)*0.6f),Quaternion.identity,new Vector3(0.5f*s,0.4f*s,0.5f*s))));
        }
        // ③ 草本叶
        void AddGrassBlade(List<CombineInstance> list,float x,float y,float z,float r1,float r2)
        {
            float h=0.35f+r1*0.4f;
            var rot=Quaternion.Euler(0,r2*180f,0);
            list.Add(CI(_quad,Matrix4x4.TRS(new Vector3(x,y+h*0.5f,z),rot,new Vector3(0.5f,h,1))));
            list.Add(CI(_quad,Matrix4x4.TRS(new Vector3(x,y+h*0.5f,z),rot*Quaternion.Euler(0,90f,0),new Vector3(0.5f,h,1))));
        }
        // 花朵（herb=草本层较高；否则地被层贴地小花）
        void AddFlower(List<CombineInstance> list,float x,float y,float z,float r,bool herb)
        {
            float h=herb?0.26f+r*0.12f:0.10f+r*0.06f;
            float w=herb?0.34f:0.20f;
            var rot=Quaternion.Euler(0,r*360f,0);
            list.Add(CI(_quad,Matrix4x4.TRS(new Vector3(x,y+h*0.5f,z),rot,new Vector3(w,h,1))));
            list.Add(CI(_quad,Matrix4x4.TRS(new Vector3(x,y+h*0.5f,z),rot*Quaternion.Euler(0,90f,0),new Vector3(w,h,1))));
        }
        // ④ 地被小叶
        void AddGroundTuft(List<CombineInstance> list,float x,float y,float z,float r)
        {
            float h=0.08f+r*0.06f;var rot=Quaternion.Euler(0,r*360f,0);
            list.Add(CI(_quad,Matrix4x4.TRS(new Vector3(x,y+h*0.5f,z),rot,new Vector3(0.32f,h,1))));
            list.Add(CI(_quad,Matrix4x4.TRS(new Vector3(x,y+h*0.5f,z),rot*Quaternion.Euler(0,90f,0),new Vector3(0.32f,h,1))));
        }

        // ============ V6.3.1 LV3 近景精模（更多分枝/分层叶簇/多瓣层） ============
        void AddTree3(List<CombineInstance> bark,List<CombineInstance> la,List<CombineInstance> lb,
            float x,float y,float z,float r,bool conifer)
        {
            float s=(0.8f+r*0.7f)*1.4f;
            bark.Add(CI(_cylinder,Matrix4x4.TRS(new Vector3(x,y+1.1f*s,z),Quaternion.identity,new Vector3(0.30f*s,1.15f*s,0.30f*s))));
            // 两根斜分枝
            bark.Add(CI(_cylinder,Matrix4x4.TRS(new Vector3(x+0.35f*s,y+1.9f*s,z),Quaternion.Euler(0,0,-32f),new Vector3(0.10f*s,0.7f*s,0.10f*s))));
            bark.Add(CI(_cylinder,Matrix4x4.TRS(new Vector3(x-0.32f*s,y+1.85f*s,z-0.1f),Quaternion.Euler(0,0,30f),new Vector3(0.10f*s,0.66f*s,0.10f*s))));
            if(conifer)
            {   // V6.3.4 三层三角锥塔冠，明暗交错
                la.Add(CI(_cone,Matrix4x4.TRS(new Vector3(x,y+2.0f*s,z),Quaternion.identity,new Vector3(2.1f*s,2.2f*s,2.1f*s))));
                lb.Add(CI(_cone,Matrix4x4.TRS(new Vector3(x,y+3.0f*s,z),Quaternion.identity,new Vector3(1.55f*s,1.8f*s,1.55f*s))));
                la.Add(CI(_cone,Matrix4x4.TRS(new Vector3(x,y+3.9f*s,z),Quaternion.identity,new Vector3(1.05f*s,1.5f*s,1.05f*s))));
            }
            else
            {   // V6.6.1 阔叶近景：7 团浓密分层 + 顶冠，明暗交错
                la.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x,y+2.4f*s,z),Quaternion.identity,Vector3.one*1.2f*s)));
                la.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x,y+3.15f*s,z),Quaternion.identity,Vector3.one*0.78f*s)));
                lb.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x+0.58f*s,y+2.15f*s,z+0.28f),Quaternion.identity,Vector3.one*0.8f*s)));
                la.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x-0.54f*s,y+2.22f*s,z-0.3f),Quaternion.identity,Vector3.one*0.74f*s)));
                lb.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x+0.12f*s,y+2.78f*s,z-0.52f),Quaternion.identity,Vector3.one*0.66f*s)));
                la.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x-0.2f*s,y+2.72f*s,z+0.52f),Quaternion.identity,Vector3.one*0.6f*s)));
                lb.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x+0.42f*s,y+2.85f*s,z+0.4f),Quaternion.identity,Vector3.one*0.5f*s)));
            }
        }
        void AddShrub3(List<CombineInstance> list,float x,float y,float z,float r1,float r2)
        {
            float s=0.5f+r1*0.5f;
            list.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x,y+0.34f*s,z),Quaternion.identity,new Vector3(0.82f*s,0.58f*s,0.82f*s))));
            list.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x+(r2-0.5f)*0.6f,y+0.44f*s,z+(r1-0.5f)*0.6f),Quaternion.identity,new Vector3(0.52f*s,0.42f*s,0.52f*s))));
            list.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x+(r1-0.5f)*0.5f,y+0.5f*s,z+(r2-0.5f)*0.5f),Quaternion.identity,new Vector3(0.4f*s,0.34f*s,0.4f*s))));
            list.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x+(0.5f-r2)*0.5f,y+0.4f*s,z+(0.5f-r1)*0.5f),Quaternion.identity,new Vector3(0.36f*s,0.3f*s,0.36f*s))));
        }
        void AddGrassBlade3(List<CombineInstance> list,float x,float y,float z,float r1,float r2)
        {   // 四片交叉叶（0/90 + 45/135）
            float h=0.38f+r1*0.44f; var q=Quaternion.Euler(0,r2*180f,0);
            list.Add(CI(_quad,Matrix4x4.TRS(new Vector3(x,y+h*0.5f,z),q,new Vector3(0.5f,h,1))));
            list.Add(CI(_quad,Matrix4x4.TRS(new Vector3(x,y+h*0.5f,z),q*Quaternion.Euler(0,90f,0),new Vector3(0.5f,h,1))));
            list.Add(CI(_quad,Matrix4x4.TRS(new Vector3(x,y+h*0.5f,z),q*Quaternion.Euler(0,45f,0),new Vector3(0.42f,h*0.9f,1))));
            list.Add(CI(_quad,Matrix4x4.TRS(new Vector3(x,y+h*0.5f,z),q*Quaternion.Euler(0,135f,0),new Vector3(0.42f,h*0.9f,1))));
        }
        void AddGroundTuft3(List<CombineInstance> list,float x,float y,float z,float r)
        {
            float h=0.09f+r*0.07f;var q=Quaternion.Euler(0,r*360f,0);
            for(int k=0;k<4;k++) list.Add(CI(_quad,Matrix4x4.TRS(new Vector3(x,y+h*0.5f,z),q*Quaternion.Euler(0,k*45f,0),new Vector3(0.3f,h,1))));
        }
        // LV3 花朵：三片交叉花瓣层 + 中心色点（centerList 复用另一花色做花心）
        void AddFlower3(List<CombineInstance> petals,List<CombineInstance> center,float x,float y,float z,float r,bool herb)
        {
            float h=herb?0.28f+r*0.14f:0.11f+r*0.07f;
            float w=herb?0.36f:0.22f; var q=Quaternion.Euler(0,r*360f,0);
            for(int k=0;k<3;k++) petals.Add(CI(_quad,Matrix4x4.TRS(new Vector3(x,y+h*0.5f,z),q*Quaternion.Euler(0,k*60f,0),new Vector3(w,h,1))));
            center.Add(CI(_sphere,Matrix4x4.TRS(new Vector3(x,y+h,z),Quaternion.identity,Vector3.one*(herb?0.07f:0.045f))));
        }

        CombineInstance CI(Mesh m,Matrix4x4 t)=>new(){mesh=m,transform=t};
        void BuildCombine(GameObject root,string name,List<CombineInstance> inst,Material mat,int minTier,int maxTier)
        {
            if(inst.Count==0) return;
            var go=new GameObject(name); go.transform.SetParent(root.transform);
            var mf=go.AddComponent<MeshFilter>(); var mr=go.AddComponent<MeshRenderer>();
            var mesh=new Mesh(){indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.CombineMeshes(inst.ToArray(),true,true);
            mesh.RecalculateBounds();
            mf.sharedMesh=mesh; mr.sharedMaterial=mat;
            mr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On; mr.receiveShadows=true;
            go.isStatic=true;
            // V6.3.1：只在全局档位 [minTier,maxTier] 区间显示，近景 LV3 与中景 LV2 互斥不重叠
            LODManager.RegisterCullBand(mr,minTier,maxTier);
        }

        static Mesh Builtin(string n)=>Resources.GetBuiltinResource<Mesh>(n);
        static Mesh BuildQuad()
        {
            var m=new Mesh();
            m.vertices=new[]{new Vector3(-0.5f,0,0),new Vector3(0.5f,0,0),new Vector3(0.5f,1,0),new Vector3(-0.5f,1,0)};
            m.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
            m.triangles=new[]{0,2,1,0,3,2};
            m.RecalculateNormals(); return m;
        }
    }

    /// <summary>大榕树生长器：随游戏年代由幼苗长大，到寿命(300-800年)枯萎后原地萌发新株。</summary>
    public class BanyanTree : MonoBehaviour
    {
        public float BirthYear=1f, LifeSpan=500f;
        float _lastScale=-1f;
        void Update()
        {
            var gm=GameManager.Instance; if(gm==null)return;
            float year=gm.State.Year;
            float age=year-BirthYear;
            if(age>=LifeSpan){ BirthYear=year; LifeSpan=300f+Random.value*500f; age=0f; } // 枯荣更替：新株
            if(age<0f)age=0f;
            // 0~120年由0.35幼苗长到1.0成熟，之后缓慢增至1.3的巨榕
            float g = age<120f ? 0.35f+0.65f*(age/120f) : Mathf.Min(1.3f,1f+(age-120f)/800f*0.3f);
            if(Mathf.Abs(g-_lastScale)>0.002f){ transform.localScale=Vector3.one*g; _lastScale=g; }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.World;

namespace PixelToCivilization.Rendering
{
    /// <summary>LOD 层级（对齐用户口径）：Lv3 完全体(近/一屏内) 20-60Hz；Lv2 简化体(中/很小) 10Hz；Lv1 像素点(远/看不清) 1Hz；更远直接剔除。</summary>
    public enum LodTier { Hidden=0, Far=1, Mid=2, Near=3 }

    /// <summary>
    /// V6.2.2 LOD 动态按需加载&amp;渲染系统。
    /// ·个体物体（建筑/船/鸟/鱼/车/人）用 Unity <see cref="LODGroup"/> 组件建立多档几何：
    ///   3 档=完全体/简化体/像素点（建筑车船等）；2 档=完全体/简化体（鸟/鱼，无难看像素点）。引擎按屏幕占比自动切换、过远剔除。
    /// ·鸟/鱼额外支持"最近 N 只全显"：屏幕占比达标者里只让距离最近的前 N 只保持完全体，其余强制降为简化体（数量上限内仍控算力）。
    /// ·<see cref="LodAgent"/> 给出 1Hz/10Hz/20-60Hz 逻辑刷新闸门；静态合并网格（地形/植被）走 <see cref="LODManager"/> 全局档位。
    /// </summary>
    public static class LODKit
    {
        // 默认屏幕占比过渡阈值（3 档物体）
        public const float ThNear = 0.14f;
        public const float ThMid  = 0.030f;
        public const float ThFar  = 0.006f;

        static Mesh _cube;
        static Mesh CubeMesh
        {
            get
            {
                if (_cube==null)
                {
                    var g=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _cube=Object.Instantiate(g.GetComponent<MeshFilter>().sharedMesh);
                    Object.DestroyImmediate(g);
                }
                return _cube;
            }
        }

        // —— 最近 N 只全显调度 ——
        class CapGroup{public int Cap;public readonly List<LodAgent> Members=new();}
        static readonly Dictionary<string,CapGroup> _groups=new();
        static float _schedTimer;

        /// <summary>
        /// 为"完全体"根物体挂 LOD。
        /// tierCount=3：完全体(nearTh)/简化体(0.03)/像素点(lowTh)；tierCount=2：完全体(nearTh)/简化体(lowTh)，再远剔除。
        /// capGroup/capFull：给定分组内、屏幕占比≥nearTh 的物体里，仅最近 capFull 只完全体，其余强制简化。
        /// </summary>
        public static LodAgent Attach(GameObject root, float worldSize=1f, int tierCount=3,
            float nearTh=ThNear, float lowTh=ThFar, string capGroup=null, int capFull=0, float midTh=ThMid)
        {
            if (root==null) return null;
            var full=RenderersUnder(root);
            if (full.Count==0) return null;
            tierCount = tierCount<=2 ? 2 : 3;
            var old=root.transform.Find("LOD2_Simple"); if(old) Object.Destroy(old.gameObject);
            var old2=root.transform.Find("LOD1_Pixel"); if(old2) Object.Destroy(old2.gameObject);

            // V6.3.1：若工厂提供了作者子体 "LV2"（简化体），中景直接用它而非自动方块
            var authoredLV2=root.transform.Find("LV2");
            Renderer[] simpleR;
            if(authoredLV2!=null)
            {
                var sl=authoredLV2.GetComponentsInChildren<Renderer>(true);
                simpleR=sl.Length>0?sl:null;
            } else simpleR=null;
            // V6.3.4：作者子体 "LV1"（旧精模下沉为远景），替代自动像素方块
            var authoredLV1=root.transform.Find("LV1");
            Renderer[] farR=null;
            if(authoredLV1!=null){ var fl=authoredLV1.GetComponentsInChildren<Renderer>(true); if(fl.Length>0)farR=fl; }

            Color avg=AverageColor(full);
            Bounds b=CombinedBounds(full);
            GameObject simpleGo=null; Renderer[] simpleAuto=null;
            if(simpleR==null){ simpleGo=MakeProxy(root,"LOD2_Simple",b,avg,false); simpleAuto=RenderersOf(simpleGo); }

            var lg=root.GetComponent<LODGroup>() ?? root.AddComponent<LODGroup>();
            LOD[] lods;
            var midR = simpleR ?? simpleAuto;
            if(tierCount>=3)
            {
                lods=new LOD[3];
                lods[0]=new LOD(nearTh, full.ToArray());
                lods[1]=new LOD(midTh,  midR);
                if(farR!=null) lods[2]=new LOD(lowTh, farR);
                else { var pixel=MakeProxy(root,"LOD1_Pixel",b,avg,true); lods[2]=new LOD(lowTh, RenderersOf(pixel)); }
            }
            else
            {   // 鸟/鱼：完全体 → 简化体 → 剔除（无像素点）
                lods=new LOD[2];
                lods[0]=new LOD(nearTh, full.ToArray());
                lods[1]=new LOD(lowTh,  midR);
            }
            for(int i=0;i<lods.Length;i++) lods[i].fadeTransitionWidth=0f;
            lg.SetLODs(lods); lg.RecalculateBounds(); lg.fadeMode=LODFadeMode.None;

            var agent=root.GetComponent<LodAgent>() ?? root.AddComponent<LodAgent>();
            agent.WorldSize=Mathf.Max(0.2f,worldSize);
            agent.NearTh=nearTh; agent.Group=lg;
            if(!string.IsNullOrEmpty(capGroup)&&capFull>0)
            {
                if(!_groups.TryGetValue(capGroup,out var cg)){cg=new CapGroup();_groups[capGroup]=cg;}
                cg.Cap=capFull;
                if(!cg.Members.Contains(agent))cg.Members.Add(agent);
                agent.CapGroup=capGroup;
            }
            return agent;
        }

        /// <summary>最近 N 只全显调度（0.4s 一次，低成本）。由任意 LodAgent.Update 驱动。</summary>
        public static void ScheduleTick(float dt)
        {
            _schedTimer-=dt; if(_schedTimer>0f)return; _schedTimer=0.4f;
            var cam=Camera.main; if(cam==null)return;
            foreach(var kv in _groups)
            {
                var cg=kv.Value;
                cg.Members.RemoveAll(a=>a==null);
                // 距离从近到远
                cg.Members.Sort((a,b)=>a.Dist.CompareTo(b.Dist));
                int full=0;
                foreach(var a in cg.Members)
                {
                    if(a.Group==null)continue;
                    if(a.Ratio>=a.NearTh && full<cg.Cap){ full++; a.Group.ForceLOD(-1); } // 最近 N 只：自动（完全体）
                    else if(a.Ratio>=a.NearTh){ a.Group.ForceLOD(1); }                    // 达标但超出名额：强制简化体
                    else a.Group.ForceLOD(-1);                                             // 未达标：按占比自动 简化/剔除
                }
            }
        }

        static GameObject MakeProxy(GameObject root,string name,Bounds b,Color c,bool pixel)
        {
            var go=new GameObject(name);
            go.transform.SetParent(root.transform,false);
            var mf=go.AddComponent<MeshFilter>(); mf.sharedMesh=CubeMesh;
            var mr=go.AddComponent<MeshRenderer>(); mr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows=false;
            mr.sharedMaterial=ShaderHelper.Mat(c);
            Vector3 cLocal=root.transform.InverseTransformPoint(b.center);
            Vector3 sLocal=root.transform.InverseTransformVector(b.size);
            sLocal=new Vector3(Mathf.Abs(sLocal.x),Mathf.Abs(sLocal.y),Mathf.Abs(sLocal.z));
            if (pixel)
            {
                float m=Mathf.Max(sLocal.x,Mathf.Max(sLocal.y,sLocal.z))*0.42f;
                sLocal=new Vector3(m,m,m);
            }
            else
            {
                sLocal=Vector3.Max(sLocal,Vector3.one*0.2f)*1.02f;
            }
            go.transform.localPosition=cLocal;
            go.transform.localScale=sLocal;
            go.transform.localRotation=Quaternion.identity;
            return go;
        }

        static List<Renderer> RenderersUnder(GameObject root)
        {
            var list=new List<Renderer>();
            root.GetComponentsInChildren(true,list);
            // 排除自动代理与作者中景子体 LV2（剩余即近景 LV3 完全体）
            list.RemoveAll(r=>{
                var t=r.transform;
                while(t!=null && t!=root.transform){ if(t.name=="LOD2_Simple"||t.name=="LOD1_Pixel"||t.name=="LV2"||t.name=="LV1")return true; t=t.parent; }
                return false;
            });
            return list;
        }
        static Renderer[] RenderersOf(GameObject root)
        {
            if (root==null) return System.Array.Empty<Renderer>();
            return root.GetComponentsInChildren<Renderer>(true);
        }

        static Color AverageColor(List<Renderer> rs)
        {
            Vector3 sum=Vector3.zero; int n=0;
            foreach(var r in rs)
            {
                var m=r.sharedMaterial; if(m==null) continue;
                Color c=m.HasProperty("_Color")?m.color:Color.gray;
                sum+=(Vector3)new Vector3(c.r,c.g,c.b); n++;
            }
            if(n==0)return Color.gray;
            sum/=n; return new Color(sum.x,sum.y,sum.z,1f);
        }
        static Bounds CombinedBounds(List<Renderer> rs)
        {
            bool first=true; var b=new Bounds();
            foreach(var r in rs){ if(first){b=r.bounds;first=false;} else b.Encapsulate(r.bounds); }
            if(first)b=new Bounds(rs[0].transform.position,Vector3.one);
            return b;
        }

        /// <summary>屏幕占比近似（物体世界尺寸 / 视口在该距离的世界高度）。</summary>
        public static float ScreenRatio(Vector3 worldPos,float size,Camera cam)
        {
            if(cam==null)return 1f;
            float dist=Vector3.Distance(cam.transform.position,worldPos);
            if(dist<1e-3f)return 1f;
            return size/(dist*Mathf.Tan(cam.fieldOfView*Mathf.Deg2Rad*0.5f)*2f);
        }
    }

    /// <summary>挂在每个 LOD 物体上：当前层级、屏幕占比、距离，与分档刷新闸门（近20-60Hz/中10Hz/远1Hz）。</summary>
    public class LodAgent : MonoBehaviour
    {
        public float WorldSize=1f;
        public float NearTh=LODKit.ThNear;
        public string CapGroup;
        public LODGroup Group;
        public LodTier Tier=LodTier.Near;
        public float Ratio=1f, Dist;
        float _acc;
        void Update()
        {
            var cam=Camera.main;
            LODManager.Recompute(Time.unscaledDeltaTime);
            if(cam!=null)
            {
                Dist=Vector3.Distance(cam.transform.position,transform.position);
                Ratio=LODKit.ScreenRatio(transform.position,WorldSize,cam);
                Tier = Ratio>=NearTh?LodTier.Near : Ratio>=0.004f?LodTier.Mid : LodTier.Far;
            }
            if(!string.IsNullOrEmpty(CapGroup)) LODKit.ScheduleTick(Time.unscaledDeltaTime);
        }
        public bool Tick(float dt)
        {
            float interval=Tier==LodTier.Near?0f:Tier==LodTier.Mid?0.1f:1f;
            if(interval<=0f)return true;
            _acc+=dt;
            if(_acc<interval)return false;
            _acc=0f;return true;
        }
    }

    /// <summary>全局 LOD 档位：静态合并网格（地形/植被）远距裁剪与相机空间天气降频。</summary>
    public static class LODManager
    {
        public static LodTier Global=LodTier.Near;
        static float _timer;
        static Camera _cam;
        class CullItem{public Renderer R;public int MinTier,MaxTier=(int)LodTier.Near;}
        static readonly List<CullItem> _cull=new();

        public static void RegisterCull(Renderer r,int minTierToShow)
        {
            if(r==null)return;
            _cull.Add(new CullItem{R=r,MinTier=minTierToShow,MaxTier=(int)LodTier.Near});
        }
        /// <summary>只在全局档位落在 [minTier,maxTier] 区间时显示（V6.3.1 静态植被近景LV3/中景LV2互斥）。</summary>
        public static void RegisterCullBand(Renderer r,int minTier,int maxTier)
        {
            if(r==null)return;
            _cull.Add(new CullItem{R=r,MinTier=minTier,MaxTier=maxTier});
        }
        public static void ClearCull(){_cull.Clear();}

        public static void Recompute(float dt)
        {
            _timer-=dt; if(_timer>0f)return; _timer=0.25f;
            if(_cam==null)_cam=Camera.main; if(_cam==null)return;
            float alt=_cam.transform.position.y;
            Global=alt<130f?LodTier.Near:alt<300f?LodTier.Mid:LodTier.Far;
            int g=(int)Global;
            for(int i=_cull.Count-1;i>=0;i--)
            {
                var it=_cull[i]; if(it.R==null){_cull.RemoveAt(i);continue;}
                bool show=g>=it.MinTier && g<=it.MaxTier;
                if(it.R.enabled!=show)it.R.enabled=show;
            }
        }
        public static float Interval=>Global==LodTier.Near?0f:Global==LodTier.Mid?0.1f:1f;
    }
}

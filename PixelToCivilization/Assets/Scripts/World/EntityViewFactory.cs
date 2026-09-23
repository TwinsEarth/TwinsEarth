using UnityEngine;
using PixelToCivilization.Actors;

namespace PixelToCivilization.World
{
    /// <summary>移动实体（敌军/战船/投射物/舰队/太空基地）的简单程序化视图工厂</summary>
    public static class EntityViewFactory
    {
        public static Transform EnsureRoot(string name, Transform parent=null)
        {
            var found=GameObject.Find(name);
            if (found!=null) return found.transform;
            var go=new GameObject(name);
            if (parent!=null) go.transform.SetParent(parent);
            return go.transform;
        }

        public static GameObject Spawn(string name, Transform parent, PrimitiveType type, Color c, float scale=1f)
        {
            var go=GameObject.CreatePrimitive(type);
            go.name=name;
            if (parent!=null) go.transform.SetParent(parent);
            var col=go.GetComponent<Collider>(); if (col!=null) Object.Destroy(col);
            var r=go.GetComponent<Renderer>();
            if (r!=null) r.sharedMaterial=ShaderHelper.Mat(c);
            go.transform.localScale=Vector3.one*scale;
            return go;
        }

        /// <summary>V6.1.1 程序化人形士兵/居民（带代码骨骼动画）</summary>
        public static GameObject SpawnHumanoid(string name, Transform parent, Color outfit, float scale=1f, bool armored=false)
        {
            var go=new GameObject(name);
            if(parent!=null) go.transform.SetParent(parent);
            // V7.0.2 士兵统一走 soldier 职业（铁盔/长矛/盾/军旗），Q版比例，时代军服在 Build 内按 armored 偏铁灰
            HumanoidFactory.Build(go,outfit,scale,armored,"soldier","commoner",1,0,0,24);
            return go;
        }

        /// <summary>V6.1.1 程序化多时代载具/舰船（轮子/桨可动）；V6.1.3 sub 区分具体车型/船型</summary>
        public static GameObject SpawnVehicle(string name, Transform parent, VehicleKind kind, Color hull, float scale=1f, string sub=null)
        {
            var go=new GameObject(name);
            if(parent!=null) go.transform.SetParent(parent);
            VehicleFactory.Build(go,kind,hull,scale,sub);
            return go;
        }

        public static Color Hex(long h)=>new(((h>>16)&255)/255f,((h>>8)&255)/255f,(h&255)/255f);

        /// <summary>把视图贴到地形表面（若有地形查询）</summary>
        public static void Place(GameObject view, WorldGenerator terrain, float x, float z, float yOffset=0f)
        {
            if (view==null) return;
            float y = terrain!=null ? terrain.HeightAt(x,z) : 0f;
            view.transform.position=new Vector3(x,y+yOffset,z);
        }
    }
}

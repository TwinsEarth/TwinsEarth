using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.World;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// V6.1.5 殖民时代系统：大航海(公元1000/明·清)后在已发现港口或海洋副本「新大陆」节点建立殖民地，
    /// 殖民地逐年上贡金/特产/研究/文化、可升级三级、会爆发动乱需派军镇压、失守则独立；
    /// 每座殖民地 +2% 全局金币产出，累计 8 座达成「日不落」纪事。全要素进存档。
    /// </summary>
    public class ColonizationSystem : GameSystemBase
    {
        public static readonly string[] ColonyNames =
        { "新泉州","新广州","占城","旧港","满剌加","苏门答剌","锡兰","古里","忽鲁谟斯","木骨都束","麻林","吕宋","苏禄","扶桑","新大陆东岸","新大陆西岸" };
        public static readonly (int gold,int wood) FoundCost = (200,100);
        private bool _sunNeverSet;
        private int _nameSeq;
        private Transform _root;
        private WorldGenerator _terrain;

        public override void Init(GameManager gm)
        {
            base.Init(gm);
            _terrain=Object.FindObjectOfType<WorldGenerator>();
            _root=EntityViewFactory.EnsureRoot("Colonies",gm.transform);
        }

        /// <summary>每帧为缺失视图的殖民地补建 3D 据点（读档恢复/异常兜底，对齐 MilitarySystem 的重建模式）</summary>
        public override void Tick(float dt)
        {
            foreach (var c in S.Colonies) if (c.View==null) SpawnColonyView(c);
        }

        /// <summary>生成海外据点：石质基座 + 旗杆 + 国旗，等级越高越大、基座越华贵（贸易站木色→殖民地石青→领地鎏金）</summary>
        private void SpawnColonyView(Colony c)
        {
            if (c.View!=null) return;
            var go=new GameObject("Colony_"+c.Name);
            go.transform.SetParent(_root,false);
            float s=1f+(c.Level-1)*0.35f;
            long baseHex = c.Level==1?0xC8A165 : c.Level==2?0x9FB6C8:0xFFD700;
            var pedestal=EntityViewFactory.Spawn("Ped",go.transform,PrimitiveType.Cylinder,EntityViewFactory.Hex(baseHex),1.9f*s);
            pedestal.transform.localPosition=new Vector3(0,0.4f,0);
            var pole=EntityViewFactory.Spawn("Pole",go.transform,PrimitiveType.Cylinder,EntityViewFactory.Hex(0x4A3520),1f);
            pole.transform.localPosition=new Vector3(0,2.4f*s,0); pole.transform.localScale=new Vector3(0.12f,3.2f*s,0.12f);
            var flag=EntityViewFactory.Spawn("Flag",go.transform,PrimitiveType.Cube,EntityViewFactory.Hex(0xE0463A),1f);
            flag.transform.localPosition=new Vector3(0.75f*s,3.7f*s,0);
            flag.transform.localScale=new Vector3(1.5f*s,0.85f*s,0.1f);
            EntityViewFactory.Place(go,_terrain,c.X,c.Z,0f);
            c.View=go;
        }

        private void RefreshColonyView(Colony c)
        {
            if (c.View) Object.Destroy(c.View);
            c.View=null;
            SpawnColonyView(c);
        }

        /// <summary>是否进入殖民时代（大航海开启 或 明·清时代及以后）</summary>
        public bool EraOpen => S.AgeOfSail || S.Era>=4;

        public bool CanFound(out string why)
        {
            why=null;
            if (!EraOpen){ why="尚未进入大航海/殖民时代（公元1000年、明·清时代开启）"; return false; }
            if (S.Ships.Count<1){ why="至少需要 1 艘船才能远航殖民"; return false; }
            if (S.GetRes("gold")<FoundCost.gold || S.GetRes("wood")<FoundCost.wood)
            { why="建立殖民地需 金"+FoundCost.gold+" 木"+FoundCost.wood; return false; }
            return true;
        }

        /// <summary>建立殖民地（x,z 为海洋副本节点/远海坐标，缺省随机远海点）</summary>
        public bool FoundColony(float x=9999f, float z=9999f)
        {
            if (!CanFound(out var why)){ GM.AddEvent("bad",why); return false; }
            S.AddRes("gold",-FoundCost.gold); S.AddRes("wood",-FoundCost.wood);
            if (x>9000f){ float ang=Random.value*Mathf.PI*2f, dist=120+Random.value*60f; x=Mathf.Cos(ang)*dist; z=Mathf.Sin(ang)*dist; }
            string name = ColonyNames[(_nameSeq++) % ColonyNames.Length];
            if (_nameSeq>ColonyNames.Length) name+="·"+_nameSeq;
            string[] resIds={ "spice","cotton","gem","ivory","frankincense","coffee" };
            var c=new Colony
            {
                Id="colony_"+_nameSeq, Name=name, Level=1, Pop=10, Loyalty=100,
                ResId=resIds[Random.Range(0,resIds.Length)], X=x, Z=z
            };
            S.Colonies.Add(c);
            SpawnColonyView(c);
            if (!S.ColonialAge) S.ColonialAge=true;
            GM.AddEvent("good","🌍 于海外建立殖民地「"+name+"」，岁输方物");
            return true;
        }

        public static readonly Dictionary<int,Dictionary<string,int>> UpgradeCosts=new()
        {
            [2]=new(){["gold"]=300,["iron"]=20},
            [3]=new(){["gold"]=800,["steel"]=30},
        };

        public bool Upgrade(Colony c)
        {
            if (c==null || c.Level>=3) return false;
            int to=c.Level+1;
            var cost=UpgradeCosts[to];
            if (!S.CanAfford(cost)){ GM.AddEvent("bad","资源不足，无法将 "+c.Name+" 升格"); return false; }
            S.Pay(cost); c.Level=to; c.Pop=to==2?30:80; c.Loyalty=Mathf.Min(100,c.Loyalty+15);
            RefreshColonyView(c);   // V6.1.5 升格后据点尺寸/配色随等级升级
            GM.AddEvent("good","🏛️ "+c.Name+" 升格为"+(to==2?"殖民地":"领地"));
            return true;
        }

        /// <summary>派军镇压动乱：耗粮50，需军用战船或≥10兵力</summary>
        public bool Suppress(Colony c)
        {
            bool force=false;
            foreach (var s in S.Ships) if (s.Military){ force=true; break; }
            if (!force && S.MilSoldiers>=10) force=true;
            if (!force){ GM.AddEvent("bad","需军用战船或至少10名士兵才能跨洋镇压"); return false; }
            if (S.GetRes("food")<50){ GM.AddEvent("bad","军粮不足（需粮50），无法镇压"); return false; }
            S.AddRes("food",-50); c.Loyalty=100;
            GM.AddEvent("good","⚔️ 已跨洋平定 "+c.Name+" 动乱，安定恢复");
            return true;
        }

        public override void OnYear(int year)
        {
            float goldBonus=0f;
            for (int i=S.Colonies.Count-1;i>=0;i--)
            {
                var c=S.Colonies[i];
                // 动乱检定：等级越低越易乱
                float unrestRate=c.Level==1?0.08f:c.Level==2?0.05f:0.03f;
                bool unrest=Random.value<unrestRate;
                if (unrest)
                {
                    c.Loyalty-=20;
                    GM.AddEvent("bad","⚠️ 殖民地「"+c.Name+"」爆发动乱，本年停止上贡");
                    if (c.Loyalty<=0)
                    {
                        if(c.View)Object.Destroy(c.View);
                        S.Colonies.RemoveAt(i);
                        GM.AddEvent("bad","🏴 殖民地「"+c.Name+"」宣告独立，海外领地丧失");
                        continue;
                    }
                    continue; // 动乱年不上贡
                }
                c.Loyalty=Mathf.Min(100,c.Loyalty+3);
                // V6.1.5 平衡：Lv1 年贡 8→12 金，回本约 17 游戏年，加快殖民正反馈；Lv2/3 维持 20/40
                int gold=c.Level==1?12:c.Level==2?20:40;
                int tribute=c.Level==1?3:c.Level==2?5:8;
                S.AddRes("gold",gold);
                if (S.OceanResources.ContainsKey(c.ResId)) S.OceanResources[c.ResId]+=tribute;
                goldBonus+=gold;
                if (c.Level>=2){ S.AddRes("research",c.Level==2?2:5); if(c.Level==3) S.AddRes("culture",10); }
            }
            // 每座殖民地 +2% 全局金币产出（按年折算，随殖民地数线性）
            if (S.Colonies.Count>0) S.AddRes("gold", Mathf.RoundToInt(goldBonus*0.02f*S.Colonies.Count));
            // 日不落纪事
            if (!_sunNeverSet && S.Colonies.Count>=8)
            {
                _sunNeverSet=true; S.AddRes("gold",1000);
                GM.AddEvent("good","🌐 八大殖民地遍及四海，日不落帝国肇建，赐金千两");
            }
        }
    }
}

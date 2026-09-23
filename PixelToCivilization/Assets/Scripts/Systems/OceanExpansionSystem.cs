using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;

namespace PixelToCivilization.Systems
{
    /// <summary>远洋舰队（航行动画用）</summary>
    public class OceanFleet { public float Progress; public float Speed; public int Target; public bool Returned; }
    /// <summary>海洋贸易站/资源点</summary>
    public class OceanTradePost { public string Type; public string Resource; public float X,Z,Amount; public float Timer; }

    /// <summary>
    /// 海洋大开发副本 —— 对齐 v5.9.9：明朝(era4)解锁、出海口传送门、宝船远航、发现港口、海洋特产与贸易。
    /// </summary>
    public class OceanExpansionSystem : GameSystemBase
    {
        // 郑和下西洋航线港口
        public static readonly string[] Ports =
        { "占城","爪哇","旧港","满剌加","苏门答腊","锡兰","古里","忽鲁谟斯","木骨都束","麻林","天方" };
        public static readonly string[] OceanResIds = { "spice","cotton","gem","ivory","frankincense","coffee" };
        public static readonly Dictionary<string,string> ResNames = new()
        { {"spice","香料"},{"cotton","棉花"},{"gem","宝石"},{"ivory","象牙"},{"frankincense","乳香"},{"coffee","咖啡"} };

        public List<OceanFleet> Fleets = new();
        public List<OceanTradePost> TradePosts = new();

        public void UnlockExpansion()
        {
            if (S.OceanUnlocked) return;
            S.OceanUnlocked = true;
            GM.AddEvent("good","🌊 明朝海洋大开发！出海口已开启，宝船可远航四海！");
        }

        /// <summary>派遣舰队（木50金30）</summary>
        public bool SendFleet()
        {
            if (S.GetRes("wood")<50 || S.GetRes("gold")<30) { GM.AddEvent("bad","木材或金币不足，无法远航"); return false; }
            S.AddRes("wood",-50); S.AddRes("gold",-30);
            Fleets.Add(new OceanFleet{ Target=S.OceanDiscovered.Count, Speed=0.3f+Random.value*0.2f });
            GM.AddEvent("good","🚢 宝船起航，目标：未知港口");
            return true;
        }

        public void EnterMap() { S.CurrentMap="ocean"; }
        public void LeaveMap() { S.CurrentMap="home"; }

        public override void Tick(float dt)
        {
            if (S.CurrentMap!="ocean")
            {
                // 在主地图时舰队仍在航行
                AdvanceFleets(dt);
                return;
            }
            AdvanceFleets(dt);
            // 贸易站每5秒采集
            foreach (var st in TradePosts)
            {
                if (st.Type!="station") continue;
                st.Timer+=dt;
                if (st.Timer>=5)
                {
                    st.Timer=0;
                    foreach (var rp in TradePosts)
                        if (rp.Type=="resource" && rp.Amount>0 &&
                            Vector2.Distance(new Vector2(st.X,st.Z),new Vector2(rp.X,rp.Z))<20)
                        {
                            float amt=Mathf.Min(5,rp.Amount); rp.Amount-=amt;
                            S.OceanResources[rp.Resource]=S.OceanResources.Or(rp.Resource)+amt;
                        }
                }
            }
        }

        private void AdvanceFleets(float dt)
        {
            foreach (var f in Fleets)
            {
                if (f.Returned) continue;
                f.Progress += f.Speed*dt;
                if (f.Progress>=1 && S.OceanDiscovered.Count < Ports.Length)
                {
                    f.Returned=true;
                    int idx=S.OceanDiscovered.Count;
                    string port=Ports[Mathf.Min(idx,Ports.Length-1)];
                    if (!S.OceanDiscovered.Contains(port))
                    {
                        S.OceanDiscovered.Add(port);
                        GM.AddEvent("good","🗺️ 宝船发现新港口："+port+"！开辟贸易航线");
                        // 发现奖励：金币 + 随机特产
                        S.AddRes("gold",100+idx*20);
                        string res=OceanResIds[Random.Range(0,OceanResIds.Length)];
                        S.OceanResources[res]=S.OceanResources.Or(res)+20;
                        TradePosts.Add(new OceanTradePost{Type="resource",Resource=res,X=Random.Range(-40,40f),Z=Random.Range(-40,40f),Amount=100});
                    }
                }
            }
            Fleets.RemoveAll(f=>f.Returned);
        }

        /// <summary>海洋特产折算金币</summary>
        public void SellOceanResources()
        {
            float gold=0;
            foreach (var k in OceanResIds) gold += S.OceanResources.Or(k)*5;
            if (gold<=0) return;
            foreach (var k in OceanResIds) S.OceanResources[k]=0;
            S.AddRes("gold",gold);
            GM.AddEvent("good","💰 海洋特产贸易，获得"+Mathf.RoundToInt(gold)+"金币");
        }
    }
}

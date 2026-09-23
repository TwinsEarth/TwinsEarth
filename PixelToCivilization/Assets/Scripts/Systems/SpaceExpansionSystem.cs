using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;

namespace PixelToCivilization.Systems
{
    /// <summary>太空基地</summary>
    public class SpaceBase { public string Type; public float X,Z; }

    /// <summary>
    /// 太空探索副本 —— 对齐 v5.9.9：新中国(era6)解锁、太空电梯/飞船/戴森云/月球/火星五大工程、太空资源、火星移民胜利。
    /// </summary>
    public class SpaceExpansionSystem : GameSystemBase
    {
        public static readonly string[] ResIds={"helium3","titanium","antimatter","darkenergy","solarCrystal"};
        public static readonly Dictionary<string,string> ResNames=new()
        { {"helium3","氦-3"},{"titanium","钛矿"},{"antimatter","反物质"},{"darkenergy","暗能量"},{"solarCrystal","太阳晶体"} };
        public List<SpaceBase> Bases=new();

        public void UnlockExploration()
        {
            if (S.SpaceUnlocked) return;
            S.SpaceUnlocked=true;
            GM.AddEvent("good","🚀 太空时代开启！建造太空飞船探索宇宙！");
        }

        public void EnterMap(){ S.CurrentMap="space"; }
        public void LeaveMap(){ S.CurrentMap="home"; }

        /// <summary>建造太空工程（对齐 buildSpaceProject）</summary>
        public bool BuildProject(string proj)
        {
            switch (proj)
            {
                case "elevator":
                    if (S.GetRes("steel")<50||S.GetRes("carbon")<20) return Fail();
                    S.AddRes("steel",-50);S.AddRes("carbon",-20);
                    S.SpElevator=Mathf.Min(100,S.SpElevator+20);
                    if (S.SpElevator>=100) GM.AddEvent("good","🗼 太空电梯建成！太空建设速度翻倍");
                    return true;
                case "ship":
                    if (S.GetRes("steel")<30||S.GetRes("fusion")<10) return Fail();
                    S.AddRes("steel",-30);S.AddRes("fusion",-10);S.SpShips++;
                    GM.AddEvent("good","🛸 宇宙飞船建造完成！现有"+S.SpShips+"艘");
                    return true;
                case "dyson":
                    if (S.GetRes("fusion")<50) return Fail();
                    S.AddRes("fusion",-50);S.SpDyson=Mathf.Min(100,S.SpDyson+10);
                    if (S.SpDyson>=100){ GM.AddEvent("good","☀️ 戴森云建成！能源无限！"); S.AddRes("fusion",9999); }
                    return true;
                case "lunar":
                    if (S.GetRes("steel")<60||S.GetRes("fusion")<15) return Fail();
                    S.AddRes("steel",-60);S.AddRes("fusion",-15);S.SpLunar=Mathf.Min(100,S.SpLunar+25);
                    if (S.SpLunar>=100){ GM.AddEvent("good","🌙 月球基地建成！持续开采氦-3"); Bases.Add(new SpaceBase{Type="lunar"}); }
                    return true;
                case "mars":
                    if (S.GetRes("steel")<100||S.GetRes("fusion")<30) return Fail();
                    S.AddRes("steel",-100);S.AddRes("fusion",-30);S.SpMars=Mathf.Min(100,S.SpMars+20);
                    if (S.SpMars>=100){ S.Victory=true; GM.AddEvent("good","🎉 火星移民成功！人类文明迈向星际！"); Bases.Add(new SpaceBase{Type="mars"}); }
                    return true;
            }
            return false;
        }
        private bool Fail(){ GM.AddEvent("bad","资源不足，无法建造太空工程"); return false; }

        public override void Tick(float dt)
        {
            // 月球基地持续产氦-3；太空电梯加速
            if (S.SpLunar>=100) S.AddRes("helium3", dt*0.5f*(S.SpElevator>=100?2:1));
            if (S.SpDyson>=100) S.AddRes("fusion", dt*1f);
        }
    }
}

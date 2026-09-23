using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;

namespace PixelToCivilization.Systems
{
    /// <summary>科技系统：前置校验、开始研究；研究点累积在EconomySystem</summary>
    public class TechSystem : GameSystemBase
    {
        public bool CanResearch(string id, out string reason)
        {
            reason = null;
            if (!GM.Techs.TryGetValue(id, out var t)) { reason = "无此科技"; return false; }
            if (S.ResearchedTechs.Contains(id)) { reason = "已研究"; return false; }
            if (S.CurrentResearch != null) { reason = "已有研究进行中"; return false; }
            if (t.HasRequirement)
                foreach (var r in t.Requires)
                    if (!S.ResearchedTechs.Contains(r)) { reason = "前置科技未完成"; return false; }
            return true;
        }

        public bool StartResearch(string id)
        {
            if (!CanResearch(id, out var reason))
            {
                if (reason != null) GM.AddEvent("bad", reason);
                return false;
            }
            S.CurrentResearch = id; S.ResearchProgress = 0;
            GM.AddEvent("good","开始研究：" + GM.Techs[id].Name);
            return true;
        }

        public float Progress01(string id)
        {
            if (!GM.Techs.TryGetValue(id, out var t) || t.Cost <= 0) return 0;
            return S.CurrentResearch == id ? Mathf.Clamp01(S.ResearchProgress / t.Cost) : 0;
        }

        public bool IsResearched(string id) => S.ResearchedTechs.Contains(id);
        public bool IsAvailable(string id) => CanResearch(id, out _);

        /// <summary>当前时代及之前的科技</summary>
        public List<TechDefinition> TechsOfEra(int era)
        {
            var list = new List<TechDefinition>();
            foreach (var t in TechDatabase.CreateAll()) if (t.Era <= era) list.Add(t);
            return list;
        }
    }
}

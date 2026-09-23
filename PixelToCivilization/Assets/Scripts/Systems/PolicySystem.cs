using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.Data;

namespace PixelToCivilization.Systems
{
    /// <summary>政策系统：开关政策、民心影响（对齐 togglePolicy）</summary>
    public class PolicySystem : GameSystemBase
    {
        public bool IsActive(string id) => S.Policies.Contains(id);

        public bool Toggle(string id)
        {
            if (!GM.Policies.TryGetValue(id, out var p)) return false;
            if (S.Policies.Contains(id)) { S.Policies.Remove(id); return false; }
            S.Policies.Add(id);
            if (p.Morale != 0) S.Happiness = Mathf.Clamp(S.Happiness + p.Morale, 0, 100);
            GM.AddEvent("info", (p.Morale < 0 ? "⚠️" : "📜") + " 推行政策：" + p.Name);
            return true;
        }

        /// <summary>取某政策效果倍率，缺省1</summary>
        public float EffectMult(string effectKey)
        {
            float mult = 1f;
            foreach (var id in S.Policies)
                if (GM.Policies.TryGetValue(id, out var p) && p.Effect != null &&
                    p.Effect.TryGetValue(effectKey, out var v)) mult *= v;
            return mult;
        }

        public List<PolicyDefinition> PoliciesOfEra(int era)
        {
            var list = new List<PolicyDefinition>();
            foreach (var p in PolicyDatabase.CreateAll()) if (p.Era <= era) list.Add(p);
            return list;
        }
    }
}

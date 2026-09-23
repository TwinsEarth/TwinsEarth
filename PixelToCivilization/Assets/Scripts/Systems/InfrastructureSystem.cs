using UnityEngine;
using PixelToCivilization.Core;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// 基础设施系统 —— 对齐 v5.9.9：电网容量、电力覆盖率（≥50%时现代产出×1.5）、AI加成、戴森云无限能源。
    /// </summary>
    public class InfrastructureSystem : GameSystemBase
    {
        // 每座现代建筑的基础用电
        private const float PowerPerModernBuilding = 10f;

        public override void Tick(float dt)
        {
            UpdatePowerCoverage();
        }

        /// <summary>建筑建成时的特殊产能效果</summary>
        public void OnBuildingBuilt(string type)
        {
            switch (type)
            {
                case "power_plant":
                    S.ElectricGrid += 50; GM.AddEvent("good","⚡ 发电站并网，电网容量+50"); break;
                case "fusion_plant":
                    S.ElectricGrid += 200; GM.AddEvent("good","☢️ 聚变电站并网，电网容量+200"); break;
                case "ai_lab":
                    S.AiBonus = 0.3f; GM.AddEvent("good","🤖 AI实验室建成！所有产出+30%"); break;
                case "dyson_swarm":
                    S.AddRes("fusion", 9999);
                    S.ElectricGrid += 10000;
                    GM.AddEvent("good","☀️ 戴森云建成！能源无限！"); break;
            }
        }

        public void UpdatePowerCoverage()
        {
            if (S.Era < 6) { S.PowerCoverage = 100; return; }
            float need = 0, covered = 0;
            foreach (var b in S.Buildings)
            {
                bool modern = b.Def != null && (b.Def.Cat=="能源"||b.Def.Cat=="科技"||b.Type=="factory_modern"||
                    b.Type=="skyscraper"||b.Type=="high_speed_rail"||b.Type=="airport"||b.Type=="highway_modern");
                if (modern)
                {
                    need += PowerPerModernBuilding;
                    if (S.ElectricGrid > 0) covered += PowerPerModernBuilding;
                }
            }
            S.PowerCoverage = need > 0 ? Mathf.Min(100, covered/need*100) : 100;
        }
    }
}

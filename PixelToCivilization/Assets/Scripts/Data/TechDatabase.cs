// 自动生成自 v5.9.9 TECH_DEFS，请勿手改
using System.Collections.Generic;

namespace PixelToCivilization.Data
{
    public static partial class TechDatabase
    {
        public static List<TechDefinition> CreateAll()
        {
            var list = new List<TechDefinition>();
            list.Add(new TechDefinition{ Id="bronze_casting", Name="青铜铸造", Era=0, Cost=50, Desc="解锁青铜坊", Requires=null });
            list.Add(new TechDefinition{ Id="writing", Name="文字", Era=0, Cost=80, Desc="文化+20%", Requires=null });
            list.Add(new TechDefinition{ Id="irrigation", Name="灌溉", Era=0, Cost=60, Desc="农田产出+50%", Requires=null });
            list.Add(new TechDefinition{ Id="calendar", Name="历法", Era=0, Cost=70, Desc="农业产出+20%", Requires=new[]{"writing"} });
            list.Add(new TechDefinition{ Id="iron_smelting", Name="冶铁术", Era=1, Cost=100, Desc="解锁冶铁坊", Requires=new[]{"bronze_casting"} });
            list.Add(new TechDefinition{ Id="crossbow", Name="弩机", Era=1, Cost=80, Desc="军事+30%", Requires=null });
            list.Add(new TechDefinition{ Id="great_wall_tech", Name="长城工程", Era=1, Cost=150, Desc="解锁长城", Requires=new[]{"iron_smelting"} });
            list.Add(new TechDefinition{ Id="paper", Name="造纸术", Era=1, Cost=120, Desc="科技速度+20%", Requires=new[]{"writing"} });
            list.Add(new TechDefinition{ Id="horse_riding", Name="骑兵战术", Era=1, Cost=90, Desc="解锁马厩", Requires=null });
            list.Add(new TechDefinition{ Id="canal_engineering", Name="运河工程", Era=2, Cost=150, Desc="解锁运河", Requires=new[]{"iron_smelting"} });
            list.Add(new TechDefinition{ Id="imperial_exam", Name="科举制度", Era=2, Cost=130, Desc="学堂效果翻倍", Requires=new[]{"paper"} });
            list.Add(new TechDefinition{ Id="block_printing", Name="雕版印刷", Era=2, Cost=100, Desc="文化+30%", Requires=new[]{"paper"} });
            list.Add(new TechDefinition{ Id="compass_early", Name="指南针雏形", Era=2, Cost=90, Desc="海洋探索+1", Requires=null });
            list.Add(new TechDefinition{ Id="movable_type", Name="活字印刷", Era=3, Cost=120, Desc="科技速度+50%", Requires=new[]{"block_printing"} });
            list.Add(new TechDefinition{ Id="gunpowder", Name="火药", Era=3, Cost=140, Desc="解锁火药武器", Requires=new[]{"iron_smelting"} });
            list.Add(new TechDefinition{ Id="compass", Name="指南针", Era=3, Cost=100, Desc="解锁远洋航行", Requires=new[]{"compass_early"} });
            list.Add(new TechDefinition{ Id="paper_money", Name="交子", Era=3, Cost=110, Desc="商业+50%", Requires=new[]{"block_printing"} });
            list.Add(new TechDefinition{ Id="porcelain", Name="制瓷术", Era=3, Cost=80, Desc="解锁瓷窑", Requires=null });
            list.Add(new TechDefinition{ Id="treasure_ship", Name="宝船建造", Era=4, Cost=200, Desc="解锁宝船厂", Requires=new[]{"compass","gunpowder"} });
            list.Add(new TechDefinition{ Id="ocean_navigation", Name="远洋航海", Era=4, Cost=180, Desc="海洋副本范围扩大", Requires=new[]{"compass"} });
            list.Add(new TechDefinition{ Id="artillery", Name="火炮", Era=4, Cost=160, Desc="军事+50%", Requires=new[]{"gunpowder"} });
            list.Add(new TechDefinition{ Id="steam_engine", Name="蒸汽机", Era=5, Cost=250, Desc="解锁近代工厂", Requires=new[]{"iron_smelting"} });
            list.Add(new TechDefinition{ Id="telegraph_tech", Name="电报", Era=5, Cost=180, Desc="信息传递加速", Requires=null });
            list.Add(new TechDefinition{ Id="railway", Name="铁路", Era=5, Cost=220, Desc="解锁铁路", Requires=new[]{"steam_engine"} });
            list.Add(new TechDefinition{ Id="modern_military", Name="新式陆军", Era=5, Cost=200, Desc="解锁新军", Requires=new[]{"artillery","steam_engine"} });
            list.Add(new TechDefinition{ Id="electricity", Name="电力", Era=6, Cost=300, Desc="解锁发电站", Requires=new[]{"steam_engine"} });
            list.Add(new TechDefinition{ Id="computer", Name="计算机", Era=6, Cost=350, Desc="解锁数据中心", Requires=new[]{"electricity"} });
            list.Add(new TechDefinition{ Id="ai_tech", Name="人工智能", Era=6, Cost=500, Desc="解锁AI实验室", Requires=new[]{"computer"} });
            list.Add(new TechDefinition{ Id="high_speed_rail_tech", Name="高铁技术", Era=6, Cost=280, Desc="解锁高铁", Requires=new[]{"electricity","railway"} });
            list.Add(new TechDefinition{ Id="aviation", Name="航空", Era=6, Cost=250, Desc="解锁机场", Requires=null });
            list.Add(new TechDefinition{ Id="internet", Name="互联网", Era=6, Cost=300, Desc="科技+50%", Requires=new[]{"computer"} });
            list.Add(new TechDefinition{ Id="space_elevator_tech", Name="太空电梯", Era=7, Cost=800, Desc="解锁太空电梯", Requires=new[]{"ai_tech"} });
            list.Add(new TechDefinition{ Id="fusion_power", Name="核聚变", Era=7, Cost=600, Desc="解锁聚变电站", Requires=new[]{"electricity"} });
            list.Add(new TechDefinition{ Id="dyson_swarm_tech", Name="戴森云", Era=7, Cost=1000, Desc="解锁戴森云", Requires=new[]{"fusion_power"} });
            list.Add(new TechDefinition{ Id="lunar_landing", Name="登月", Era=7, Cost=500, Desc="解锁月球基地", Requires=new[]{"space_elevator_tech"} });
            list.Add(new TechDefinition{ Id="mars_landing", Name="登火", Era=7, Cost=900, Desc="解锁火星移民", Requires=new[]{"lunar_landing","dyson_swarm_tech"} });
            list.Add(new TechDefinition{ Id="carbon_nanotube", Name="碳纳米管", Era=7, Cost=400, Desc="太空电梯材料", Requires=new[]{"ai_tech"} });
            return list;
        }
    }
}

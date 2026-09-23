// 自动生成自 v5.9.9 POLICY_DEFS，请勿手改
using System.Collections.Generic;

namespace PixelToCivilization.Data
{
    public static partial class PolicyDatabase
    {
        public static List<PolicyDefinition> CreateAll()
        {
            var list = new List<PolicyDefinition>();
            list.Add(new PolicyDefinition{ Id="land_reform", Name="井田制", Era=0, Desc="土地公有，农业+15%", Risk="", Morale=0, Effect=new Dictionary<string,float>{["food"]=1.15f} });
            list.Add(new PolicyDefinition{ Id="enfeoffment", Name="分封制", Era=0, Desc="诸侯自治，文化+10%但叛乱风险", Risk="rebellion", Morale=0, Effect=new Dictionary<string,float>{["culture"]=1.1f} });
            list.Add(new PolicyDefinition{ Id="centralization", Name="郡县制", Era=1, Desc="中央集权，行政效率+20%", Risk="", Morale=0, Effect=new Dictionary<string,float>{["admin"]=1.2f} });
            list.Add(new PolicyDefinition{ Id="military_service", Name="兵役制", Era=1, Desc="征兵速度+50%", Risk="", Morale=0, Effect=new Dictionary<string,float>{["conscription"]=1.5f} });
            list.Add(new PolicyDefinition{ Id="canal_labor", Name="征发徭役", Era=2, Desc="建造加速但民心-10", Risk="", Morale=-10, Effect=new Dictionary<string,float>{["buildSpeed"]=1.3f} });
            list.Add(new PolicyDefinition{ Id="imperial_exam_p", Name="科举取士", Era=2, Desc="人才辈出，科技+25%", Risk="", Morale=0, Effect=new Dictionary<string,float>{["research"]=1.25f} });
            list.Add(new PolicyDefinition{ Id="open_sea", Name="开海通商", Era=4, Desc="海洋贸易+100%", Risk="", Morale=0, Effect=new Dictionary<string,float>{["seaTrade"]=2f} });
            list.Add(new PolicyDefinition{ Id="sea_ban", Name="海禁", Era=4, Desc="海防+50%但贸易-80%", Risk="", Morale=0, Effect=new Dictionary<string,float>{["defense"]=1.5f,["seaTrade"]=0.2f} });
            list.Add(new PolicyDefinition{ Id="self_strengthening", Name="洋务运动", Era=5, Desc="工业+50%", Risk="", Morale=0, Effect=new Dictionary<string,float>{["industry"]=1.5f} });
            list.Add(new PolicyDefinition{ Id="reform", Name="维新变法", Era=5, Desc="科技+30%但保守派不满", Risk="", Morale=0, Effect=new Dictionary<string,float>{["research"]=1.3f} });
            list.Add(new PolicyDefinition{ Id="five_year_plan", Name="五年计划", Era=6, Desc="工业产出+80%", Risk="", Morale=0, Effect=new Dictionary<string,float>{["industry"]=1.8f} });
            list.Add(new PolicyDefinition{ Id="reform_opening", Name="改革开放", Era=6, Desc="贸易+100%，科技+30%", Risk="", Morale=0, Effect=new Dictionary<string,float>{["trade"]=2f,["research"]=1.3f} });
            list.Add(new PolicyDefinition{ Id="tech_self_reliance", Name="自主创新", Era=6, Desc="科技+50%", Risk="", Morale=0, Effect=new Dictionary<string,float>{["research"]=1.5f} });
            list.Add(new PolicyDefinition{ Id="global_coop", Name="人类命运共同体", Era=7, Desc="全产出+20%", Risk="", Morale=0, Effect=new Dictionary<string,float>{["all"]=1.2f} });
            list.Add(new PolicyDefinition{ Id="space_colonization", Name="星际殖民", Era=7, Desc="太空建设+100%", Risk="", Morale=0, Effect=new Dictionary<string,float>{["space"]=2f} });
            return list;
        }
    }
}

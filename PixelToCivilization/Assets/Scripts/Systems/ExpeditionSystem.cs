using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// V6.1.6 海洋大开发 &amp; 太空探索「网格探索副本」：9×9 海图/星图、迷雾遮罩、方向移动/自动探索、
    /// 视野揭雾、节点事件（港口殖民、鱼群/特产、海怪/风暴/沉船/洋流；星球前哨、小行星/气态巨星、
    /// 遗迹/陨石/辐射/虫洞/戴森云）、补给与战力、归零返航。网格/位置/节点全要素进存档。
    /// 与既有 Ocean/SpaceExpansionSystem（数值远航/五大工程）叠加共存，不替换。
    /// </summary>
    public class ExpeditionSystem : GameSystemBase
    {
        public const int ViewRadius = 1;

        /// <summary>仅准备/初始化网格（打开面板用，不改变 CurrentMap，避免“看一眼面板就被切进副本”）</summary>
        public void Prepare(string type)
        {
            var e=type=="space"?S.SpaceExp:S.OceanExp;
            EnsureInit(e);
        }
        public void Enter(string type)
        {
            Prepare(type);
            S.CurrentMap=type=="space"?"space":"ocean";
        }
        public void ReturnHome(string type)
        {
            var e=type=="space"?S.SpaceExp:S.OceanExp;
            e.Power=e.MaxPower; e.Supply=100; e.PosX=e.PosY=e.N/2; // 返航整补、回到母港
            Reveal(e,e.PosX,e.PosY);
            S.CurrentMap="home";
            GM.AddEvent("info",type=="space"?"🛰️ 飞船返航整补":"⛵ 舰队返航整补");
        }

        private void EnsureInit(ExpeditionState e)
        {
            if (e.Inited){ if(e.Seen==null||e.Seen.Length==0) Reveal(e,e.PosX,e.PosY); return; }
            int n=e.N;
            e.Seen=new byte[n*n]; e.NodeKind=new string[n*n]; e.NodeUsed=new int[n*n];
            e.PosX=e.PosY=n/2; e.Power=e.MaxPower=100; e.Supply=100;
            int target=e.MapType=="ocean"?15:14, placed=0, guard=0;
            while (placed<target && guard++<300)
            {
                int x=Random.Range(0,n), y=Random.Range(0,n);
                if (x==n/2&&y==n/2) continue;
                int idx=e.Idx(x,y); if (e.NodeKind[idx]!=null) continue;
                e.NodeKind[idx]=RollNode(e.MapType); placed++;
            }
            Reveal(e,e.PosX,e.PosY);
            e.Inited=true;
            e.LastEvent=e.MapType=="ocean"?"⛵ 自母港起航，探索未知海域":"🚀 自太空港起航，探索未知星域";
        }

        private string RollNode(string type)
        {
            if (type=="ocean")
            {
                string[] k={"fish","res","res","monster","storm","wreck","current","port","port","res"};
                return k[Random.Range(0,k.Length)];
            }
            string[] s={"asteroid","gas","ruin","meteor","radiation","wormhole","dyson","moon","mars","asteroid"};
            return s[Random.Range(0,s.Length)];
        }

        private void Reveal(ExpeditionState e,int cx,int cy)
        {
            for (int dy=-ViewRadius;dy<=ViewRadius;dy++)
                for (int dx=-ViewRadius;dx<=ViewRadius;dx++)
                {
                    int x=cx+dx,y=cy+dy; if(!e.InBounds(x,y))continue;
                    e.Seen[e.Idx(x,y)]=1;
                }
        }

        private bool PayStep(ExpeditionState e)
        {
            if (e.MapType=="ocean")
            {
                if (S.GetRes("food")<2||S.GetRes("gold")<1) return false;
                S.AddRes("food",-2); S.AddRes("gold",-1);
            }
            else
            {
                if (S.GetRes("fusion")<1||S.GetRes("steel")<1) return false;
                S.AddRes("fusion",-1); S.AddRes("steel",-1);
            }
            return true;
        }

        /// <summary>向 (dx,dy) 移动一格并结算；返回事件文本</summary>
        public string Move(string type,int dx,int dy)
        {
            var e=type=="space"?S.SpaceExp:S.OceanExp; EnsureInit(e);
            int nx=e.PosX+dx, ny=e.PosY+dy;
            if (!e.InBounds(nx,ny)) return "已到海图/星图边缘，无法继续";
            if (!PayStep(e)){ ForcedReturn(e); return "补给耗尽，舰队/飞船被迫返航整补"; }
            e.PosX=nx; e.PosY=ny; Reveal(e,nx,ny);
            string msg=Resolve(e);
            if (e.Power<=0||e.Supply<=0){ ForcedReturn(e); msg+="；战力/补给耗尽，已返航整补"; }
            e.LastEvent=msg; return msg;
        }

        /// <summary>自动探索：朝相邻未揭开格走一步，否则随机</summary>
        public string AutoExplore(string type)
        {
            var e=type=="space"?S.SpaceExp:S.OceanExp; EnsureInit(e);
            int[] dxs={0,0,1,-1}, dys={1,-1,0,0};
            int bx=0,by=0; bool found=false;
            // 打乱方向顺序
            for (int i=0;i<4;i++){ int j=Random.Range(i,4);(dxs[i],dxs[j])=(dxs[j],dxs[i]);(dys[i],dys[j])=(dys[j],dys[i]); }
            for (int i=0;i<4;i++)
            {
                int x=e.PosX+dxs[i], y=e.PosY+dys[i];
                if (e.InBounds(x,y)&&e.Seen[e.Idx(x,y)]==0){ bx=dxs[i];by=dys[i];found=true;break; }
            }
            if (!found){ bx=dxs[0];by=dys[0]; }
            return Move(type,bx,by);
        }

        private void ForcedReturn(ExpeditionState e)
        {
            e.Power=e.MaxPower; e.Supply=100; e.PosX=e.PosY=e.N/2;
            Reveal(e,e.PosX,e.PosY); S.CurrentMap="home";
        }

        public string CurrentNode(ExpeditionState e)=>e.NodeKind?[e.Idx(e.PosX,e.PosY)];

        private string Resolve(ExpeditionState e)
        {
            string k=CurrentNode(e); if (string.IsNullOrEmpty(k))
                return e.MapType=="ocean"?"🌊 这片海域风平浪静":"✨ 这片星域空寂无物";
            int idx=e.Idx(e.PosX,e.PosY);
            switch (k)
            {
                // ---- 海洋 ----
                case "port":
                    if (e.NodeUsed[idx]==0){ e.NodeUsed[idx]=1; S.AddRes("gold",30); return "⚓ 发现天然良港/新大陆海岸，可于此「建立殖民地」（金+30）"; }
                    return "⚓ 重返已发现良港，可于此建立殖民地";
                case "fish": S.AddRes("food",20); return "🐟 遭遇鱼群，捕捞获粮 20";
                case "res": { string r=OceanExpansionSystem.OceanResIds[Random.Range(0,OceanExpansionSystem.OceanResIds.Length)];
                    S.OceanResources[r]=S.OceanResources.Or(r)+8; return "📦 采获海洋特产 "+OceanExpansionSystem.ResNames[r]+" +8"; }
                case "monster": {
                    if (e.Power>=30){ e.Power-=25; int g=60+Random.Range(0,60); S.AddRes("gold",g); return "🐙 海怪来袭！舰队力战斩之（战力-25，金+"+g+"）"; }
                    e.Power-=40; return "🐙 海怪凶猛，舰队受损（战力-40）"; }
                case "storm": e.Supply=Mathf.Max(0,e.Supply-15); return "🌪️ 遭遇风暴，补给损失 15";
                case "wreck": { int g=50+Random.Range(0,71); S.AddRes("gold",g); return "💰 发现沉船宝藏，金+"+g; }
                case "current": Teleport(e); return "🌀 遭遇强劲洋流，被卷往他处";
                // ---- 太空 ----
                case "moon":
                    if (e.NodeUsed[idx]==0){ e.NodeUsed[idx]=1; return "🌙 抵达月球轨道，可「建立月球前哨」"; }
                    return "🌙 重返月球轨道，可建立/扩建前哨";
                case "mars":
                    if (e.NodeUsed[idx]==0){ e.NodeUsed[idx]=1; return "🔴 抵达火星轨道，可「建立火星前哨」（满进度即火星移民胜利）"; }
                    return "🔴 重返火星轨道";
                case "asteroid": {
                    if (Random.value<0.5f){ S.AddRes("titanium",8); return "☄️ 小行星带采矿，钛矿+8"; }
                    S.AddRes("helium3",6); return "☄️ 月壤小行星，氦-3 +6"; }
                case "gas": S.AddRes("fusion",10); return "🪐 气态巨星采集，聚变能+10";
                case "ruin": S.AddRes("research",30); return "🛸 发现外星遗迹，研究+30";
                case "meteor": e.Power=Mathf.Max(0,e.Power-20); return "💥 陨石撞击，飞船战力-20";
                case "radiation": e.Supply=Mathf.Max(0,e.Supply-15); return "☢️ 穿越辐射带，补给损失15";
                case "wormhole": Teleport(e); return "🌌 穿越虫洞，被抛至星图另一端";
                case "dyson": S.SpDyson=Mathf.Min(100,S.SpDyson+8); return "☀️ 抵达近恒星采集点，戴森云进度+8%";
            }
            return "";
        }

        private void Teleport(ExpeditionState e)
        {
            for (int t=0;t<30;t++)
            {
                int x=Random.Range(0,e.N), y=Random.Range(0,e.N);
                if (x==e.PosX&&y==e.PosY) continue;
                e.PosX=x;e.PosY=y;Reveal(e,x,y);return;
            }
        }

        /// <summary>当前格为港口时建立殖民地（对接 ColonizationSystem）</summary>
        public bool ColonizeHere()
        {
            var e=S.OceanExp; EnsureInit(e);
            if (CurrentNode(e)!="port"){ GM.AddEvent("bad","当前位置不是可殖民的良港"); return false; }
            float wx=(e.PosX-e.N/2)*30f, wz=(e.PosY-e.N/2)*30f;
            return GM.Colonization.FoundColony(wx,wz);
        }

        /// <summary>当前格为月球/火星时建立前哨，推进五大工程进度（对接既有火星胜利）</summary>
        public bool BuildOutpostHere()
        {
            var e=S.SpaceExp; EnsureInit(e);
            string k=CurrentNode(e);
            if (k=="moon")
            {
                if (S.GetRes("steel")<60||S.GetRes("fusion")<15){ GM.AddEvent("bad","建立月球前哨需 钢60 聚变15"); return false; }
                S.AddRes("steel",-60);S.AddRes("fusion",-15);
                S.SpLunar=Mathf.Min(100,S.SpLunar+25);
                e.NodeUsed[e.Idx(e.PosX,e.PosY)]=2;
                GM.AddEvent("good","🌙 月球前哨落成，月球基地进度 "+Mathf.RoundToInt(S.SpLunar)+"%");
                return true;
            }
            if (k=="mars")
            {
                if (S.GetRes("steel")<100||S.GetRes("fusion")<30){ GM.AddEvent("bad","建立火星前哨需 钢100 聚变30"); return false; }
                S.AddRes("steel",-100);S.AddRes("fusion",-30);
                S.SpMars=Mathf.Min(100,S.SpMars+20);
                e.NodeUsed[e.Idx(e.PosX,e.PosY)]=2;
                GM.AddEvent("good","🔴 火星前哨落成，火星移民进度 "+Mathf.RoundToInt(S.SpMars)+"%");
                if (S.SpMars>=100){ S.Victory=true; GM.AddEvent("good","🎉 火星移民成功！人类迈向星际！"); }
                return true;
            }
            GM.AddEvent("bad","当前星球无法建立前哨");
            return false;
        }

        // ===== UI 元数据 =====
        public static string NodeIcon(string k) => k switch
        {
            "port"=>"⚓","fish"=>"🐟","res"=>"📦","monster"=>"🐙","storm"=>"🌪️","wreck"=>"💰","current"=>"🌀",
            "moon"=>"🌙","mars"=>"🔴","asteroid"=>"☄️","gas"=>"🪐","ruin"=>"🛸","meteor"=>"💥",
            "radiation"=>"☢️","wormhole"=>"🌌","dyson"=>"☀️",_=>""
        };
        public static string NodeName(string k) => k switch
        {
            "port"=>"良港","fish"=>"鱼群","res"=>"特产","monster"=>"海怪","storm"=>"风暴","wreck"=>"沉船","current"=>"洋流",
            "moon"=>"月球","mars"=>"火星","asteroid"=>"小行星","gas"=>"气态巨星","ruin"=>"遗迹","meteor"=>"陨石",
            "radiation"=>"辐射","wormhole"=>"虫洞","dyson"=>"戴森云",_=>""
        };
    }
}

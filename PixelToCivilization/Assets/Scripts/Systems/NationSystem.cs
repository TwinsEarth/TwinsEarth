using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using PixelToCivilization.World;

namespace PixelToCivilization.Systems
{
    /// <summary>
    /// V6.1.3 天下分合系统：随游戏年份/朝代周期性在「列国并立(split，3~7 国)」与「大一统(unify，单一王朝)」间演化。
    /// 开局万邦并立（2~5 处聚落方国）；每阶段持续 160~340 游戏年：
    /// 分裂→统一：最强国（玩家强盛时即玩家）吞并余国，国号取当前朝代；
    /// 统一→分裂：旧势力于旧村址复国、不足则在新陆地立据点，凑够 3~7 国并立。
    /// </summary>
    public class NationSystem : GameSystemBase
    {
        // 势力旗帜调色板（玩家固定朱红 d9402f，AI 依次取色）
        public static readonly string[] FlagPalette =
            {"2fa3d9","8e5bd0","e08a2e","27ae89","d0447a","c9a227","4a6fd0","b0673a","6fa84a","9aa3b0"};
        // 诸侯国号池（上古至中古常见单字国号）
        static readonly string[] NamePool =
            {"夏","商","周","齐","鲁","晋","秦","楚","燕","韩","赵","魏","吴","越","宋","卫","郑","陈","蔡","曹",
             "蜀","梁","凉","代","虢","虞","许","邾","滕","薛","莒","邠","雍","冀","荆","扬","益","徐"};

        WorldGenerator _terrain;

        public override void Init(GameManager gm)
        {
            base.Init(gm);
            _terrain = Object.FindObjectOfType<WorldGenerator>();
        }

        /// <summary>开局按聚落中心建立方国（第 0 个为玩家「华夏」，其余为 AI 邻邦）</summary>
        public void InitNations(List<Vector3> centers)
        {
            S.Nations.Clear(); S.VillageX.Clear(); S.VillageZ.Clear();
            for (int i=0;i<centers.Count;i++)
            {
                var c=centers[i];
                S.VillageX.Add(c.x); S.VillageZ.Add(c.z);
                var n=new NationEntity
                {
                    Id=i, Cx=c.x, Cz=c.z, VillageId=i, IsPlayer=(i==0), Alive=true,
                    ContinentId = _terrain!=null ? Mathf.Max(1,_terrain.ContinentAt(c.x,c.z)) : 1,
                    Name = i==0 ? "华夏" : PickName(S.Nations),
                    ColorHex = i==0 ? "d9402f" : FlagPalette[((i-1)%FlagPalette.Length)],
                    Pop = i==0 ? S.Pop : Random.Range(24,64),
                };
                n.Power=n.Pop;
                S.Nations.Add(n);
            }
            S.PlayerNationId=0;
            S.WorldPhase="split";
            S.PhaseYearsLeft=Random.Range(160,240); // 第一波大一统约在 160~240 年后
            GM.AddEvent("info",$"天下万邦并立，共 {S.Nations.Count} 处聚落方国");
        }

        public override void OnYear(int year)
        {
            if (S.Nations==null || S.Nations.Count==0) return;
            // 各国人口/国力消长（玩家国跟随真实人口）
            foreach (var n in S.Nations)
            {
                if (!n.Alive) continue;
                if (n.IsPlayer) n.Pop=S.Pop;
                else n.Pop=Mathf.Max(6,n.Pop+Random.Range(-3,4));
                n.Power=n.Pop*(0.8f+Random.value*0.45f);
            }
            if (S.PhaseYearsLeft>0){ S.PhaseYearsLeft--; return; }
            if (S.WorldPhase=="split") Unify(); else Split();
        }

        // ---------- 大一统：最强国吞并余国（玩家存续时始终由玩家立朝，保证历史主角体验）----------
        void Unify()
        {
            NationEntity lead=null;
            foreach (var n in S.Nations) if (n.Alive && n.IsPlayer){lead=n;break;}
            if (lead==null){ float best=-1f; foreach (var n in S.Nations) if (n.Alive && n.Power>best){best=n.Power;lead=n;} }
            if (lead==null){ S.PhaseYearsLeft=Random.Range(180,280); return; }
            int absorbed=0, overseas=0;
            foreach (var n in S.Nations) if (n.Alive && n!=lead)
            {
                // V6.1.7 大航海前只能统一本大陆，隔海大陆保持独立发展；航海后方可四海归一
                if(!S.AgeOfSail && n.ContinentId!=lead.ContinentId){ overseas++; continue; }
                n.Alive=false; n.Note=lead.Name; absorbed++;
            }
            lead.Name=UnifiedDynastyName();
            S.WorldPhase="unify";
            S.PhaseYearsLeft=Random.Range(220,340);
            string who=lead.IsPlayer?"我朝":lead.Name;
            string tail = overseas>0?$"，另有 {overseas} 国远隔重洋、各自为政":"";
            GM.AddEvent("good",$"🏛️ {who}完成大一统（吞并 {absorbed} 国），国号「{lead.Name}」"+tail);
        }

        // ---------- 分裂：复国 + 新立，凑够 3~7 国 ----------
        void Split()
        {
            // 原统一王朝回到诸侯身份（玩家固定华夏，AI 换诸侯名）
            foreach (var n in S.Nations)
                if (n.Alive) n.Name = n.IsPlayer ? "华夏" : PickName(S.Nations);

            int target=Random.Range(3,8); // 3..7
            // 1) 旧国于旧村址复国
            foreach (var n in S.Nations)
            {
                if (AliveCount()>=target) break;
                if (!n.Alive)
                {   // 旧国于旧村址复国（村落建筑始终保留，仅恢复政权与旗号）
                    n.Alive=true; n.Name=PickName(S.Nations); n.ColorHex=PickColor();
                    n.Pop=Random.Range(20,70); n.Power=n.Pop;
                }
            }
            // 2) 仍不足：在无人村址/新陆地新立方国
            int guard=0;
            while (AliveCount()<target && guard++<60)
            {
                if (!FindOutpostSpot(out float x,out float z)) break;
                int id=S.Nations.Count;
                string hex=PickColor();
                InitialSettlementBuilder.BuildOutpost(GM,_terrain,new Vector3(x,0,z),hex,S.VillageX.Count);
                S.VillageX.Add(x); S.VillageZ.Add(z);
                var n=new NationEntity{Id=id,Cx=x,Cz=z,VillageId=S.VillageX.Count-1,Alive=true,
                    ContinentId=Mathf.Max(1,_terrain.ContinentAt(x,z)),
                    Name=PickName(S.Nations),ColorHex=hex,Pop=Random.Range(18,56)};
                n.Power=n.Pop; S.Nations.Add(n);
            }
            S.WorldPhase="split";
            S.PhaseYearsLeft=Random.Range(180,280);
            GM.AddEvent("bad",$"⚔️ 天下分崩，群雄并起，共 {AliveCount()} 国割据");
        }

        /// <summary>找一个距所有现存国都足够远的陆地村址</summary>
        bool FindOutpostSpot(out float x,out float z)
        {
            x=0;z=0;
            if (_terrain==null) return false;
            for (int t=0;t<40;t++)
            {
                if (!_terrain.RandomLandPoint(out x,out z)) continue;
                if (_terrain.BiomeAt(x,z)==BiomeKind.Desert) continue;
                if (_terrain.HeightAt(x,z)>3.4f) continue;
                bool ok=true;
                foreach (var n in S.Nations)
                    if (n.Alive && (n.Cx-x)*(n.Cx-x)+(n.Cz-z)*(n.Cz-z)<58f*58f){ok=false;break;}
                if (ok) return true;
            }
            return false;
        }

        int AliveCount(){ int c=0; foreach(var n in S.Nations) if(n.Alive)c++; return c; }

        string UnifiedDynastyName()
        {
            var dn=GM.Time!=null?GM.Time.DynastyName:"王朝";
            if (string.IsNullOrEmpty(dn)) return "中央王朝";
            if (dn.Contains("三皇")||dn.Contains("五帝")) return "炎黄联盟";
            return dn;
        }

        // 未被使用的诸侯国号
        string PickName(List<NationEntity> list)
        {
            var used=new HashSet<string>();
            foreach (var n in list) if(n!=null&&!string.IsNullOrEmpty(n.Name))used.Add(n.Name);
            var pool=new List<string>(NamePool);
            for (int i=pool.Count-1;i>0;i--){int j=Random.Range(0,i+1);(pool[i],pool[j])=(pool[j],pool[i]);}
            foreach (var p in pool) if(!used.Contains(p))return p;
            return "方国"+Random.Range(10,99);
        }
        // 未被现存国占用的旗帜色
        string PickColor()
        {
            var used=new HashSet<string>();
            foreach (var n in S.Nations) if(n.Alive)used.Add(n.ColorHex);
            foreach (var c in FlagPalette) if(!used.Contains(c))return c;
            return FlagPalette[Random.Range(0,FlagPalette.Length)];
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using PixelToCivilization.Core;
using Random = UnityEngine.Random; // 消除 System.Random 与 UnityEngine.Random 歧义

namespace PixelToCivilization.AI
{
    /// <summary>九神定义（对齐 v5.9.9 GOD_DEFS）</summary>
    public class GodDef
    {
        public string Id, Name, Domain, Desc; public long Color;
        public GodDef(string id,string n,string domain,long color,string desc)
        { Id=id;Name=n;Domain=domain;Color=color;Desc=desc; }
    }

    /// <summary>
    /// 九神多智能体 —— 对齐 v5.9.9：皇天/神农/嫘祖/蚩尤/公输班/屈原/关羽/妈祖/太上老君，
    /// 每5~15秒每位神30%概率自主决策，影响天命/农业/纺织/战争/工匠/文化/武运/海洋/科技。
    /// </summary>
    public class GodsSystem : GameSystemBase
    {
        public readonly List<GodDef> Gods = new()
        {
            new("huangtian","皇天上帝","天命",0xFFD700,"王朝兴衰"),
            new("shennong","神农","农业",0x4CAF50,"五谷丰登"),
            new("leizu","嫘祖","纺织",0xE91E63,"蚕桑丝织"),
            new("chiyou","蚩尤","战争",0xF44336,"兵主战神"),
            new("gongshu","公输班","工匠",0xFF9800,"百工之祖"),
            new("quyuan","屈原","文化",0x9C27B0,"文曲星"),
            new("guanyu","关羽","武运",0x795548,"武圣"),
            new("mazu","妈祖","海洋",0x00BCD4,"海神"),
            new("taishang","太上老君","科技",0x607D8B,"炼丹术数"),
        };
        public Dictionary<string,int> LastDecision { get; } = new();
        private float _timer;

        public override void Init(GameManager gm){ base.Init(gm); foreach (var g in Gods) LastDecision[g.Id]=0; }

        public override void Tick(float dt)
        {
            _timer-=dt;
            if (_timer>0) return;
            _timer=5+Random.value*10;
            foreach (var god in Gods) if (Random.value<0.3f) MakeDecision(god.Id);
        }

        public void MakeDecision(string godId)
        {
            switch (godId)
            {
                case "huangtian":
                    if (S.DynastyMorale<30) S.DynastyMorale=60+Random.value*30f;
                    else S.DynastyMorale=Mathf.Min(100,S.DynastyMorale+5);
                    break;
                case "shennong": S.AddRes("food",30); GM.AddEvent("good","🌾 神农显灵，粮食丰收+30"); break;
                case "leizu": S.AddRes("goods",10); GM.AddEvent("good","🧵 嫘祖赐福，丝织品+10"); break;
                case "chiyou":
                    if (S.Era>=1 && !S.WarActive && Random.value<0.4f) { S.WarActive=true; GM.AddEvent("bad","⚔️ 蚩尤降战，烽烟再起"); }
                    else S.MilFirepower+=2;
                    break;
                case "gongshu": GM.AddEvent("good","🔨 公输班托梦，建造效率提升"); break;
                case "quyuan": S.AddRes("culture",20); GM.AddEvent("good","📜 屈原显圣，文化+20"); break;
                case "guanyu": S.MilDefense+=5; GM.AddEvent("good","⚔️ 关羽显灵，国防+5"); break;
                case "mazu":
                    if (S.Era>=4){ S.AddRes("gold",20); GM.AddEvent("good","🌊 妈祖保佑，海上贸易+20金"); }
                    break;
                case "taishang": S.AddRes("research",15); GM.AddEvent("good","🔮 太上老君显灵，科技+15"); break;
            }
            LastDecision[godId]=S.Year;
        }
    }
}

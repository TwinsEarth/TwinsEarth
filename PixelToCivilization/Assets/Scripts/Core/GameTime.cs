using System;
using UnityEngine;
using PixelToCivilization.Data;

namespace PixelToCivilization.Core
{
    /// <summary>
    /// 游戏时间系统 —— 对齐 v5.9.9：游戏年份连续递增，朝代/时代/公历全部由年份换算。
    /// 1倍速下 dt*365 累加到 day，day满365为1年。
    /// </summary>
    public class GameTime : MonoBehaviour
    {
        public GameState State;
        public System.Collections.Generic.List<EraDefinition> Eras;
        public System.Collections.Generic.List<DynastyDefinition> Dynasties;

        public event Action<int> OnYearAdvanced;          // 进入新游戏年份
        public event Action<int, int> OnDynastyChanged;   // (新idx,游戏年)
        public event Action<int, int> OnEraChanged;       // (新era,旧era)

        public void Init(GameState s,
            System.Collections.Generic.List<EraDefinition> eras,
            System.Collections.Generic.List<DynastyDefinition> dyn)
        {
            State = s; Eras = eras; Dynasties = dyn;
        }

        /// <summary>每帧推进（dt已乘速度）</summary>
        public void Tick(float dt)
        {
            if (State == null || !State.Running || State.Paused) return;

            State.Day += dt * GameConstants.DaySeconds;
            // 高倍速下可能一次跨年多次
            int guard = 0;
            while (State.Day >= GameConstants.YearDays && guard++ < 2000)
            {
                State.Day -= GameConstants.YearDays;
                State.Year++;
                AccumulateCryo();   // V6.1.9 加速累计年数，满100年触发冷冻
                AdvanceYear();
            }
            RecomputeDynastyEra();
        }

        /// <summary>V6.1.9 仅在玩家加速(倍速>1)正常推进时累计游戏年；满阈值进入冷冻冷却。
        /// Debug 跳年 JumpToYear 不经过本方法，故压测/跳朝代不会误触冷冻。冷冻期间不再累计，解冻时由外部清零。</summary>
        private void AccumulateCryo()
        {
            if (State.CryoActive || State.Speed <= 1f) return;
            State.CryoAccumYears += 1f;
            if (State.CryoAccumYears >= GameConstants.CryoYearThreshold)
            {
                State.CryoActive = true;
                State.CryoRemainSec = GameConstants.CryoCooldownSec;
            }
        }

        /// <summary>按年份重算朝代与时代（年份连续递增，不跳年）</summary>
        private void RecomputeDynastyEra()
        {
            int newDyn = DynastyDatabase.GetIndexByYear(State.Year, Dynasties);
            if (newDyn != State.DynastyIdx)
            {
                State.DynastyIdx = newDyn;
                State.DynastyMorale = 60 + UnityEngine.Random.value * 30f;
                OnDynastyChanged?.Invoke(newDyn, State.Year);
            }
            int newEra = DynastyDatabase.GetEraByYear(State.Year, Eras);
            if (newEra != State.Era)
            {
                int old = State.Era;
                State.Era = newEra;
                OnEraChanged?.Invoke(newEra, old);
            }
        }

        /// <summary>每过一游戏年触发（建筑老化、人口增长等由各系统订阅）</summary>
        private void AdvanceYear() => OnYearAdvanced?.Invoke(State.Year);

        // ===== 显示换算 =====
        public DynastyDefinition CurrentDynasty =>
            Dynasties != null && State.DynastyIdx < Dynasties.Count ? Dynasties[State.DynastyIdx] : null;
        public EraDefinition CurrentEra =>
            Eras != null && State.Era < Eras.Count ? Eras[State.Era] : null;

        public int GregorianYear => DynastyDatabase.GetGregorian(State.Year, CurrentDynasty);
        public string GregorianText => DynastyDatabase.FormatGregorian(GregorianYear);
        public string DynastyName => CurrentDynasty?.Name ?? "三皇五帝";
        public string EraName => CurrentEra?.Name ?? "";

        /// <summary>年内进度 0-1（用于昼夜/季节）</summary>
        public float YearProgress => State.Day / GameConstants.YearDays;

        /// <summary>Debug 跳到指定游戏年：逐年补发 OnYearAdvanced，使人口/经济年结/天下分合/地图延展/历史事件/灾害全部补算，最后重算朝代时代</summary>
        private void JumpToYear(int target)
        {
            int guard=0;
            while (State.Year < target && guard++ < 12000)
            {
                State.Year++;
                AdvanceYear();
            }
            State.Day=0;
            RecomputeDynastyEra();
        }

        /// <summary>V6.1.2 Debug：跳到下一朝代起始年（自动联动时代，并逐年补结，不再直接改年份跳过年结）</summary>
        public bool DebugNextDynasty()
        {
            int next=State.DynastyIdx+1;
            if(Dynasties==null||next>=Dynasties.Count) return false;
            JumpToYear(Dynasties[next].YearStart);return true;
        }
        /// <summary>V6.1.2 Debug：推进到下一时代起始年（逐年补结）</summary>
        public bool DebugAdvanceEra()
        {
            int next=State.Era+1;
            if(Eras==null||next>=Eras.Count) return false;
            JumpToYear(Eras[next].StartYear);return true;
        }
        /// <summary>V6.1.8 公开跳年（万年存续压测用，逐年补结所有系统含九神 SafetyNet）</summary>
        public void DebugJumpTo(int target){ if(target>State.Year) JumpToYear(target); RecomputeDynastyEra(); }
    }
}

using UnityEngine;
using PixelToCivilization.Data;

namespace PixelToCivilization.Core
{
    /// <summary>所有游戏子系统基类：统一持有状态与数据库，按帧/按年Tick</summary>
    public abstract class GameSystemBase : MonoBehaviour
    {
        protected GameManager GM;
        protected GameState S => GM.State;
        protected System.Collections.Generic.List<EraDefinition> Eras => GM.Eras;
        protected System.Collections.Generic.List<DynastyDefinition> Dynasties => GM.Dynasties;

        public virtual void Init(GameManager gm) { GM = gm; }
        /// <summary>每帧（dt已乘游戏速度）</summary>
        public virtual void Tick(float dt) { }
        /// <summary>每过一个游戏年</summary>
        public virtual void OnYear(int year) { }
        /// <summary>时代切换</summary>
        public virtual void OnEra(int newEra, int oldEra) { }
        /// <summary>朝代更迭</summary>
        public virtual void OnDynasty(int idx, int year) { }
    }
}

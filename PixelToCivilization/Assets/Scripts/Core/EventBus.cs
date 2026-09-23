using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelToCivilization.Core
{
    /// <summary>
    /// 全局事件总线 - 解耦各系统间通信
    /// </summary>
    public class EventBus : MonoBehaviour
    {
        public static EventBus Instance { get; private set; }

        private readonly Dictionary<string, Delegate> _events = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Subscribe<T>(string eventName, Action<T> handler)
        {
            if (_events.TryGetValue(eventName, out var del))
                _events[eventName] = Delegate.Combine(del, handler);
            else
                _events[eventName] = handler;
        }

        public void Subscribe(string eventName, Action handler)
        {
            if (_events.TryGetValue(eventName, out var del))
                _events[eventName] = Delegate.Combine(del, handler);
            else
                _events[eventName] = handler;
        }

        public void Unsubscribe<T>(string eventName, Action<T> handler)
        {
            if (_events.TryGetValue(eventName, out var del))
            {
                var newDel = Delegate.Remove(del, handler);
                if (newDel == null) _events.Remove(eventName);
                else _events[eventName] = newDel;
            }
        }

        public void Unsubscribe(string eventName, Action handler)
        {
            if (_events.TryGetValue(eventName, out var del))
            {
                var newDel = Delegate.Remove(del, handler);
                if (newDel == null) _events.Remove(eventName);
                else _events[eventName] = newDel;
            }
        }

        public void Publish<T>(string eventName, T data)
        {
            if (_events.TryGetValue(eventName, out var del))
                (del as Action<T>)?.Invoke(data);
        }

        public void Publish(string eventName)
        {
            if (_events.TryGetValue(eventName, out var del))
                (del as Action)?.Invoke();
        }

        // 预定义事件名
        public const string EraChanged = "EraChanged";
        public const string DynastyChanged = "DynastyChanged";
        public const string TechUnlocked = "TechUnlocked";
        public const string BuildingBuilt = "BuildingBuilt";
        public const string PopulationChanged = "PopulationChanged";
        public const string WarDeclared = "WarDeclared";
        public const string DisasterOccurred = "DisasterOccurred";
        public const string YearPassed = "YearPassed";
        public const string GameWon = "GameWon";
    }
}

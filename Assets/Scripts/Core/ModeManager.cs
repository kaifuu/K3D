using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    public struct ModeEntry { public string Id; public string Title; public Func<DrillMode> Factory; }

    /// <summary>
    /// 模式目录与切换:销毁旧 ModeRoot → 清状态 → 新模式 Build。
    /// Current==null 表示处于主菜单。
    /// </summary>
    public static class ModeManager
    {
        static readonly List<ModeEntry> catalog = new List<ModeEntry>();
        public static IReadOnlyList<ModeEntry> Catalog => catalog;
        public static DrillMode Current { get; private set; }
        public static bool InMenu => Current == null;

        public static void Register(string id, string title, Func<DrillMode> factory)
        {
            catalog.RemoveAll(e => e.Id == id);
            catalog.Add(new ModeEntry { Id = id, Title = title, Factory = factory });
        }

        public static void Enter(string id, ModeStartParams p = null)
        {
            int idx = catalog.FindIndex(c => c.Id == id);
            if (idx < 0) { Debug.LogError($"[ModeManager] 未知模式 {id}"); return; }
            ExitToMenu();

            var root = new GameObject("ModeRoot").transform;
            var mode = catalog[idx].Factory();
            mode.Ctx = new DrillContext { ModeRoot = root, Params = p ?? new ModeStartParams() };

            Current = mode;
            DrillClock.Stop();
            GameState.Reset();
            EventBus.Clear();
            WindField.Configure(new Vector3(1f, 0f, 0.35f), mode.Ctx.Params.WindMps);   // 全模式统一风场
            mode.Build();
            EnvironmentRig.Install(mode.Ctx).ApplyParams(mode.Ctx.Params);   // 全模式统一昼夜/天气
            EventBus.Publish("模式", id, $"进入演练模式:{catalog[idx].Title}", EventGrade.Op);

            if (mode.Ctx.Params.AutoStart)
            {
                StartDrill();
#if UNITY_EDITOR
                mode.RunHeadlessScenario(HeadlessBridge.Scenario);
#endif
            }
        }

        public static void StartDrill()
        {
            if (Current == null || DrillClock.State != PlayState.Setup) return;
            DrillClock.Start();
            Current.OnStart();
        }

        public static void StopDrill()
        {
            if (Current == null) return;
            Current.OnStop();
            DrillClock.Stop();
            EventBus.Publish("演练", "", "演练已停止", EventGrade.Op);
        }

        public static void ExitToMenu()
        {
            if (Current == null) return;
            Current.OnStop();
            DrillClock.Stop();
            if (Current.Ctx?.ModeRoot != null) UnityEngine.Object.Destroy(Current.Ctx.ModeRoot.gameObject);
            Current = null;
        }
    }
}

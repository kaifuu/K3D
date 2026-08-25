#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 无头参数桥:SimRunner(编辑器侧)经 SessionState(跨域重载)传参,
    /// PlatformBoot 启动时读取并直进指定模式。
    /// </summary>
    public static class HeadlessBridge
    {
        const string kMode = "Headless.Mode";
        const string kScenario = "Headless.Scenario";

        public static void SetHeadless(string mode, string scenario)
        {
            SessionState.SetString(kMode, mode ?? "");
            SessionState.SetString(kScenario, scenario ?? "");
        }

        public static string Mode => SessionState.GetString(kMode, "");
        public static string Scenario => SessionState.GetString(kScenario, "");
        public static bool HasHeadless => !string.IsNullOrEmpty(Mode);

        /// <summary>PlatformBoot.Start 调用:无头模式直进,跳过主菜单等待</summary>
        public static void TryAutoEnter()
        {
            if (!HasHeadless) return;
            var p = new ModeStartParams { AutoStart = true };
            ModeManager.Enter(Mode, p);
        }

        public static void Clear()
        {
            SessionState.SetString(kMode, "");
            SessionState.SetString(kScenario, "");
        }
    }
}
#endif

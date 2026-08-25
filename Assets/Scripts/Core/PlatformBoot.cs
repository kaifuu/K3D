using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 平台入口(场景 Boot 物体上唯一常驻组件):
    /// 搭建内核服务与常驻 UI,每帧驱动演练时钟与当前模式。
    /// </summary>
    public class PlatformBoot : MonoBehaviour
    {
        public static PlatformBoot I { get; private set; }

        void OnEnable() => I = this;   // 域重载防御

        void Start()
        {
            var svc = new GameObject("~Services");
            svc.AddComponent<FXManager>();
            svc.AddComponent<ScenarioRunner>();
            svc.AddComponent<ReplayService>();
            svc.AddComponent<ReplayPlayer>();

            var ui = new GameObject("~UIRoot");
            ui.AddComponent<UIRoot>();

            ModeRegistration.RegisterAll();

#if UNITY_EDITOR
            HeadlessBridge.TryAutoEnter();
#endif
        }

        void Update()
        {
            DrillClock.Tick();
            if (DrillClock.CanSimulate) ModeManager.Current?.OnTick(Time.deltaTime);
        }
    }
}

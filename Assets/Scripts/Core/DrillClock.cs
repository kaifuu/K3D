using System;
using UnityEngine;

namespace DroneSim
{
    public enum PlayState { Setup, Running, Paused, Replaying }

    /// <summary>
    /// 演练时钟:全平台唯一时间源。开始/暂停/倍速/停止/回放状态机,
    /// 通过 Time.timeScale 冻结或加速全部模拟(AI/物理/粒子/协程自然跟随),
    /// 暂停时相机等表现层改用 unscaledDeltaTime 不冻结。
    /// </summary>
    public static class DrillClock
    {
        public static PlayState State { get; private set; } = PlayState.Setup;
        public static float Speed { get; private set; } = 1f;
        public static float SimTime { get; private set; }
        /// <summary>一切模拟逻辑(AI/输入/生成/飞行)的唯一门禁</summary>
        public static bool CanSimulate => State == PlayState.Running;
        public static bool InReplay => State == PlayState.Replaying;

        public static event Action<PlayState> StateChanged;

        public static void Start() { SimTime = 0f; Set(PlayState.Running); }
        public static void Pause() { if (State == PlayState.Running) Set(PlayState.Paused); }
        public static void Resume() { if (State == PlayState.Paused) Set(PlayState.Running); }
        public static void Stop() => Set(PlayState.Setup);

        public static void SetSpeed(float s)
        {
            var v = Mathf.Clamp(s, 0.25f, 4f);
            if (Mathf.Approximately(v, Speed)) return;
            Speed = v;
        }

        public static void EnterReplay() { if (State != PlayState.Setup) Set(PlayState.Replaying); }
        public static void ExitReplay() { if (State == PlayState.Replaying) Set(PlayState.Paused); }

        /// <summary>仅 PlatformBoot 调用:同步 timeScale 并在运行态累计演练时间</summary>
        internal static void Tick()
        {
            float target = (State == PlayState.Running || State == PlayState.Replaying) ? Speed : 0f;
            if (!Mathf.Approximately(Time.timeScale, target)) Time.timeScale = target;
            if (State == PlayState.Running) SimTime += Time.deltaTime;
        }

        static void Set(PlayState s)
        {
            if (State == s) return;
            State = s;
            try { StateChanged?.Invoke(s); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}

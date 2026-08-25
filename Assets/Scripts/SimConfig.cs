using UnityEngine;

namespace DroneSim
{
    public enum ThreatLevel { Watch = 1, Warn = 2, Threat = 3 }

    /// <summary>全局参数</summary>
    public static class SimConfig
    {
        public const float NoFlyRadius = 60f;      // 核心禁飞区
        public const float WarningRadius = 120f;   // 预警区
        public const float PerimeterRadius = 200f; // 巡逻警戒圈
        public const float RadarRange = 420f;      // 雷达探测距离
        public const float CounterRange = 260f;    // 反制手段作用距离
        public const float SpawnRadius = 300f;     // 目标生成距离
    }

    /// <summary>
    /// 监管反制判分(P11 实例化):每个推演会话一份,由 GameState.Reset() 重建。
    /// </summary>
    public class RegulatorScore
    {
        public int Wave;
        public int Score;
        public float FacilityIntegrity = 100f;
        public int NeutralizedJam, NeutralizedNet, NeutralizedLaser, TurnedBack, Breaches, BlockNeutralized;
        public bool AutoDefend = true;
        public bool FacilityDown;
        public EnemyDrone Selected;

        public void Reset()
        {
            Wave = 0; Score = 0; FacilityIntegrity = 100f;
            NeutralizedJam = NeutralizedNet = NeutralizedLaser = TurnedBack = Breaches = BlockNeutralized = 0;
            AutoDefend = true; FacilityDown = false; Selected = null;
            SimEvents.Clear();
        }

        public void OnTurnedBack(EnemyDrone d)
        { Score += 60; TurnedBack++; SimEvents.Add($"[监管] {d.DroneId} 收到RemoteID警告后调头离场 (+60)"); }

        public void OnJamLanded(EnemyDrone d)
        { Score += 80; NeutralizedJam++; SimEvents.Add($"[反制] {d.DroneId} 被电磁干扰迫降,处置完成 (+80)"); }

        public void OnNetCaptured(EnemyDrone d)
        { Score += 100; NeutralizedNet++; SimEvents.Add($"[反制] {d.DroneId} 被捕获网拦截捕获 (+100)"); }

        public void OnLaserKilled(EnemyDrone d)
        {
            NeutralizedLaser++;
            if (d.KillViolation) { Score -= 50; SimEvents.Add($"[违规] {d.DroneId} 为合规无人机,误击违规! (-50)", EventGrade.Warn); }
            else { Score += 70; SimEvents.Add($"[反制] {d.DroneId} 被激光武器硬摧毁 (+70)"); }
        }

        public void OnBreach(EnemyDrone d)
        {
            Score -= 150; Breaches++;
            FacilityIntegrity = Mathf.Max(0f, FacilityIntegrity - 10f);
            SimEvents.Add($"[突破] {d.DroneId} 闯入核心区引爆! 设施完整度-10 (-150)", EventGrade.Critical);
            if (FacilityIntegrity <= 0f && !FacilityDown)
            { FacilityDown = true; SimEvents.Add("[推演] 核心设施损毁,防御失败! 停止生成新目标", EventGrade.Critical); }
        }

        public void OnBlockNeutralized(int n)
        { Score += 60 * n; SimEvents.Add($"[反制] 区域信号阻断:{n} 机同时失联迫降 (+{60 * n})", EventGrade.Op); }
    }

    /// <summary>
    /// 判分状态外观(P11 起转发到 RegulatorScore 实例;模式进入时 ModeManager 调 Reset 重建)。
    /// 保留静态调用形态,存量代码(EnemyDrone/CounterUnit/RegulatorMode/SimRunner)不改。
    /// </summary>
    public static class GameState
    {
        public static RegulatorScore Current { get; private set; }

        public static int Wave { get => Current?.Wave ?? 0; set { if (Current != null) Current.Wave = value; } }
        public static int Score { get => Current?.Score ?? 0; set { if (Current != null) Current.Score = value; } }
        public static float FacilityIntegrity => Current?.FacilityIntegrity ?? 100f;
        public static int NeutralizedJam => Current?.NeutralizedJam ?? 0;
        public static int NeutralizedNet => Current?.NeutralizedNet ?? 0;
        public static int NeutralizedLaser => Current?.NeutralizedLaser ?? 0;
        public static int TurnedBack => Current?.TurnedBack ?? 0;
        public static int Breaches => Current?.Breaches ?? 0;
        public static int BlockNeutralized => Current?.BlockNeutralized ?? 0;
        public static bool AutoDefend { get => Current?.AutoDefend ?? true; set { if (Current != null) Current.AutoDefend = value; } }
        public static bool FacilityDown => Current?.FacilityDown ?? false;
        public static EnemyDrone Selected { get => Current?.Selected; set { if (Current != null) Current.Selected = value; } }

        /// <summary>重置判分状态:每次进入推演重建实例(模式进入时 ModeManager 自动调用)</summary>
        public static void Reset()
        {
            Current = new RegulatorScore();
        }

        public static void OnTurnedBack(EnemyDrone d) => Current?.OnTurnedBack(d);
        public static void OnJamLanded(EnemyDrone d) => Current?.OnJamLanded(d);
        public static void OnNetCaptured(EnemyDrone d) => Current?.OnNetCaptured(d);
        public static void OnLaserKilled(EnemyDrone d) => Current?.OnLaserKilled(d);
        public static void OnBreach(EnemyDrone d) => Current?.OnBreach(d);
        public static void OnBlockNeutralized(int n) => Current?.OnBlockNeutralized(n);
    }

    /// <summary>事件日志兼容桥:新代码直接用 EventBus.Publish(category, subjectId, msg, grade)</summary>
    public static class SimEvents
    {
        public static void Clear() => EventBus.Clear();
        public static void Add(string msg, EventGrade grade = EventGrade.Info) =>
            EventBus.Publish("演练", "", msg, grade);
    }
}

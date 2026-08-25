using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    public struct ReplaySample
    {
        public Vector3 Pos;
        public Quaternion Rot;
        public float Rpm01;
        public float Speed;
    }

    public class ReplayFrame
    {
        public float T;                    // 演练时刻 s
        public string[] Names;             // 本帧采样的机体名(与 Samples 对齐)
        public ReplaySample[] Samples;
    }

    /// <summary>
    /// 复盘记录服务(模块9):Running 期 10Hz 采样全部 FlightBody
    /// (FlightBody.OnEnable 自动登记),内存滚动上限 15 分钟;
    /// 进入回放后由 ReplayPlayer 按帧插值摆布机体。
    /// </summary>
    public class ReplayService : MonoBehaviour
    {
        public static ReplayService I { get; private set; }
        public const float SampleHz = 10f;
        const float kMaxSec = 900f;

        static readonly List<FlightBody> tracked = new List<FlightBody>(32);
        readonly List<ReplayFrame> frames = new List<ReplayFrame>(4096);
        float sampleAcc;

        public bool HasData => frames.Count > 1;
        public float Duration => frames.Count > 0 ? frames[frames.Count - 1].T : 0f;
        public int FrameCount => frames.Count;
        public IReadOnlyList<ReplayFrame> Frames => frames;
        public static IReadOnlyList<FlightBody> Tracked => tracked;

        void OnEnable()
        {
            I = this;   // 域重载防御
            DrillClock.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            DrillClock.StateChanged -= OnStateChanged;
            if (I == this) I = null;
        }

        void OnStateChanged(PlayState s)
        {
            if (s == PlayState.Setup) { frames.Clear(); sampleAcc = 0f; }   // 新演练重新记录
        }

        public static void Track(FlightBody b)
        {
            if (b != null && !tracked.Contains(b)) tracked.Add(b);
        }

        public static void Untrack(FlightBody b) => tracked.Remove(b);

        void Update()
        {
            if (!DrillClock.CanSimulate) return;
            sampleAcc += Time.deltaTime;                       // timeScale 已含倍速 → 按"演练秒"采样
            if (sampleAcc < 1f / SampleHz) return;
            sampleAcc = 0f;

            if (frames.Count > SampleHz * kMaxSec) frames.RemoveAt(0);   // 滚动窗口

            int n = 0;
            for (int i = 0; i < tracked.Count; i++) if (tracked[i] != null) n++;
            if (n == 0) return;

            var f = new ReplayFrame
            {
                T = DrillClock.SimTime,
                Names = new string[n],
                Samples = new ReplaySample[n]
            };
            int k = 0;
            for (int i = 0; i < tracked.Count; i++)
            {
                var b = tracked[i];
                if (b == null) continue;
                f.Names[k] = b.name;
                f.Samples[k] = new ReplaySample
                {
                    Pos = b.transform.position,
                    Rot = b.transform.rotation,
                    Rpm01 = b.Rpm01,
                    Speed = b.Speed
                };
                k++;
            }
            frames.Add(f);
        }
    }
}

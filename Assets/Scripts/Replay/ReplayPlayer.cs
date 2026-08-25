using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 回放驱动器(模块9):进入回放后全部模拟自然冻结(CanSimulate=false),
    /// 由采样帧插值直接摆布机体位姿/旋翼转速;退出回放恢复进入时刻的实况位姿。
    /// 支持任意时刻 Seek、播放/暂停/单步;回放速度跟随全局倍速。
    /// </summary>
    public class ReplayPlayer : MonoBehaviour
    {
        public static ReplayPlayer I { get; private set; }

        public float Cursor;          // 回放头(演练秒)
        public bool Playing = true;

        public float Duration => ReplayService.I != null ? ReplayService.I.Duration : 0f;
        public int FrameCount => ReplayService.I != null ? ReplayService.I.FrameCount : 0;

        struct LivePose { public Transform Tr; public Vector3 Pos; public Quaternion Rot; }
        readonly List<LivePose> livePoses = new List<LivePose>(16);
        readonly Dictionary<string, FlightBody> byName = new Dictionary<string, FlightBody>(16);

        void OnEnable() => I = this;   // 域重载防御

        public void Enter()
        {
            var rs = ReplayService.I;
            if (rs == null || !rs.HasData) return;
            if (DrillClock.State == PlayState.Setup || DrillClock.InReplay) return;

            livePoses.Clear();
            byName.Clear();
            foreach (var b in ReplayService.Tracked)
            {
                if (b == null) continue;
                livePoses.Add(new LivePose { Tr = b.transform, Pos = b.transform.position, Rot = b.transform.rotation });
                byName[b.name] = b;
            }
            Cursor = Mathf.Min(DrillClock.SimTime, rs.Duration);
            Playing = true;
            DrillClock.EnterReplay();
            TrajectoryDrawer.BuildAll();
            TrajectoryDrawer.UpdateBright(Cursor);
            EventBus.Publish("复盘", "replay", $"进入回溯复盘(已记录 {rs.FrameCount} 帧 / {rs.Duration:0.0}s)", EventGrade.Op);
        }

        public void Exit()
        {
            if (!DrillClock.InReplay) return;
            for (int i = 0; i < livePoses.Count; i++)
                if (livePoses[i].Tr != null)
                    livePoses[i].Tr.SetPositionAndRotation(livePoses[i].Pos, livePoses[i].Rot);
            TrajectoryDrawer.Clear();
            DrillClock.ExitReplay();   // → Paused,可继续实况
            EventBus.Publish("复盘", "replay", "退出回放,已恢复演练实况位姿", EventGrade.Op);
        }

        public void Seek(float t)
        {
            Cursor = Mathf.Clamp(t, 0f, Duration);
            ApplyAt(Cursor);
            TrajectoryDrawer.UpdateBright(Cursor);
        }

        public void Step(float deltaSec)
        {
            Playing = false;
            Seek(Cursor + deltaSec);
        }

        void Update()
        {
            if (!DrillClock.InReplay) return;
            if (Playing)
            {
                Cursor += Time.deltaTime * DrillClock.Speed;
                if (Cursor >= Duration) { Cursor = Duration; Playing = false; }
                ApplyAt(Cursor);
                TrajectoryDrawer.UpdateBright(Cursor);
            }
        }

        void ApplyAt(float t)
        {
            var frames = ReplayService.I != null ? ReplayService.I.Frames : null;
            if (frames == null || frames.Count < 2) return;

            if (t <= frames[0].T) { ApplyFrame(frames[0], frames[0], 0f); return; }
            int last = frames.Count - 1;
            if (t >= frames[last].T) { ApplyFrame(frames[last], frames[last], 0f); return; }

            int lo = 0, hi = last - 1;                      // 二分:frames[lo].T <= t < frames[lo+1].T
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (frames[mid].T <= t) lo = mid; else hi = mid - 1;
            }
            var a = frames[lo];
            var b = frames[lo + 1];
            ApplyFrame(a, b, (t - a.T) / Mathf.Max(1e-4f, b.T - a.T));
        }

        void ApplyFrame(ReplayFrame a, ReplayFrame b, float u)
        {
            for (int i = 0; i < a.Names.Length; i++)
            {
                if (!byName.TryGetValue(a.Names[i], out var body) || body == null) continue;
                var sa = a.Samples[i];
                var sb = sa;
                if (b != a)
                {
                    int j = i < b.Names.Length && b.Names[i] == a.Names[i] ? i : -1;
                    if (j < 0)
                        for (int q = 0; q < b.Names.Length; q++)
                            if (b.Names[q] == a.Names[i]) { j = q; break; }
                    if (j >= 0) sb = b.Samples[j];
                    else u = 0f;
                }
                body.transform.SetPositionAndRotation(
                    Vector3.Lerp(sa.Pos, sb.Pos, u), Quaternion.Slerp(sa.Rot, sb.Rot, u));
                var rot = body.GetComponent<RotorSpin>();
                if (rot != null) rot.SetRpm(Mathf.Lerp(sa.Rpm01, sb.Rpm01, u));
            }
        }
    }
}

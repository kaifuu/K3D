using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 回放时间轴(模块9,仅回放态显示):滑块任意 Seek、播放/暂停、±5s 单步、
    /// 倍速跟随全局;Op 级以上事件画成可点击刻度(点击跳转到该时刻)。
    /// </summary>
    public static class TimelineUI
    {
        public static void Draw()
        {
            var p = ReplayPlayer.I;
            if (p == null || !DrillClock.InReplay) return;
            float dur = Mathf.Max(0.1f, p.Duration);

            var bar = new Rect(8, Screen.height - 152, Mathf.Min(Screen.width - 16, 1100), 96);
            GUI.Box(bar, "");
            UIRoot.Hot(bar);

            // ---- 滑块 + 时刻 ----
            float tx = bar.x + 14, tw = bar.width - 28;
            var track = new Rect(tx, bar.y + 14, tw, 20);
            float newT = GUI.HorizontalSlider(track, p.Cursor, 0f, dur);
            if (!Mathf.Approximately(newT, p.Cursor)) { p.Playing = false; p.Seek(newT); }
            GUI.Label(new Rect(tx, bar.y + 34, tw, 16),
                $"回放 {p.Cursor:0.0}s / {dur:0.0}s   帧 {ReplayService.I?.FrameCount ?? 0}   轨迹线 {TrajectoryDrawer.LineCount}",
                PanelKit.Mini);

            // ---- 事件刻度(Op 以上,可点击跳转) ----
            var evs = EventBus.All;
            for (int i = 0; i < evs.Count; i++)
            {
                var e = evs[i];
                if (e.Grade < EventGrade.Op || e.Time > dur) continue;
                float xTick = track.x + e.Time / dur * track.width;
                var tick = new Rect(xTick - 2, track.y + 18, 5, 10);
                GUI.color = e.Grade == EventGrade.Critical ? new Color(1f, 0.35f, 0.3f)
                          : e.Grade == EventGrade.Warn ? new Color(1f, 0.8f, 0.3f)
                          : new Color(0.55f, 0.85f, 1f);
                if (GUI.Button(tick, GUIContent.none) && !p.Playing)
                { p.Seek(e.Time); break; }
                GUI.color = Color.white;
            }

            // ---- 控制排 ----
            float y = bar.y + 56, x = tx;
            if (GUI.Button(new Rect(x, y, 44, 26), "⏮")) { p.Playing = false; p.Seek(0f); }
            x += 48;
            if (GUI.Button(new Rect(x, y, 44, 26), "-5s")) p.Step(-5f);
            x += 48;
            if (GUI.Button(new Rect(x, y, 60, 26), p.Playing ? "⏸ 暂停" : "▶ 播放")) p.Playing = !p.Playing;
            x += 64;
            if (GUI.Button(new Rect(x, y, 44, 26), "+5s")) p.Step(5f);
            x += 48;
            if (GUI.Button(new Rect(x, y, 44, 26), "⏭")) { p.Playing = false; p.Seek(dur); }
            x += 48;
            GUI.Label(new Rect(x, y, 90, 22), $"倍速 {DrillClock.Speed:0.##}x([ ])", PanelKit.Small);
            x += 94;
            if (GUI.Button(new Rect(x, y, 90, 26), "退出回放")) p.Exit();
        }
    }
}

using UnityEngine;

namespace DroneSim
{
    /// <summary>演练控制条:开始/暂停/继续/停止/倍速/返回主菜单(全局,任何模式可用)</summary>
    public static class DrillControlBar
    {
        static readonly float[] speeds = { 0.5f, 1f, 2f, 4f };

        public static void Draw()
        {
            var r = new Rect(8, 8, 720, 32);
            GUI.Box(r, "");
            UIRoot.Hot(r);

            float x = 14, y = 13, w = 64, h = 22;
            var st = DrillClock.State;

            if (st == PlayState.Setup && PanelKit.Btn(x, y, w, h, "开始")) ModeManager.StartDrill();
            x += w + 4;
            if (st == PlayState.Running && PanelKit.Btn(x, y, w, h, "暂停")) DrillClock.Pause();
            else if (st == PlayState.Paused && PanelKit.Btn(x, y, w, h, "继续")) DrillClock.Resume();
            x += w + 4;
            if (st != PlayState.Setup && PanelKit.Btn(x, y, w, h, "停止")) ModeManager.StopDrill();
            x += w + 4;

            // 倍速循环
            bool canSpeed = st == PlayState.Running || st == PlayState.Paused;
            if (canSpeed && PanelKit.Btn(x, y, 74, h, $"倍速 {DrillClock.Speed:0.##}x"))
            {
                int idx = 0;
                for (int i = 0; i < speeds.Length; i++)
                    if (Mathf.Approximately(speeds[i], DrillClock.Speed)) { idx = i; break; }
                DrillClock.SetSpeed(speeds[(idx + 1) % speeds.Length]);
            }
            x += 74 + 4;

            bool canReplay = (st == PlayState.Running || st == PlayState.Paused)
                             && ReplayService.I != null && ReplayService.I.HasData;
            if (PanelKit.Btn(x, y, 88, h, "回溯复盘", canReplay)) ReplayPlayer.I?.Enter();
            x += 88 + 4;
            if (PanelKit.Btn(x, y, 100, h, "返回主菜单")) ModeManager.ExitToMenu();
        }
    }
}

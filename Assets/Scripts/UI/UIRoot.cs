using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 常驻 UI 总装:主菜单 或 (控制条+状态栏+事件日志+模式侧板+提示条+悬浮标注)。
    /// 同时提供 MouseOverGUI 检测,防止点 UI 时穿透触发场景点击。
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        static readonly List<Rect> hotRects = new List<Rect>(16);
        /// <summary>上一帧鼠标是否悬停在任何 UI 面板上(场景点击前检查)</summary>
        public static bool MouseOverGUI { get; private set; }
        static readonly StringBuilder hintSb = new StringBuilder(256);

        void Update()
        {
            // 全局热键(仅在模式内生效)
            if (!ModeManager.InMenu)
            {
                if (Input.GetKeyDown(KeyCode.P))
                {
                    if (DrillClock.State == PlayState.Running) DrillClock.Pause();
                    else if (DrillClock.State == PlayState.Paused) DrillClock.Resume();
                }
                if (Input.GetKeyDown(KeyCode.LeftBracket)) DrillClock.SetSpeed(Mathf.Max(0.25f, DrillClock.Speed / 2f));
                if (Input.GetKeyDown(KeyCode.RightBracket)) DrillClock.SetSpeed(Mathf.Min(4f, DrillClock.Speed * 2f));
                if (Input.GetKeyDown(KeyCode.Escape)) ModeManager.ExitToMenu();
            }
        }

        void OnGUI()
        {
            PanelKit.Ensure();
            hotRects.Clear();

            if (ModeManager.InMenu)
            {
                MainMenuUI.Draw();
            }
            else
            {
                DrillControlBar.Draw();
                StatusBarUI.Draw();
                EventLogUI.Draw();
                if (DrillClock.InReplay) TimelineUI.Draw();

                // 模式专属侧板
                var m = ModeManager.Current;
                var side = new Rect(8, 118, 330, 300);
                GUI.Box(side, "");
                UIRoot.Hot(side);
                m?.DrawSidePanel(new Rect(side.x + 6, side.y + 26, side.width - 12, side.height - 34));

                // 底部提示条
                hintSb.Length = 0;
                m?.DrawHint(hintSb);
                if (hintSb.Length > 0)
                {
                    var bar = new Rect(8, Screen.height - 40, Mathf.Min(Screen.width - 16, 1100), 32);
                    GUI.Box(bar, "");
                    Hot(bar);
                    GUI.Label(new Rect(16, Screen.height - 35, bar.width - 16, 22),
                        hintSb.ToString(), PanelKit.Small);
                }

                m?.DrawOverlay();
            }

            var cam = ModeManager.Current?.Ctx.MainCamera ?? Camera.main;
            Overlay.DrawAll(cam);
            Overlay.ClearFrame();

            // 鼠标悬停检测(供下一帧 Update 使用)
            bool over = false;
            var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            for (int i = 0; i < hotRects.Count; i++)
                if (hotRects[i].Contains(mouse)) { over = true; break; }
            MouseOverGUI = over;
        }

        /// <summary>各面板注册自身区域用于悬停检测</summary>
        public static void Hot(Rect r) => hotRects.Add(r);
    }
}

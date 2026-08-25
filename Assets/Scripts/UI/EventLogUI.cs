using UnityEngine;

namespace DroneSim
{
    /// <summary>事件日志面板:EventBus 最新事件,按等级着色</summary>
    public static class EventLogUI
    {
        static Vector2 scroll;

        public static void Draw()
        {
            var list = EventBus.Recent(40);
            var r = new Rect(Screen.width - 338, 8, 330, 270);
            GUI.Box(r, "");
            UIRoot.Hot(r);
            GUI.Label(new Rect(r.x + 8, 12, r.width - 16, 20), "事件日志", PanelKit.Header);

            float contentH = list.Count * 19f;
            scroll = GUI.BeginScrollView(new Rect(r.x + 4, 34, r.width - 8, r.height - 40), scroll,
                                         new Rect(0, 0, r.width - 24, Mathf.Max(contentH, 100)));
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                string color = e.Grade switch
                {
                    EventGrade.Op => "#7fd8ff",
                    EventGrade.Warn => "#ffd050",
                    EventGrade.Critical => "#ff5040",
                    _ => "white"
                };
                string txt = $"<color={color}>[{PanelKit.FmtTime(e.Time)}][{e.Category}] {e.Message}</color>";
                GUI.Label(new Rect(2, i * 19f, r.width - 28, 18), txt, PanelKit.Small);
            }
            GUI.EndScrollView();
        }
    }
}

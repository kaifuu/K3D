using UnityEngine;

namespace DroneSim
{
    /// <summary>状态栏:模式名/演练时间/状态/倍速/环境摘要</summary>
    public static class StatusBarUI
    {
        public static void Draw()
        {
            var m = ModeManager.Current;
            if (m == null) return;
            var r = new Rect(8, 44, 330, 68);
            GUI.Box(r, "");
            UIRoot.Hot(r);

            string state = DrillClock.State switch
            {
                PlayState.Setup => "待开始",
                PlayState.Running => "运行中",
                PlayState.Paused => "已暂停",
                PlayState.Replaying => "回放中",
                _ => "?"
            };
            GUI.Label(new Rect(16, 48, 316, 20), m.Title, PanelKit.Header);
            GUI.Label(new Rect(16, 70, 160, 18), $"演练时间  {PanelKit.FmtTime(DrillClock.SimTime)}", PanelKit.Label);
            GUI.Label(new Rect(170, 70, 160, 18), $"状态  {state}", PanelKit.Label);
            var p = m.Ctx.Params;
            GUI.Label(new Rect(16, 90, 316, 18),
                $"环境  {PhaseName(p.Phase)} / {WeatherName(p.Weather)} / 风 {p.WindMps:0.#}m/s", PanelKit.Small);
        }

        public static string PhaseName(DayPhase p) =>
            p == DayPhase.Day ? "白昼" : p == DayPhase.Dusk ? "黄昏" : "夜晚";
        public static string WeatherName(WeatherKind w) => w switch
        {
            WeatherKind.Clear => "晴",
            WeatherKind.Rain => "雨",
            WeatherKind.Snow => "雪",
            WeatherKind.Fog => "雾",
            WeatherKind.Dust => "沙尘",
            _ => "?"
        };
    }
}

using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 分级预警面板(模块10):按优先级降序列出来袭目标,
    /// 威胁红/预警琥珀/关注青三色 + 处置建议,点击行即锁定目标。
    /// </summary>
    public static class ThreatPanelUI
    {
        static readonly Color cThreat = new Color(1f, 0.4f, 0.35f);
        static readonly Color cWarn = new Color(1f, 0.75f, 0.3f);
        static readonly Color cWatch = new Color(0.45f, 0.85f, 1f);

        public static void Draw(ThreatGrader grader, Rect r, ref float y)
        {
            if (grader == null) return;
            int threat = grader.CountAt(ThreatLevel.Threat);
            int warn = grader.CountAt(ThreatLevel.Warn);
            int watch = grader.CountAt(ThreatLevel.Watch);
            GUI.Label(new Rect(r.x, y, r.width, 20),
                $"分级预警  <color=#ff6660>威胁 {threat}</color>  <color=#ffbf4d>预警 {warn}</color>  <color=#73d9ff>关注 {watch}</color>",
                PanelKit.Header);
            y += 24;

            int rows = Mathf.Min(grader.Ranked.Count, 10);
            for (int i = 0; i < rows; i++)
            {
                var g = grader.Ranked[i];
                if (g.Drone == null) continue;
                var row = new Rect(r.x, y, r.width, 20);
                var sel = GameState.Selected == g.Drone;
                if (sel) GUI.Box(row, "");
                if (GUI.Button(row, GUIContent.none)) GameState.Selected = g.Drone;

                var col = g.Level == ThreatLevel.Threat ? cThreat : g.Level == ThreatLevel.Warn ? cWarn : cWatch;
                var prev = GUI.color; GUI.color = col;
                GUI.Label(new Rect(r.x + 4, y, r.width - 8, 18),
                    $"{(sel ? "▶" : " ")}{g.Drone.DroneId} P{g.Priority:0} {g.DistCore:0}m → {g.Advice}", PanelKit.Small);
                GUI.color = prev;
                y += 21;
            }
            if (rows == 0)
            {
                GUI.Label(new Rect(r.x, y, r.width, 16), "空域洁净,无来袭目标", PanelKit.Mini);
                y += 18;
            }
        }
    }
}

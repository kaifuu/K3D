using UnityEngine;

namespace DroneSim
{
    /// <summary>IMGUI 样式与控件工具(全部 UI 面板共用;样式只创建一次)</summary>
    public static class PanelKit
    {
        public static GUIStyle Header, Label, Small, Mini, Button, MiniButton, Box;
        static bool ready;

        public static void Ensure()
        {
            if (ready) return;
            Header = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            Label = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            Small = new GUIStyle(GUI.skin.label) { fontSize = 11, richText = true };
            Mini = new GUIStyle(GUI.skin.label) { fontSize = 10, richText = true };
            Button = new GUIStyle(GUI.skin.button) { fontSize = 12 };
            MiniButton = new GUIStyle(GUI.skin.button) { fontSize = 11 };
            Box = new GUIStyle(GUI.skin.box);
            ready = true;
        }

        public static void Section(Rect r, string title)
        {
            GUI.Box(r, "");
            GUI.Label(new Rect(r.x + 8, r.y + 4, r.width - 16, 20), title, Header);
        }

        public static bool Btn(float x, float y, float w, float h, string text, bool enabled = true)
        {
            var prev = GUI.enabled;
            GUI.enabled = enabled;
            bool hit = GUI.Button(new Rect(x, y, w, h), text, MiniButton);
            GUI.enabled = prev;
            return enabled && hit;
        }

        public static bool ToggleBtn(float x, float y, float w, float h, string text, bool on)
        {
            var bg = GUI.backgroundColor;
            GUI.backgroundColor = on ? new Color(0.35f, 0.85f, 0.45f) : bg;
            bool hit = GUI.Button(new Rect(x, y, w, h), text, MiniButton);
            GUI.backgroundColor = bg;
            return hit;
        }

        public static string FmtTime(float t)
        {
            int m = Mathf.FloorToInt(t / 60f), s = Mathf.FloorToInt(t % 60f);
            return $"{m:D2}:{s:D2}";
        }
    }
}

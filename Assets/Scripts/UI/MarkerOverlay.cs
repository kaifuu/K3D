using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 世界→屏幕悬浮标注层:各系统在 Update 期提交(Label/Bracket/Ring/Vignette/Noise),
    /// UIRoot.OnGUI 统一绘制后 ClearFrame。IMGUI 无深度遮挡,接受标注穿透。
    /// 闪烁动画一律用 realtimeSinceStartup(暂停时不死白)。
    /// </summary>
    public static class Overlay
    {
        struct LabelItem { public Vector3 World; public string Text; public Color Color; }
        struct BracketItem { public Vector3 World; public float W, H; public Color Color; public float BlinkHz; }
        struct RingItem { public Vector3 World; public float RadiusPx, Fill01, Spin; public Color Color; }

        static readonly List<LabelItem> labels = new List<LabelItem>(32);
        static readonly List<BracketItem> brackets = new List<BracketItem>(16);
        static readonly List<RingItem> rings = new List<RingItem>(16);
        static Color vignetteColor = Color.black;
        static float vignetteA;
        static float noiseA;

        static Texture2D dotTex, vignetteTex, noiseTex;

        // ---------- 提交接口(Update 期调用) ----------
        public static void Label(Vector3 world, string text, Color c) =>
            labels.Add(new LabelItem { World = world, Text = text, Color = c });

        public static void Bracket(Vector3 world, float wPx, float hPx, Color c, float blinkHz = 0f) =>
            brackets.Add(new BracketItem { World = world, W = wPx, H = hPx, Color = c, BlinkHz = blinkHz });

        public static void Ring(Vector3 world, float radiusPx, float fill01, float spin01, Color c) =>
            rings.Add(new RingItem { World = world, RadiusPx = radiusPx, Fill01 = fill01, Spin = spin01, Color = c });

        public static void Vignette(Color c, float a01) { vignetteColor = c; vignetteA = Mathf.Clamp01(a01); }
        public static void Noise(float a01) => noiseA = Mathf.Clamp01(a01);

        // ---------- 绘制(UIRoot.OnGUI 调用) ----------
        internal static void DrawAll(Camera cam)
        {
            if (cam == null) return;

            foreach (var b in brackets) DrawBracket(cam, b);
            foreach (var r in rings) DrawRing(cam, r);
            foreach (var l in labels) DrawLabel(cam, l);

            if (vignetteA > 0.005f) DrawVignette();
            if (noiseA > 0.005f) DrawNoise();
        }

        internal static void ClearFrame()
        {
            labels.Clear(); brackets.Clear(); rings.Clear();
            vignetteA = 0f; noiseA = 0f;   // 常驻效果需持有方每帧重新提交
        }

        static bool ToScreen(Camera cam, Vector3 world, out Vector2 sp)
        {
            sp = Vector2.zero;
            var p = cam.WorldToScreenPoint(world);
            if (p.z <= 0.1f) return false;                       // 在相机背后
            sp = new Vector2(p.x, Screen.height - p.y);          // IMGUI y 向下
            return sp.x > -120 && sp.x < Screen.width + 120 && sp.y > -120 && sp.y < Screen.height + 120;
        }

        static void DrawLabel(Camera cam, LabelItem l)
        {
            if (!ToScreen(cam, l.World, out var sp)) return;
            var style = PanelKit.Mini;
            var content = new GUIContent(l.Text);
            var size = style.CalcSize(content);
            var rect = new Rect(sp.x - size.x / 2, sp.y - size.y / 2, size.x, size.y);
            var prev = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), content, style);
            GUI.color = l.Color;
            GUI.Label(rect, content, style);
            GUI.color = prev;
        }

        static void DrawBracket(Camera cam, BracketItem b)
        {
            if (!ToScreen(cam, b.World, out var sp)) return;
            float blink = 1f;
            if (b.BlinkHz > 0f)
                blink = Mathf.Sin(Time.realtimeSinceStartup * b.BlinkHz * Mathf.PI * 2f) > 0f ? 1f : 0.25f;
            var c = new Color(b.Color.r, b.Color.g, b.Color.b, b.Color.a * blink);
            float arm = Mathf.Min(b.W, b.H) * 0.32f, th = 2f;
            float x0 = sp.x - b.W / 2, x1 = sp.x + b.W / 2, y0 = sp.y - b.H / 2, y1 = sp.y + b.H / 2;
            var prev = GUI.color; GUI.color = c;
            DrawRect(new Rect(x0, y0, arm, th)); DrawRect(new Rect(x0, y0, th, arm));
            DrawRect(new Rect(x1 - arm, y0, arm, th)); DrawRect(new Rect(x1 - th, y0, th, arm));
            DrawRect(new Rect(x0, y1 - th, arm, th)); DrawRect(new Rect(x0, y1 - arm, th, arm));
            DrawRect(new Rect(x1 - arm, y1 - th, arm, th)); DrawRect(new Rect(x1 - th, y1 - arm, th, arm));
            GUI.color = prev;
        }

        static void DrawRing(Camera cam, RingItem r)
        {
            if (!ToScreen(cam, r.World, out var sp)) return;
            var tex = DotTex();
            const int n = 20;
            float dotSize = Mathf.Clamp(r.RadiusPx / 9f, 4f, 12f);
            var prev = GUI.color;
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n + r.Spin) * Mathf.PI * 2f;
                var p = sp + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r.RadiusPx;
                bool filled = (float)i / n <= r.Fill01;
                GUI.color = new Color(r.Color.r, r.Color.g, r.Color.b, filled ? 0.95f : 0.25f);
                GUI.DrawTexture(new Rect(p.x - dotSize / 2, p.y - dotSize / 2, dotSize, dotSize), tex);
            }
            GUI.color = prev;
        }

        static void DrawRect(Rect r)
        {
            var tex = DotTex();
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill);
        }

        static void DrawVignette()
        {
            if (vignetteTex == null)
            {
                const int s = 256;
                vignetteTex = new Texture2D(s, s, TextureFormat.RGBA32, false);
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x - s / 2f) / (s / 2f), dy = (y - s / 2f) / (s / 2f);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01((d - 0.55f) / 0.45f);   // 边缘渐深
                        px[y * s + x] = new Color(255, 255, 255, (byte)(a * 255));
                    }
                vignetteTex.SetPixels32(px);
                vignetteTex.Apply();
            }
            var prev = GUI.color;
            GUI.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, vignetteA);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignetteTex);
            GUI.color = prev;
        }

        static void DrawNoise()
        {
            if (noiseTex == null)
            {
                const int s = 128;
                noiseTex = new Texture2D(s, s, TextureFormat.RGBA32, false);
                var px = new Color32[s * s];
                var rng = new System.Random(12345);
                for (int i = 0; i < px.Length; i++)
                { byte v = (byte)rng.Next(60, 200); px[i] = new Color(v, v, v, 255); }
                noiseTex.SetPixels32(px);
                noiseTex.Apply();
                noiseTex.wrapMode = TextureWrapMode.Repeat;
            }
            // 随机偏移平铺,制造颗粒跳动
            float ox = Random.value * 128, oy = Random.value * 128;
            const int tile = 160;
            var prev = GUI.color;
            GUI.color = new Color(1, 1, 1, noiseA);
            for (float y = -oy; y < Screen.height; y += tile)
                for (float x = -ox; x < Screen.width; x += tile)
                    GUI.DrawTexture(new Rect(x, y, tile, tile), noiseTex);
            GUI.color = prev;
        }

        static Texture2D DotTex()
        {
            if (dotTex != null) return dotTex;
            const int s = 16;
            dotTex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(s / 2f - .5f, s / 2f - .5f));
                    float a = Mathf.Clamp01((s / 2f - d) / 1.5f);
                    px[y * s + x] = new Color(255, 255, 255, (byte)(a * 255));
                }
            dotTex.SetPixels32(px);
            dotTex.Apply();
            return dotTex;
        }
    }
}

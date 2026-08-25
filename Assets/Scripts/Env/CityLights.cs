using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 城市夜灯:外围剪影楼群(发光窗带)+ 起降场周边街灯,
    /// 点光预算 14 个(≤24);SetNight 1.2 秒平滑点亮,约 18% 窗带 Perlin 闪烁。
    /// </summary>
    public class CityLights : MonoBehaviour
    {
        public bool LightsOn => lightsOn;
        public float MaxPointIntensity { get; private set; }

        struct Lit
        {
            public Renderer[] Windows;
            public Material Mat;
            public Light Point;
            public float FlickerSeed;
            public bool Flickers;
        }

        readonly List<Lit> lits = new List<Lit>(26);
        bool lightsOn;
        float intensity01;

        static float Hash(float i) => Frac(Mathf.Sin(i * 12.9898f) * 43758.5453f);
        static float Frac(float v) => v - Mathf.Floor(v);

        public void Build(Transform parent)
        {
            // ---- 外围楼群(半径150~215,不干扰演练场) ----
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f + Hash(i) * 0.35f;
                float r = 150f + Hash(i + 40f) * 65f;
                float h = 24f + Hash(i + 80f) * 46f;
                float w = 10f + Hash(i + 120f) * 10f;
                var pos = new Vector3(Mathf.Cos(a) * r, h / 2f, Mathf.Sin(a) * r);

                var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCol(tower);
                tower.name = $"Skyline{i}";
                tower.transform.SetParent(parent, false);
                tower.transform.position = pos;
                tower.transform.rotation = Quaternion.Euler(0f, Hash(i + 160f) * 90f, 0f);
                tower.transform.localScale = new Vector3(w, h, w);
                tower.GetComponent<Renderer>().material =
                    MaterialLib.Wall(i, new Vector2(w / 3.5f, h / 3.5f));

                // 朝场心一面的发光窗带(每楼独立材质,闪烁按楼)
                int rows = Mathf.Max(2, (int)(h / 9f));
                var winMat = EnvironmentBuilder.UnlitMat(new Color(1f, 0.82f, 0.45f, 0f));
                var wins = new Renderer[rows];
                for (int rI = 0; rI < rows; rI++)
                {
                    var win = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    DestroyCol(win);
                    win.name = "Win";
                    win.transform.SetParent(tower.transform, false);
                    float wy = (rI + 0.6f) / rows * (h - 4f) + 2f - h / 2f;
                    win.transform.localPosition = new Vector3(0f, wy, -w / 2f - 0.12f);
                    win.transform.localScale = new Vector3(w * 0.72f, 1.1f, 0.06f);
                    var rend = win.GetComponent<Renderer>();
                    rend.material = winMat;
                    wins[rI] = rend;
                }

                var lit = new Lit
                {
                    Windows = wins,
                    Mat = winMat,
                    FlickerSeed = Hash(i + 7f) * 97f,
                    Flickers = Hash(i + 7f) < 0.18f,
                };
                if (i % 2 == 0)
                {
                    var pl = tower.AddComponent<Light>();
                    pl.type = LightType.Point;
                    pl.color = new Color(1f, 0.8f, 0.5f);
                    pl.range = 55f;
                    pl.intensity = 0f;
                    pl.shadows = LightShadows.None;
                    lit.Point = pl;
                }
                lits.Add(lit);
            }

            // ---- 街灯(半径26 环,照亮起降坪周边) ----
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + 0.26f;
                var pos = new Vector3(Mathf.Cos(a) * 26f, 0f, Mathf.Sin(a) * 26f);
                var lamp = new GameObject($"StreetLamp{i}");
                lamp.transform.SetParent(parent, false);
                lamp.transform.position = pos;

                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                DestroyCol(pole);
                pole.name = "Pole";
                pole.transform.SetParent(lamp.transform, false);
                pole.transform.localScale = new Vector3(0.16f, 2.2f, 0.16f);
                pole.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                pole.GetComponent<Renderer>().material = MaterialLib.Metal(new Color(0.2f, 0.21f, 0.24f), 1f);

                var headMat = EnvironmentBuilder.UnlitMat(new Color(1f, 0.85f, 0.55f, 0f));
                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                DestroyCol(head);
                head.name = "Head";
                head.transform.SetParent(lamp.transform, false);
                head.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                head.transform.localPosition = new Vector3(0f, 4.55f, 0f);
                head.GetComponent<Renderer>().material = headMat;

                var pl = lamp.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = new Color(1f, 0.82f, 0.55f);
                pl.range = 24f;
                pl.intensity = 0f;
                pl.shadows = LightShadows.None;

                lits.Add(new Lit
                {
                    Windows = new[] { head.GetComponent<Renderer>() },
                    Mat = headMat,
                    Point = pl,
                    FlickerSeed = Hash(i + 31f) * 97f,
                    Flickers = false,
                });
            }
        }

        public void SetNight(bool on)
        {
            if (lightsOn == on) return;
            lightsOn = on;
            EventBus.Publish("环境", "city", on ? "城市灯光点亮" : "城市灯光熄灭", EventGrade.Info);
        }

        void Update()
        {
            intensity01 = Mathf.MoveTowards(intensity01, lightsOn ? 1f : 0f, Time.unscaledDeltaTime / 1.2f);
            float t = Time.unscaledTime;
            MaxPointIntensity = 0f;
            for (int i = 0; i < lits.Count; i++)
            {
                var l = lits[i];
                float glow = intensity01;
                if (l.Flickers && glow > 0.01f)
                    glow *= 0.62f + 0.38f * Mathf.PerlinNoise(l.FlickerSeed, t * 3.2f) * 2f;
                glow = Mathf.Clamp01(glow);
                l.Mat.SetColor("_Color", new Color(1f, 0.82f, 0.45f, 0.85f * glow));
                if (l.Point != null)
                {
                    l.Point.intensity = 1.15f * glow;
                    if (l.Point.intensity > MaxPointIntensity) MaxPointIntensity = l.Point.intensity;
                }
            }
        }

        static void DestroyCol(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }
    }
}

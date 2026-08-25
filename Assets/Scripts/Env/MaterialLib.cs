using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// CC0 PBR 贴图材质库(V1 实物还原)。
    /// 贴图位于 Resources/Art/Textures(_d 反照率 / _n 法线,来源与授权见 SOURCES.txt)。
    /// 任一贴图缺失时回退纯色 Standard —— 无资源环境(验收机/最小检出库)观感退回旧版,行为不变(双轨)。
    /// 事件语义色(火/烟/激光/分区环/轨迹线)不走本库,保持 Unlit 自发光。
    /// </summary>
    public static class MaterialLib
    {
        static readonly Color White = Color.white;

        /// <summary>指定贴图集是否存在(指标导出用)</summary>
        public static bool HasTex(string key) => Resources.Load<Texture2D>($"Art/Textures/{key}_d") != null;

        /// <summary>程序化天空盒材质(昼夜 Preset 驱动参数;找不到着色器返回 null 走纯色天幕)</summary>
        public static Material CreateSky()
        {
            var sh = Shader.Find("Skybox/Procedural");
            if (sh == null) return null;
            var m = new Material(sh);
            if (m.HasProperty("_SunSize")) m.SetFloat("_SunSize", 0.04f);
            if (m.HasProperty("_SunSizeConvergence")) m.SetFloat("_SunSizeConvergence", 5f);
            if (m.HasProperty("_AtmosphereThickness")) m.SetFloat("_AtmosphereThickness", 1f);
            if (m.HasProperty("_SkyTint")) m.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f));
            if (m.HasProperty("_Exposure")) m.SetFloat("_Exposure", 1.25f);
            return m;
        }

        /// <summary>贴图材质;texTint 乘贴图,fallbackColor 为贴图缺失时的纯色回退</summary>
        static Material LitTex(string key, Color texTint, Color fallbackColor, float gloss, Vector2 tiling, float bump = 1f)
        {
            var m = new Material(Shader.Find("Standard"));
            var d = Resources.Load<Texture2D>($"Art/Textures/{key}_d");
            if (d == null)
            {
                m.color = fallbackColor;
            }
            else
            {
                m.mainTexture = d;
                m.color = texTint;
                m.SetTextureScale("_MainTex", tiling);
                var n = Resources.Load<Texture2D>($"Art/Textures/{key}_n");
                if (n != null && m.HasProperty("_BumpMap"))
                {
                    m.SetTexture("_BumpMap", n);
                    m.SetFloat("_BumpScale", bump);
                }
            }
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            if (d != null)
            {
                // 掠射角抗 mip 塌缩:街景相机平视时地面/立面都被压成单色,必须各向异性
                d.filterMode = FilterMode.Trilinear;
                d.anisoLevel = 9;
            }
            return m;
        }

        static Vector2 V2(float t) => new Vector2(t, t);

        /// <summary>主地面:磨损混凝土(worldMeters 为地面边长,贴图 4m/格平铺)</summary>
        public static Material Ground(float worldMeters) =>
            LitTex("ground", White, new Color(0.16f, 0.2f, 0.17f), 0.28f, V2(worldMeters / 4f));

        /// <summary>外围草场大平面(5m/格)</summary>
        public static Material GrassField(float worldMeters) =>
            LitTex("grass", White, new Color(0.2f, 0.3f, 0.16f), 0.5f, V2(worldMeters / 5f));

        /// <summary>建筑立面三变体(混凝土A/混凝土B/砖),tiles = (宽向格数, 高向格数)</summary>
        public static Material Wall(int variant, Vector2 tiles)
        {
            switch (Mathf.Abs(variant) % 3)
            {
                case 0: return LitTex("wallA", White, new Color(0.5f, 0.52f, 0.5f), 0.2f, tiles);
                case 1: return LitTex("wallB", new Color(0.92f, 0.93f, 0.95f), new Color(0.55f, 0.57f, 0.6f), 0.18f, tiles);
                default: return LitTex("brick", White, new Color(0.42f, 0.24f, 0.2f), 0.22f, tiles);
            }
        }

        // ================= V5 实景:程序生成幕墙贴图 =================

        static Texture2D facadeCache, facadeEmisCache;
        const int FacadeCell = 64, FacadeGrid = 4;   // 256×256 = 4×4 窗格

        static float CellHash(int cx, int cy, int salt)
        {
            float h = Mathf.Sin(cx * 91.7f + cy * 47.3f + salt * 13.1f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        /// <summary>幕墙窗格贴图:每格 = 浅灰窗框 + 深蓝玻璃(顶亮底暗,天光反射),
        /// 格间亮度抖动打破重复感;任何外部贴图缺失都不影响 —— 城市观感核心。</summary>
        public static Texture2D FacadeTexture()
        {
            if (facadeCache != null) return facadeCache;
            int size = FacadeCell * FacadeGrid;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            // 掠射角(街谷两侧立面离轴 60°+)mip 会把整张贴图平均成单色 —— 各向异性是窗格可见的前提
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 9;
            var px = new Color[size * size];
            for (int cy = 0; cy < FacadeGrid; cy++)
            for (int cx = 0; cx < FacadeGrid; cx++)
            {
                float jit = 0.86f + CellHash(cx, cy, 3) * 0.28f;       // 玻璃明暗抖动
                // 对比度拉满:浅亮窗框 + 深玻璃渐变,远处 mip 平均后仍能读出格线
                var frame = new Color(0.74f * jit, 0.74f * jit, 0.76f * jit);
                var glassTop = new Color(0.30f * jit, 0.38f * jit, 0.50f * jit);
                var glassBot = new Color(0.045f, 0.065f, 0.10f);
                for (int y = 0; y < FacadeCell; y++)
                for (int x = 0; x < FacadeCell; x++)
                {
                    bool frameX = x < 6 || x >= FacadeCell - 6;
                    bool frameY = y < 7 || y >= FacadeCell - 5;
                    Color c;
                    if (frameX || frameY) c = frame;
                    else
                    {
                        float g = Mathf.Sqrt((y - 7f) / (FacadeCell - 12f));   // 底暗顶亮渐变
                        c = Color.Lerp(glassBot, glassTop, g * g);
                    }
                    if (y < 3) c *= 0.55f;   // 楼板下缘投影线(加强,楼层线更清晰)
                    px[(cy * FacadeCell + y) * size + cx * FacadeCell + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply(true, false);
            facadeCache = tex;
            return tex;
        }

        /// <summary>夜窗发光贴图:约 45% 窗格亮暖光,其余近黑;帧(窗框)不发光。
        /// CityLights 夜间启用 _Emission 用,与昼面同格对位。</summary>
        public static Texture2D FacadeEmissive()
        {
            if (facadeEmisCache != null) return facadeEmisCache;
            int size = FacadeCell * FacadeGrid;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 9;
            var px = new Color[size * size];
            var black = new Color(0.02f, 0.02f, 0.02f, 0f);
            for (int cy = 0; cy < FacadeGrid; cy++)
            for (int cx = 0; cx < FacadeGrid; cx++)
            {
                bool on = CellHash(cx, cy, 11) < 0.45f;
                float warm = 0.55f + CellHash(cx, cy, 17) * 0.45f;
                var lit = new Color(1f * warm, 0.78f * warm, 0.45f * warm, 0f);
                for (int y = 0; y < FacadeCell; y++)
                for (int x = 0; x < FacadeCell; x++)
                {
                    bool frameX = x < 6 || x >= FacadeCell - 6;
                    bool frameY = y < 7 || y >= FacadeCell - 5;
                    bool win = !frameX && !frameY;
                    px[(cy * FacadeCell + y) * size + cx * FacadeCell + x] = win && on ? lit : black;
                }
            }
            tex.SetPixels(px);
            tex.Apply(true, false);
            facadeEmisCache = tex;
            return tex;
        }

        /// <summary>幕墙材质:五色系 tint × 窗格贴图,高光滑度吃天空盒反射。
        /// tiles = (宽/12, 高/12.8) —— 一格贴图 = 4 窗 × 4 层。</summary>
        public static Material CurtainWall(int variant, Vector2 tiles)
        {
            Color[] tints =
            {
                new Color(0.88f, 0.82f, 0.70f),   // 暖米黄
                new Color(0.82f, 0.84f, 0.86f),   // 浅灰
                new Color(0.66f, 0.74f, 0.84f),   // 蓝灰玻璃
                new Color(0.92f, 0.92f, 0.90f),   // 白
                new Color(0.72f, 0.75f, 0.78f),   // 冷灰
            };
            float[] gloss = { 0.78f, 0.82f, 0.92f, 0.72f, 0.85f };
            int v = Mathf.Abs(variant) % tints.Length;
            var m = new Material(Shader.Find("Standard"))
            {
                mainTexture = FacadeTexture(),
                color = tints[v],
            };
            m.SetTextureScale("_MainTex", tiles);
            m.SetFloat("_Glossiness", gloss[v]);
            return m;
        }

        static Texture2D cloudCache;

        /// <summary>云贴图(256×128):若干径向衰减白斑叠加,RGBA,alpha 平滑边缘</summary>
        public static Texture2D CloudTexture()
        {
            if (cloudCache != null) return cloudCache;
            const int w = 256, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 9;
            var px = new Color[w * h];
            // 确定性云斑种子(位置/半径/强度)
            var spots = new (float x, float y, float r, float s)[]
            {
                (0.30f, 0.55f, 0.22f, 0.9f), (0.48f, 0.48f, 0.28f, 1.0f), (0.66f, 0.58f, 0.20f, 0.85f),
                (0.40f, 0.38f, 0.16f, 0.7f), (0.58f, 0.40f, 0.15f, 0.65f), (0.22f, 0.62f, 0.12f, 0.55f),
                (0.78f, 0.50f, 0.11f, 0.5f), (0.50f, 0.66f, 0.13f, 0.6f),
            };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                float a = 0f;
                foreach (var sp in spots)
                {
                    float dx = Mathf.Abs(u - sp.x), dy = (v - sp.y) * 0.55f;   // 椭圆斑
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / sp.r;
                    if (d < 1f) a += (1f - d) * (1f - d) * sp.s;
                }
                a = Mathf.Clamp01(a);
                px[y * w + x] = new Color(1f, 1f, 1f, a);   // 颜色由材质随天色调
            }
            tex.SetPixels(px);
            tex.Apply(true, false);
            cloudCache = tex;
            return tex;
        }

        /// <summary>平屋顶(深色混凝土)</summary>
        public static Material Roof(Vector2 tiles) =>
            LitTex("ground", new Color(0.34f, 0.35f, 0.37f), new Color(0.2f, 0.21f, 0.22f), 0.18f, tiles);

        /// <summary>金属板(炮塔/雷达/天线类设施)</summary>
        public static Material Metal(Color tint, float tiles = 2f) =>
            LitTex("metal", tint, tint * 0.7f, 0.55f, V2(tiles));

        /// <summary>柏油停机坪/道路(4m/格)</summary>
        public static Material Asphalt(float worldMeters) =>
            LitTex("asphalt", White, new Color(0.14f, 0.15f, 0.16f), 0.3f, V2(worldMeters / 4f));

        /// <summary>裸土(工事/施工区,5m/格)</summary>
        public static Material Dirt(float worldMeters) =>
            LitTex("dirt", White, new Color(0.3f, 0.24f, 0.16f), 0.4f, V2(worldMeters / 5f));
    }
}

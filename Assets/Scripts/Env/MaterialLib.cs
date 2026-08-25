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

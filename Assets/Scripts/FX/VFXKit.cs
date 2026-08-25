using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 纯代码粒子/贴图工具库:程序化生成 雨丝/雪粒/沙尘 贴图(零外部资源),
    /// 三类 ParticleSystem 工厂(世界空间模拟 + culling=Ignore,无头批处理下照常模拟)。
    /// </summary>
    public static class VFXKit
    {
        static Texture2D rainTex, snowTex, dustTex;

        // ---------- 程序化贴图 ----------
        public static Texture2D RainTexture()
        {
            if (rainTex != null) return rainTex;
            const int w = 8, h = 64;
            var t = new Texture2D(w, h, TextureFormat.ARGB32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float vy = Mathf.Sin(y / (float)(h - 1) * Mathf.PI);               // 两端渐隐的竖条
                for (int x = 0; x < w; x++)
                {
                    float vx = 1f - Mathf.Abs(x - (w - 1) * 0.5f) / (w * 0.5f);    // 中间最亮
                    px[y * w + x] = new Color(1f, 1f, 1f, vy * vx);
                }
            }
            t.SetPixels(px);
            t.Apply();
            rainTex = t;
            return t;
        }

        public static Texture2D SnowTexture()
        {
            if (snowTex != null) return snowTex;
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.ARGB32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), new Vector2(n - 1, n - 1) * 0.5f) / (n * 0.5f);
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 1.7f));
                }
            t.SetPixels(px);
            t.Apply();
            snowTex = t;
            return t;
        }

        public static Texture2D DustTexture()
        {
            if (dustTex != null) return dustTex;
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.ARGB32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var d = new Vector2(x - (n - 1) * 0.5f, y - (n - 1) * 0.5f);
                    float r = d.magnitude / (n * 0.5f);
                    float a = Mathf.Atan2(d.y, d.x);
                    float lump = 0.6f + 0.4f * Mathf.PerlinNoise(Mathf.Cos(a) * 2.2f + 7.3f, Mathf.Sin(a) * 2.2f + r * 3.5f);
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 1.4f) * lump);
                }
            t.SetPixels(px);
            t.Apply();
            dustTex = t;
            return t;
        }

        public static Material ParticleMat(Texture2D tex, Color tint)
        {
            var m = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
            m.SetTexture("_MainTex", tex);
            m.SetColor("_TintColor", tint);
            return m;
        }

        // ---------- 粒子系统工厂 ----------
        static ParticleSystem Base(Transform parent, string name, Vector3 boxScale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;   // 发射盒跟相机,粒子留在世界
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;   // 无头/离屏仍模拟(默认随 timeScale 暂停)
            var em = ps.emission;
            em.rateOverTime = 0f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = boxScale;
            return ps;
        }

        /// <summary>雨:竖直长条拉伸面片,重力加速,密度→发射率</summary>
        public static ParticleSystem MakeRain(Transform parent)
        {
            var ps = Base(parent, "RainFX", new Vector3(80f, 34f, 80f));
            var main = ps.main;
            main.startLifetime = 2.2f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.gravityModifier = 1.4f;
            main.startSize = 0.1f;
            main.maxParticles = 4500;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 2.6f;
            r.material = ParticleMat(RainTexture(), new Color(0.85f, 0.93f, 1f, 0.55f));
            return ps;
        }

        /// <summary>雪:慢降 + 噪声飘摆的软圆粒</summary>
        public static ParticleSystem MakeSnow(Transform parent)
        {
            var ps = Base(parent, "SnowFX", new Vector3(90f, 40f, 90f));
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            main.gravityModifier = 0.12f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
            main.maxParticles = 2200;
            var no = ps.noise;
            no.enabled = true;
            no.strength = 0.8f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.material = ParticleMat(SnowTexture(), new Color(1f, 1f, 1f, 0.85f));
            return ps;
        }

        /// <summary>沙尘:贴地大团软斑,水平风速驱动(velocityOverLifetime 由天气系统每帧写)</summary>
        public static ParticleSystem MakeDust(Transform parent)
        {
            var ps = Base(parent, "DustFX", new Vector3(90f, 18f, 90f));
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.gravityModifier = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(2.6f, 5f);
            main.maxParticles = 1400;
            var no = ps.noise;
            no.enabled = true;
            no.strength = 1.1f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.material = ParticleMat(DustTexture(), new Color(1f, 0.88f, 0.65f, 0.6f));
            return ps;
        }

        public static void SetRate(ParticleSystem ps, float perSecond)
        {
            if (ps == null) return;
            var em = ps.emission;
            em.rateOverTime = perSecond;
        }

        // ---------- P8 应急战术:火焰 / 烟柱 / 落点扬尘 ----------

        public static Texture2D FireTexture()
        {
            if (fireTex != null) return fireTex;
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.ARGB32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), new Vector2(n - 1, n - 1) * 0.5f) / (n * 0.5f);
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f));  // 软圆,核心亮
                }
            t.SetPixels(px);
            t.Apply();
            fireTex = t;
            return t;
        }

        static Texture2D fireTex;

        /// <summary>火焰:起始速度 0,靠 velocityOverLifetime 垂直上升 + 噪声翻卷,颜色橙→深红随机</summary>
        public static ParticleSystem MakeFire(Transform parent)
        {
            var ps = Base(parent, "FireFX", new Vector3(3.6f, 0.6f, 3.6f));
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.1f);
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.3f, 2.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.62f, 0.18f, 1f), new Color(1f, 0.32f, 0.1f, 1f));
            main.maxParticles = 500;
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.y = new ParticleSystem.MinMaxCurve(3.2f, 4.6f);
            var no = ps.noise;
            no.enabled = true;
            no.strength = 0.7f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0f, 1f) }
            });
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.material = ParticleMat(FireTexture(), new Color(1f, 1f, 1f, 1f));
            return ps;
        }

        /// <summary>烟柱:慢升大团灰絮,尺寸渐大 + 尾段渐隐(P8 火场)</summary>
        public static ParticleSystem MakeSmoke(Transform parent)
        {
            var ps = Base(parent, "SmokeFX", new Vector3(2.4f, 0.6f, 2.4f));
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(2.0f, 3.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.24f, 0.24f, 0.27f, 0.55f), new Color(0.4f, 0.4f, 0.42f, 0.42f));
            main.maxParticles = 600;
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.y = new ParticleSystem.MinMaxCurve(1.5f, 2.4f);
            var no = ps.noise;
            no.enabled = true;
            no.strength = 1.15f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                alphaKeys = new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0.32f, 0.55f), new GradientAlphaKey(0f, 1f) }
            });
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.6f, AnimationCurve.Linear(0f, 0.65f, 1f, 1.35f));   // 随寿命放大
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.material = ParticleMat(DustTexture(), new Color(1f, 1f, 1f, 1f));
            return ps;
        }

        /// <summary>落点扬尘:扁盒水平迸发 + 微重力,一次性 Emit(P8 投送)</summary>
        public static ParticleSystem MakePuff(Transform parent)
        {
            var ps = Base(parent, "PuffFX", new Vector3(1.4f, 0.25f, 1.4f));
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.gravityModifier = 0.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.7f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.72f, 0.65f, 0.52f, 0.75f), new Color(0.55f, 0.5f, 0.42f, 0.6f));
            main.maxParticles = 200;
            var no = ps.noise;
            no.enabled = true;
            no.strength = 0.5f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                alphaKeys = new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) }
            });
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.material = ParticleMat(DustTexture(), new Color(1f, 1f, 1f, 1f));
            return ps;
        }
    }
}

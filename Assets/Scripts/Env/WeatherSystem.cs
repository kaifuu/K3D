using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 天气系统:晴/雨/雪/雾/沙尘 五态。
    /// 粒子发射盒跟随相机(世界空间模拟,粒子留在世界);雾密度与地面湿润平滑过渡;
    /// 沙尘叠全屏噪点;雾色由昼夜时段基调 + 天气色调合成。
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        public Camera Cam;
        public WeatherKind Kind { get; private set; } = WeatherKind.Clear;
        public float Density01 { get; private set; } = 0.5f;

        public bool FogOn => RenderSettings.fog;
        public float FogDensity => RenderSettings.fogDensity;
        public float GroundGloss => groundMat != null && groundMat.HasProperty("_Glossiness")
            ? groundMat.GetFloat("_Glossiness") : 0f;

        public int RainAlive => rain != null ? rain.particleCount : 0;
        public int SnowAlive => snow != null ? snow.particleCount : 0;
        public int DustAlive => dust != null ? dust.particleCount : 0;
        public int AliveParticles => RainAlive + SnowAlive + DustAlive;

        ParticleSystem rain, snow, dust;
        Material groundMat;
        Color dryColor = new Color(0.16f, 0.2f, 0.17f);
        Color groundTarget;
        Color fogTint = new Color(0.62f, 0.7f, 0.78f);
        float fogTarget, glossTarget, glossCur = 0.2f;

        public void Setup(Transform parent, Camera cam, Material ground)
        {
            Cam = cam;
            groundMat = ground;
            if (groundMat != null)
            {
                dryColor = groundMat.color;
                if (groundMat.HasProperty("_Glossiness")) glossCur = groundMat.GetFloat("_Glossiness");
            }
            groundTarget = dryColor;

            var fx = new GameObject("WeatherFX");
            fx.transform.SetParent(parent, false);
            rain = VFXKit.MakeRain(fx.transform);
            snow = VFXKit.MakeSnow(fx.transform);
            dust = VFXKit.MakeDust(fx.transform);
            rain.Play(); snow.Play(); dust.Play();   // 常开:发射率 0 即停止

            RenderSettings.fogMode = FogMode.Exponential;
            ApplyRates();
        }

        /// <summary>雾色基调(昼夜控制器每帧同步)</summary>
        public void SetFogTint(Color c) => fogTint = c;

        public void SetWeather(WeatherKind k, float density)
        {
            Kind = k;
            Density01 = Mathf.Clamp(density, 0.05f, 1f);
            EventBus.Publish("环境", "weather", $"天气切换 {k} 浓度 {Density01:P0}", EventGrade.Info);
            ApplyRates();
        }

        void ApplyRates()
        {
            float d = Density01;
            fogTarget = Kind switch
            {
                WeatherKind.Rain => 0.0035f + 0.004f * d,
                WeatherKind.Snow => 0.002f + 0.0032f * d,
                WeatherKind.Fog => 0.016f + 0.05f * d,
                WeatherKind.Dust => 0.012f + 0.045f * d,
                _ => 0f,
            };
            VFXKit.SetRate(rain, Kind == WeatherKind.Rain ? 350f + 2300f * d : 0f);
            VFXKit.SetRate(snow, Kind == WeatherKind.Snow ? 160f + 950f * d : 0f);
            VFXKit.SetRate(dust, Kind == WeatherKind.Dust ? 90f + 380f * d : 0f);
            groundTarget = Kind == WeatherKind.Rain ? dryColor * 0.55f : dryColor;
            glossTarget = Kind == WeatherKind.Rain ? 0.88f : 0.2f;
        }

        void Update()
        {
            // 发射盒跟随相机(沙尘重力为零,须贴低才进画面;雨雪自落可高位)
            if (Cam != null)
            {
                var cp = Cam.transform.position;
                var p = cp + Vector3.up * 16f;
                rain.transform.position = p;
                snow.transform.position = p;
                dust.transform.position = cp;   // 尘带对准相机高度(视域中心)
            }

            // 沙尘风速驱动
            if (dust != null && Kind == WeatherKind.Dust)
            {
                var w = WindField.Steady * 2.6f;
                var vel = dust.velocityOverLifetime;
                vel.x = new ParticleSystem.MinMaxCurve(w.x * 0.7f, w.x * 1.3f);
                vel.z = new ParticleSystem.MinMaxCurve(w.z * 0.7f, w.z * 1.3f);
            }

            // 雾/地面 湿滑过渡(模拟时间:暂停即冻结)
            float dt = Time.deltaTime;
            RenderSettings.fog = fogTarget > 0.0001f || RenderSettings.fogDensity > 0.0001f;
            RenderSettings.fogDensity = Mathf.MoveTowards(RenderSettings.fogDensity, fogTarget, dt * 0.012f);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, ComposeFogColor(), Mathf.Clamp01(dt * 2f));
            if (groundMat != null)
            {
                glossCur = Mathf.MoveTowards(glossCur, glossTarget, dt * 0.5f);
                groundMat.SetFloat("_Glossiness", glossCur);
                groundMat.color = Color.Lerp(groundMat.color, groundTarget, Mathf.Clamp01(dt * 1.5f));
            }

            // 沙尘全屏噪点遮挡
            Overlay.Noise(Kind == WeatherKind.Dust ? Density01 * 0.38f : 0f);
        }

        Color ComposeFogColor()
        {
            switch (Kind)
            {
                case WeatherKind.Dust: return Color.Lerp(fogTint, new Color(0.55f, 0.44f, 0.3f), 0.65f);
                case WeatherKind.Snow: return Color.Lerp(fogTint, new Color(0.8f, 0.84f, 0.9f), 0.4f);
                case WeatherKind.Rain: return Color.Lerp(fogTint, new Color(0.4f, 0.45f, 0.52f), 0.35f);
                default: return fogTint;
            }
        }
    }
}

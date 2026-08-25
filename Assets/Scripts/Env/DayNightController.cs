using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 昼夜时段控制:三档预设(太阳旋转/颜色/强度、三色环境光、相机天幕色、雾色基调),
    /// 3 秒平滑渐变(unscaled 时间驱动,Setup/暂停期同样可观赏)。
    /// </summary>
    public class DayNightController : MonoBehaviour
    {
        public struct Preset
        {
            public Vector3 SunEuler;
            public Color SunColor;
            public float SunIntensity;
            public Color AmbSky, AmbEq, AmbGround, Bg, FogTint;
            public float AtmThick, SunSize, Expo;      // 程序化天空盒参数(V1)
        }

        public static readonly Preset[] Presets =
        {
            // 白昼(低角度暖阳 → 长影子,实景摄影感)
            new Preset { SunEuler = new Vector3(38f, -24f, 0f), SunColor = new Color(1f, 0.93f, 0.8f), SunIntensity = 1.18f,
                AmbSky = new Color(0.36f, 0.47f, 0.64f), AmbEq = new Color(0.5f, 0.47f, 0.42f), AmbGround = new Color(0.21f, 0.2f, 0.18f),
                Bg = new Color(0.42f, 0.62f, 0.85f), FogTint = new Color(0.62f, 0.7f, 0.78f),
                AtmThick = 1f, SunSize = 0.04f, Expo = 1.25f },
            // 黄昏(低角度暖阳)
            new Preset { SunEuler = new Vector3(9f, 62f, 0f), SunColor = new Color(1f, 0.6f, 0.36f), SunIntensity = 0.78f,
                AmbSky = new Color(0.42f, 0.28f, 0.34f), AmbEq = new Color(0.5f, 0.35f, 0.28f), AmbGround = new Color(0.2f, 0.15f, 0.12f),
                Bg = new Color(0.78f, 0.42f, 0.3f), FogTint = new Color(0.72f, 0.52f, 0.44f),
                AtmThick = 1.45f, SunSize = 0.06f, Expo = 1.05f },
            // 夜晚(月光)
            new Preset { SunEuler = new Vector3(24f, 155f, 0f), SunColor = new Color(0.55f, 0.66f, 0.95f), SunIntensity = 0.16f,
                AmbSky = new Color(0.055f, 0.075f, 0.14f), AmbEq = new Color(0.075f, 0.085f, 0.13f), AmbGround = new Color(0.025f, 0.03f, 0.05f),
                Bg = new Color(0.012f, 0.02f, 0.05f), FogTint = new Color(0.05f, 0.07f, 0.12f),
                AtmThick = 0.85f, SunSize = 0.02f, Expo = 0.07f },
        };

        public Light Sun;
        public Camera Cam;
        /// <summary>程序化天空盒材质(EnvironmentRig 注入;null = 纯色天幕旧路径)</summary>
        public Material Sky;
        public DayPhase Phase => phase;

        Preset from, to;
        Quaternion fromRot, toRot;
        float blend = 1f;
        DayPhase phase = DayPhase.Day;
        Color fogCur = Presets[0].FogTint;

        public float SunIntensity => Sun != null ? Sun.intensity : 0f;
        public Color CurrentFogTint => fogCur;
        float BlendK => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blend));

        public void SetPhase(DayPhase p, bool immediate)
        {
            phase = p;
            from = Snapshot();
            fromRot = Sun != null ? Sun.transform.rotation : Quaternion.Euler(from.SunEuler);
            to = Presets[(int)p];
            toRot = Quaternion.Euler(to.SunEuler);
            blend = immediate ? 1f : 0f;
            if (immediate) Apply(1f);
        }

        /// <summary>抓取当前真实场景光照态作为渐变起点(中途再切也连续)</summary>
        Preset Snapshot()
        {
            var s = new Preset
            {
                SunEuler = Vector3.zero,
                SunColor = Sun != null ? Sun.color : Color.white,
                SunIntensity = Sun != null ? Sun.intensity : 1f,
                AmbSky = RenderSettings.ambientSkyColor,
                AmbEq = RenderSettings.ambientEquatorColor,
                AmbGround = RenderSettings.ambientGroundColor,
                Bg = Cam != null ? Cam.backgroundColor : Presets[0].Bg,
                FogTint = fogCur,
                AtmThick = SkyFloat("_AtmosphereThickness", to.AtmThick),
                SunSize = SkyFloat("_SunSize", to.SunSize),
                Expo = SkyFloat("_Exposure", to.Expo),
            };
            return s;
        }

        float SkyFloat(string prop, float fallback)
        {
            if (Sky == null || !Sky.HasProperty(prop)) return fallback;
            return Sky.GetFloat(prop);
        }

        void Update()
        {
            if (blend >= 1f) return;
            blend = Mathf.Clamp01(blend + Time.unscaledDeltaTime / 3f);
            Apply(BlendK);
        }

        void Apply(float k)
        {
            if (Sun != null)
            {
                Sun.transform.rotation = Quaternion.Slerp(fromRot, toRot, k);
                Sun.color = Color.Lerp(from.SunColor, to.SunColor, k);
                Sun.intensity = Mathf.Lerp(from.SunIntensity, to.SunIntensity, k);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(from.AmbSky, to.AmbSky, k);
            RenderSettings.ambientEquatorColor = Color.Lerp(from.AmbEq, to.AmbEq, k);
            RenderSettings.ambientGroundColor = Color.Lerp(from.AmbGround, to.AmbGround, k);
            bool skyOn = Sky != null && RenderSettings.skybox == Sky;
            if (skyOn)
            {
                if (Cam != null) Cam.clearFlags = CameraClearFlags.Skybox;
                if (Sky.HasProperty("_AtmosphereThickness"))
                    Sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(from.AtmThick, to.AtmThick, k));
                if (Sky.HasProperty("_SunSize"))
                    Sky.SetFloat("_SunSize", Mathf.Lerp(from.SunSize, to.SunSize, k));
                if (Sky.HasProperty("_Exposure"))
                    Sky.SetFloat("_Exposure", Mathf.Lerp(from.Expo, to.Expo, k));
            }
            else if (Cam != null)
            {
                Cam.clearFlags = CameraClearFlags.SolidColor;
                Cam.backgroundColor = Color.Lerp(from.Bg, to.Bg, k);
            }
            fogCur = Color.Lerp(from.FogTint, to.FogTint, k);
        }
    }
}

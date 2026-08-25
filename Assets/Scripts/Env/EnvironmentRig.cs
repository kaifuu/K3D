using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 环境总装:ModeManager 在每个模式 Build 后 Install,
    /// 按 ModeStartParams 统一应用 昼夜时段/天气(风力已由 ModeManager 直配 WindField)。
    /// 所有模式因此免费获得菜单环境参数;EnvAdaptMode 借此实时切换。
    /// </summary>
    public class EnvironmentRig : MonoBehaviour
    {
        public static EnvironmentRig I { get; private set; }
        void OnEnable() => I = this;                       // 域重载防御:重建即重注册
        void OnDisable() { if (I == this) I = null; }

        public DayNightController DayNight { get; private set; }
        public WeatherSystem Weather { get; private set; }
        public CityLights City { get; private set; }

        public static EnvironmentRig Install(DrillContext ctx)
        {
            var root = ctx.ModeRoot;

            // 太阳:优先复用模式自建的平行光,没有则补一盏
            Light sun = null;
            foreach (var li in root.GetComponentsInChildren<Light>())
                if (li.type == LightType.Directional) { sun = li; break; }
            if (sun == null) sun = EnvironmentBuilder.BuildLighting(root);

            // 地面材质(雨天湿润联动,可缺省)
            Material groundMat = null;
            var ground = root.Find("Ground");
            if (ground != null)
            {
                var rend = ground.GetComponent<Renderer>();
                if (rend != null) groundMat = rend.material;
            }

            var go = new GameObject("EnvironmentRig");
            go.transform.SetParent(root, false);
            var rig = go.AddComponent<EnvironmentRig>();

            rig.DayNight = go.AddComponent<DayNightController>();
            rig.DayNight.Sun = sun;
            rig.DayNight.Cam = ctx.MainCamera != null ? ctx.MainCamera : Camera.main;
            RenderSettings.sun = sun;                      // 程序化天空盒的太阳盘跟随此灯

            // V1 实物还原:程序化天空盒(昼夜 Preset 驱动);无该着色器则维持纯色天幕
            var sky = MaterialLib.CreateSky();
            rig.DayNight.Sky = sky;
            RenderSettings.skybox = sky;
            if (sky != null && rig.DayNight.Cam != null)
                rig.DayNight.Cam.clearFlags = CameraClearFlags.Skybox;

            rig.City = go.AddComponent<CityLights>();
            rig.City.Build(root);

            // V5 实景:反射探针(盒式覆盖全场)—— 幕墙/车漆拿到天空与街区反射
            // (枚举在 UnityEngine.Rendering 下,时间切片枚举名为 ReflectionProbeTimeSlicingMode)
            var probeGo = new GameObject("ReflectionProbe");
            probeGo.transform.SetParent(root, false);
            probeGo.transform.position = new Vector3(0f, 26f, 40f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.EveryFrame;
            // timeSlicing 默认 IndividualFaces(逐面分帧刷新),6000.5 运行时无该可写属性
            probe.size = new Vector3(620f, 110f, 620f);
            probe.resolution = 128;
            probe.nearClipPlane = 0.3f;
            probe.farClipPlane = 800f;

            // V5 实景:云层(程序云贴图公告板,亮度随昼夜环境色)
            CloudLayer.Build(root);

            rig.Weather = go.AddComponent<WeatherSystem>();
            rig.Weather.Setup(go.transform, rig.DayNight.Cam, groundMat);
            return rig;
        }

        public void ApplyParams(ModeStartParams p)
        {
            SetPhase(p.Phase);
            Weather.SetWeather(p.Weather, p.WeatherDensity);
        }

        public void SetPhase(DayPhase p)
        {
            DayNight.SetPhase(p, false);
            City.SetNight(p == DayPhase.Night);
        }

        public void SetWeather(WeatherKind k, float density) => Weather.SetWeather(k, density);

        void Update()
        {
            // 昼夜渐变期间雾色基调同步给天气系统
            if (Weather != null && DayNight != null)
                Weather.SetFogTint(DayNight.CurrentFogTint);
        }
    }
}

using System.Text;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 模块3 昼夜与天气适应:六边巡航航线自动飞行 + 城市楼群街灯;
    /// 侧板实时切换 白昼/黄昏/夜晚 × 晴/雨/雪/雾/沙尘 + 浓度/风力;
    /// 指标导出 太阳强度/雾密度/粒子数/湿地光泽/帧率(无头矩阵验收)。
    /// </summary>
    public class EnvAdaptMode : DrillMode
    {
        public override string Id => "env";
        public override string Title => "昼夜与天气适应";
        public override string Brief =>
            "白昼/黄昏/夜晚平滑渐变,雨雪雾沙尘浓度可调,大风扰动联动飞行;城市夜灯、雨天湿地反光、沙尘噪点遮挡。";

        FlightBody body;
        RouteFollower follower;
        PlayerFlightInput pInput;
        RouteData route;
        RouteVisual visual;
        EnvironmentRig rig;
        Vector3 padPos = new Vector3(0f, 0.55f, 0f);
        float density = 0.7f;
        float windMps = 2f;
        float fps = 60f;

        public override void Build()
        {
            EnvironmentBuilder.ResetToDayDefault();
            EnvironmentBuilder.BuildLighting(Root);
            EnvironmentBuilder.CreateGround(Root);
            EnvironmentBuilder.MakeRing(Root, 130f, new Color(0.3f, 0.5f, 0.6f, 0.22f), "FieldBound", 0.05f);
            BuildMidBlocks();
            EnvironmentBuilder.MakeFlatDisc(Root, 6f, new Color(0.15f, 0.65f, 0.85f, 0.18f), "Pad");
            EnvironmentBuilder.MakeRing(Root, 6.4f, new Color(0.2f, 0.8f, 1f, 0.5f), "PadRing", 0.06f);

            // ---- 玩家机体(本模式自动巡航,不接玩家输入) ----
            var go = DroneFactory.Spawn(DroneRole.Player, Root, padPos, "PlayerDrone");
            body = go.AddComponent<FlightBody>();
            body.HomePos = padPos;
            pInput = go.AddComponent<PlayerFlightInput>();
            pInput.Body = body;
            pInput.Enabled = false;
            follower = go.AddComponent<RouteFollower>();
            follower.Body = body;

            // ---- 六边巡航环(半径60 高26,展示光效与天气纵深) ----
            route = new RouteData();
            follower.Route = route;
            visual = NewGo("RouteVisual").AddComponent<RouteVisual>();
            visual.Route = route;
            var markers = NewGo("Waypoints").AddComponent<WaypointMarker>();
            markers.Route = route;
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + Mathf.PI / 6f;
                route.Add(new Vector3(Mathf.Cos(a) * 60f, 26f, Mathf.Sin(a) * 60f));
            }
            route.Loop = true;

            var cam = CameraDirector.CreateCamera(Root);
            CameraDirector.Follow(cam, go.transform, 17f);
            Ctx.MainCamera = cam;
        }

        void BuildMidBlocks()
        {
            // 场中低矮障碍(高度≤20,留出26m航线净空),制造光影与雾纵深
            var spots = new[]
            {
                new Vector3(-30f, 0f, -38f), new Vector3(34f, 0f, -30f), new Vector3(42f, 0f, 26f),
                new Vector3(-38f, 0f, 32f), new Vector3(-2f, 0f, 58f), new Vector3(4f, 0f, -62f),
            };
            float[] hts = { 14f, 18f, 12f, 20f, 10f, 16f };
            for (int i = 0; i < spots.Length; i++)
                PropKit.Building(Root, spots[i], 11f, hts[i], 11f, i % 3);
        }

        public override void OnStart()
        {
            rig = EnvironmentRig.I;
            follower.Cruise = 9f;
            follower.DeviationLimit = 25f;   // 环境演示:不启用航线偏差告警
            follower.StartRoute();
        }

        public override void OnTick(float dt)
        {
            if (body == null) return;
            rig = rig ?? EnvironmentRig.I;
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.06f);

            if (Input.GetKeyDown(KeyCode.R))
            {
                body.Teleport(padPos, 0f);
                body.ResetStats();
                follower.StartRoute();
                EventBus.Publish("任务", "", "机体已归位重置", EventGrade.Op);
            }

            var cam = Ctx.MainCamera;
            if (cam != null)
            {
                Overlay.Label(body.transform.position + Vector3.up * 2.2f,
                    $"ME  alt {body.Altitude:0.0}m  spd {body.Speed:0.0}m/s", new Color(0.4f, 0.9f, 1f));
                if (rig != null && rig.Weather != null)
                    Overlay.Label(padPos + Vector3.up * 8f,
                        $"{PhaseName(rig.DayNight.Phase)} · {WeatherName(rig.Weather.Kind)} {rig.Weather.Density01:P0}",
                        new Color(0.75f, 0.85f, 0.6f));
            }
        }

        static string PhaseName(DayPhase p) => p switch
        { DayPhase.Day => "白昼", DayPhase.Dusk => "黄昏", _ => "夜晚" };
        static string WeatherName(WeatherKind k) => k switch
        { WeatherKind.Clear => "晴", WeatherKind.Rain => "雨", WeatherKind.Snow => "雪",
          WeatherKind.Fog => "雾", _ => "沙尘" };

        // ---------- UI ----------
        public override void DrawSidePanel(Rect r)
        {
            if (body == null || rig == null || rig.Weather == null || rig.DayNight == null) return;
            float y = r.y;

            GUI.Label(new Rect(r.x, y, r.width, 20), "环境控制", PanelKit.Header);
            y += 24;
            GUI.Label(new Rect(r.x, y, r.width, 16), "时段(3 秒平滑渐变)", PanelKit.Mini);
            y += 18;
            float w3 = (r.width - 8f) / 3f;
            string[] ph = { "白昼", "黄昏", "夜晚" };
            for (int i = 0; i < 3; i++)
                if (PanelKit.ToggleBtn(r.x + i * (w3 + 4f), y, w3, 24, ph[i], (int)rig.DayNight.Phase == i))
                    rig.SetPhase((DayPhase)i);
            y += 30;

            GUI.Label(new Rect(r.x, y, r.width, 16), "天气", PanelKit.Mini);
            y += 18;
            float w5 = (r.width - 16f) / 5f;
            string[] wk = { "晴", "雨", "雪", "雾", "沙尘" };
            for (int i = 0; i < 5; i++)
                if (PanelKit.ToggleBtn(r.x + i * (w5 + 4f), y, w5, 24, wk[i], (int)rig.Weather.Kind == i))
                    rig.SetWeather((WeatherKind)i, density);
            y += 30;

            GUI.Label(new Rect(r.x, y, r.width, 16), $"天气浓度 {density:P0}", PanelKit.Mini);
            y += 16;
            float nd = GUI.HorizontalSlider(new Rect(r.x, y, r.width, 14), density, 0.05f, 1f);
            if (!Mathf.Approximately(nd, density)) { density = nd; rig.SetWeather(rig.Weather.Kind, density); }
            y += 22;

            GUI.Label(new Rect(r.x, y, r.width, 16), $"风力 {windMps:0.#} m/s", PanelKit.Mini);
            y += 16;
            float nw = GUI.HorizontalSlider(new Rect(r.x, y, r.width, 14), windMps, 0f, 12f);
            if (!Mathf.Approximately(nw, windMps))
            {
                windMps = nw;
                WindField.Configure(new Vector3(1f, 0f, 0.35f), windMps);
            }
            y += 26;

            GUI.Label(new Rect(r.x, y, r.width, 20), "环境状态", PanelKit.Header);
            y += 22;
            GUI.Label(new Rect(r.x, y, r.width, 16),
                $"太阳强度 {rig.DayNight.SunIntensity:0.00}   雾 {(rig.Weather.FogOn ? $"{rig.Weather.FogDensity * 1000:0.0}‰" : "关")}", PanelKit.Small);
            y += 16;
            GUI.Label(new Rect(r.x, y, r.width, 16),
                $"粒子 雨{rig.Weather.RainAlive} 雪{rig.Weather.SnowAlive} 尘{rig.Weather.DustAlive}   帧率 {fps:0}", PanelKit.Small);
            y += 16;
            GUI.Label(new Rect(r.x, y, r.width, 16),
                $"巡航 里程{follower.Dist:0}m 圈{follower.Loops}   高度 {body.Altitude:0.0}m", PanelKit.Small);
            y += 24;
            GUI.Label(new Rect(r.x, y, r.width, r.y + r.height - y),
                "切时段看天色渐变与城市夜灯;\n雨=湿地反光+雨丝,雪=飘落堆积感,\n雾=能见度下降,沙尘=噪点遮挡+贴地尘团;\n风力滑块实时加大飞行扰动。R 机体归位。", PanelKit.Mini);
        }

        public override void DrawHint(StringBuilder sb)
        {
            sb.Append("侧板实时切换 时段×天气×浓度×风力 | 夜晚看城市灯光 | 沙尘有噪点遮挡 | R 机体归位");
        }

        public override void WriteMetrics(StringBuilder sb)
        {
            if (body == null || rig == null || rig.Weather == null) return;
            sb.AppendLine($"env=phase:{rig.DayNight.Phase} weather:{rig.Weather.Kind} density:{rig.Weather.Density01:0.00}");
            sb.AppendLine($"sun=intensity:{rig.DayNight.SunIntensity:0.00} cityLights:{rig.City.LightsOn} maxPoint:{rig.City.MaxPointIntensity:0.00}");
            sb.AppendLine($"fog=on:{rig.Weather.FogOn} density:{rig.Weather.FogDensity:0.000} gloss:{rig.Weather.GroundGloss:0.00}");
            sb.AppendLine($"particles=rain:{rig.Weather.RainAlive} snow:{rig.Weather.SnowAlive} dust:{rig.Weather.DustAlive}");
            sb.AppendLine($"wind={WindField.SpeedMps:0.0}m/s fps:{fps:0}");
            sb.AppendLine($"cruise=dist:{follower.Dist:0}m loops:{follower.Loops} droneAlt:{body.Altitude:0.0}");
        }

        // ---------- 无头剧本:昼夜×天气矩阵 ----------
        public override void RunHeadlessScenario(string name)
        {
            var sc = ScenarioRunner.I;
            if (sc == null || body == null) return;
            rig = EnvironmentRig.I;

            sc.At(6f, () => HeadlessAssert.Check(rig.DayNight.SunIntensity > 0.9f && !rig.Weather.FogOn,
                $"6s 基线白昼(太阳 {rig.DayNight.SunIntensity:0.00} 雾关)"));
            sc.At(10f, () => rig.SetPhase(DayPhase.Dusk));
            sc.At(14f, () => HeadlessAssert.Check(rig.DayNight.SunIntensity > 0.5f && rig.DayNight.SunIntensity < 0.95f,
                $"14s 黄昏过渡(太阳 {rig.DayNight.SunIntensity:0.00})"));
            sc.At(18f, () => rig.SetWeather(WeatherKind.Rain, 0.7f));
            sc.At(24f, () => HeadlessAssert.Check(rig.Weather.FogOn && rig.Weather.RainAlive > 400 && rig.Weather.GroundGloss > 0.5f,
                $"24s 雨生效(雾{rig.Weather.FogDensity * 1000:0.0}‰ 粒子{rig.Weather.RainAlive} 光泽{rig.Weather.GroundGloss:0.00})"));
            sc.At(28f, () => rig.SetPhase(DayPhase.Night));
            sc.At(34f, () => HeadlessAssert.Check(rig.City.LightsOn && rig.DayNight.SunIntensity < 0.3f && rig.City.MaxPointIntensity > 0.5f,
                $"34s 夜灯点亮(太阳 {rig.DayNight.SunIntensity:0.00} 点光 {rig.City.MaxPointIntensity:0.00})"));
            sc.At(38f, () => rig.SetWeather(WeatherKind.Snow, 0.8f));
            sc.At(44f, () => HeadlessAssert.Check(rig.Weather.SnowAlive > 300,
                $"44s 雪生效(粒子 {rig.Weather.SnowAlive})"));
            sc.At(48f, () => rig.SetWeather(WeatherKind.Fog, 0.8f));
            sc.At(54f, () => HeadlessAssert.Check(rig.Weather.FogDensity > 0.03f,
                $"54s 雾生效(密度 {rig.Weather.FogDensity:0.000})"));
            sc.At(58f, () => rig.SetWeather(WeatherKind.Dust, 0.8f));
            sc.At(64f, () => HeadlessAssert.Check(rig.Weather.FogDensity > 0.015f && rig.Weather.DustAlive > 100,
                $"64s 沙尘生效(雾{rig.Weather.FogDensity:0.000} 粒子{rig.Weather.DustAlive})"));
            sc.At(68f, () => { rig.SetWeather(WeatherKind.Clear, 0.5f); rig.SetPhase(DayPhase.Day); });
            sc.At(74f, () => HeadlessAssert.Check(!rig.Weather.FogOn && rig.DayNight.SunIntensity > 0.9f && rig.Weather.AliveParticles == 0,
                $"74s 恢复白昼晴(雾关 太阳 {rig.DayNight.SunIntensity:0.00} 粒子 {rig.Weather.AliveParticles})"));
            sc.At(76f, () => WindField.Configure(new Vector3(1f, 0f, 0.3f), 10f));
            sc.At(78f, () => HeadlessAssert.Check(WindField.SpeedMps > 9f && follower.Dist > 250f && body.Altitude > 15f,
                $"78s 大风扰动+持续巡航(风 {WindField.SpeedMps:0.0}m/s 里程 {follower.Dist:0}m)"));
        }
    }
}

using System.Text;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 模块8 设备故障模拟:巡航中面板/按键注入四类故障
    /// (GPS干扰/低电量/电机故障/陀螺漂移),观察现象+测量值,解除后恢复;
    /// 无头剧本自动依次注入并断言(抖动RMS/限速比/横滚峰值/航向漂移/恢复)。
    /// </summary>
    public class FaultMode : DrillMode
    {
        public override string Id => "fault";
        public override string Title => "设备故障模拟";
        public override string Brief =>
            "巡航中注入 GPS干扰/低电量/电机故障/陀螺漂移四类故障:定位抖动、自动限速、失衡侧倾+停转旋翼+灰烟、航向持续偏转;实时测量抖动RMS/限速比/横滚峰值/漂移角,一键解除恢复。";

        RouteData route;
        RouteVisual visual;
        WaypointMarker markers;
        RouteFollower follower;
        FaultService service;
        FaultEffects fx;
        FlightBody body;
        PlayerFlightInput pInput;
        Vector3 padPos = new Vector3(0f, 0.55f, 0f);

        public override void Build()
        {
            EnvironmentBuilder.ResetToDayDefault();
            EnvironmentBuilder.BuildLighting(Root);
            EnvironmentBuilder.CreateGround(Root);
            EnvironmentBuilder.MakeRing(Root, 120f, new Color(0.3f, 0.5f, 0.6f, 0.22f), "FieldBound", 0.05f);
            BuildScenery();
            EnvironmentBuilder.MakeFlatDisc(Root, 6f, new Color(0.15f, 0.65f, 0.85f, 0.18f), "Pad");
            EnvironmentBuilder.MakeRing(Root, 6.4f, new Color(0.2f, 0.8f, 1f, 0.5f), "PadRing", 0.06f);

            // ---- 玩家机体 + 巡航跟随器 ----
            var go = DroneFactory.Spawn(DroneRole.Player, Root, padPos, "FaultDrone");
            body = go.AddComponent<FlightBody>();
            body.HomePos = padPos;
            pInput = go.AddComponent<PlayerFlightInput>();
            pInput.Body = body;
            follower = go.AddComponent<RouteFollower>();
            follower.Body = body;

            // ---- 故障服务 + 视觉表征 ----
            service = NewGo("FaultService").AddComponent<FaultService>();
            service.Bind(body, follower);
            fx = NewGo("FaultEffects").AddComponent<FaultEffects>();
            fx.Body = body;

            // ---- 航线物件 ----
            route = new RouteData();
            follower.Route = route;
            visual = NewGo("RouteVisual").AddComponent<RouteVisual>();
            visual.Route = route;
            markers = NewGo("Waypoints").AddComponent<WaypointMarker>();
            markers.Route = route;

            // ---- 相机 ----
            var cam = CameraDirector.CreateCamera(Root);
            CameraDirector.Follow(cam, go.transform, 15f);
            Ctx.MainCamera = cam;
        }

        void BuildScenery()
        {
            var spots = new[] {
                new Vector3(-78f, 0f, -20f), new Vector3(85f, 0f, 40f) };
            float[] hts = { 24f, 18f };
            for (int i = 0; i < spots.Length; i++)
                PropKit.Building(Root, spots[i], 14f, hts[i], 14f, i);
        }

        void InjectKind(FaultKind k)
        {
            if (k == FaultKind.None) return;
            if (service.Active == k) return;
            if (!follower.Started) StartCruise();   // 未巡航则自动开航(保证故障有载体)
            service.Inject(k);
            fx.Show(k);
        }

        void ClearFault()
        {
            service.Clear();
            fx.Dispose();
        }

        void StartCruise()
        {
            if (route.Count < 2 || follower.Started) return;
            pInput.Enabled = false;
            follower.Cruise = 12f;
            follower.DeviationLimit = 8f;
            follower.StartRoute();
        }

        // ---------- 交互 ----------
        public override void OnTick(float dt)
        {
            if (body == null) return;

            pInput.Enabled = !follower.Started;
            markers.HighlightIndex = follower.Active && route.Count >= 2
                ? route.IndexAt(Mathf.Repeat(follower.Dist, route.TotalLength))
                : -1;

            if (Input.GetKeyDown(KeyCode.Alpha1)) InjectKind(FaultKind.GpsJam);
            if (Input.GetKeyDown(KeyCode.Alpha2)) InjectKind(FaultKind.LowBattery);
            if (Input.GetKeyDown(KeyCode.Alpha3)) InjectKind(FaultKind.MotorFault);
            if (Input.GetKeyDown(KeyCode.Alpha4)) InjectKind(FaultKind.GyroDrift);
            if (Input.GetKeyDown(KeyCode.C)) ClearFault();
            if (Input.GetKeyDown(KeyCode.R)) ResetAll();

            // 悬浮标注
            var cam = Ctx.MainCamera;
            if (cam != null)
            {
                var dp = body.transform.position;
                Overlay.Label(dp + Vector3.up * 2.2f,
                    $"ME  alt {body.Altitude:0.0}m  spd {body.Speed:0.0}m  偏差 {follower.Deviation:0.0}m", new Color(0.4f, 0.9f, 1f));
                if (service.Active != FaultKind.None)
                {
                    var msg = FaultBrief();
                    Overlay.Label(dp + Vector3.up * 3.6f, msg, new Color(1f, 0.4f, 0.3f));
                }
            }
        }

        string FaultBrief()
        {
            switch (service.Active)
            {
                case FaultKind.GpsJam: return $"⚠ GPS干扰 抖动RMS {service.JitterRms:0.0}m/s";
                case FaultKind.LowBattery: return $"⚠ 低电量 限速 {body.HorizSpeed:0.0}/{service.SpeedRef:0.0}m/s";
                case FaultKind.MotorFault: return $"⚠ 电机故障 横滚 {body.RollDeg:0.0}° 峰值 {service.RollPeak:0.0}°";
                case FaultKind.GyroDrift: return $"⚠ 陀螺漂移 航向偏转 {Mathf.Abs(Mathf.DeltaAngle(service.YawAtInject, body.HeadingDeg)):0}°";
            }
            return "";
        }

        void ResetAll()
        {
            ClearFault();
            follower.StopRoute();
            body.Teleport(padPos, 0f);
            body.ResetStats();
            EventBus.Publish("故障", "", "机体已归位重置,故障全部解除", EventGrade.Op);
        }

        // ---------- UI ----------
        public override void DrawSidePanel(Rect r)
        {
            if (body == null || service == null) return;
            float y = r.y;
            float bw = (r.width - 8f) / 2f;

            GUI.Label(new Rect(r.x, y, r.width, 20), "故障注入(单故障模型)", PanelKit.Header);
            y += 24;
            if (PanelKit.ToggleBtn(r.x, y, bw, 26, "1 GPS干扰", service.Active == FaultKind.GpsJam)) InjectKind(FaultKind.GpsJam);
            if (PanelKit.ToggleBtn(r.x + bw + 8f, y, bw, 26, "2 低电量", service.Active == FaultKind.LowBattery)) InjectKind(FaultKind.LowBattery);
            y += 30;
            if (PanelKit.ToggleBtn(r.x, y, bw, 26, "3 电机故障", service.Active == FaultKind.MotorFault)) InjectKind(FaultKind.MotorFault);
            if (PanelKit.ToggleBtn(r.x + bw + 8f, y, bw, 26, "4 陀螺漂移", service.Active == FaultKind.GyroDrift)) InjectKind(FaultKind.GyroDrift);
            y += 30;
            if (PanelKit.Btn(r.x, y, r.width - 12f, 24, "解除故障 (C)", service.Active != FaultKind.None)) ClearFault();
            y += 30;

            GUI.Label(new Rect(r.x, y, r.width, 20), "实时测量", PanelKit.Header);
            y += 22;
            var prev = GUI.color;
            if (service.Active != FaultKind.None) GUI.color = new Color(1f, 0.55f, 0.4f);
            GUI.Label(new Rect(r.x, y, r.width, 16),
                $"当前故障 <b>{service.ActiveName}</b>  持续 {service.Elapsed:0.0}s", PanelKit.Small);
            GUI.color = prev;
            y += 18;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"抖动RMS {service.JitterRms:0.00} m/s   速度 {body.HorizSpeed:0.0}/{service.SpeedRef:0.0} m/s", PanelKit.Small);
            y += 18;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"横滚 {body.RollDeg:0.0}°(峰值 {service.RollPeak:0.0}°)   航向漂移 {Mathf.Abs(Mathf.DeltaAngle(service.YawAtInject, body.HeadingDeg)):0}°", PanelKit.Small);
            y += 24;

            GUI.Label(new Rect(r.x, y, r.width, 20), "巡航", PanelKit.Header);
            y += 22;
            GUI.Label(new Rect(r.x, y, r.width, 16),
                follower.Started ? $"巡航中 速度 {follower.Cruise:0.0}m/s 偏差 {follower.Deviation:0.0}m 圈 {follower.Loops}" : "待命(注入故障将自动开航)", PanelKit.Small);
            y += 26;

            GUI.Label(new Rect(r.x, y, r.width, r.y + r.height - y),
                "1-4 注入故障 C解除 R重置\nGPS干扰→机体抖动 | 低电→自动限速\n电机→侧倾+停桨+灰烟 | 陀螺→缓慢偏航", PanelKit.Mini);
        }

        public override void DrawHint(StringBuilder sb)
        {
            sb.Append("1-4 注入故障 C解除 R重置 | 巡航自动建立,观察现象与测量值");
        }

        public override void WriteMetrics(StringBuilder sb)
        {
            if (service == null || body == null) return;
            sb.AppendLine($"fault=active:{service.ActiveName} elapsed:{service.Elapsed:0.0}s");
            sb.AppendLine($"fmeas=jitterRMS:{service.JitterRms:0.00}mps speed:{body.HorizSpeed:0.0}/{service.SpeedRef:0.0}mps rollPeak:{service.RollPeak:0.0}deg yawDrift:{Mathf.Abs(Mathf.DeltaAngle(service.YawAtInject, body.HeadingDeg)):0.0}deg");
            sb.AppendLine($"cruise=started:{follower.Started} cruiseSpd:{follower.Cruise:0.0} dev:{follower.Deviation:0.0}m maxDev:{follower.MaxDeviation:0.0}m loops:{follower.Loops}");
            sb.AppendLine($"drone=alt:{body.Altitude:0.0} spd:{body.Speed:0.0} roll:{body.RollDeg:0.0} yaw:{body.HeadingDeg:0} dist:{body.DistanceFlown:0}");
        }

        // ---------- 无头剧本 ----------
        public override void RunHeadlessScenario(string name)
        {
            var sc = ScenarioRunner.I;
            if (sc == null || body == null) return;

            // 程序化打点:80m 方环 @20m,巡航 12m/s
            route.Add(new Vector3(0f, 20f, 0f));
            route.Add(new Vector3(60f, 20f, 0f));
            route.Add(new Vector3(60f, 20f, 60f));
            route.Add(new Vector3(0f, 20f, 60f));
            route.Loop = true;
            pInput.Enabled = false;
            StartCruise();

            sc.At(8f, () => HeadlessAssert.Check(body.Altitude > 12f && follower.Active,
                $"8s 巡航建立(高度 {body.Altitude:0.0}m 偏差 {follower.Deviation:0.0}m)"));

            // ---- 故障1:GPS干扰(12-18s) ----
            sc.At(12f, () => InjectKind(FaultKind.GpsJam));
            sc.At(16f, () => HeadlessAssert.Check(service.JitterRms > 0.8f,
                $"16s GPS干扰速度抖动RMS {service.JitterRms:0.00} m/s > 0.8"));
            sc.At(18f, () => ClearFault());

            // ---- 故障2:低电量(24-31s) ----
            sc.At(24f, () => InjectKind(FaultKind.LowBattery));
            sc.At(29f, () => HeadlessAssert.Check(body.HorizSpeed < 0.6f * service.SpeedRef,
                $"29s 低电限速 {body.HorizSpeed:0.0} < {0.6f * service.SpeedRef:0.0} m/s(参考 {service.SpeedRef:0.0})"));
            sc.At(31f, () => ClearFault());

            // ---- 故障3:电机故障(34-39s) ----
            sc.At(34f, () => InjectKind(FaultKind.MotorFault));
            sc.At(38f, () => HeadlessAssert.Check(service.RollPeak > 12f,
                $"38s 电机故障横滚峰值 {service.RollPeak:0.0}° > 12°(当前 {body.RollDeg:0.0}°)"));
            sc.At(39f, () => ClearFault());
            sc.At(41f, () => HeadlessAssert.Check(Mathf.Abs(body.RollDeg) < 6f,
                $"41s 电机恢复横滚回正 {body.RollDeg:0.0}° < 6°"));

            // ---- 故障4:陀螺漂移(悬停中注入,44-48s) ----
            sc.At(43f, () => follower.Pause());
            sc.At(44f, () => InjectKind(FaultKind.GyroDrift));
            sc.At(47f, () =>
            {
                float d = Mathf.Abs(Mathf.DeltaAngle(service.YawAtInject, body.HeadingDeg));
                HeadlessAssert.Check(d > 30f, $"47s 陀螺漂移航向偏转 {d:0}° > 30°");
            });
            sc.At(48f, () => { ClearFault(); follower.Resume(); });

            // ---- 总恢复 ----
            sc.At(54f, () => HeadlessAssert.Check(
                follower.Active && body.MaxSpeed >= 15.9f && body.RollBiasDeg == 0f && body.YawBiasDeg == 0f,
                $"54s 全部恢复(动力 {body.MaxSpeed:0.0} 偏置 {body.RollBiasDeg:0.0}/{body.YawBiasDeg:0.0} 巡航 {body.HorizSpeed:0.0}m/s)"));
        }
    }
}

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 模块2 动态航线巡航:打点/手绘/拖拽编辑航线,流光基线 + 呼吸航点;
    /// 自动巡航(胡萝卜点跟随)、断点续飞(重投影接回);
    /// 侧风超偏 → 基线红闪 + 3D 引导箭头 + 偏差数值与 1Hz 序列导出。
    /// </summary>
    public class RouteMode : DrillMode
    {
        public override string Id => "route";
        public override string Title => "动态航线巡航";
        public override string Brief =>
            "打点/手绘/拖拽三种方式编辑航线,流光基线与航点标记;自动巡航、断点续飞;侧风超偏触发红线闪烁与3D引导箭头,偏差实时显示并可导出。";

        RouteData route;
        RouteVisual visual;
        WaypointMarker markers;
        RouteFollower follower;
        RouteEditor editor;
        FlightBody body;
        PlayerFlightInput pInput;
        FlightAutopilot auto;
        Vector3 padPos = new Vector3(0f, 0.55f, 0f);

        readonly List<float> devSeries = new List<float>(300);   // 1Hz 偏差采样(无头导出)
        float devTimer;

        public override void Build()
        {
            EnvironmentBuilder.ResetToDayDefault();
            EnvironmentBuilder.BuildLighting(Root);
            EnvironmentBuilder.CreateGround(Root);
            EnvironmentBuilder.MakeRing(Root, 120f, new Color(0.3f, 0.5f, 0.6f, 0.22f), "FieldBound", 0.05f);
            BuildScenery();
            EnvironmentBuilder.MakeFlatDisc(Root, 6f, new Color(0.15f, 0.65f, 0.85f, 0.18f), "Pad");
            EnvironmentBuilder.MakeRing(Root, 6.4f, new Color(0.2f, 0.8f, 1f, 0.5f), "PadRing", 0.06f);

            // ---- 玩家机体 ----
            var go = DroneFactory.Spawn(DroneRole.Player, Root, padPos, "PlayerDrone");
            body = go.AddComponent<FlightBody>();
            body.HomePos = padPos;
            pInput = go.AddComponent<PlayerFlightInput>();
            pInput.Body = body;
            auto = go.AddComponent<FlightAutopilot>();
            auto.Body = body;
            auto.enabled = false;
            follower = go.AddComponent<RouteFollower>();
            follower.Body = body;

            // ---- 航线物件 ----
            route = new RouteData();
            follower.Route = route;
            visual = NewGo("RouteVisual").AddComponent<RouteVisual>();
            visual.Route = route;
            markers = NewGo("Waypoints").AddComponent<WaypointMarker>();
            markers.Route = route;
            var guide = go.AddComponent<DeviationGuide>();
            guide.Drone = go.transform;
            guide.Route = route;
            guide.Follower = follower;

            // ---- 相机 ----
            var cam = CameraDirector.CreateCamera(Root);
            CameraDirector.Follow(cam, go.transform, 15f);
            Ctx.MainCamera = cam;
            editor = new RouteEditor(route, cam, markers);
        }

        void BuildScenery()
        {
            // 边缘地标(确定性,避开中心航道区)
            var spots = new[] {
                new Vector3(-70f, 0f, -50f), new Vector3(65f, 0f, -55f), new Vector3(72f, 0f, 58f), new Vector3(-65f, 0f, 55f) };
            float[] hts = { 26f, 18f, 32f, 14f };
            for (int i = 0; i < spots.Length; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = $"Block{i}";
                b.transform.SetParent(Root, false);
                b.transform.position = spots[i] + Vector3.up * (hts[i] / 2f);
                b.transform.localScale = new Vector3(14f, hts[i], 14f);
                b.GetComponent<Renderer>().material = EnvironmentBuilder.StdMat(new Color(0.45f, 0.47f, 0.5f));
            }
        }

        // ---------- 交互 ----------
        public override void OnTick(float dt)
        {
            if (body == null) return;

            editor.EditingEnabled = !follower.Started && !auto.enabled;
            editor.Tick();

            // 航点高亮:当前目标
            markers.HighlightIndex = follower.Active && route.Count >= 2
                ? route.IndexAt(Mathf.Repeat(follower.Dist, route.TotalLength))
                : -1;

            // 告警联动 + 偏差 1Hz 采样
            visual.Alarm = follower.Started && follower.AlarmNow;
            if (follower.Started)
            {
                devTimer += dt;
                if (devTimer >= 1f)
                {
                    devTimer = 0f;
                    devSeries.Add(follower.Deviation);
                    if (devSeries.Count > 300) devSeries.RemoveAt(0);
                }
            }

            // 指令源仲裁:编辑期玩家飞;巡航期跟随器;返航期自驾仪
            pInput.Enabled = !follower.Started && !auto.enabled;
            if (auto.enabled && auto.RouteDone) auto.BeginLanding();

            if (Input.GetKeyDown(KeyCode.R)) ResetAll();
            if (Input.GetKeyDown(KeyCode.H)) ReturnHome();

            // 悬浮标注
            var cam = Ctx.MainCamera;
            if (cam != null)
            {
                var dp = body.transform.position;
                Overlay.Label(dp + Vector3.up * 2.2f,
                    $"ME  alt {body.Altitude:0.0}m  spd {body.Speed:0.0}m" +
                    (follower.Started ? $"  偏差 {follower.Deviation:0.0}m" : ""), new Color(0.4f, 0.9f, 1f));
                if (follower.Active)
                    Overlay.Label(follower.Carrot + Vector3.up * 1.6f, "▶ 航线跟踪点", new Color(1f, 0.85f, 0.3f));
                if (visual.Alarm)
                    Overlay.Label(dp + Vector3.up * 3.4f, $"⚠ 偏离航线 {follower.Deviation:0.0}m", new Color(1f, 0.35f, 0.25f));
            }
        }

        void ResetAll()
        {
            follower.StopRoute();
            follower.MaxDeviation = 0f;
            follower.AlarmTriggered = false;
            devSeries.Clear();
            auto.ResetRoute();
            auto.enabled = false;
            body.Teleport(padPos, 0f);
            body.ResetStats();
            EventBus.Publish("任务", "", "机体已归位重置", EventGrade.Op);
        }

        void ReturnHome()
        {
            if (auto.enabled) return;
            follower.StopRoute();
            pInput.Enabled = false;
            auto.ResetRoute();
            auto.enabled = true;
            auto.Enqueue(new Vector3(padPos.x, 20f, padPos.z));
            EventBus.Publish("飞行", "route", "返航:目标归航点,将自动降落", EventGrade.Op);
        }

        // ---------- UI ----------
        public override void DrawSidePanel(Rect r)
        {
            if (body == null || route == null) return;
            float y = r.y;
            float w3 = (r.width - 8f) / 3f;

            GUI.Label(new Rect(r.x, y, r.width, 20), "航线编辑", PanelKit.Header);
            y += 24;
            if (PanelKit.ToggleBtn(r.x, y, w3, 24, "打点", editor.CurTool == RouteEditor.Tool.Place)) editor.CurTool = RouteEditor.Tool.Place;
            if (PanelKit.ToggleBtn(r.x + w3 + 4f, y, w3, 24, "手绘", editor.CurTool == RouteEditor.Tool.Draw)) editor.CurTool = RouteEditor.Tool.Draw;
            if (PanelKit.ToggleBtn(r.x + (w3 + 4f) * 2f, y, w3, 24, "拖拽", editor.CurTool == RouteEditor.Tool.Drag)) editor.CurTool = RouteEditor.Tool.Drag;
            y += 30;

            GUI.Label(new Rect(r.x, y, r.width, 16), $"航点高度 {editor.WaypointAlt:0} m", PanelKit.Mini);
            y += 16;
            editor.WaypointAlt = GUI.HorizontalSlider(new Rect(r.x, y, r.width, 14), editor.WaypointAlt, 5f, 40f);
            y += 22;

            if (PanelKit.ToggleBtn(r.x, y, r.width / 2f - 2f, 22, route.Loop ? "闭环:开" : "闭环:关", route.Loop)) route.Loop = !route.Loop;
            if (PanelKit.Btn(r.x + r.width / 2f + 2f, y, r.width / 2f - 12f, 22, "清空 (X)")) route.Clear();
            y += 26;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"航点 <b>{route.Count}</b>   总长 <b>{route.TotalLength:0} m</b>", PanelKit.Small);
            y += 26;

            GUI.Label(new Rect(r.x, y, r.width, 20), "巡航控制", PanelKit.Header);
            y += 24;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"巡航速度 {follower.Cruise:0.0} m/s", PanelKit.Mini);
            y += 16;
            follower.Cruise = GUI.HorizontalSlider(new Rect(r.x, y, r.width, 14), follower.Cruise, 4f, 14f);
            y += 20;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"偏差告警阈值 {follower.DeviationLimit:0} m", PanelKit.Mini);
            y += 16;
            follower.DeviationLimit = GUI.HorizontalSlider(new Rect(r.x, y, r.width, 14), follower.DeviationLimit, 3f, 15f);
            y += 24;

            string st = !follower.Started ? "待命" : follower.Active ? "巡航中" : "已暂停(断点)";
            var devCol = follower.AlarmNow ? Color.red : Color.white;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"状态 {st}   进度 <b>{follower.Progress01 * 100:0}%</b>   圈 {follower.Loops}", PanelKit.Small);
            y += 16;
            var prev = GUI.color; GUI.color = devCol;
            GUI.Label(new Rect(r.x, y, r.width, 16),
                $"当前偏差 {follower.Deviation:0.0} m   最大 {follower.MaxDeviation:0.0} m", PanelKit.Small);
            GUI.color = prev;
            y += 24;

            float bw = (r.width - 8f) / 2f;
            if (PanelKit.Btn(r.x, y, bw, 24, "开始巡航", route.Count >= 2 && !follower.Started && !auto.enabled))
            { devSeries.Clear(); follower.StartRoute(); }
            if (PanelKit.Btn(r.x + bw + 8f, y, bw, 24, "暂停", follower.Active)) follower.Pause();
            y += 28;
            if (PanelKit.Btn(r.x, y, bw, 24, "续飞", follower.Started && !follower.Active)) follower.Resume();
            if (PanelKit.Btn(r.x + bw + 8f, y, bw, 24, "停止巡航", follower.Started)) follower.StopRoute();
            y += 28;
            if (PanelKit.Btn(r.x, y, r.width - 12f, 24, "返航降落 (H)", !auto.enabled && !follower.Started)) ReturnHome();
            y += 30;

            GUI.Label(new Rect(r.x, y, r.width, r.y + r.height - y),
                "编辑:左键按工具打点/手绘/拖动 Z撤销 X清空\n巡航:开始→自动跟踪 暂停→断点 续飞→重投影接回\n侧风超偏→红线闪烁+箭头指引 | R重置 H返航", PanelKit.Mini);
        }

        public override void DrawHint(StringBuilder sb)
        {
            sb.Append("左键编辑航线(打点/手绘/拖点) Z撤销 X清空 | 开始巡航/暂停/续飞 | H返航 R重置 | 右键+滚轮视角");
        }

        public override void WriteMetrics(StringBuilder sb)
        {
            if (route == null || follower == null) return;
            sb.AppendLine($"route=points:{route.Count} len:{route.TotalLength:0}m loop:{route.Loop}");
            sb.AppendLine($"cruise=started:{follower.Started} active:{follower.Active} progress:{follower.Progress01 * 100:0}% loops:{follower.Loops} dist:{follower.Dist:0}m");
            sb.AppendLine($"dev=cur:{follower.Deviation:0.0}m max:{follower.MaxDeviation:0.0}m limit:{follower.DeviationLimit:0}m alarmTriggered:{follower.AlarmTriggered}");
            sb.AppendLine($"drone=alt:{body.Altitude:0.0} spd:{body.Speed:0.0} dist:{body.DistanceFlown:0} wind:{WindField.SpeedMps:0.0}m/s");
            if (devSeries.Count > 0)
            {
                var tail = devSeries.GetRange(Mathf.Max(0, devSeries.Count - 12), Mathf.Min(12, devSeries.Count));
                sb.AppendLine("devSeries1Hz(tail)=" + string.Join(",", tail.ConvertAll(v => v.ToString("0.0"))));
            }
        }

        // ---------- 无头剧本 ----------
        public override void RunHeadlessScenario(string name)
        {
            var sc = ScenarioRunner.I;
            if (sc == null || body == null) return;

            // 程序化打点:50m 方环 @18m(覆盖 RouteData.Add/闭环/总长)
            route.Add(new Vector3(0f, 18f, 0f));
            route.Add(new Vector3(50f, 18f, 0f));
            route.Add(new Vector3(50f, 18f, 50f));
            route.Add(new Vector3(0f, 18f, 50f));
            route.Loop = true;
            follower.Cruise = 8f;
            follower.DeviationLimit = 3f;
            pInput.Enabled = false;
            follower.StartRoute();

            float distPause = 0f;
            var windDir = new Vector3(1f, 0f, 0.35f).normalized;

            sc.At(12f, () => HeadlessAssert.Check(body.Altitude > 10f, $"12s 起飞入线(高度 {body.Altitude:0.0}m)"));
            sc.At(15f, () => HeadlessAssert.Check(follower.Dist > 60f, $"15s 巡航推进(里程 {follower.Dist:0}m)"));
            sc.At(24f, () => { follower.Pause(); distPause = follower.Dist; });
            sc.At(29f, () => HeadlessAssert.Check(follower.Dist - distPause < 3f, $"29s 暂停断点无推进(Δ {follower.Dist - distPause:0.0}m)"));
            sc.At(30f, () => follower.Resume());
            sc.At(36f, () => HeadlessAssert.Check(follower.Dist > distPause + 20f, $"36s 断点续飞恢复推进(+{follower.Dist - distPause:0}m)"));
            sc.At(38f, () => WindField.Configure(windDir, 12f));
            sc.At(52f, () => HeadlessAssert.Check(follower.AlarmTriggered && follower.MaxDeviation > follower.DeviationLimit,
                $"52s 侧风超偏告警(最大 {follower.MaxDeviation:0.0}m > {follower.DeviationLimit:0}m)"));
            sc.At(53f, () => WindField.Configure(windDir, 2f));
            sc.At(66f, () => HeadlessAssert.Check(follower.Deviation < follower.DeviationLimit, $"66s 风停回归收敛(偏差 {follower.Deviation:0.0}m)"));
            sc.At(76f, () =>
            {
                HeadlessAssert.Check(follower.Loops >= 1, $"76s 闭环圈数≥1 (实际 {follower.Loops})");
                HeadlessAssert.Check(follower.Dist > 400f, $"76s 持续推进里程>400m (实际 {follower.Dist:0}m)");
                HeadlessAssert.Check(route.Count == 4 && Mathf.Abs(route.TotalLength - 200f) < 1f,
                    $"航线核验(4点 总长 {route.TotalLength:0}m)");
            });
        }
    }
}

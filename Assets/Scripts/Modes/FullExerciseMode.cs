using System.Text;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 模块9 综合演练与复盘:60s 想定串联多模块能力——
    /// 航线巡航→侧风→链路失联→黑飞入侵喊话驱离→GPS干扰故障→解除返航降落;
    /// 全程 10Hz 采样,演练后一键回放(时间轴 Seek/单步/事件刻度跳转/轨迹线)。
    /// </summary>
    public class FullExerciseMode : DrillMode
    {
        public override string Id => "full";
        public override string Title => "综合演练与复盘";
        public override string Brief =>
            "60秒综合想定:巡航→侧风→失联→黑飞驱离→GPS干扰→返航降落,全程自动记录;结束后回溯复盘:时间轴滑块/单步/事件刻度跳转,已飞亮线+全程淡线轨迹。";

        RouteData route;
        RouteFollower follower;
        FaultService service;
        FlightBody body;
        PlayerFlightInput pInput;
        FlightAutopilot auto;
        LinkLoss link;
        IntruderAlert intruder;
        Vector3 padPos = new Vector3(0f, 0.55f, 0f);

        public override void Build()
        {
            EnvironmentBuilder.ResetToDayDefault();
            EnvironmentBuilder.BuildLighting(Root);
            EnvironmentBuilder.CreateGround(Root);
            EnvironmentBuilder.MakeRing(Root, 120f, new Color(0.3f, 0.5f, 0.6f, 0.22f), "FieldBound", 0.05f);
            EnvironmentBuilder.MakeFlatDisc(Root, 6f, new Color(0.15f, 0.65f, 0.85f, 0.18f), "Pad");
            EnvironmentBuilder.MakeRing(Root, 6.4f, new Color(0.2f, 0.8f, 1f, 0.5f), "PadRing", 0.06f);

            var go = DroneFactory.Spawn(DroneRole.Player, Root, padPos, "ExerciseDrone");
            body = go.AddComponent<FlightBody>();
            body.HomePos = padPos;
            pInput = go.AddComponent<PlayerFlightInput>();
            pInput.Body = body;
            auto = go.AddComponent<FlightAutopilot>();
            auto.Body = body;
            auto.enabled = false;
            follower = go.AddComponent<RouteFollower>();
            follower.Body = body;
            follower.Route = route = new RouteData();

            service = NewGo("FaultService").AddComponent<FaultService>();
            service.Bind(body, follower);

            link = NewGo("LinkLoss").AddComponent<LinkLoss>();
            link.Body = body;
            link.Autopilot = auto;
            link.Duration = 5f;

            var intruderGo = DroneFactory.Spawn(DroneRole.Red, Root, new Vector3(70f, 18f, -40f), "BlackFly");
            intruderGo.SetActive(false);
            intruder = intruderGo.AddComponent<IntruderAlert>();
            intruder.Target = new Vector3(10f, 22f, 10f);

            var cam = CameraDirector.CreateCamera(Root);
            CameraDirector.Follow(cam, go.transform, 15f);
            Ctx.MainCamera = cam;
        }

        // ---------- 交互 ----------
        public override void OnTick(float dt)
        {
            if (body == null) return;
            pInput.Enabled = !follower.Started && !auto.enabled;

            if (Input.GetKeyDown(KeyCode.R))
            {
                ReplayPlayer.I?.Exit();
                service.Clear();
                follower.StopRoute();
                auto.ResetRoute();
                auto.enabled = false;
                if (link.Lost) auto.enabled = false;   // 失联保护期自驾保持禁用(至多 5s 自动恢复)
                body.Teleport(padPos, 0f);
                body.ResetStats();
                EventBus.Publish("演练", "", "综合演练已重置", EventGrade.Op);
            }

            var cam = Ctx.MainCamera;
            if (cam != null)
            {
                var dp = body.transform.position;
                Overlay.Label(dp + Vector3.up * 2.2f,
                    $"ME  alt {body.Altitude:0.0}m  spd {body.Speed:0.0}m  偏差 {follower.Deviation:0.0}m", new Color(0.4f, 0.9f, 1f));
                if (DrillClock.InReplay)
                    Overlay.Label(dp + Vector3.up * 3.6f,
                        $"● 回放 {ReplayPlayer.I?.Cursor ?? 0f:0.0}s", new Color(0.7f, 0.5f, 1f));
            }
        }

        public override void DrawSidePanel(Rect r)
        {
            if (body == null) return;
            float y = r.y;
            GUI.Label(new Rect(r.x, y, r.width, 20), "综合演练", PanelKit.Header);
            y += 24;
            string st = DrillClock.InReplay ? "回放复盘" : follower.Started ? "想定执行中" : auto.enabled ? "自动返航" : "待命";
            GUI.Label(new Rect(r.x, y, r.width, 16), $"阶段 {st}", PanelKit.Small);
            y += 18;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"高度 {body.Altitude:0.0}m 速度 {body.Speed:0.0}m/s", PanelKit.Small);
            y += 18;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"失联 {link.LostSeconds:0.0}s 漂移 {link.DriftM:0.0}m", PanelKit.Small);
            y += 18;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"故障 {service.ActiveName}  黑飞 {(intruder.Left ? "已离场" : intruder.Deterred ? "驱离中" : intruder.Active ? "入侵" : "无")}", PanelKit.Small);
            y += 26;

            var rp = ReplayPlayer.I;
            bool canReplay = ReplayService.I != null && ReplayService.I.HasData;
            if (PanelKit.Btn(r.x, y, r.width - 12f, 24, "回溯复盘", canReplay && !DrillClock.InReplay && DrillClock.State != PlayState.Setup)) rp?.Enter();
            y += 28;
            if (PanelKit.Btn(r.x, y, r.width - 12f, 24, "退出回放", DrillClock.InReplay)) rp?.Exit();
            y += 30;

            GUI.Label(new Rect(r.x, y, r.width, r.y + r.height - y),
                "想定:巡航→侧风→失联→黑飞驱离→GPS干扰→返航\nR重置 | 暂停后点[回溯复盘]看时间轴", PanelKit.Mini);
        }

        public override void DrawHint(StringBuilder sb)
        {
            sb.Append("自动想定执行(也可暂停/倍速) | 暂停后点[回溯复盘]:滑块/单步/事件刻度 | R重置");
        }

        public override void WriteMetrics(StringBuilder sb)
        {
            if (body == null) return;
            var rp = ReplayPlayer.I;
            var rs = ReplayService.I;
            sb.AppendLine($"ex=alt:{body.Altitude:0.0} spd:{body.Speed:0.0} dist:{body.DistanceFlown:0} wind:{WindField.SpeedMps:0.0}m/s");
            sb.AppendLine($"story=linkLost:{link.LostSeconds:0.0}s drift:{link.DriftM:0.0}m intruder:{(intruder.Left ? "离场" : intruder.Deterred ? "驱离中" : intruder.Active ? "入侵" : "无")} fault:{service.ActiveName}");
            sb.AppendLine($"replay=frames:{rs?.FrameCount ?? 0} dur:{(rs?.Duration ?? 0f):0.0}s cursor:{(rp?.Cursor ?? 0f):0.0}s inReplay:{DrillClock.InReplay} lines:{TrajectoryDrawer.LineCount}");
        }

        // ---------- 无头剧本 ----------
        public override void RunHeadlessScenario(string name)
        {
            var sc = ScenarioRunner.I;
            if (sc == null || body == null) return;

            // 想定航线:80m 方环 @22m
            route.Add(new Vector3(0f, 22f, 0f));
            route.Add(new Vector3(60f, 22f, 0f));
            route.Add(new Vector3(60f, 22f, 60f));
            route.Add(new Vector3(0f, 22f, 60f));
            route.Loop = true;
            pInput.Enabled = false;
            follower.Cruise = 10f;
            follower.DeviationLimit = 9f;
            follower.StartRoute();

            // ---- 60s 想定(模块1/2/3/5/8 串联) ----
            sc.At(6f, () => WindField.Configure(new Vector3(1f, 0f, 0.35f).normalized, 8f));
            sc.At(12f, () => link.Begin());
            sc.At(18f, () => intruder.Spawn(new Vector3(70f, 18f, -40f)));
            sc.At(26f, () => intruder.Deter());
            sc.At(30f, () => service.Inject(FaultKind.GpsJam));
            sc.At(37f, () => service.Clear());
            sc.At(40f, () => WindField.Configure(new Vector3(1f, 0f, 0.35f).normalized, 2f));
            sc.At(46f, () =>
            {
                follower.StopRoute();
                pInput.Enabled = false;
                auto.ResetRoute();
                auto.enabled = true;
                auto.Enqueue(new Vector3(padPos.x, 20f, padPos.z));
                EventBus.Publish("演练", "", "想定结束:自动返航降落", EventGrade.Op);
            });

            sc.At(8f, () => HeadlessAssert.Check(body.Altitude > 14f && follower.Active,
                $"8s 巡航建立(高度 {body.Altitude:0.0}m)"));
            sc.At(14f, () => HeadlessAssert.Check(WindField.SpeedMps > 5f,
                $"14s 侧风已加载({WindField.SpeedMps:0.0} m/s)"));
            sc.At(15f, () => HeadlessAssert.Check(link.Lost, "15s 链路失联保护中"));
            sc.At(20f, () => HeadlessAssert.Check(link.Recovered,
                $"20s 链路自动恢复(失联 {link.Duration:0.0}s 漂移 {link.DriftM:0.0}m)"));
            sc.At(24f, () => HeadlessAssert.Check(intruder.Active && !intruder.Deterred, "24s 黑飞入侵未驱离"));
            sc.At(28f, () => HeadlessAssert.Check(intruder.Deterred && !intruder.Left, "28s 喊话驱离生效"));
            sc.At(36f, () => HeadlessAssert.Check(service.JitterRms > 0.8f,
                $"36s GPS干扰抖动RMS {service.JitterRms:0.00} m/s > 0.8"));
            sc.At(48f, () => HeadlessAssert.Check(auto.enabled,
                "48s 返航自驾已接管"));
            sc.At(56f, () => HeadlessAssert.Check(
                new Vector2(body.transform.position.x, body.transform.position.z).magnitude < 45f,
                $"56s 归航接近中(水平距原点 {new Vector2(body.transform.position.x, body.transform.position.z).magnitude:0}m)"));

            // ---- 回放阶段(墙钟剧本在进入回放时注册,回放态 SimTime 冻结) ----
            sc.At(60f, () =>
            {
                ReplayPlayer.I.Enter();
                ReplayPlayer.I.Playing = false;
                ReplayPlayer.I.Seek(10f);

                sc.AtReal(1.5f, () =>
                {
                    ReplayPlayer.I.Seek(30f);
                    HeadlessAssert.Check(ReplayService.I.FrameCount >= 500,
                        $"回放采样帧数 {ReplayService.I.FrameCount} ≥ 500(60s×10Hz)");
                    HeadlessAssert.Check(ReplayService.I.Duration >= 55f,
                        $"回放时长 {ReplayService.I.Duration:0.0}s ≥ 55s");
                    int ops = 0;
                    foreach (var e in EventBus.All) if (e.Grade >= EventGrade.Op) ops++;
                    HeadlessAssert.Check(ops >= 10, $"Op 级以上事件 {ops} 条 ≥ 10(时间轴刻度)");
                    HeadlessAssert.Check(TrajectoryDrawer.LineCount >= 1,
                        $"轨迹线已构建 {TrajectoryDrawer.LineCount} 条");
                    // Seek 精度:回放位姿应贴近 30s 采样位姿
                    float best = float.MaxValue; Vector3 sampled = Vector3.zero;
                    foreach (var f in ReplayService.I.Frames)
                    {
                        float d = Mathf.Abs(f.T - 30f);
                        if (d < best)
                        {
                            best = d;
                            for (int i = 0; i < f.Names.Length; i++)
                                if (f.Names[i] == body.name) sampled = f.Samples[i].Pos;
                        }
                    }
                    float err = Vector3.Distance(body.transform.position, sampled);
                    HeadlessAssert.Check(err < 0.5f, $"Seek(30s) 位姿复现误差 {err:0.00}m < 0.5m");
                });
                sc.AtReal(2.2f, () => ReplayPlayer.I.Seek(50f));
            });
        }
    }
}

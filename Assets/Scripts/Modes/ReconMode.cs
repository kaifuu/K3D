using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 模块4 侦察巡检:环场自动巡航 + 扇形扫描波识别目标;
    /// 可见光/红外热成像双视角一键切换,云台镜头锁定跟踪与平滑变焦,
    /// 已识别目标包壳描边 + 分类色悬浮标注。指标导出识别数/瞄准误差(无头验收)。
    /// </summary>
    public class ReconMode : DrillMode
    {
        public override string Id => "recon";
        public override string Title => "侦察巡检";
        public override string Brief =>
            "可视/红外双视角,云台镜头平滑跟随,扇形扫描波纹,目标识别高亮标注与锁定跟踪变焦。";

        FlightBody body;
        RouteFollower follower;
        PlayerFlightInput pInput;
        ReconCameraRig rig;
        ScanPulse scan;
        readonly List<ScannableTarget> targets = new List<ScannableTarget>(8);
        Vector3 padPos = new Vector3(0f, 0.55f, 78f);
        float fps = 60f;
        int trackIdx = -1;

        public override void Build()
        {
            RendererRegistry.Clear();   // 防域重载残留
            EnvironmentBuilder.ResetToDayDefault();
            EnvironmentBuilder.BuildLighting(Root);
            RendererRegistry.Register(CreateFacility(), ThermalClass.Ambient);
            var ground = EnvironmentBuilder.CreateGround(Root);
            if (ground != null && ground.GetComponent<Renderer>() != null)
                RendererRegistry.Register(ground.GetComponent<Renderer>(), ThermalClass.Ambient);
            EnvironmentBuilder.MakeRing(Root, 130f, new Color(0.3f, 0.5f, 0.6f, 0.22f), "FieldBound", 0.05f);
            EnvironmentBuilder.MakeFlatDisc(Root, 6f, new Color(0.15f, 0.65f, 0.85f, 0.18f), "Pad");
            EnvironmentBuilder.MakeRing(Root, 6.4f, new Color(0.2f, 0.8f, 1f, 0.5f), "PadRing", 0.06f);

            // ---- 巡检目标(车=热 / 人=温 / 设备=冷) ----
            targets.Add(ScannableTarget.Create(Root, "车辆-01", ThermalClass.Hot, new Vector3(34f, 0f, -18f)));
            targets.Add(ScannableTarget.Create(Root, "车辆-02", ThermalClass.Hot, new Vector3(-42f, 0f, 8f), true));
            targets.Add(ScannableTarget.Create(Root, "人员-01", ThermalClass.Warm, new Vector3(28f, 0f, -6f), true));
            targets.Add(ScannableTarget.Create(Root, "人员-02", ThermalClass.Warm, new Vector3(38f, 0f, -30f)));
            targets.Add(ScannableTarget.Create(Root, "人员-03", ThermalClass.Warm, new Vector3(-34f, 0f, 24f), true));
            targets.Add(ScannableTarget.Create(Root, "设备-01", ThermalClass.Cold, new Vector3(-6f, 0f, 42f)));
            targets.Add(ScannableTarget.Create(Root, "设备-02", ThermalClass.Cold, new Vector3(14f, 0f, 56f)));

            // ---- 玩家机体(自动环绕巡航) ----
            var go = DroneFactory.Spawn(DroneRole.Player, Root, padPos, "PlayerDrone");
            body = go.AddComponent<FlightBody>();
            body.HomePos = padPos;
            pInput = go.AddComponent<PlayerFlightInput>();
            pInput.Body = body;
            pInput.Enabled = false;
            follower = go.AddComponent<RouteFollower>();
            follower.Body = body;

            // ---- 环场巡查航线(半径50 高35) ----
            var route = new RouteData();
            follower.Route = route;
            var visual = NewGo("RouteVisual").AddComponent<RouteVisual>();
            visual.Route = route;
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                route.Add(new Vector3(Mathf.Cos(a) * 50f, 35f, Mathf.Sin(a) * 50f));
            }
            route.Loop = true;

            // ---- 扫描波 ----
            scan = NewGo("ScanPulse").AddComponent<ScanPulse>();
            scan.Setup(go.transform, targets);

            // ---- 云台侦察相机(本模式不用追随机) ----
            var cam = CameraDirector.CreateCamera(Root);
            rig = cam.gameObject.AddComponent<ReconCameraRig>();
            rig.Drone = go.transform;
            Ctx.MainCamera = cam;
        }

        Renderer CreateFacility()
        {
            Renderer first = null;
            var spots = new[]
            {
                new Vector3(-30f, 0f, -36f), new Vector3(40f, 0f, -44f), new Vector3(46f, 0f, 30f),
                new Vector3(-44f, 0f, 34f), new Vector3(0f, 0f, -60f),
            };
            float[] hts = { 9f, 7f, 8f, 6f, 10f };
            float[] wds = { 14f, 10f, 12f, 9f, 16f };
            for (int i = 0; i < spots.Length; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = $"Facility{i}";
                b.transform.SetParent(Root, false);
                b.transform.position = spots[i] + Vector3.up * (hts[i] / 2f);
                b.transform.localScale = new Vector3(wds[i], hts[i], wds[i]);
                var r = b.GetComponent<Renderer>();
                r.material = EnvironmentBuilder.StdMat(new Color(0.4f, 0.42f, 0.46f));
                RendererRegistry.Register(r, ThermalClass.Ambient);
                if (first == null) first = r;
            }
            return first;
        }

        public override void OnStart()
        {
            follower.Cruise = 8f;
            follower.DeviationLimit = 30f;
            follower.StartRoute();
            scan.StartScan();
        }

        public override void OnTick(float dt)
        {
            if (body == null) return;
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.06f);

            // 快捷键:T 红外切换 / Tab 换跟踪目标 / 滚轮&+- 变焦
            if (Input.GetKeyDown(KeyCode.T)) ThermalView.SetOn(Ctx.MainCamera, !ThermalView.On);
            if (Input.GetKeyDown(KeyCode.Tab)) CycleTrack(1);
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) > 0.001f) rig.FovTarget = Mathf.Clamp(rig.FovTarget - wheel * 30f, 15f, 58f);

            // 悬浮层:扫描状态 + 目标标注 + 红外暗角
            var cam = Ctx.MainCamera;
            if (cam != null)
            {
                int n = CountIdentified();
                Overlay.Label(body.transform.position + Vector3.up * 2.4f,
                    $"ME  alt {body.Altitude:0.0}m  spd {body.Speed:0.0}m/s   扫描 {scan.Swept:0}°  识别 {n}/{targets.Count}",
                    new Color(0.4f, 0.9f, 1f));
                var tracked = trackIdx >= 0 && trackIdx < targets.Count ? targets[trackIdx] : null;
                TargetMarker.DrawAll(cam, targets, tracked);
            }
            ThermalView.DrawFrameFx();
        }

        void CycleTrack(int dir)
        {
            // 只在已识别目标里循环
            for (int step = 1; step <= targets.Count; step++)
            {
                int i = (trackIdx + dir * step + targets.Count) % targets.Count;
                if (targets[i].Identified)
                {
                    trackIdx = i;
                    rig.Track = targets[i];
                    rig.Tracking = true;
                    EventBus.Publish("侦察", "track", $"云台锁定跟踪 {targets[i].Label}", EventGrade.Op);
                    return;
                }
            }
            rig.Tracking = false;
        }

        int CountIdentified()
        {
            int n = 0;
            foreach (var t in targets) if (t != null && t.Identified) n++;
            return n;
        }

        // ---------- UI ----------
        public override void DrawSidePanel(Rect r)
        {
            if (body == null || scan == null || rig == null) return;
            float y = r.y;

            GUI.Label(new Rect(r.x, y, r.width, 20), "侦察控制", PanelKit.Header);
            y += 24;
            float w2 = (r.width - 6f) / 2f;
            if (PanelKit.ToggleBtn(r.x, y, w2, 24, "可见光", !ThermalView.On))
                ThermalView.SetOn(Ctx.MainCamera, false);
            if (PanelKit.ToggleBtn(r.x + w2 + 6f, y, w2, 24, "红外热像", ThermalView.On))
                ThermalView.SetOn(Ctx.MainCamera, true);
            y += 32;

            GUI.Label(new Rect(r.x, y, r.width, 16), $"镜头变焦 FOV {rig.FovTarget:0}°", PanelKit.Mini);
            y += 16;
            float nf = GUI.HorizontalSlider(new Rect(r.x, y, r.width, 14), rig.FovTarget, 15f, 58f);
            if (!Mathf.Approximately(nf, rig.FovTarget)) rig.FovTarget = nf;
            y += 24;

            if (PanelKit.ToggleBtn(r.x, y, r.width, 24, scan.Scanning ? "停止扫描" : "启动扫描", scan.Scanning))
            {
                if (scan.Scanning) scan.StopScan(); else scan.StartScan();
            }
            y += 30;

            if (GUI.Button(new Rect(r.x, y, r.width, 22), "重置识别(重新扫描)"))
            {
                scan.ResetAll();
                trackIdx = -1;
                rig.Tracking = false;
                EventBus.Publish("侦察", "scan", "识别状态已重置", EventGrade.Op);
            }
            y += 30;

            if (GUI.Button(new Rect(r.x, y, r.width, 22), "锁定跟踪下一个目标 ▸")) CycleTrack(1);
            y += 26;
            var tracked = trackIdx >= 0 && trackIdx < targets.Count ? targets[trackIdx] : null;
            if (tracked != null)
                GUI.Label(new Rect(r.x, y, r.width, 16),
                    $"跟踪 {tracked.Label}  瞄准误差 {rig.AimErrorDeg(tracked):0.#}°", PanelKit.Small);
            else
                GUI.Label(new Rect(r.x, y, r.width, 16), "未跟踪(扫描识别后可锁定)", PanelKit.Small);
            y += 22;

            GUI.Label(new Rect(r.x, y, r.width, 20), $"目标 ({CountIdentified()}/{targets.Count} 已识别)", PanelKit.Header);
            y += 20;
            foreach (var t in targets)
            {
                if (t == null) continue;
                var c = TargetMarker.ClassColor(t.Class);
                var prev = GUI.color; GUI.color = c;
                float d = Vector3.Distance(Ctx.MainCamera != null ? Ctx.MainCamera.transform.position : Vector3.zero,
                    t.transform.position);
                string mark = t.Identified ? "■" : "□";
                GUI.Label(new Rect(r.x, y, r.width, 15), $"{mark} {t.Label,-8} {t.ClassName()} {d:0}m", PanelKit.Mini);
                GUI.color = prev;
                y += 15;
                if (y > r.y + r.height - 20) break;
            }
            y += 6;
            GUI.Label(new Rect(r.x, y, r.width, r.y + r.height - y),
                "T 切红外 / Tab 换锁定 / 滚轮变焦;\n红外下车=白橙 人=亮黄 设备=紫蓝;\n扫描波掠过即识别,包壳描边+分类色标注。",
                PanelKit.Mini);
        }

        public override void DrawHint(StringBuilder sb)
        {
            sb.Append("T 红外切换 | Tab 锁定跟踪 | 滚轮变焦 | 侧板:扫描开关/识别列表/变焦");
        }

        public override void WriteMetrics(StringBuilder sb)
        {
            if (body == null || scan == null || rig == null) return;
            var tracked = trackIdx >= 0 && trackIdx < targets.Count ? targets[trackIdx] : null;
            sb.AppendLine($"view=thermal:{ThermalView.On} fov:{(Ctx.MainCamera != null ? Ctx.MainCamera.fieldOfView : 0f):0.0}");
            sb.AppendLine($"scan=scanning:{scan.Scanning} swept:{scan.Swept:0}deg rate:{scan.RateDeg:0}deg/s");
            sb.AppendLine($"targets=total:{targets.Count} identified:{CountIdentified()}");
            sb.AppendLine($"track={tracked != null} label:{tracked?.Label ?? "-"} aimErr:{rig.AimErrorDeg(tracked):0.#}deg");
            sb.AppendLine($"orbit=dist:{follower.Dist:0}m loops:{follower.Loops} alt:{body.Altitude:0.0} fps:{fps:0}");
            var cam0 = Ctx.MainCamera;
            var tk = trackIdx >= 0 && trackIdx < targets.Count ? targets[trackIdx] : null;
            if (cam0 != null && tk != null)
                sb.AppendLine($"dbg=cam:{cam0.transform.position:0.0} drone:{body.transform.position:0.0} " +
                    $"track:{tk.transform.position:0.0} dCam:{Vector3.Distance(cam0.transform.position, tk.transform.position):0.0}m");
        }

        // ---------- 无头剧本:扫描识别 → 红外对比 → 锁定变焦 ----------
        public override void RunHeadlessScenario(string name)
        {
            var sc = ScenarioRunner.I;
            if (sc == null || scan == null) return;

            sc.At(4f, () => HeadlessAssert.Check(scan.Scanning && scan.Swept > 20f,
                $"4s 扫描运行(已扫 {scan.Swept:0}°)"));
            sc.At(10f, () => HeadlessAssert.Check(CountIdentified() >= 2,
                $"10s 首批识别({CountIdentified()}/7)"));
            sc.At(16f, () => { ThermalView.SetOn(Ctx.MainCamera, true); CycleTrack(1); });
            sc.At(20f, () => HeadlessAssert.Check(ThermalView.On && CountIdentified() >= 4,
                $"20s 红外生效+识别过半(视角 {(ThermalView.On ? "IR" : "VIS")} {CountIdentified()}/7)"));
            sc.At(24f, () => HeadlessAssert.Check(CountIdentified() == targets.Count,
                $"24s 全部识别({CountIdentified()}/7)"));
            sc.At(26f, () => CycleTrack(1));
            sc.At(30f, () =>
            {
                var t = trackIdx >= 0 ? targets[trackIdx] : null;
                HeadlessAssert.Check(t != null && rig.Tracking && rig.AimErrorDeg(t) < 12f,
                    $"30s 云台锁定收敛(误差 {rig.AimErrorDeg(t):0.#}°)");
            });
            sc.At(32f, () => rig.FovTarget = 18f);
            sc.At(36f, () => HeadlessAssert.Check(Ctx.MainCamera != null && Ctx.MainCamera.fieldOfView < 22f,
                $"36s 变焦到位(FOV {Ctx.MainCamera.fieldOfView:0.0})"));
            sc.At(40f, () => ThermalView.SetOn(Ctx.MainCamera, false));
            sc.At(44f, () => HeadlessAssert.Check(!ThermalView.On && CountIdentified() == targets.Count && follower.Dist > 100f,
                $"44s 视角还原+巡检持续(识别 {CountIdentified()}/7 里程 {follower.Dist:0}m)"));
        }
    }
}

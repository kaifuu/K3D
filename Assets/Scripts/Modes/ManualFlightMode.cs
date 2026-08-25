using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 模块1 无人机飞行操控:键鼠/手柄全向飞行训练场。
    /// 起飞坪 + 矩形穿越环航线 + 障碍柱;遥测面板实时读数;
    /// 无头剧本用自驾仪完成 起飞→矩形巡航→归航降落 全流程断言。
    /// </summary>
    public class ManualFlightMode : DrillMode
    {
        public override string Id => "manual";
        public override string Title => "无人机飞行操控";
        public override string Brief => "键鼠/手柄全向飞行:俯仰横滚偏航升降悬停返航,姿态倾斜回正、旋翼转速联动、侧风抖动漂移、惯性滑行。";

        FlightBody body;
        PlayerFlightInput pInput;
        FlightAutopilot auto;

        struct Gate { public Transform T; public Material Mat; public bool Passed; }
        readonly List<Gate> gates = new List<Gate>(4);
        int gatesPassed;
        Vector3 padPos = new Vector3(0f, 0.55f, 0f);

        // 矩形巡航航点(无头剧本用,与穿越环重合便于断言)
        static readonly Vector3[] cruisePath =
        {
            new Vector3(0f, 20f, 0f),
            new Vector3(45f, 20f, 45f),
            new Vector3(-45f, 20f, 45f),
            new Vector3(-45f, 20f, -45f),
            new Vector3(45f, 20f, -45f),
            new Vector3(45f, 20f, 45f),
            new Vector3(0f, 20f, 0f),
        };

        public override void Build()
        {
            EnvironmentBuilder.ResetToDayDefault();
            EnvironmentBuilder.BuildLighting(Root);
            EnvironmentBuilder.CreateGround(Root);
            EnvironmentBuilder.MakeRing(Root, 130f, new Color(0.3f, 0.5f, 0.6f, 0.22f), "FieldBound", 0.05f);

            BuildPad();
            BuildGates();
            BuildPillars();

            // ---- 玩家机体 ----
            var go = DroneFactory.Spawn(DroneRole.Player, Root, padPos, "PlayerDrone");

            body = go.AddComponent<FlightBody>();
            body.HomePos = padPos;
            pInput = go.AddComponent<PlayerFlightInput>();
            pInput.Body = body;
            auto = go.AddComponent<FlightAutopilot>();
            auto.Body = body;
            auto.enabled = false;   // 交互时玩家操控;无头剧本接管

            // ---- 相机(追尾) ----
            var cam = CameraDirector.CreateCamera(Root);
            CameraDirector.Follow(cam, go.transform, 13f);
            Ctx.MainCamera = cam;
        }

        void BuildPad()
        {
            EnvironmentBuilder.MakeFlatDisc(Root, 6f, new Color(0.15f, 0.65f, 0.85f, 0.18f), "Pad");
            EnvironmentBuilder.MakeRing(Root, 6.4f, new Color(0.2f, 0.8f, 1f, 0.5f), "PadRing", 0.06f);
        }

        void BuildGates()
        {
            // 环心/朝向:顶(-X来向) 左(-Z) 底(+X) 右(+Z),与矩形巡航边中点重合
            BuildGate(new Vector3(0f, 20f, 45f), 270f, "Gate-Top");
            BuildGate(new Vector3(-45f, 20f, 0f), 180f, "Gate-Left");
            BuildGate(new Vector3(0f, 20f, -45f), 90f, "Gate-Bottom");
            BuildGate(new Vector3(45f, 20f, 0f), 0f, "Gate-Right");
        }

        void BuildGate(Vector3 center, float yawDeg, string name)
        {
            var root = NewGo(name);
            root.transform.position = center;
            root.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            var mat = EnvironmentBuilder.UnlitMat(new Color(0.25f, 0.88f, 1f, 0.8f));
            const int seg = 22;
            const float r = 5f;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                if (i > 0)
                {
                    var chord = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    DestroyCol(chord);
                    chord.name = "Seg";
                    chord.transform.SetParent(root.transform, false);
                    chord.transform.localPosition = (prev + p) * 0.5f;
                    chord.transform.rotation = root.transform.rotation * Quaternion.LookRotation(p - prev);
                    chord.transform.localScale = new Vector3(0.22f, 0.22f, Vector3.Distance(prev, p) + 0.06f);
                    chord.GetComponent<Renderer>().material = mat;
                }
                prev = p;
            }

            // 支撑立柱(环两侧落地)
            for (int s = 0; s < 2; s++)
            {
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCol(pole);
                pole.name = "Pole";
                pole.transform.SetParent(root.transform, false);
                float px = s == 0 ? -r : r;
                float topY = center.y;
                pole.transform.localPosition = new Vector3(px, topY / 2f, 0f);
                pole.transform.localScale = new Vector3(0.24f, topY, 0.24f);
                pole.GetComponent<Renderer>().material = EnvironmentBuilder.StdMat(new Color(0.42f, 0.46f, 0.5f));
            }

            gates.Add(new Gate { T = root.transform, Mat = mat });
        }

        void BuildPillars()
        {
            // 确定性布置,避开矩形航道(|x|或|z|≈45)与起飞坪
            float[] angs = { 20f, 65f, 115f, 160f, 200f, 250f, 295f, 340f };
            float[] rads = { 78f, 95f, 82f, 100f, 88f, 96f, 80f, 92f };
            float[] hts = { 24f, 10f, 18f, 30f, 12f, 22f, 16f, 26f };
            var mat = EnvironmentBuilder.StdMat(new Color(0.5f, 0.48f, 0.45f));
            for (int i = 0; i < angs.Length; i++)
            {
                float a = angs[i] * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a) * rads[i], hts[i] / 2f, Mathf.Sin(a) * rads[i]);
                var pil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pil.name = $"Pillar{i}";
                pil.transform.SetParent(Root, false);
                pil.transform.position = p;
                pil.transform.localScale = new Vector3(3.2f, hts[i] / 2f, 3.2f);
                pil.GetComponent<Renderer>().material = mat;
            }
        }

        static void DestroyCol(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }

        // ---------- 交互 ----------
        public override void OnTick(float dt)
        {
            if (body == null) return;

            // 穿越环判定(球形近似)
            for (int i = 0; i < gates.Count; i++)
            {
                if (gates[i].Passed) continue;
                if (Vector3.Distance(body.transform.position, gates[i].T.position) < 5.5f)
                {
                    var g = gates[i];
                    g.Passed = true;
                    gates[i] = g;
                    gatesPassed++;
                    if (g.Mat != null) g.Mat.color = new Color(0.3f, 1f, 0.45f, 0.85f);
                    EventBus.Publish("任务", g.T.name, $"穿越 {g.T.name} 完成 ({gatesPassed}/{gates.Count})", EventGrade.Op);
                }
            }

            // 归位重置
            if (Input.GetKeyDown(KeyCode.R)) ResetDrone();

            // 自驾航线飞完 → 自动触发降落(不依赖固定时刻)
            if (auto != null && auto.enabled && auto.RouteDone) auto.BeginLanding();

            // 悬浮标注:机体读数 + 环状态
            var cam = Ctx.MainCamera;
            if (cam != null)
            {
                Overlay.Label(body.transform.position + Vector3.up * 2.2f,
                    $"ME  alt {body.Altitude:0.0}m  spd {body.Speed:0.0}m/s", new Color(0.4f, 0.9f, 1f));
                for (int i = 0; i < gates.Count; i++)
                {
                    bool passed = gates[i].Passed;
                    bool next = !passed && gatesPassed == i;   // 顺路下一环
                    Overlay.Label(gates[i].T.position + Vector3.up * 6.2f,
                        passed ? "✓ 已穿越" : next ? "▶ 下一目标" : "○ 待穿越",
                        passed ? new Color(0.35f, 1f, 0.5f) : next ? new Color(1f, 0.85f, 0.3f) : new Color(0.6f, 0.75f, 0.85f));
                }
            }
        }

        void ResetDrone()
        {
            body.Teleport(padPos, 0f);
            body.ResetStats();
            auto.ResetRoute();
            gatesPassed = 0;
            for (int i = 0; i < gates.Count; i++)
            {
                var g = gates[i];
                g.Passed = false;
                gates[i] = g;
                if (g.Mat != null) g.Mat.color = new Color(0.25f, 0.88f, 1f, 0.8f);
            }
            EventBus.Publish("任务", "", "机体已归位重置", EventGrade.Op);
        }

        // ---------- UI ----------
        public override void DrawSidePanel(Rect r)
        {
            if (body == null) return;
            float y = r.y;
            GUI.Label(new Rect(r.x, y, r.width, 20), "飞行遥测", PanelKit.Header);
            y += 24;

            string state = body.Landed ? "接地" : auto != null && auto.enabled ? "自驾" : "飞行";
            GUI.Label(new Rect(r.x, y, r.width, 20),
                $"高度 <b>{body.Altitude:0.0} m</b>   升降 {body.VertSpeed:+0.0;-0.0;-} m/s   [{state}]", PanelKit.Small);
            y += 20;
            GUI.Label(new Rect(r.x, y, r.width, 20),
                $"地速 <b>{body.Speed:0.0} m/s</b>   航向 {body.HeadingDeg:0}°   俯仰 {body.PitchDeg:+0;-0}° 横滚 {body.RollDeg:+0;-0}°", PanelKit.Small);
            y += 20;
            GUI.Label(new Rect(r.x, y, r.width, 20),
                $"旋翼转速 <b>{body.Rpm01 * 100:0}%</b>   飞行距离 {body.DistanceFlown:0} m", PanelKit.Small);
            y += 24;

            float wDir = Mathf.Atan2(WindField.Direction.x, WindField.Direction.z) * Mathf.Rad2Deg;
            GUI.Label(new Rect(r.x, y, r.width, 20),
                $"风况: {WindField.SpeedMps:0.0} m/s @ {wDir:0}°   阵风 ±{WindField.SpeedMps * 0.4f:0.0} m/s", PanelKit.Small);
            y += 24;

            GUI.Label(new Rect(r.x, y, r.width, 20), $"任务:穿越环 <b>{gatesPassed}/{gates.Count}</b>   最大高度 {body.MaxAlt:0. m}   最大速度 {body.MaxSpeedReached:0.0} m/s", PanelKit.Small);
            y += 26;
            if (PanelKit.Btn(r.x, y, 130, 24, "归位重置 (R)")) ResetDrone();
            y += 34;

            GUI.Label(new Rect(r.x, y, r.width, 18), "操控说明", PanelKit.Header);
            y += 20;
            GUI.Label(new Rect(r.x, y, r.width, r.y + r.height - y),
                "键盘: W/S前后 A/D左右 Q/E偏航\n         Space上升 Shift下降 Ctrl刹车\n手柄: 左摇杆移动 LB/RB偏航 A升 B降 X刹车\n视角: 右键拖动环绕 滚轮距离 C追尾/自由", PanelKit.Mini);
        }

        public override void DrawHint(StringBuilder sb)
        {
            sb.Append("W/S/A/D平移 Q/E偏航 Space/Shift升降 Ctrl刹车 | R归位 | 右键+滚轮视角 C追尾 | 穿越发光环得分");
        }

        public override void WriteMetrics(StringBuilder sb)
        {
            if (body == null) return;
            sb.AppendLine($"mode=manual alt={body.Altitude:0.0} speed={body.Speed:0.0} state={(body.Landed ? "landed" : "air")}");
            sb.AppendLine($"maxAlt={body.MaxAlt:0.0} maxSpeed={body.MaxSpeedReached:0.0} dist={body.DistanceFlown:0} tilt=pitch{body.PitchDeg:+0;-0}/roll{body.RollDeg:+0;-0}");
            sb.AppendLine($"gates={gatesPassed}/{gates.Count} waypoints={auto?.VisitedCount ?? 0} autoLanded={(auto != null && auto.LandedDone)}");
            sb.AppendLine($"wind={WindField.SpeedMps:0.0}m/s rotor={body.Rpm01 * 100:0}%");
        }

        // ---------- 无头剧本 ----------
        public override void RunHeadlessScenario(string name)
        {
            var sc = ScenarioRunner.I;
            if (sc == null || body == null) return;

            // 自驾仪接管:起飞→矩形巡航→归航→自动降落
            pInput.Enabled = false;
            auto.enabled = true;
            foreach (var wp in cruisePath) auto.Enqueue(wp);
            // 降落由 OnTick 在航线完成(RouteDone)时自动触发,不挂固定时刻

            sc.At(15f, () => HeadlessAssert.Check(body.Altitude > 10f, $"15s 已起飞 (高度 {body.Altitude:0.0}m)"));
            sc.At(35f, () => HeadlessAssert.Check(auto.VisitedCount >= 3, $"35s 已过航点≥3 (实际 {auto.VisitedCount})"));
            sc.At(60f, () => HeadlessAssert.Check(auto.VisitedCount >= 6, $"60s 已过航点≥6 (实际 {auto.VisitedCount})"));
            sc.At(75f, () =>
            {
                HeadlessAssert.Check(auto.VisitedCount >= 7, $"75s 巡航完成≥7 (实际 {auto.VisitedCount})");
                HeadlessAssert.Check(gatesPassed >= 2, $"穿越环≥2 (实际 {gatesPassed})");
                HeadlessAssert.Check(body.MaxAlt >= 15f, $"最大高度≥15m (实际 {body.MaxAlt:0.0})");
                HeadlessAssert.Check(body.MaxSpeedReached >= 6f, $"惯性加速最大速度≥6m/s (实际 {body.MaxSpeedReached:0.0})");
            });
            sc.At(95f, () => HeadlessAssert.Check(auto.LandedDone && body.Landed,
                $"95s 自动降落完成 (done={auto.LandedDone} landed={body.Landed})"));
        }
    }
}

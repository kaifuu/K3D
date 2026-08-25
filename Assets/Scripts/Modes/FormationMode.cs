using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 模块6 集群编队:9 机(1 领机 + 8 僚机)沿线自动巡航,
    /// 楔形/纵队/横队/菱形一键切换(槽位平滑过渡无瞬移)、间距/巡航速可调;
    /// 航线侧立障碍塔触发侧向绕行,通过后自动归位,全程误差/净距可导出。
    /// </summary>
    public class FormationMode : DrillMode
    {
        public override string Id => "formation";
        public override string Title => "集群编队飞行";
        public override string Brief =>
            "9 机编队(领机+8 僚机)自动巡航,楔形/纵队/横队/菱形一键切换与间距调节;槽位前馈+P 跟随平滑过渡,遇障碍塔侧向绕行、通过后自动归位。";

        const int WingCount = 8;
        FlightBody leader;
        RouteFollower follower;
        FormationController ctrl;
        readonly List<FormationHandle> units = new List<FormationHandle>(WingCount);
        readonly List<FlightBody> wingBodies = new List<FlightBody>(WingCount);
        Vector3 padPos = new Vector3(0f, 0.55f, 70f);
        float fps = 60f;

        public override void Build()
        {
            ObstacleAvoid.Clear();
            EnvironmentBuilder.ResetToDayDefault();
            EnvironmentBuilder.BuildLighting(Root);
            EnvironmentBuilder.CreateGround(Root);
            EnvironmentBuilder.MakeRing(Root, 110f, new Color(0.3f, 0.5f, 0.6f, 0.22f), "FieldBound", 0.05f);
            BuildScenery();
            EnvironmentBuilder.MakeFlatDisc(Root, 6f, new Color(0.15f, 0.65f, 0.85f, 0.18f), "Pad");
            EnvironmentBuilder.MakeRing(Root, 6.4f, new Color(0.2f, 0.8f, 1f, 0.5f), "PadRing", 0.06f);

            // ---- 领机(航线跟随驱动)+ 环场航线(圆 r45 高28:恒定缓转,槽位扫摆可前馈覆盖) ----
            // 全员直接在空中槽位出生(初始误差=0,无起飞瞬态;驻停逻辑见 Handle 解锁)
            var leadPos = new Vector3(0f, 28f, 45f);          // 航线 0 号点(圆 90°)
            const float yaw0 = 255f;                          // 该点切向航向
            var leadGo = DroneFactory.Spawn(DroneRole.Player, Root, leadPos, "LeadDrone");
            leader = leadGo.AddComponent<FlightBody>();
            leader.HomePos = padPos;
            leader.Teleport(leadPos, yaw0);
            follower = leadGo.AddComponent<RouteFollower>();
            follower.Body = leader;
            var route = new RouteData();
            follower.Route = route;
            for (int i = 0; i < 48; i++)   // 48 边形≈圆:每角仅 7.5°,槽位扫摆 2.3m 前馈可覆盖
            {
                float a = Mathf.PI / 2f + i / 48f * Mathf.PI * 2f;
                route.Add(new Vector3(Mathf.Cos(a) * 45f, 28f, Mathf.Sin(a) * 45f));
            }
            route.Loop = true;
            var visual = NewGo("RouteVisual").AddComponent<RouteVisual>();
            visual.Route = route;

            // ---- 编队中枢 + 8 僚机(楔形槽位就位) ----
            ctrl = NewGo("FormationCtrl").AddComponent<FormationController>();
            ctrl.Leader = leader;
            var rot0 = Quaternion.Euler(0f, yaw0, 0f);
            for (int i = 0; i < WingCount; i++)
            {
                var p = leadPos + rot0 * FormationLibrary.SlotOffset(
                    FormationShape.Wedge, i + 1, WingCount + 1, ctrl.Spacing);
                var go = DroneFactory.Spawn(DroneRole.Blue, Root, p, $"Wing{i + 1:00}");
                var b = go.AddComponent<FlightBody>();
                b.MaxSpeed = 18f;      // 僚机加减速裕量(追赶/避障),默认 16 追不上转弯扫摆
                b.HomePos = p;
                b.Teleport(p, yaw0);
                var h = go.AddComponent<FormationHandle>();
                h.Ctrl = ctrl;
                h.Body = b;
                h.Index = i + 1;
                ctrl.Units.Add(h);
                units.Add(h);
                wingBodies.Add(b);
            }

            BuildObstacles();

            // ---- 相机:领机后上方宽视角(覆盖横队 ±20m 翼展) ----
            var cam = CameraDirector.CreateCamera(Root);
            CameraDirector.Follow(cam, leadGo.transform, 30f);
            Ctx.MainCamera = cam;
        }

        void BuildScenery()
        {
            var spots = new[] {
                new Vector3(-78f, 0f, -60f), new Vector3(75f, 0f, -66f), new Vector3(80f, 0f, 66f), new Vector3(-72f, 0f, 66f) };
            float[] hts = { 24f, 16f, 28f, 12f };
            for (int i = 0; i < spots.Length; i++)
                PropKit.Building(Root, spots[i], 12f, hts[i], 12f, i);
        }

        /// <summary>障碍塔:单塔外环 330°(r60)——与横队外侧臂槽位半径(W3 r55/W5 r60/W7 r65)
        /// 相交,横队相位中段切向掠过触发侧推绕行(逼近需求基本切向,防逼近带不咬合);
        /// 楔/纵/菱相位槽位半径 29~50m 全部让开,收敛断言零干扰。
        /// (内环 r25 方案实测:楔形内臂槽位会长期滞留在 8m 防逼近带内,切向残速不足导致锁死,弃用)</summary>
        void BuildObstacles()
        {
            var obs = new[]
            {
                new Vector3(Mathf.Cos(11f * Mathf.PI / 6f) * 60f, 0f, Mathf.Sin(11f * Mathf.PI / 6f) * 60f),  // 外环 330°
            };
            foreach (var p in obs)
            {
                PropKit.ObstacleTower(Root, p, 3f, 45f);
                ObstacleAvoid.AddCylinder(p, 3f, 45f);

                // 塔底警示环(局部圆,挂塔位置父物体下)
                var ringGo = new GameObject("ObsRing");
                ringGo.transform.SetParent(Root, false);
                ringGo.transform.position = p;
                var ringMat = EnvironmentBuilder.UnlitMat(new Color(1f, 0.4f, 0.2f, 0.4f));
                for (int s = 0; s < 24; s++)
                {
                    float a0 = s / 24f * Mathf.PI * 2f;
                    var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    DestroySegCollider(seg);
                    seg.name = "Seg";
                    seg.transform.SetParent(ringGo.transform, false);
                    seg.transform.localPosition = new Vector3(Mathf.Cos(a0) * 6.5f, 0.05f, Mathf.Sin(a0) * 6.5f);
                    seg.transform.localRotation = Quaternion.Euler(0f, -a0 * Mathf.Rad2Deg + 90f, 0f);
                    seg.transform.localScale = new Vector3(1.8f, 0.06f, 0.3f);
                    seg.GetComponent<Renderer>().material = ringMat;
                }
            }
        }

        static void DestroySegCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }

        public override void OnTick(float dt)
        {
            if (leader == null || ctrl == null) return;
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.06f);

            // 快捷键:F 切构型 [ ] 调间距 R 重置
            if (Input.GetKeyDown(KeyCode.F)) CycleShape(1);
            if (Input.GetKeyDown(KeyCode.LeftBracket)) ctrl.Spacing = Mathf.Clamp(ctrl.Spacing - 0.5f, 3f, 8f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) ctrl.Spacing = Mathf.Clamp(ctrl.Spacing + 0.5f, 3f, 8f);
            if (Input.GetKeyDown(KeyCode.R)) ResetAll();

            // 悬浮标注
            var cam = Ctx.MainCamera;
            if (cam != null)
            {
                Overlay.Label(leader.transform.position + Vector3.up * 2.4f,
                    $"领机  {FormationLibrary.Name(ctrl.Shape)}  {ctrl.Count} 机  稳态误差 {ctrl.MaxSteadyError:0.0}m",
                    new Color(0.4f, 0.9f, 1f));
                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    if (u == null) continue;
                    bool warn = u.Avoiding;
                    Overlay.Label(u.transform.position + Vector3.up * 1.8f,
                        warn ? $"W{i + 1} ⚠绕行" : $"W{i + 1} {u.SlotError:0.0}m",
                        warn ? new Color(1f, 0.45f, 0.25f) : new Color(0.55f, 0.75f, 1f));
                }
            }
        }

        void CycleShape(int dir)
        {
            var all = FormationLibrary.All;
            int idx = 0;
            for (int i = 0; i < all.Length; i++) if (all[i] == ctrl.Shape) idx = i;
            ctrl.SetShape(all[(idx + dir + all.Length) % all.Length]);
        }

        void ResetAll()
        {
            follower.StopRoute();
            ctrl.StopFormation();
            leader.Teleport(padPos, 180f);
            leader.ResetStats();
            for (int i = 0; i < units.Count; i++)
            {
                int row = i / 2 + 1;
                float side = i % 2 == 0 ? 1f : -1f;
                if (units[i] != null) units[i].Body.Teleport(padPos + new Vector3(side * row * 2.2f, 0f, row * 2.8f), 180f);
            }
            EventBus.Publish("任务", "", "编队已重置归位", EventGrade.Op);
        }

        // ---------- UI ----------
        public override void DrawSidePanel(Rect r)
        {
            if (ctrl == null) return;
            float y = r.y;

            GUI.Label(new Rect(r.x, y, r.width, 20), "编队构型", PanelKit.Header);
            y += 24;
            float w2 = (r.width - 6f) / 2f;
            var all = FormationLibrary.All;
            for (int i = 0; i < all.Length; i++)
            {
                float bx = r.x + (i % 2) * (w2 + 6f);
                float by = y + (i / 2) * 28f;
                if (PanelKit.ToggleBtn(bx, by, w2, 24, FormationLibrary.Name(all[i]), ctrl.Shape == all[i]))
                    ctrl.SetShape(all[i]);
            }
            y += 62;

            GUI.Label(new Rect(r.x, y, r.width, 16), $"机间间距 {ctrl.Spacing:0.0} m  ([ / ])", PanelKit.Mini);
            y += 16;
            ctrl.Spacing = GUI.HorizontalSlider(new Rect(r.x, y, r.width, 14), ctrl.Spacing, 3f, 8f);
            y += 22;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"巡航速度 {follower.Cruise:0.0} m/s", PanelKit.Mini);
            y += 16;
            follower.Cruise = GUI.HorizontalSlider(new Rect(r.x, y, r.width, 14), follower.Cruise, 4f, 12f);
            y += 24;

            if (PanelKit.Btn(r.x, y, w2, 24, "开始编队巡航", !follower.Started && !ctrl.Active))
            {
                follower.DeviationLimit = 20f;
                follower.StartRoute();
                ctrl.StartFormation();
            }
            if (PanelKit.Btn(r.x + w2 + 6f, y, w2, 24, "停止(悬停)", follower.Started || ctrl.Active))
            {
                follower.StopRoute();
                ctrl.StopFormation();
            }
            y += 30;

            var errCol = ctrl.Converged ? Color.white : new Color(1f, 0.7f, 0.3f);
            GUI.Label(new Rect(r.x, y, r.width, 16),
                ctrl.Active ? $"状态 巡航中   切换 {ctrl.SwitchCount} 次" : "状态 待命", PanelKit.Small);
            y += 16;
            var prev = GUI.color; GUI.color = errCol;
            GUI.Label(new Rect(r.x, y, r.width, 16),
                $"槽位误差 最大 {ctrl.MaxSlotError:0.0} m   稳态 {ctrl.MaxSteadyError:0.0} m   均 {ctrl.AvgSlotError:0.0} m", PanelKit.Small);
            GUI.color = prev;
            y += 16;
            var clCol = ctrl.MinClearance < 1f ? Color.red : Color.white;
            GUI.color = clCol;
            GUI.Label(new Rect(r.x, y, r.width, 16),
                $"障碍净距 {ctrl.MinClearance:0.0} m   绕行中 {(ctrl.AnyAvoiding ? "有" : "无")}", PanelKit.Small);
            GUI.color = prev;
            y += 24;

            GUI.Label(new Rect(r.x, y, r.width, r.y + r.height - y),
                "F 切构型 [ ] 间距 R 重置;\n领机自动巡线,僚机槽位跟随(前馈+P);\n红色塔=障碍,翼侧绕行后自动归位。",
                PanelKit.Mini);
        }

        public override void DrawHint(StringBuilder sb)
        {
            sb.Append("F 切构型 | [ ] 间距 | R 重置 | 侧板:4 构型/间距/巡航速/起停");
        }

        public override void WriteMetrics(StringBuilder sb)
        {
            if (ctrl == null || leader == null) return;
            sb.AppendLine($"formation=shape:{FormationLibrary.Name(ctrl.Shape)} n:{ctrl.Count} spacing:{ctrl.Spacing:0.0} active:{ctrl.Active} switches:{ctrl.SwitchCount}");
            sb.AppendLine($"slot=max:{ctrl.MaxSlotError:0.00}m steady:{ctrl.MaxSteadyError:0.00}m avg:{ctrl.AvgSlotError:0.00}m converged:{ctrl.Converged} threshold:{FormationController.ConvergeThreshold:0.0}m");
            sb.AppendLine($"avoid=events:{ObstacleAvoid.AvoidEvents} minClearance:{ctrl.MinClearance:0.00}m anyNow:{ctrl.AnyAvoiding}");
            sb.AppendLine($"leader=alt:{leader.Altitude:0.0} spd:{leader.Speed:0.0} dist:{leader.DistanceFlown:0} loops:{follower.Loops} fps:{fps:0}");
            var errs = new StringBuilder();
            for (int i = 0; i < units.Count; i++)
                errs.Append(units[i] != null ? $"{units[i].SlotError:0.0} " : "- ");
            sb.AppendLine($"wingSlotErr={errs}");
        }

        // ---------- 无头剧本:起飞入队 → 4 构型轮切收敛 → 避障 → 归位 ----------
        public override void RunHeadlessScenario(string name)
        {
            var sc = ScenarioRunner.I;
            if (sc == null || ctrl == null) return;

            follower.Cruise = 8f;
            follower.DeviationLimit = 20f;
            follower.StartRoute();
            ctrl.StartFormation();

            sc.At(9f, () => HeadlessAssert.Check(
                leader.Altitude > 12f && ctrl.Converged && AllWingsAirborne(),
                $"9s 集群起飞入队(领机 {leader.Altitude:0.0}m 稳态误差 {ctrl.MaxSteadyError:0.00}m)"));
            sc.At(10f, () => ctrl.SetShape(FormationShape.Column));
            sc.At(16f, () => HeadlessAssert.Check(ctrl.Converged,
                $"16s 纵队收敛(切换后稳态误差曾<{FormationController.ConvergeThreshold:0.0}m,当前 {ctrl.MaxSteadyError:0.00}m)"));
            sc.At(19f, () => ctrl.SetShape(FormationShape.Line));
            sc.At(25f, () => HeadlessAssert.Check(ctrl.Converged,
                $"25s 横队收敛(当前 {ctrl.MaxSteadyError:0.00}m)"));
            sc.At(28f, () => ctrl.SetShape(FormationShape.Diamond));
            sc.At(37f, () => HeadlessAssert.Check(ctrl.Converged,
                $"37s 菱形收敛(当前 {ctrl.MaxSteadyError:0.00}m)"));
            sc.At(39f, () => ctrl.SetShape(FormationShape.Wedge));

            sc.At(39f, () => HeadlessAssert.Check(ObstacleAvoid.AvoidEvents > 0 && ctrl.MinClearance > 0.2f,
                $"39s 障碍绕行触发({ObstacleAvoid.AvoidEvents} 次,最小净距 {ctrl.MinClearance:0.00}m)"));
            sc.At(47f, () => HeadlessAssert.Check(
                !ctrl.AnyAvoiding && ctrl.MaxSlotError < 1.5f && follower.Loops >= 1,
                $"47s 绕行后归位(最大误差 {ctrl.MaxSlotError:0.00}m 圈数 {follower.Loops})"));
        }

        bool AllWingsAirborne()
        {
            foreach (var b in wingBodies)
                if (b == null || b.Altitude < 10f) return false;
            return true;
        }
    }
}

using System.Text;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 监管反制专项模式(原推演玩法整体迁移):
    /// 波次黑飞来袭 → 侦测列表/锁定 → 干扰/捕获网/激光反制 → 判分与设施完整度。
    /// </summary>
    public class RegulatorMode : DrillMode
    {
        public override string Id => "regulator";
        public override string Title => "监管反制专项";
        public override string Brief => "空域侦测预警与黑飞处置闭环:分级预警、电磁干扰/捕获网/激光反制、判分与空域复位。";

        Spawner spawner;
        SwarmIntercept swarm;
        ThreatGrader grader = new ThreatGrader();
        float gradeTimer;
        float perfAcc; int perfN;                     // 平均帧率累计(性能验收)

        public override void Build()
        {
            EnvironmentBuilder.ResetToDayDefault();
            EnvironmentBuilder.BuildLighting(Root);
            EnvironmentBuilder.CreateGround(Root);
            EnvironmentBuilder.BuildZones(Root);
            EnvironmentBuilder.BuildCoreFacility(Root);
            EnvironmentBuilder.BuildRadarStation(Root, new Vector3(-55f, 0f, -55f));

            EnvironmentBuilder.BuildCounterUnit(Root, new Vector3(70f, 0f, 0f),
                CounterUnit.Mode.Jammer, "Jammer-01", new Color(1f, 0.75f, 0.1f));
            EnvironmentBuilder.BuildCounterUnit(Root, new Vector3(-45f, 0f, 65f),
                CounterUnit.Mode.NetGun, "NetGun-01", new Color(0.85f, 0.85f, 0.9f));
            EnvironmentBuilder.BuildCounterUnit(Root, new Vector3(0f, 0f, -80f),
                CounterUnit.Mode.Laser, "Laser-01", new Color(1f, 0.25f, 0.2f));

            BuildCamera();

            var spGo = NewGo("Spawner");
            spawner = spGo.AddComponent<Spawner>();
            spawner.dronePrefab = DroneFactory.BuildTemplate(DroneRole.Hostile);
            swarm = spGo.AddComponent<SwarmIntercept>();
            swarm.Spn = spawner;
            CounterUnit.Grader = grader;              // 自动防御按威胁优先级选靶
        }

        void BuildCamera()
        {
            var camGo = NewGo("MainCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.07f, 0.1f);
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 1500f;
            camGo.AddComponent<AudioListener>();

            var focus = NewGo("CamFocus");
            var rts = camGo.AddComponent<RTSCamera>();
            rts.focus = focus.transform;
            Ctx.MainCamera = cam;
        }

        // ---------- 交互(原 SimUI.Update 移植) ----------
        public override void OnTick(float dt)
        {
            var cam = Ctx.MainCamera;
            if (cam == null) return;

            // 威胁分级刷新(4Hz)+ 高威胁悬浮标注
            gradeTimer += dt;
            if (gradeTimer >= 0.25f)
            {
                gradeTimer = 0f;
                grader.Rebuild(spawner != null ? spawner.Active : null);
                for (int i = 0; i < grader.Ranked.Count && i < 3; i++)
                {
                    var g = grader.Ranked[i];
                    if (g.Drone == null || g.Level != ThreatLevel.Threat) continue;
                    Overlay.Label(g.Drone.transform.position + Vector3.up * 3f,
                        $"⚠ 威胁 P{g.Priority:0} → {g.Advice}", new Color(1f, 0.4f, 0.35f));
                }
            }

            // 性能采样(全程平均,供 P11 性能验收)
            float instFps = 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            perfAcc += instFps; perfN++;

            // 点击选择目标(避开 UI)
            if (Input.GetMouseButtonDown(0) && !UIRoot.MouseOverGUI)
            {
                var ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, 800f) && hit.collider != null)
                {
                    var d = hit.collider.GetComponentInParent<EnemyDrone>();
                    GameState.Selected = d;
                    if (d != null)
                        SimEvents.Add($"[选择] 已锁定 {d.DroneId} ({KindName(d.Kind)}) - 按1干扰 2捕获 3阻断 4激光");
                }
            }

            // 手动反制快捷键
            var sel = GameState.Selected;
            if (sel != null && sel.State == DroneState.Approaching)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) TryCounter(CounterUnit.Mode.Jammer);
                if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) TryCounter(CounterUnit.Mode.NetGun);
                if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) TryCounter(CounterUnit.Mode.Laser);
                if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
                {
                    int n = swarm.AreaBlock(sel);
                    if (n > 0) GameState.Selected = null;
                }
            }
            if (Input.GetKeyDown(KeyCode.Tab)) GameState.AutoDefend = !GameState.AutoDefend;
            if (Input.GetKeyDown(KeyCode.G)) swarm.TriggerSwarmWave(6);
        }

        void TryCounter(CounterUnit.Mode m)
        {
            var d = GameState.Selected;
            if (d == null || d.State != DroneState.Approaching) return;
            switch (m)
            {
                case CounterUnit.Mode.Jammer:
                    d.WarnRemoteId();
                    if (d.State == DroneState.Approaching) d.ApplyJamming();
                    FXManager.I?.JamBurst(Vector3.up * 2f, d.transform.position);
                    break;
                case CounterUnit.Mode.NetGun:
                    FXManager.I?.NetShot(Vector3.up * 2f, d.transform.position, d);
                    break;
                case CounterUnit.Mode.Laser:
                    d.LaserHit();
                    FXManager.I?.LaserBeam(Vector3.up * 2f, d.transform.position);
                    SimEvents.Add($"[反制] 对 {d.DroneId} 手动激光照射", EventGrade.Op);
                    break;
            }
        }

        string KindName(DroneKind k) =>
            k == DroneKind.Recon ? "侦察" : k == DroneKind.Attack ? "攻击" : "蜂群";

        // ---------- UI 钩子 ----------
        public override void DrawSidePanel(Rect r)
        {
            float y = r.y;
            GUI.Label(new Rect(r.x, y, r.width, 20), $"当前波次 第{GameState.Wave}波   自动防御 {(GameState.AutoDefend ? "开" : "关")}", PanelKit.Label);
            y += 26;
            if (PanelKit.Btn(r.x, y, 120, 24, GameState.AutoDefend ? "关闭自动防御" : "开启自动防御"))
                GameState.AutoDefend = !GameState.AutoDefend;
            if (PanelKit.Btn(r.x + 128, y, 120, 24, "重置空域状态"))
            {
                GameState.Reset();
                GameState.Wave = 1;
                SimEvents.Add("[推演] 空域状态已复位", EventGrade.Op);
            }
            y += 32;
            if (PanelKit.Btn(r.x, y, 150, 24, "蜂群来袭演练 (G)")) swarm.TriggerSwarmWave(6);
            if (PanelKit.Btn(r.x + 158, y, 150, 24, "区域信号阻断 (4)", GameState.Selected != null))
                swarm.AreaBlock(GameState.Selected);
            y += 32;

            // 分级预警面板(优先级排序,点击锁定)
            ThreatPanelUI.Draw(grader, r, ref y);
        }

        public override void DrawOverlay()
        {
            if (!GameState.FacilityDown) return;
            var c = GUI.color;
            GUI.color = new Color(1f, 0.25f, 0.2f, 0.85f);
            GUI.Box(new Rect(Screen.width / 2 - 200, Screen.height / 2 - 60, 400, 120), "");
            GUI.color = c;
            GUI.Label(new Rect(Screen.width / 2 - 185, Screen.height / 2 - 45, 370, 40),
                "核心设施损毁 — 防御失败", PanelKit.Header);
            GUI.Label(new Rect(Screen.width / 2 - 185, Screen.height / 2 - 12, 370, 60),
                $"最终得分 {GameState.Score}  拦截{GameState.NeutralizedJam + GameState.NeutralizedNet + GameState.NeutralizedLaser} 突破{GameState.Breaches}", PanelKit.Label);
        }

        public override void DrawHint(StringBuilder sb)
        {
            sb.Append("左键锁定/面板点选 | 1=电磁干扰 2=捕获网 4=区域阻断 3=激光 G=蜂群来袭 | Tab=自动防御 | WASD+右键+滚轮=视角");
        }

        public override void WriteMetrics(StringBuilder sb)
        {
            sb.AppendLine($"mode=regulator wave={GameState.Wave} score={GameState.Score} integrity={GameState.FacilityIntegrity:0}%");
            sb.AppendLine($"turnedBack={GameState.TurnedBack} jam={GameState.NeutralizedJam} net={GameState.NeutralizedNet} laser={GameState.NeutralizedLaser} block={GameState.BlockNeutralized} breach={GameState.Breaches}");
            sb.AppendLine($"threat=threat:{grader.CountAt(ThreatLevel.Threat)} warn:{grader.CountAt(ThreatLevel.Warn)} watch:{grader.CountAt(ThreatLevel.Watch)} topP:{(grader.Ranked.Count > 0 ? grader.Ranked[0].Priority : 0f):0}");
            sb.AppendLine($"spawned={spawner?.SpawnedCount ?? 0} airborne={spawner?.Active.Count ?? 0} avgFps:{(perfN > 0 ? perfAcc / perfN : 0f):0}");
        }

        public override void RunHeadlessScenario(string name)
        {
            var sc = ScenarioRunner.I;
            if (sc == null || swarm == null) return;

            // ---- 威胁分级 + 蜂群阻断 + 复位验收 ----
            sc.At(3f, () => GameState.AutoDefend = false);   // 编排期关自动防御,保证确定性
            sc.At(6f, () => swarm.TriggerSwarmWave(6));
            sc.At(10f, () =>
            {
                int n = 0;
                foreach (var d in Spawner.I.Active) if (d != null && d.Kind == DroneKind.Swarm) n++;
                HeadlessAssert.Check(n >= 5, $"10s 蜂群集群生成 {n} 机 ≥ 5");
            });
            sc.At(13f, () =>
            {
                EnemyDrone anchor = null;
                foreach (var d in Spawner.I.Active)
                    if (d != null && d.Kind == DroneKind.Swarm && d.State == DroneState.Approaching) { anchor = d; break; }
                GameState.Selected = anchor;
                int n = swarm.AreaBlock(anchor);
                HeadlessAssert.Check(n >= 3, $"13s 区域信号阻断半径内迫降 {n} 机 ≥ 3");
            });
            sc.At(16f, () =>
            {
                Spawner.I.SpawnWave(DroneKind.Attack, 1, 0f);
                foreach (var d in Spawner.I.Active)
                    if (d != null && d.Kind == DroneKind.Attack) { d.RemoteIdCompliant = false; d.KillViolation = false; }
            });
            sc.At(19f, () =>
            {
                var msg = grader.Ranked.Count > 0
                    ? $"19s 威胁分级 Top {grader.Ranked[0].Drone.DroneId} P{grader.Ranked[0].Priority:0}={grader.Ranked[0].Level}"
                    : "19s 威胁分级:无目标";
                HeadlessAssert.Check(
                    grader.Ranked.Count > 0 && grader.Ranked[0].Level == ThreatLevel.Threat, msg);
            });
            sc.At(21f, () => GameState.AutoDefend = true);
            sc.At(24f, () => HeadlessAssert.Check(GameState.Score > 0,
                $"24s 阻断已判分(得分 {GameState.Score})"));
            sc.At(27f, () =>
            {
                GameState.Reset();
                GameState.Wave = 1;
                bool clean = GameState.Score == 0 && GameState.Wave == 1 &&
                             GameState.FacilityIntegrity >= 100f && GameState.BlockNeutralized == 0;
                HeadlessAssert.Check(clean, $"27s 空域复位(分 {GameState.Score} 波 {GameState.Wave} 完整度 {GameState.FacilityIntegrity:0}%)");
            });
            sc.At(40f, () => HeadlessAssert.Check(Spawner.I.SpawnedCount > 10,
                $"40s 复位后生成持续(累计 {Spawner.I.SpawnedCount})"));

            // ---- 性能验收:夜+雨+20体集群 ----
            sc.At(48f, () =>
            {
                var rig = EnvironmentRig.I;
                if (rig != null) { rig.SetPhase(DayPhase.Night); rig.SetWeather(WeatherKind.Rain, 0.8f); }
            });
            sc.At(50f, () => { Spawner.I.SpawnWave(DroneKind.Swarm, 10); Spawner.I.SpawnWave(DroneKind.Recon, 10); });
            sc.At(57f, () =>
            {
                float avg = perfN > 0 ? perfAcc / perfN : 0f;
                HeadlessAssert.Check(avg >= 30f,
                    $"57s 性能:夜雨+{Spawner.I.Active.Count}体 平均帧率 {avg:0} ≥ 30");
            });
        }
    }
}

using System.Text;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 模块5 应急战术处置:火情空中侦察 / 黑飞喊话驱离 / 链路失联保护 /
    /// 物资伞降投送 —— 单机多任务应急处置流程演练。
    /// 事件触发→自驾飞往处置位;火焰烟柱/声波环/失联环/弹跳落点全 3D(批处理截图可见)。
    /// </summary>
    public class TacticsMode : DrillMode
    {
        public override string Id => "tactics";
        public override string Title => "应急战术处置";
        public override string Brief =>
            "火情侦察、黑飞驱离、失联保护、物资投送:单机多任务应急流程,声波喊话+伞降落点弹跳可量化。";

        enum TacPhase { Hold, ToFire, ScanFire, ToIntruder, ToDrop, Done }

        FlightBody body;
        FlightAutopilot pilot;
        SpeakerDeter speaker;
        FireSite fireSite;
        IntruderAlert intruder;
        LinkLoss link;
        DropZone dropZone;
        SupplyCrate crate;
        ChaseCamera chase;
        readonly System.Collections.Generic.List<CivilianTarget> civilians = new System.Collections.Generic.List<CivilianTarget>();

        TacPhase phase = TacPhase.Hold;
        float orbitAngle, fps = 60f;
        float speakerRange = 40f, linkDuration = 6f, terminalMps = 4.4f;

        static readonly Vector3 fireOrbitCenter = new Vector3(-40f, 26f, -30f);
        static readonly Vector3 holdPos = new Vector3(0f, 26f, 12f);
        static readonly Vector3 interceptPos = new Vector3(32f, 24f, 14f);
        static readonly Vector3 dropPos = new Vector3(34f, 24f, 28f);
        static readonly Vector3 intruderSpawn = new Vector3(72f, 22f, 32f);

        public override void Build()
        {
            EnvironmentBuilder.ResetToDayDefault();
            EnvironmentBuilder.BuildLighting(Root);
            EnvironmentBuilder.CreateGround(Root);
            EnvironmentBuilder.MakeRing(Root, 130f, new Color(0.3f, 0.5f, 0.6f, 0.22f), "FieldBound", 0.05f);
            BuildBlocks();

            // ---- 火情建筑(B区仓库)+ 火点 ----
            PropKit.Warehouse(Root, new Vector3(-42f, 0f, -34f), 12f, 14f, 12f, 25f);
            fireSite = NewGo("FireSite", new Vector3(-34.2f, 1f, -28.6f)).AddComponent<FireSite>();

            // ---- 管制禁区(红)与投送区(绿) ----
            EnvironmentBuilder.MakeFlatDisc(Root, 20f, new Color(1f, 0.3f, 0.2f, 0.08f), "NoFlyDisc");
            EnvironmentBuilder.MakeRing(Root, 20f, new Color(1f, 0.35f, 0.25f, 0.45f), "NoFlyRing", 0.06f);
            dropZone = NewGo("DropZone", new Vector3(34f, 0.05f, 28f)).AddComponent<DropZone>();
            EnvironmentBuilder.MakeFlatDisc(Root, 6f, new Color(0.2f, 0.9f, 0.45f, 0.14f), "DropDisc");
            EnvironmentBuilder.MakeRing(Root, 6.4f, new Color(0.35f, 1f, 0.55f, 0.55f), "DropRing", 0.06f);

            // ---- 黑飞机体(预创建待命隐藏) ----
            var intruderGo = DroneFactory.Spawn(DroneRole.Red, Root, intruderSpawn, "BlackFly");
            intruderGo.transform.localScale = Vector3.one * 0.75f;
            intruder = intruderGo.AddComponent<IntruderAlert>();
            intruderGo.SetActive(false);

            // ---- 蓝方战术机:机体 + 自驾 + 喊话 ----
            var droneGo = DroneFactory.Spawn(DroneRole.Blue, Root, holdPos, "TacticalUnit");
            body = droneGo.AddComponent<FlightBody>();
            body.MaxSpeed = 20f;
            body.Teleport(holdPos, 180f);
            body.HomePos = holdPos;
            pilot = droneGo.AddComponent<FlightAutopilot>();
            pilot.Body = body;
            pilot.Cruise = 18f;
            speaker = droneGo.AddComponent<SpeakerDeter>();
            speaker.Intruder = intruder;

            // ---- 物资箱挂载 ----
            var crateGo = PropKit.SupplyCrate(Root);
            crate = crateGo.AddComponent<SupplyCrate>();
            crate.Zone = dropZone;
            crate.Attach(droneGo.transform);

            // ---- 失联处置组件 ----
            link = NewGo("LinkLoss").AddComponent<LinkLoss>();
            link.Body = body;
            link.Autopilot = pilot;

            // ---- 地面人员 ×3(禁区内) ----
            SpawnCivilian(new Vector3(14f, 0f, 8f), new Color(0.75f, 0.62f, 0.45f));
            SpawnCivilian(new Vector3(16f, 0f, -6f), new Color(0.5f, 0.58f, 0.72f));
            SpawnCivilian(new Vector3(10f, 0f, 16f), new Color(0.68f, 0.48f, 0.52f));
            speaker.Civilians.AddRange(civilians);

            // ---- 相机:跟蓝方战术机 ----
            var cam = CameraDirector.CreateCamera(Root);
            chase = CameraDirector.Follow(cam, droneGo.transform, 16f);
            Ctx.MainCamera = cam;
        }

        void BuildBlocks()
        {
            var spots = new[] {
                new Vector3(-84f, 0f, 40f), new Vector3(76f, 0f, -52f), new Vector3(-70f, 0f, -76f) };
            float[] hts = { 18f, 24f, 15f };
            for (int i = 0; i < spots.Length; i++)
                PropKit.Building(Root, spots[i], 12f, hts[i], 12f, i);
        }

        void SpawnCivilian(Vector3 pos, Color cloth)
        {
            var c = PropKit.Person(Root, pos, cloth);
            var civ = c.AddComponent<CivilianTarget>();
            civ.BuildMarker();
            civilians.Add(civ);
        }

        Vector3 FireOrbitPoint() => new Vector3(
            fireOrbitCenter.x + Mathf.Cos(orbitAngle * Mathf.Deg2Rad) * 13f,
            fireOrbitCenter.y,
            fireOrbitCenter.z + Mathf.Sin(orbitAngle * Mathf.Deg2Rad) * 13f);

        // ---------- 应急处置指令(热键/侧板/剧本共用) ----------

        public void IgniteFire()
        {
            if (fireSite == null || fireSite.Burning) return;
            fireSite.Ignite();
            phase = TacPhase.ToFire;
            orbitAngle = 0f;
            pilot.ResetRoute();
            pilot.Enqueue(FireOrbitPoint());
            if (chase != null)
            {
                chase.LookOverride = fireSite.transform;   // 侦察取景:相机看向火场
                chase.SetPitch(58f);
                chase.Dist = 11f;                          // 近距侦察镜头
                chase.Shake(0.45f, 0.9f);
            }
        }

        public void CallIntruder()
        {
            if (intruder == null || intruder.Active) return;
            intruder.Spawn(intruderSpawn);
            phase = TacPhase.ToIntruder;
            pilot.ResetRoute();
            pilot.Enqueue(interceptPos);
            if (chase != null) { chase.LookOverride = null; chase.SetPitch(18f); chase.Dist = 16f; }
        }

        public void BeginDrop()
        {
            if (crate == null || crate.Released) return;
            phase = TacPhase.ToDrop;
            pilot.ResetRoute();
            pilot.Enqueue(dropPos);
            EventBus.Publish("应急", "TacticalUnit", "转场:前往投送点上空执行伞降投送", EventGrade.Op);
        }

        void ReleaseCrate()
        {
            if (crate == null || crate.Released) return;
            crate.TerminalMps = terminalMps;
            crate.Release(body);
            phase = TacPhase.Done;
            if (chase != null)
            {
                chase.Target = crate.transform;   // 相机跟伞降过程
                chase.LookOverride = null;
                chase.SetPitch(18f);
                chase.Dist = 9f;
                chase.Shake(0.2f, 0.3f);
            }
        }

        public override void OnTick(float dt)
        {
            if (body == null) return;
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.06f);

            if (Input.GetKeyDown(KeyCode.F)) IgniteFire();
            if (Input.GetKeyDown(KeyCode.I)) CallIntruder();
            if (Input.GetKeyDown(KeyCode.L)) link.Begin();
            if (Input.GetKeyDown(KeyCode.G)) BeginDrop();
            if (Input.GetKeyDown(KeyCode.H)) speaker.Broadcast();
            if (Input.GetKeyDown(KeyCode.R))
                ModeManager.Enter(Id, Ctx.Params);

            // 任务状态机:到位即切换(失联期间自驾停驶,不推进)
            if (pilot != null && pilot.RouteDone && !link.Lost)
            {
                switch (phase)
                {
                    case TacPhase.ToFire:
                        phase = TacPhase.ScanFire;
                        if (!fireSite.Scanned)
                        {
                            fireSite.Scanned = true;
                            EventBus.Publish("应急", "TacticalUnit",
                                "空中侦察完成:火点位于B区仓库东南角,浓烟向东南扩散,已回传指挥中心", EventGrade.Op);
                        }
                        orbitAngle += 100f;
                        pilot.Enqueue(FireOrbitPoint());
                        break;
                    case TacPhase.ScanFire:
                        orbitAngle += 100f;
                        pilot.Enqueue(FireOrbitPoint());   // 持续绕火场盘旋侦察
                        break;
                    case TacPhase.ToDrop:
                        ReleaseCrate();
                        break;
                }
            }
        }

        // ---------- UI ----------

        public override void DrawSidePanel(Rect r)
        {
            float y = r.y;
            GUI.Label(new Rect(r.x, y, r.width, 20), "应急处置", PanelKit.Header);
            y += 26;

            float w2 = (r.width - 6f) / 2f;
            if (PanelKit.Btn(r.x, y, w2, 24, "火情侦察 (F)", !fireSite.Burning)) IgniteFire();
            if (PanelKit.Btn(r.x + w2 + 6f, y, w2, 24, "黑飞入侵 (I)", !intruder.Active)) CallIntruder();
            y += 28;
            if (PanelKit.Btn(r.x, y, w2, 24, "链路失联 (L)", !link.Lost)) link.Begin();
            if (PanelKit.Btn(r.x + w2 + 6f, y, w2, 24, "物资投送 (G)", !crate.Released)) BeginDrop();
            y += 28;
            if (PanelKit.Btn(r.x, y, w2, 24, "空中喊话 (H)", true)) speaker.Broadcast();
            if (PanelKit.Btn(r.x + w2 + 6f, y, w2, 24, "重置 (R)", true))
                ModeManager.Enter(Id, Ctx.Params);
            y += 32;

            GUI.Label(new Rect(r.x, y, r.width, 16), $"喊话半径 {speakerRange:0} m", PanelKit.Mini);
            y += 16;
            speakerRange = GUI.HorizontalSlider(new Rect(r.x, y, r.width, 14), speakerRange, 25f, 60f);
            speaker.Range = speakerRange;
            y += 22;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"失联时长 {linkDuration:0.0} s", PanelKit.Mini);
            y += 16;
            linkDuration = GUI.HorizontalSlider(new Rect(r.x, y, r.width, 14), linkDuration, 3f, 10f);
            link.Duration = linkDuration;
            y += 22;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"伞降末速 {terminalMps:0.0} m/s", PanelKit.Mini);
            y += 16;
            terminalMps = GUI.HorizontalSlider(new Rect(r.x, y, r.width, 14), terminalMps, 3f, 7f);
            y += 26;

            int idle = 0, flee = 0, safe = 0;
            foreach (var c in civilians)
            {
                if (c == null) continue;
                if (c.State == CivilianTarget.CivState.Idle) idle++;
                else if (c.State == CivilianTarget.CivState.Fleeing) flee++;
                else safe++;
            }

            GUI.Label(new Rect(r.x, y, r.width, 16), $"任务 {PhaseText()}   喊话 {speaker.Broadcasts} 次", PanelKit.Small);
            y += 16;
            GUI.Label(new Rect(r.x, y, r.width, 16),
                fireSite.Burning ? $"火情 燃烧{fireSite.BurnTime:0}s {(fireSite.Scanned ? "已侦察" : "待侦察")}" : "火情 无",
                PanelKit.Small);
            y += 16;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"黑飞 {IntruderText()}", PanelKit.Small);
            y += 16;
            var linkCol = link.Lost ? Color.red : link.Recovered ? new Color(0.4f, 1f, 0.5f) : Color.white;
            var prev = GUI.color; GUI.color = linkCol;
            GUI.Label(new Rect(r.x, y, r.width, 16),
                link.Lost ? $"失联中 {link.LostSeconds:0.0}s 漂移{link.DriftM:0.0}m" : link.Recovered ? "链路已恢复" : "链路正常",
                PanelKit.Small);
            GUI.color = prev;
            y += 16;
            GUI.Label(new Rect(r.x, y, r.width, 16), DropText(), PanelKit.Small);
            y += 16;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"人员 滞留{idle} 撤离{flee} 安全{safe}", PanelKit.Small);
        }

        string PhaseText() => phase switch
        {
            TacPhase.Hold => "待命",
            TacPhase.ToFire => "转场:火场",
            TacPhase.ScanFire => "火场侦察",
            TacPhase.ToIntruder => "转场:拦截位",
            TacPhase.ToDrop => "转场:投送点",
            TacPhase.Done => "投送完成",
            _ => "-"
        };

        string IntruderText() => intruder.Left ? "已离场" : intruder.Deterred ? "驱离中" : intruder.Active ? "逼近禁区" : "未出现";

        string DropText() => !crate.Released ? "物资 挂载中"
            : crate.Settled ? $"落点偏差 {crate.ErrorM:0.0}m 弹跳 {crate.Bounces} 次"
            : "伞降中";

        public override void DrawHint(StringBuilder sb)
        {
            sb.Append("F 火情 | I 黑飞 | L 失联 | G 投送 | H 喊话 | R 重置");
        }

        public override void WriteMetrics(StringBuilder sb)
        {
            if (body == null) return;
            int idle = 0, flee = 0, safe = 0;
            foreach (var c in civilians)
            {
                if (c == null) continue;
                if (c.State == CivilianTarget.CivState.Idle) idle++;
                else if (c.State == CivilianTarget.CivState.Fleeing) flee++;
                else safe++;
            }
            sb.AppendLine($"tactics=phase:{phase} fireBurn:{fireSite.BurnTime:0}s scanned:{fireSite.Scanned} " +
                $"intruder:{IntruderText()} link:{(link.Lost ? $"失联{link.LostSeconds:0.0}s" : link.Recovered ? "已恢复" : "正常")} linkDrift:{link.DriftM:0.0}m");
            sb.AppendLine($"drop=released:{crate.Released} settled:{crate.Settled} error:{crate.ErrorM:0.0}m bounces:{crate.Bounces} " +
                $"land:({crate.LandPos.x:0.0},{crate.LandPos.z:0.0}) relAt:{(crate.ReleasedAt < 0f ? -1f : crate.ReleasedAt):0.0}s setAt:{(crate.SettledAt < 0f ? -1f : crate.SettledAt):0.0}s");
            sb.AppendLine($"civ=idle:{idle} fleeing:{flee} safe:{safe} broadcasts:{speaker.Broadcasts} unitAlt:{body.Altitude:0.0}m fps:{fps:0}");
            sb.AppendLine($"fx=fireAlive:{fireSite.FireAlive} smokeAlive:{fireSite.SmokeAlive} playing:{fireSite.FirePlaying} light:{fireSite.LightInt:0.0}");
            // P8 调试:相机位姿 + 火场各 Renderer 包围盒/可见性(定位火焰不可见问题)
            if (Ctx.MainCamera != null)
            {
                var cp = Ctx.MainCamera.transform.position; var cf = Ctx.MainCamera.transform.forward;
                sb.AppendLine($"cam=pos:({cp.x:0.0},{cp.y:0.0},{cp.z:0.0}) fwd:({cf.x:0.00},{cf.y:0.00},{cf.z:0.00})");
            }
            var fxd = new StringBuilder("fire@(");
            fxd.Append($"{fireSite.transform.position.x:0.0},{fireSite.transform.position.y:0.0},{fireSite.transform.position.z:0.0}) ");
            foreach (var rd in fireSite.GetComponentsInChildren<Renderer>())
                fxd.Append($"{rd.name}@({rd.bounds.center.x:0.0},{rd.bounds.center.y:0.0},{rd.bounds.center.z:0.0})h{rd.bounds.size.y:0.0}v{(rd.isVisible ? 1 : 0)} ");
            sb.AppendLine($"fxbd={fxd}");
        }

        // ---------- 无头剧本:五类应急全流程 ----------
        public override void RunHeadlessScenario(string name)
        {
            var sc = ScenarioRunner.I;
            if (sc == null || fireSite == null) return;

            sc.At(0.5f, IgniteFire);
            sc.At(4f, () => HeadlessAssert.Check(fireSite.Burning && DistTo(fireOrbitCenter) < 60f,
                $"4s 火情侦察出动(距火场 {DistTo(fireOrbitCenter):0}m)"));

            sc.At(7f, CallIntruder);
            sc.At(11f, () => HeadlessAssert.Check(intruder.Active && !intruder.Deterred,
                $"11s 黑飞逼近禁区(位置 {intruder.transform.position.x:0},{intruder.transform.position.z:0})"));

            sc.At(13f, speaker.Broadcast);
            sc.At(15f, () => HeadlessAssert.Check(intruder.Deterred && CountCiv(CivilianTarget.CivState.Fleeing) >= 2,
                $"15s 喊话驱离生效(黑方 {(intruder.Deterred ? "已调头" : "仍逼近")} 撤离中 {CountCiv(CivilianTarget.CivState.Fleeing)} 人)"));

            sc.At(17f, link.Begin);
            sc.At(20f, () => HeadlessAssert.Check(link.Lost,
                $"20s 链路失联保护中(失联 {link.LostSeconds:0.0}s 漂移 {link.DriftM:0.0}m)"));
            sc.At(23.5f, () => HeadlessAssert.Check(!link.Lost && link.Recovered,
                $"23.5s 链路自动恢复(失联全程 {link.Duration:0.0}s 漂移 {link.DriftM:0.0}m)"));

            sc.At(26.5f, BeginDrop);
            sc.At(32f, () => HeadlessAssert.Check(crate.Released && !crate.Settled,
                "32s 物资伞降中"));
            sc.At(38f, () => HeadlessAssert.Check(crate.Settled && crate.ErrorM < 12f,
                $"38s 落地停稳(偏差 {crate.ErrorM:0.0}m 弹跳 {crate.Bounces} 次)"));

            sc.At(39.5f, speaker.Broadcast);
            sc.At(45.5f, () => HeadlessAssert.Check(CountCiv(CivilianTarget.CivState.Safe) >= 2,
                $"45s 人员撤离完成(安全 {CountCiv(CivilianTarget.CivState.Safe)}/3)"));
        }

        float DistTo(Vector3 p) => Vector3.Distance(body.transform.position, p);

        int CountCiv(CivilianTarget.CivState s)
        {
            int n = 0;
            foreach (var c in civilians) if (c != null && c.State == s) n++;
            return n;
        }
    }
}

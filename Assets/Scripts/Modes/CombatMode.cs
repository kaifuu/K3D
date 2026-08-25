using System.Text;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 模块7 红蓝对抗:红方入侵机渗透核心区侦察后突围,蓝方拦截机前出追击;
    /// 锁定充能几何加权(距离+锥角),红方感知锁定即垂直折转+降高蛇形反制,
    /// 双结局(拦截命中迫降坠落 / 红方突出防御圈逃逸),参数全可调。
    /// </summary>
    public class CombatMode : DrillMode
    {
        public override string Id => "combat";
        public override string Title => "红蓝攻防对抗";
        public override string Brief =>
            "红方渗透侦察 vs 蓝方锁定拦截:进度圈充能、锁定括号、折转反制、迫降坠落/突围逃逸双结局,攻防参数可调。";

        BlueInterceptor blue;
        RedIntruderAI red;
        InterceptWarningFX warnFX;
        Vector3 redSpawn = new Vector3(0f, 26f, 125f);
        float fps = 60f;

        // ---------- 对抗参数(侧板滑杆/无头剧本可调) ----------
        float blueSpeed = 24f;
        float redCruise = 15f;
        float redDash = 21f;
        float lockRate = 0.22f;
        float evadeLevel = 0.4f;

        public string Outcome => red == null ? "进行中"
            : red.Phase == RedPhase.Hit ? "拦截成功"
            : red.Phase == RedPhase.Escaped ? "红方逃逸"
            : "进行中";

        public override void Build()
        {
            EnvironmentBuilder.ResetToDayDefault();
            EnvironmentBuilder.BuildLighting(Root);
            EnvironmentBuilder.CreateGround(Root);
            EnvironmentBuilder.MakeRing(Root, 130f, new Color(0.3f, 0.5f, 0.6f, 0.22f), "FieldBound", 0.05f);
            BuildScenery();
            BuildCoreFacility();

            // ---- 红方入侵机(待命悬停,开始指令后渗透) ----
            var redGo = DroneFactory.Spawn(DroneRole.Red, Root, redSpawn, "RedIntruder");
            var redBody = redGo.AddComponent<FlightBody>();
            redBody.MaxSpeed = redDash;
            redBody.Teleport(redSpawn, 180f);
            red = redGo.AddComponent<RedIntruderAI>();
            red.Body = redBody;
            red.CruiseSpeed = redCruise;
            red.DashSpeed = redDash;
            red.EvadeLockLevel = evadeLevel;

            // ---- 蓝方拦截机(核心区上空待命巡逻) ----
            var blueGo = DroneFactory.Spawn(DroneRole.Blue, Root, new Vector3(38f, 24f, 0f), "BlueInterceptor");
            var blueBody = blueGo.AddComponent<FlightBody>();
            blueBody.MaxSpeed = Mathf.Max(blueSpeed + 4f, 26f);
            blueBody.Teleport(new Vector3(38f, 24f, 0f), 180f);
            blue = blueGo.AddComponent<BlueInterceptor>();
            blue.Body = blueBody;
            blue.Red = red;
            blue.CruiseSpeed = blueSpeed;
            blue.LockRate = lockRate;
            red.Hunter = blue;

            // ---- 可视化与告警 ----
            var viz = NewGo("LockVisualizer").AddComponent<LockVisualizer>();
            viz.Blue = blue;
            viz.Red = red;
            warnFX = NewGo("WarnFX").AddComponent<InterceptWarningFX>();
            warnFX.Red = red;
            warnFX.InitZone(Root, 30f);

            // ---- 相机:跟蓝方(追击视角,锁定圈在正前方) ----
            var cam = CameraDirector.CreateCamera(Root);
            CameraDirector.Follow(cam, blueGo.transform, 17f);
            Ctx.MainCamera = cam;

            ApplyKnobs();
        }

        void BuildScenery()
        {
            var spots = new[] {
                new Vector3(-82f, 0f, -48f), new Vector3(78f, 0f, -70f), new Vector3(85f, 0f, 62f), new Vector3(-75f, 0f, 70f) };
            float[] hts = { 20f, 26f, 16f, 22f };
            for (int i = 0; i < spots.Length; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = $"Block{i}";
                b.transform.SetParent(Root, false);
                b.transform.position = spots[i] + Vector3.up * (hts[i] / 2f);
                b.transform.localScale = new Vector3(12f, hts[i], 12f);
                b.GetComponent<Renderer>().material = EnvironmentBuilder.StdMat(new Color(0.45f, 0.47f, 0.5f));
            }
        }

        /// <summary>核心受护设施:3 建筑组团 + 蓝方停机坪</summary>
        void BuildCoreFacility()
        {
            var cfg = new[] {
                new Vector3(0f, 0f, 0f), new Vector3(-8f, 0f, 6f), new Vector3(7f, 0f, -5f) };
            float[] h = { 12f, 7f, 9f };
            float[] w = { 10f, 7f, 6f };
            for (int i = 0; i < cfg.Length; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = $"Core{i}";
                b.transform.SetParent(Root, false);
                b.transform.position = cfg[i] + Vector3.up * (h[i] / 2f);
                b.transform.localScale = new Vector3(w[i], h[i], w[i]);
                b.GetComponent<Renderer>().material = EnvironmentBuilder.StdMat(new Color(0.5f, 0.52f, 0.58f));
            }
            EnvironmentBuilder.MakeFlatDisc(Root, 6f, new Color(0.15f, 0.45f, 0.95f, 0.18f), "BluePad");
            EnvironmentBuilder.MakeRing(Root, 6.4f, new Color(0.3f, 0.6f, 1f, 0.5f), "BluePadRing", 0.06f);
            var pad = new GameObject("BluePadPos");
            pad.transform.SetParent(Root, false);
            pad.transform.position = new Vector3(24f, 0.55f, 26f);
        }

        void ApplyKnobs()
        {
            if (blue == null || red == null) return;
            blue.CruiseSpeed = blueSpeed;
            blue.LockRate = lockRate;
            blue.Body.MaxSpeed = Mathf.Max(blueSpeed + 4f, 26f);
            red.CruiseSpeed = redCruise;
            red.DashSpeed = redDash;
            red.EvadeLockLevel = evadeLevel;
            red.Body.MaxSpeed = redDash;
        }

        void StartCombat()
        {
            if (red == null || blue == null) return;
            if (Outcome != "进行中") return;
            red.Begin();
            blue.BeginIntercept();
        }

        public override void OnTick(float dt)
        {
            if (red == null || blue == null) return;
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.06f);

            if (Input.GetKeyDown(KeyCode.B)) StartCombat();
            if (Input.GetKeyDown(KeyCode.R))
                ModeManager.Enter(Id, Ctx.Params);   // 整场重建(最干净的重置)

            red.FadeStep(dt);
        }

        // ---------- UI ----------
        public override void DrawSidePanel(Rect r)
        {
            if (blue == null || red == null) return;
            float y = r.y;

            GUI.Label(new Rect(r.x, y, r.width, 20), "对抗参数", PanelKit.Header);
            y += 24;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"蓝方追击速度 {blueSpeed:0} m/s", PanelKit.Mini);
            y += 16;
            blueSpeed = GUI.HorizontalSlider(new Rect(r.x, y, r.width, 14), blueSpeed, 16f, 28f);
            y += 22;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"红方巡航速度 {redCruise:0} m/s", PanelKit.Mini);
            y += 16;
            redCruise = GUI.HorizontalSlider(new Rect(r.x, y, r.width, 14), redCruise, 10f, 22f);
            y += 22;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"红方冲刺速度 {redDash:0} m/s", PanelKit.Mini);
            y += 16;
            redDash = GUI.HorizontalSlider(new Rect(r.x, y, r.width, 14), redDash, 14f, 26f);
            y += 22;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"锁定充能速率 {lockRate:0.00} /s", PanelKit.Mini);
            y += 16;
            lockRate = GUI.HorizontalSlider(new Rect(r.x, y, r.width, 14), lockRate, 0.08f, 0.35f);
            y += 22;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"红方警觉阈值 {(evadeLevel * 100f):0}%", PanelKit.Mini);
            y += 16;
            evadeLevel = GUI.HorizontalSlider(new Rect(r.x, y, r.width, 14), evadeLevel, 0.1f, 0.9f);
            y += 26;
            ApplyKnobs();

            float w2 = (r.width - 6f) / 2f;
            if (PanelKit.Btn(r.x, y, w2, 24, "开始对抗", Outcome == "进行中" && red.Phase == RedPhase.Standby))
                StartCombat();
            if (PanelKit.Btn(r.x + w2 + 6f, y, w2, 24, "重置", true))
                ModeManager.Enter(Id, Ctx.Params);
            y += 30;

            var lockCol = blue.Locked ? Color.red : (blue.Lock01 > 0.3f ? new Color(1f, 0.7f, 0.3f) : Color.white);
            GUI.Label(new Rect(r.x, y, r.width, 16), $"锁定 {blue.Lock01 * 100f:0}%   距离 {blue.Range:0} m", PanelKit.Small);
            y += 16;
            var prev = GUI.color; GUI.color = lockCol;
            GUI.Label(new Rect(r.x, y, r.width, 16),
                $"红方阶段 {RedPhaseText()}   反制 {red.EvadeCount} 次", PanelKit.Small);
            GUI.color = prev;
            y += 16;
            var outCol = Outcome == "拦截成功" ? new Color(0.4f, 1f, 0.5f)
                : Outcome == "红方逃逸" ? Color.red : Color.white;
            GUI.color = outCol;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"结局:{Outcome}", PanelKit.Small);
            GUI.color = prev;
            y += 24;

            GUI.Label(new Rect(r.x, y, r.width, r.y + r.height - y),
                "B 开始对抗 | R 重置;\n蓝方自动追击锁定,红方感知锁定即折转反制;\n进度圈满=锁定,近距命中迫降。",
                PanelKit.Mini);
        }

        string RedPhaseText() => red.Phase switch
        {
            RedPhase.Standby => "待命",
            RedPhase.Infiltrate => "渗透中",
            RedPhase.Mission => "核心区侦察",
            RedPhase.Egress => "撤离突围",
            RedPhase.Escaped => "已逃逸",
            RedPhase.Hit => "被击落",
            _ => "-"
        };

        public override void DrawHint(StringBuilder sb)
        {
            sb.Append("B 开始 | R 重置 | 侧板:攻防参数滑杆");
        }

        public override void WriteMetrics(StringBuilder sb)
        {
            if (blue == null || red == null) return;
            sb.AppendLine($"combat=outcome:{Outcome} blueSpd:{blueSpeed:0} redSpd:{redCruise:0}/{redDash:0} lockRate:{lockRate:0.00}");
            sb.AppendLine($"lock=max:{blue.MaxLock:0.00} now:{blue.Lock01:0.00} rate:{blue.LockRate:0.00} rangeMin:{blue.MinRange:0.0}m rangeNow:{blue.Range:0}m");
            sb.AppendLine($"red=phase:{red.Phase} distCore:{red.DistanceToCore:0}m breached:{red.BreachedZone} dwellDone:{red.DwellDone} evades:{red.EvadeCount}");
            sb.AppendLine($"blue=engaged:{blue.Engaged} hitAt:{(blue.HitAt < 0f ? -1f : blue.HitAt):0.0}s fps:{fps:0}");
        }

        // ---------- 无头剧本:双结局 ----------
        public override void RunHeadlessScenario(string name)
        {
            var sc = ScenarioRunner.I;
            if (sc == null || blue == null || red == null) return;

            if (name == "escape")
            {
                // 逃逸局:红方"侦察完成、撤离途中"开局 —— 贴近核心全速突围,
                // 蓝方紧急追击但速度差 6m/s 拉不开锁定圈;红方警觉极早(15% 即折转),
                // 迎头充能窗口撑不满锁 → 突出防御圈
                blueSpeed = 18f; redCruise = 21f; redDash = 24f;
                lockRate = 0.10f; evadeLevel = 0.15f;
                blue.LockRange = 42f;
                red.MissionDwell = 3f;
                red.EvadeDuration = 3.2f; red.EvadeCooldown = 1.2f;   // 高频反制:甩开锁定圈
                ApplyKnobs();
                // 红方移防至核心区边缘,朝撤离方向直接突围
                red.Body.Teleport(new Vector3(28f, 24f, 58f), 222f);
                red.BeginEgress();
                sc.At(0.5f, blue.BeginIntercept);

                sc.At(5f, () => HeadlessAssert.Check(blue.Engaged && blue.Range < 90f,
                    $"5s 紧急追击(距离 {blue.Range:0}m 锁定 {blue.Lock01 * 100f:0}%)"));
                sc.At(14f, () => HeadlessAssert.Check(red.EvadeCount >= 1 || blue.Range > 55f,
                    $"14s 红方反制/拉开(折转 {red.EvadeCount} 次 距离 {blue.Range:0}m 最大锁定 {blue.MaxLock * 100f:0}%)"));
                sc.At(40f, () => HeadlessAssert.Check(red.Phase == RedPhase.Escaped && blue.MaxLock < 1f,
                    $"40s 红方突围逃逸(阶段 {red.Phase} 最大锁定 {blue.MaxLock * 100f:0}%)"));
                return;
            }

            // 默认拦截局:蓝方快+充能快+红方迟钝(反制短、恢复长 → 大部分时间
            // 在飞任务航线,蓝方满锁后冲顶命中迫降)
            blueSpeed = 25f; redCruise = 14f; redDash = 17f;
            lockRate = 0.24f; evadeLevel = 0.8f;
            blue.LockRange = 55f;
            red.MissionDwell = 6f;
            red.EvadeDuration = 2.2f; red.EvadeCooldown = 3f;
            ApplyKnobs();
            sc.At(0.5f, StartCombat);

            sc.At(5f, () => HeadlessAssert.Check(blue.Engaged && blue.Range < 130f,
                $"5s 接敌追击(距离 {blue.Range:0}m)"));
            sc.At(9f, () => HeadlessAssert.Check(blue.MaxLock > 0.3f && blue.MaxLock <= 1f,
                $"9s 锁定充能中(最大 {blue.MaxLock * 100f:0}%)"));
            sc.At(20f, () => HeadlessAssert.Check(blue.HitAt > 0f,
                $"20s 拦截命中(命中时刻 {blue.HitAt:0.0}s 最大锁定 {blue.MaxLock * 100f:0}%)"));
            sc.At(26f, () => HeadlessAssert.Check(red.Phase == RedPhase.Hit && blue.MaxLock >= 0.999f,
                $"26s 红方迫降坠落(阶段 {red.Phase})"));
        }
    }
}

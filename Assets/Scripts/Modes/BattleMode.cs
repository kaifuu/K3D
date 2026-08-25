using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 模块11 要地联合防御战(V4 部队打仗视角):
    /// 三波敌机多方向突防核心要地,守方两座自动炮塔 + 指挥员三大技能
    /// (拦截弹幕/EMP 冻结/炮塔超载);基地完整度、战果统计、胜负判定全流程。
    /// 无头剧本:波次推进/炮塔击杀/技能效果/漏防掉血/胜利条件逐项断言。
    /// </summary>
    public class BattleMode : DrillMode
    {
        public override string Id => "battle";
        public override string Title => "要地联合防御战";
        public override string Brief =>
            "部队视角防御战:三波敌机多方向突防,自动炮塔拦截 + 指挥技能(弹幕/EMP/超载),守卫基地完整度,战报统计胜负。";

        public static BattleMode I { get; private set; }

        public readonly List<BattleRaider> Raiders = new List<BattleRaider>();
        public readonly List<BattleTurret> Turrets = new List<BattleTurret>();

        Transform core;
        ChaseCamera chase;
        public int BaseHp = 100;
        public int WaveIndex;               // 已开波的波次号(1 起)
        public int Kills, Leaked, Shots, Hits;
        public int BarrageUsed, EmpUsed, OverdriveUsed;
        public bool Started, WavesDone;
        public string Verdict => BaseHp <= 0 ? "基地失守"
            : WavesDone && AliveCount == 0 && Started ? "防御成功" : "交战中";
        public int AliveCount
        {
            get { int n = 0; for (int i = 0; i < Raiders.Count; i++) if (Raiders[i] != null && Raiders[i].Alive) n++; return n; }
        }

        // 三技能冷却
        float barrageCd, empCd, overdriveCd;
        const float BarrageCD = 16f, EmpCD = 26f, OverdriveCD = 40f;
        public float BarrageCd01 => 1f - Mathf.Clamp01(barrageCd / BarrageCD);
        public float EmpCd01 => 1f - Mathf.Clamp01(empCd / EmpCD);
        public float OverdriveCd01 => 1f - Mathf.Clamp01(overdriveCd / OverdriveCD);

        // 波次定义:时间(战斗起)/数量/HP/速度/方位角集
        static readonly (float t, int n, int hp, float spd, float[] bearings)[] waves =
        {
            (2f,  6, 3, 11f,  new[] { 15f, 75f, 150f, 210f, 285f, 345f }),
            (20f, 8, 3, 12.5f, new[] { 0f, 45f, 95f, 140f, 190f, 235f, 290f, 330f }),
            (38f, 10, 4, 14f, new[] { 10f, 50f, 85f, 125f, 165f, 205f, 245f, 280f, 320f, 350f }),
        };

        float battleT = -1f;
        float fps = 60f;

        public override void Build()
        {
            I = this;
            EnvironmentBuilder.ResetToDayDefault();
            EnvironmentBuilder.BuildLighting(Root);
            EnvironmentBuilder.CreateGround(Root);
            EnvironmentBuilder.MakeRing(Root, 130f, new Color(0.3f, 0.5f, 0.6f, 0.22f), "FieldBound", 0.05f);

            // ---- 要地:核心设施 + 防御工事环 + 防区识别环 ----
            core = EnvironmentBuilder.BuildCoreFacility(Root);
            CityKit.DefenseWorks(Root, 34f);
            EnvironmentBuilder.MakeRing(Root, 12f, new Color(0.3f, 0.85f, 1f, 0.4f), "KeepRing", 0.06f);

            // ---- 防御炮塔 ×2(东南/西北对角,火力覆盖交错) ----
            BuildTurret(new Vector3(22f, 0f, 14f), new Color(0.25f, 0.55f, 0.9f));
            BuildTurret(new Vector3(-22f, 0f, -14f), new Color(0.25f, 0.55f, 0.9f));

            // ---- 相机:核心上空战术俯瞰 ----
            var cam = CameraDirector.CreateCamera(Root);
            chase = CameraDirector.Follow(cam, core, 66f);
            chase.SetPitch(52f);
            Ctx.MainCamera = cam;

            BattleRaider.OnKilled += OnRaiderKilled;
            BattleRaider.OnReached += OnRaiderReached;
        }

        void BuildTurret(Vector3 pos, Color accent)
        {
            var go = new GameObject("DefTurret");
            go.transform.SetParent(Root, false);
            go.transform.position = pos;

            var base_ = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCol(base_);
            base_.name = "Base";
            base_.transform.SetParent(go.transform, false);
            base_.transform.localScale = new Vector3(1.6f, 0.5f, 1.6f);
            base_.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            base_.GetComponent<Renderer>().material = MaterialLib.Metal(new Color(0.24f, 0.28f, 0.33f), 2f);

            var head = new GameObject("Head");
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.35f, 0f);

            var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(housing);
            housing.name = "Housing";
            housing.transform.SetParent(head.transform, false);
            housing.transform.localScale = new Vector3(1.0f, 0.7f, 1.3f);
            housing.GetComponent<Renderer>().material = MaterialLib.Metal(accent, 2f);

            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCol(barrel);
            barrel.name = "Barrel";
            barrel.transform.SetParent(head.transform, false);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.12f, 1.8f, 0.12f);
            barrel.transform.localPosition = new Vector3(0f, 0.12f, 1.1f);
            barrel.GetComponent<Renderer>().material = MaterialLib.Metal(new Color(0.12f, 0.12f, 0.14f), 2f);

            var t = go.AddComponent<BattleTurret>();
            t.Head = head.transform;
            Turrets.Add(t);
        }

        static void DestroyCol(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }

        // ---------- 战斗流程 ----------

        public void StartBattle()
        {
            if (Started) return;
            Started = true;
            battleT = 0f;
            EventBus.Publish("战报", "指挥所", "敌机编队逼近,要地防御战打响!三级战备转一级", EventGrade.Critical);
        }

        void SpawnWave(int w)
        {
            WaveIndex = w + 1;
            var wave = waves[w];
            for (int i = 0; i < wave.n; i++)
            {
                float a = wave.bearings[i % wave.bearings.Length] * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Cos(a) * 120f, 16f + (i % 4) * 3.5f, Mathf.Sin(a) * 120f);
                var go = DroneFactory.Spawn(DroneRole.Red, Root, pos, $"Raider{w}_{i}");
                go.transform.localScale = Vector3.one * 0.8f;
                var r = go.AddComponent<BattleRaider>();
                r.HP = wave.hp;
                r.Speed = wave.spd;
                r.Altitude = pos.y;
                r.WaveIndex = w + 1;
                Raiders.Add(r);
            }
            EventBus.Publish("战报", "雷达站", $"第 {WaveIndex} 波来袭:{wave.n} 架敌机多方向突防", EventGrade.Warn);
        }

        void OnRaiderKilled(BattleRaider r)
        {
            Kills++;
        }

        void OnRaiderReached(BattleRaider r)
        {
            Leaked++;
            BaseHp = Mathf.Max(0, BaseHp - 9);
            if (chase != null) chase.Shake(0.35f, 0.6f);
            EventBus.Publish("战报", "要地", $"敌机突防命中!基地完整度降至 {BaseHp}%", EventGrade.Critical, r.transform.position);
            if (BaseHp <= 0)
                EventBus.Publish("战报", "指挥所", "基地失守……防御失败", EventGrade.Critical);
        }

        // ---------- 指挥技能 ----------

        /// <summary>拦截弹幕:核心 75m 内全部敌机受 3 点伤害</summary>
        public void FireBarrage()
        {
            if (barrageCd > 0f || !Started) return;
            barrageCd = BarrageCD;
            BarrageUsed++;
            int hit = 0;
            for (int i = Raiders.Count - 1; i >= 0; i--)
            {
                var r = Raiders[i];
                if (r == null || !r.Alive) continue;
                if (Vector3.Distance(r.transform.position, core.position) < 75f)
                {
                    hit++;
                    r.TakeDamage(3, core.position);
                }
            }
            FXManager.I?.Explode(core.position + Vector3.up * 6f, 1);
            EventBus.Publish("战报", "指挥部", $"拦截弹幕覆盖齐射!命中 {hit} 个目标", EventGrade.Op);
        }

        /// <summary>EMP 冲击:全部敌机冻结 3 秒</summary>
        public void FireEmp()
        {
            if (empCd > 0f || !Started) return;
            empCd = EmpCD;
            EmpUsed++;
            int hit = 0;
            for (int i = 0; i < Raiders.Count; i++)
            {
                var r = Raiders[i];
                if (r == null || !r.Alive) continue;
                r.Freeze(3f);
                hit++;
            }
            RingFX(core.position, 70f, new Color(0.4f, 0.8f, 1f, 0.5f), 0.8f);
            EventBus.Publish("战报", "指挥部", $"EMP 冲击释放!瘫痪 {hit} 架敌机 3 秒", EventGrade.Op);
        }

        /// <summary>炮塔超载:射程 ×1.7、射速 ×2,持续 8 秒</summary>
        public void FireOverdrive()
        {
            if (overdriveCd > 0f || !Started) return;
            overdriveCd = OverdriveCD;
            OverdriveUsed++;
            for (int i = 0; i < Turrets.Count; i++)
                if (Turrets[i] != null) Turrets[i].Overdrive = 8f;
            EventBus.Publish("战报", "指挥部", "炮塔火力超载!8 秒全功率输出", EventGrade.Op);
        }

        /// <summary>扩张冲击环(EMP/技能可视化)</summary>
        void RingFX(Vector3 center, float radius, Color c, float life)
        {
            var go = new GameObject("AbilityRing");
            go.transform.SetParent(Root, false);
            go.transform.position = center + Vector3.up * 0.5f;
            var lr = go.AddComponent<LineRenderer>();
            lr.loop = true;
            lr.positionCount = 48;
            for (int i = 0; i < 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * 3f, 0f, Mathf.Sin(a) * 3f));
            }
            lr.startWidth = 0.5f;
            lr.endWidth = 0.5f;
            lr.material = EnvironmentBuilder.UnlitMat(c);
            go.AddComponent<ExpandRing>().Init(radius, life);
        }

        class ExpandRing : MonoBehaviour
        {
            float radius, life, t;
            LineRenderer lr;
            public void Init(float r, float l) { radius = r; life = l; lr = GetComponent<LineRenderer>(); }
            void Update()
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / life);
                float rad = Mathf.Lerp(3f, radius, k);
                if (lr != null)
                {
                    for (int i = 0; i < lr.positionCount; i++)
                    {
                        float a = i / (float)lr.positionCount * Mathf.PI * 2f;
                        lr.SetPosition(i, new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad));
                    }
                    lr.startWidth = lr.endWidth = 0.5f * (1f - k);
                }
                if (t >= life) Destroy(gameObject);
            }
        }

        // ---------- 帧循环 ----------

        public override void OnTick(float dt)
        {
            if (core == null) return;
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.06f);

            if (Input.GetKeyDown(KeyCode.B)) StartBattle();
            if (Input.GetKeyDown(KeyCode.F)) FireBarrage();
            if (Input.GetKeyDown(KeyCode.E)) FireEmp();
            if (Input.GetKeyDown(KeyCode.G)) FireOverdrive();
            if (Input.GetKeyDown(KeyCode.R)) ModeManager.Enter(Id, Ctx.Params);

            // 冷却推进(演练时间)
            if (DrillClock.CanSimulate)
            {
                barrageCd = Mathf.Max(0f, barrageCd - dt);
                empCd = Mathf.Max(0f, empCd - dt);
                overdriveCd = Mathf.Max(0f, overdriveCd - dt);
            }

            // 波次调度(演练时间驱动,无头确定)
            if (Started)
            {
                if (DrillClock.CanSimulate) battleT += dt;
                for (int w = 0; w < waves.Length; w++)
                    if (battleT >= waves[w].t && WaveIndex <= w) SpawnWave(w);
                if (WaveIndex >= waves.Length && AliveCount == 0)
                {
                    if (!WavesDone)
                    {
                        WavesDone = true;
                        EventBus.Publish("战报", "指挥所", BaseHp > 0
                            ? $"防御成功!击落 {Kills} 架、漏防 {Leaked} 架,基地完整度 {BaseHp}%"
                            : "基地失守,防御失败", EventGrade.Op);
                    }
                }
            }

            // 清理已毁目标引用
            for (int i = Raiders.Count - 1; i >= 0; i--)
                if (Raiders[i] == null) Raiders.RemoveAt(i);

            // 统计炮塔射击
            Shots = 0; Hits = 0;
            for (int i = 0; i < Turrets.Count; i++)
                if (Turrets[i] != null) { Shots += Turrets[i].Shots; Hits += Turrets[i].Hits; }

            // 悬浮标注
            var cam = Ctx.MainCamera;
            if (cam != null)
            {
                Overlay.Label(core.position + Vector3.up * 16f,
                    $"要地完整度 {BaseHp}%   敌机 {AliveCount}   第 {WaveIndex} 波", new Color(1f, 0.75f, 0.35f));
            }
        }

        // ---------- UI ----------

        public override void DrawSidePanel(Rect r)
        {
            float y = r.y;
            float w2 = (r.width - 6f) / 2f;

            GUI.Label(new Rect(r.x, y, r.width, 20), "要地防御战", PanelKit.Header);
            y += 24;

            // 基地血条
            var hpCol = BaseHp > 60 ? new Color(0.4f, 0.9f, 0.5f)
                : BaseHp > 30 ? new Color(1f, 0.75f, 0.3f) : Color.red;
            var prev = GUI.color; GUI.color = hpCol;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"基地完整度 {BaseHp}%", PanelKit.Small);
            GUI.color = prev;
            y += 16;
            GUI.Box(new Rect(r.x, y, r.width * (BaseHp / 100f), 12), "");
            GUI.Box(new Rect(r.x, y, r.width, 12), "");
            y += 20;

            GUI.Label(new Rect(r.x, y, r.width, 16),
                Started ? $"交战中   第 {WaveIndex}/{waves.Length} 波   敌机 {AliveCount}" : "待命(B 开战)", PanelKit.Small);
            y += 22;

            // 指挥技能
            GUI.Label(new Rect(r.x, y, r.width, 20), "指挥技能", PanelKit.Header);
            y += 22;
            AbilityBtn(r.x, y, w2, "拦截弹幕 (F)", BarrageCd01, FireBarrage, "核心 75m 齐射·3 伤害");
            AbilityBtn(r.x + w2 + 6f, y, w2, "EMP 冲击 (E)", EmpCd01, FireEmp, "全部敌机冻结 3s");
            y += 34;
            AbilityBtn(r.x, y, w2, "炮塔超载 (G)", OverdriveCd01, FireOverdrive, "射程×1.7 射速×2·8s");
            if (PanelKit.Btn(r.x + w2 + 6f, y, w2, 24, "重置 (R)", true))
                ModeManager.Enter(Id, Ctx.Params);
            y += 32;

            // 战报
            GUI.Label(new Rect(r.x, y, r.width, 20), "战果统计", PanelKit.Header);
            y += 22;
            int sh = Mathf.Max(1, Shots);
            GUI.Label(new Rect(r.x, y, r.width, 16),
                $"击落 {Kills}   漏防 {Leaked}   命中率 {Hits * 100 / sh}%", PanelKit.Small);
            y += 16;
            GUI.Label(new Rect(r.x, y, r.width, 16),
                $"弹幕 {BarrageUsed}  EMP {EmpUsed}  超载 {OverdriveUsed}", PanelKit.Small);
            y += 16;
            var vCol = Verdict == "防御成功" ? new Color(0.4f, 1f, 0.5f)
                : Verdict == "基地失守" ? Color.red : Color.white;
            prev = GUI.color; GUI.color = vCol;
            GUI.Label(new Rect(r.x, y, r.width, 16), $"战况:{Verdict}", PanelKit.Small);
            GUI.color = prev;
            y += 24;

            GUI.Label(new Rect(r.x, y, r.width, r.y + r.height - y),
                "B 开战 | F 弹幕 | E EMP | G 超载 | R 重置;\n炮塔自动索敌,敌机抵近核心即造成损毁;\n三波敌机全部肃清且基地未破 = 防御成功。", PanelKit.Mini);
        }

        void AbilityBtn(float x, float y, float w, string label, float cd01, System.Action fire, string tip)
        {
            bool ready = cd01 >= 1f && Started;
            if (PanelKit.Btn(x, y, w, 24, label, ready)) fire();
            if (cd01 < 1f)
            {
                // 冷却遮罩 + 剩余秒
                GUI.Box(new Rect(x, y, w * cd01, 24), "");
                GUI.Label(new Rect(x + 4f, y + 3f, w, 18), $"{(1f - cd01) * 100f:0}%", PanelKit.Mini);
            }
            else
                GUI.Label(new Rect(x, y + 24, w, 14), tip, PanelKit.Mini);
        }

        public override void DrawHint(StringBuilder sb)
        {
            sb.Append("B 开战 | F 弹幕 | E EMP | G 超载 | R 重置 | 守住要地!");
        }

        public override void WriteMetrics(StringBuilder sb)
        {
            if (core == null) return;
            int sh = Mathf.Max(1, Shots);
            sb.AppendLine($"battle=started:{Started} wave:{WaveIndex}/{waves.Length} alive:{AliveCount} baseHp:{BaseHp} verdict:{Verdict} battleT:{(battleT < 0f ? -1f : battleT):0.0}s");
            sb.AppendLine($"stats=kills:{Kills} leaked:{Leaked} shots:{Shots} hits:{Hits} acc:{Hits * 100 / sh}% fps:{fps:0}");
            sb.AppendLine($"abilities=barrage:{BarrageUsed} emp:{EmpUsed} overdrive:{OverdriveUsed}");
        }

        public override void OnStop()
        {
            if (I == this) I = null;
            BattleRaider.OnKilled -= OnRaiderKilled;
            BattleRaider.OnReached -= OnRaiderReached;
            TracerFX.ClearAll();
        }

        // ---------- 无头剧本:波次/技能/漏防/胜负全流程 ----------
        public override void RunHeadlessScenario(string name)
        {
            var sc = ScenarioRunner.I;
            if (sc == null || core == null) return;

            // -scenario=city:纯取景变体 —— 不开局,固定机位同框拍 防御工事环 + 城市街区天际线
            if (name == "city")
            {
                if (chase != null) chase.enabled = false;
                var cam = Ctx.MainCamera;
                if (cam != null)
                {
                    // 沿街峡谷机位:道路在地块间隙 —— local x/z = 0/±30(±15/±45 是楼栋中轴),
                    // x=0 即南北向主路,16m 高度向北平视,两侧 14~40m 塔楼夹出一点透视;
                    // FOV 45 更接近实拍透视
                    cam.fieldOfView = 45f;
                    cam.transform.position = new Vector3(0f, 16f, 118f);
                    cam.transform.rotation = Quaternion.LookRotation(
                        new Vector3(0f, 11f, 245f) - cam.transform.position, Vector3.up);
                }
                sc.At(4f, () =>
                {
                    var district = GameObject.Find("CityDistrict");
                    int buildings = 0;
                    if (district != null)
                        foreach (var t in district.GetComponentsInChildren<Transform>())
                            if (t.name.StartsWith("Building"))
                            {
                                if (buildings < 3)
                                    Debug.Log($"[V5] 楼栋 {t.name} pos={t.position:F1} lossy={t.lossyScale:F1} active={t.gameObject.activeInHierarchy}");
                                buildings++;
                            }
                    HeadlessAssert.Check(district != null && buildings > 0,
                        $"city 楼栋 {buildings} 栋(街区子对象 {district?.transform.childCount ?? 0})");
                });
                return;
            }

            sc.At(0.5f, StartBattle);

            sc.At(6f, () => HeadlessAssert.Check(WaveIndex == 1 && AliveCount >= 5,
                $"6s 第一波来袭(敌机 {AliveCount} 架)"));
            sc.At(11f, () => HeadlessAssert.Check(Kills >= 1,
                $"11s 炮塔拦截开火(累计击落 {Kills})"));
            sc.At(13f, FireBarrage);
            sc.At(14f, () => HeadlessAssert.Check(Kills >= 3,
                $"14s 弹幕齐射生效(累计击落 {Kills})"));
            sc.At(26f, () => HeadlessAssert.Check(WaveIndex == 2 && AliveCount >= 4,
                $"26s 第二波来袭(敌机 {AliveCount})"));
            sc.At(26.5f, () =>
            {
                FireEmp();
                int alive = AliveCount;
                int frozen = 0;
                for (int i = 0; i < Raiders.Count; i++)
                    if (Raiders[i] != null && Raiders[i].Alive && Raiders[i].Frozen > 0f) frozen++;
                HeadlessAssert.Check(alive > 0 && frozen == alive,
                    $"26.5s EMP 冻结全部敌机({frozen}/{alive})");
            });
            sc.At(50.5f, () => HeadlessAssert.Check(BaseHp < 100,
                $"50.5s 有敌机突防命中要地(完整度 {BaseHp}%,漏防 {Leaked})"));
            sc.At(44f, () => HeadlessAssert.Check(WaveIndex == 3,
                $"44s 第三波来袭(敌机 {AliveCount})"));
            sc.At(46f, FireOverdrive);
            sc.At(46.5f, () => HeadlessAssert.Check(
                Turrets.Count > 0 && Turrets[0] != null && Turrets[0].EffRange > 60f,
                $"46.5s 炮塔超载生效(射程 {Turrets[0].EffRange:0}m)"));
            sc.At(50f, FireBarrage);
            sc.At(62f, () => HeadlessAssert.Check(WavesDone && AliveCount == 0 && BaseHp > 0,
                $"62s 防御成功(击落 {Kills} 漏防 {Leaked} 完整度 {BaseHp}%)"));
        }
    }
}

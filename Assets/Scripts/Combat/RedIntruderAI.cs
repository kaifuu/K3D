using UnityEngine;

namespace DroneSim
{
    public enum RedPhase { Standby, Infiltrate, Mission, Egress, Escaped, Hit }

    /// <summary>
    /// 红方入侵机(FlightBody 动力学):渗透 → 核心区盘旋侦察 → 撤离突围;
    /// 被蓝方锁定超过警觉阈值时立即反制 —— 垂直折转 + 降高至杂波层 + 全速蛇形,
    /// 摆脱后恢复任务航线。被命中则侧旋坠落(机体停驱,手工运动学)迫降淡出。
    /// </summary>
    public class RedIntruderAI : MonoBehaviour
    {
        public FlightBody Body;
        public BlueInterceptor Hunter;          // 锁定状态来源(可空:无蓝方时纯任务飞行)
        public float CruiseSpeed = 15f;         // 任务段巡航
        public float DashSpeed = 21f;           // 反制冲刺
        public float EvadeLockLevel = 0.4f;     // 锁定超过此值 → 反制
        public float EvadeDuration = 2.2f;      // 单次反制机动时长 s
        public float EvadeCooldown = 3f;        // 反制后恢复任务时长 s(期间可被追上)
        public float MissionDwell = 5f;         // 核心区滞留侦察时长 s
        public float MissionRadius = 18f;
        public float MissionAlt = 21f;
        public float EvadeAlt = 12f;            // 反制突降高度(杂波层)

        public RedPhase Phase { get; private set; } = RedPhase.Standby;
        public bool Evading { get; private set; }
        public int EvadeCount { get; private set; }
        public bool BreachedZone { get; private set; }       // 曾进入核心防御区
        public bool DwellDone { get; private set; }
        public float DistanceToCore => new Vector2(transform.position.x, transform.position.z).magnitude;

        Vector3 egressPoint = new Vector3(-95f, 26f, -80f);
        Vector3 evadeDir = Vector3.right;
        float dwell, evadeTimer, evadeCooldown, fadeT = -1f;
        TrailRenderer trail;
        Renderer bodyRenderer;

        void OnEnable()
        {
            var tr = transform.Find("Trail");
            if (tr != null) trail = tr.GetComponent<TrailRenderer>();
            var bd = transform.Find("Body");
            if (bd != null) bodyRenderer = bd.GetComponent<Renderer>();
        }

        public void Begin()
        {
            if (Phase != RedPhase.Standby) return;
            Phase = RedPhase.Infiltrate;
            EventBus.Publish("对抗", name, "红方入侵机出现,向核心设施渗透", EventGrade.Warn);
        }

        /// <summary>逃逸局专用:侦察已完成、撤离途中遭遇拦截(跳过渗透直接突围)</summary>
        public void BeginEgress()
        {
            if (Phase != RedPhase.Standby) return;
            DwellDone = true;
            BreachedZone = true;
            Phase = RedPhase.Egress;
            EventBus.Publish("对抗", name, "红方完成侦察转入撤离,蓝方紧急追击", EventGrade.Warn);
        }

        /// <summary>蓝方命中:停驱 + 侧旋坠落 + 着地爆闪 + 缩体淡出</summary>
        public void OnHit()
        {
            if (Phase == RedPhase.Hit || Phase == RedPhase.Escaped) return;
            Phase = RedPhase.Hit;
            Evading = false;
            if (Body != null) Body.enabled = false;      // 停动力学,改手工运动学
            if (trail != null) trail.material.color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
        }

        void Update()
        {
            if (!DrillClock.CanSimulate) return;
            float dt = Time.deltaTime;

            if (Phase == RedPhase.Hit) { UpdateFall(dt); return; }
            if (Phase == RedPhase.Standby || Phase == RedPhase.Escaped) return;

            // ---- 反制判定(覆盖任务航线的机动层) ----
            UpdateEvade(dt);

            if (Evading) SteerEvade();
            else switch (Phase)
            {
                case RedPhase.Infiltrate: UpdateInfiltrate(dt); break;
                case RedPhase.Mission: UpdateMission(dt); break;
                case RedPhase.Egress: UpdateEgress(dt); break;
            }
        }

        // ---------- 任务段 ----------

        void UpdateInfiltrate(float dt)
        {
            var aim = new Vector3(0f, MissionAlt, MissionRadius + 8f);
            Steer(aim, CruiseSpeed);
            if (DistanceToCore < MissionRadius + 6f)
            {
                Phase = RedPhase.Mission;
                EventBus.Publish("对抗", name, "红方抵达核心区,开始盘旋侦察", EventGrade.Warn);
            }
        }

        void UpdateMission(float dt)
        {
            // 绕核心圆弧前进(切向追踪,不瞄圆心)
            float omega = (CruiseSpeed / MissionRadius) * dt;
            float a = Mathf.Atan2(transform.position.z, transform.position.x) + omega;
            var aim = new Vector3(Mathf.Cos(a) * MissionRadius, MissionAlt, Mathf.Sin(a) * MissionRadius);
            Steer(aim, CruiseSpeed);

            if (!BreachedZone && DistanceToCore < 29f)
            {
                BreachedZone = true;
                EventBus.Publish("对抗", name, "红方闯入核心防御区!", EventGrade.Critical);
            }
            if (!DwellDone)
            {
                dwell += dt;
                if (dwell >= MissionDwell)
                {
                    DwellDone = true;
                    EventBus.Publish("对抗", name, "红方完成核心区侦察拍摄,转入撤离", EventGrade.Critical);
                }
            }
            if (DwellDone)
            {
                Phase = RedPhase.Egress;
                if (trail != null) trail.material.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            }
        }

        void UpdateEgress(float dt)
        {
            Steer(egressPoint, DashSpeed);
            if (DistanceToCore > 115f)
            {
                Phase = RedPhase.Escaped;
                EventBus.Publish("对抗", name, "红方突出防御圈逃逸,对抗结束", EventGrade.Critical);
            }
        }

        // ---------- 反制机动 ----------

        void UpdateEvade(float dt)
        {
            float lock01 = Hunter != null ? Hunter.Lock01 : 0f;

            if (!Evading && evadeCooldown <= 0f && lock01 > EvadeLockLevel)
            {
                Evading = true;
                EvadeCount++;
                evadeTimer = EvadeDuration;
                // 折转方向 = 垂直视线分量(打破尾追锥角)+ 远离蓝方分量(扩展距离)。
                // 纯垂直折转不改变距离:追击方机头跟得上侧移,锥角不破坏、充能不断
                var toRed = transform.position - Hunter.transform.position; toRed.y = 0f;
                toRed.Normalize();
                var perp = new Vector3(-toRed.z, 0f, toRed.x);
                float side = 1f;
                var hv = Hunter.Body != null ? Hunter.Body.Velocity : Vector3.zero; hv.y = 0f;
                if (Vector3.Dot(perp, hv) > 0f) side = -1f;
                evadeDir = (perp * side * 0.7f + toRed * 0.7f).normalized;
                EventBus.Publish("对抗", name, $"红方感知锁定,折转反制(第 {EvadeCount} 次)", EventGrade.Op);
            }

            if (Evading)
            {
                // 限时机动:到时即恢复任务航线(不依赖 lock 回落 —— 蓝方咬得紧时
                // 锁定度不回落,旧条件会让红方无限折转、永远到不了任务区)
                evadeTimer -= dt;
                if (evadeTimer <= 0f)
                {
                    Evading = false;
                    evadeCooldown = EvadeCooldown;
                }
            }
            else evadeCooldown -= dt;
        }

        void SteerEvade()
        {
            // 折转方向 + 蛇形 + 突降杂波层
            var jink = Vector3.Cross(Vector3.up, evadeDir) * (Mathf.Sin(DrillClock.SimTime * 2.4f) * 10f);
            var aim = transform.position + evadeDir * 40f + jink + Vector3.down * 6f;
            aim.y = EvadeAlt;
            Steer(aim, DashSpeed);
        }

        // ---------- 命中坠落(手工运动学:机体已停驱) ----------

        void UpdateFall(float dt)
        {
            transform.position += (Vector3.down * 9f + evadeDir * 2.5f) * dt;
            transform.Rotate(75f * dt, 40f * dt, 28f * dt);
            if (transform.position.y <= 0.6f)
            {
                transform.position = new Vector3(transform.position.x, 0.6f, transform.position.z);
                FXManager.I?.Explode(transform.position, 0);
                EventBus.Publish("对抗", name, "红方残骸迫降接地", EventGrade.Op);
                fadeT = 0f;
                enabled = false;      // 停坠落;缩体淡出由模式层 FadeStep 驱动(暂停安全)
            }
        }

        /// <summary>迫降淡出(CombatMode 每帧驱动,暂停安全):缩体 + 塌落</summary>
        public void FadeStep(float dt)
        {
            if (fadeT < 0f) return;
            fadeT += dt;
            float k = Mathf.Clamp01(fadeT / 1.2f);
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.05f, k);
            transform.position += Vector3.down * 0.15f * dt;
        }

        // ---------- 通用速度伺服转向 ----------

        void Steer(Vector3 target, float speed)
        {
            if (Body == null) return;
            var err = target - transform.position;
            var vh = Vector2.ClampMagnitude(new Vector2(err.x, err.z) * 0.6f, speed);
            float vy = Mathf.Clamp(err.y * 0.5f, -Body.MaxClimb * 0.8f, Body.MaxClimb * 0.8f);
            var cmd = FlightCommand.Idle;
            FlightMath.WorldVelToCmd(Body, new Vector3(vh.x, vy, vh.y), ref cmd);
            if (vh.sqrMagnitude > 1f)
            {
                float desired = Mathf.Atan2(vh.x, vh.y) * Mathf.Rad2Deg;
                cmd.YawRate = Mathf.Clamp(Mathf.DeltaAngle(Body.HeadingDeg, desired) / 60f, -1f, 1f);
            }
            cmd.Clamp();
            Body.Cmd = cmd;
        }
    }
}

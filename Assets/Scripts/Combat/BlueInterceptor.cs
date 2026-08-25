using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 蓝方拦截机(FlightBody 动力学):待命巡逻 → 接令追击(前置点截击,
    /// 纯追踪侧移目标会外螺旋)→ 锁定充能(距离+机头锥角几何加权,几何破坏则衰减)
    /// → 锁定完成冲顶命中 → 返航巡逻。全程暴露 Lock01 供红方反制与可视化。
    /// </summary>
    public class BlueInterceptor : MonoBehaviour
    {
        public FlightBody Body;
        public RedIntruderAI Red;
        public float CruiseSpeed = 24f;        // 追击速度
        public float PatrolSpeed = 10f;
        public float PatrolRadius = 38f;
        public float PatrolAlt = 24f;
        public float LockRange = 55f;          // 锁定充能最大距离
        public float LockConeDeg = 35f;        // 机头对目标锥角
        public float LockRate = 0.22f;         // 满几何充能速率(1/s)
        public float LockDecay = 0.15f;        // 几何破坏衰减速率
        public float HitRange = 9f;

        public float Lock01 { get; private set; }
        public bool Locked => Lock01 >= 1f;
        public bool Engaged { get; private set; }             // 已接令拦截
        public float Range => Red != null && Red.Phase != RedPhase.Escaped
            ? Vector3.Distance(transform.position, Red.transform.position) : 999f;
        public float MinRange { get; private set; } = 999f;
        public float MaxLock { get; private set; }
        public float HitAt { get; private set; } = -1f;       // 命中时刻(SimTime)
        public bool TargetAlive => Red != null && Red.Phase != RedPhase.Hit && Red.Phase != RedPhase.Escaped;

        float patrolA;

        public void BeginIntercept()
        {
            if (Engaged || !TargetAlive) return;
            Engaged = true;
            EventBus.Publish("对抗", name, "蓝方拦截机接令,前出追击", EventGrade.Op);
        }

        void Update()
        {
            if (Body == null || !DrillClock.CanSimulate) return;
            float dt = Time.deltaTime;

            if (!Engaged || !TargetAlive)
            {
                UpdatePatrol(dt);
                if (!TargetAlive && Engaged && HitAt < 0f && Red != null && Red.Phase == RedPhase.Escaped)
                    Engaged = false;   // 目标逃逸:解除追击回巡逻
                return;
            }

            UpdatePursuit(dt);
            UpdateLock(dt);
        }

        void UpdatePatrol(float dt)
        {
            patrolA += (PatrolSpeed / PatrolRadius) * dt;
            var aim = new Vector3(Mathf.Cos(patrolA) * PatrolRadius, PatrolAlt, Mathf.Sin(patrolA) * PatrolRadius);
            Steer(aim, PatrolSpeed);
            Lock01 = Mathf.Max(0f, Lock01 - LockDecay * dt);
        }

        void UpdatePursuit(float dt)
        {
            // 前置点截击:瞄准 红方位置+红方速度·τ(τ=接近时间),提前量封住侧移
            var rp = Red.transform.position;
            var rv = Red.Body != null ? Red.Body.Velocity : Vector3.zero;
            float range = Vector3.Distance(transform.position, rp);
            float tau = Mathf.Clamp(range / Mathf.Max(CruiseSpeed, 1f) * 0.8f, 0.3f, 2.2f);
            var aim = rp + rv * tau;
            Steer(aim, CruiseSpeed);

            MinRange = Mathf.Min(MinRange, range);

            // 锁定完成:冲顶至命中距离 → 击落
            if (Locked && range < HitRange)
            {
                HitAt = DrillClock.SimTime;
                Engaged = false;
                Lock01 = 0f;
                FXManager.I?.Explode(rp, 1);
                EventBus.Publish("对抗", name, "蓝方拦截命中!红方失控坠落", EventGrade.Critical);
                Red.OnHit();
            }
        }

        void UpdateLock(float dt)
        {
            float range = Range;
            bool redMoving = Red.Phase != RedPhase.Hit && Red.Phase != RedPhase.Escaped;
            if (!redMoving || range > LockRange)
            {
                Lock01 = Mathf.Max(0f, Lock01 - LockDecay * dt);
                return;
            }

            // 机头-视线锥角:追尾/迎头几何均可充能,侧偏衰减
            var toRed = Red.transform.position - transform.position; toRed.y = 0f;
            float bearing = Mathf.Atan2(toRed.x, toRed.z) * Mathf.Rad2Deg;
            float ang = Mathf.Abs(Mathf.DeltaAngle(Body.HeadingDeg, bearing));
            if (ang > LockConeDeg)
            {
                Lock01 = Mathf.Max(0f, Lock01 - LockDecay * dt);
                return;
            }

            float rangeQ = 1f - 0.45f * (range / LockRange);              // 越近越快
            float coneQ = 1f - 0.5f * (ang / LockConeDeg);                // 越正越快
            Lock01 = Mathf.Min(1f, Lock01 + LockRate * rangeQ * coneQ * dt);
            MaxLock = Mathf.Max(MaxLock, Lock01);
        }

        // ---------- 速度伺服转向(与红方同款) ----------

        void Steer(Vector3 target, float speed)
        {
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

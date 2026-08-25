using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 飞行体动力学(半运动学惯性模型,免调参稳定):
    /// 指令→偏航旋转到世界系目标速度(含风漂移)→ MoveTowards 惯性逼近→积分位置。
    /// 姿态为速度的视觉映射(前倾/侧倾/压坡度,MoveTowardsAngle 平滑回正),
    /// 旋翼转速=基速+空速+油门联动;阵风注入姿态抖动与位置漂移。
    /// 返航/避障/驱离/故障等外力统一走 AddImpulse / AddSustained。
    /// </summary>
    public class FlightBody : MonoBehaviour
    {
        [Header("性能参数")]
        public float MaxSpeed = 16f;        // 水平最大速度 m/s
        public float MaxClimb = 7f;         // 垂直最大速度 m/s
        public float MaxYawRate = 100f;     // 最大偏航角速度 °/s
        public float Accel = 9f;            // 加/减速度 m/s²
        public float YawAccel = 260f;       // 偏航角加速度 °/s²
        [Header("姿态表现")]
        public float MaxTilt = 24f;         // 速度映射最大倾角 °
        public float BankAngle = 10f;       // 转弯压坡度附加角 °
        public float TiltSpeed = 110f;      // 姿态回正速度 °/s
        public float TurbTilt = 0.5f;       // 每 m/s 阵风的姿态抖动 °
        [Header("环境响应")]
        [Range(0f, 1f)] public float WindResponse = 0.4f;  // 风对目标速度的注入比

        public FlightCommand Cmd;           // 每帧由指令源写入
        [Header("故障注入钩子(P9)")]
        public float YawBiasDeg;            // 偏航漂移 °/s(陀螺漂移)
        public float RollBiasDeg;           // 横滚偏置 °(电机失效侧倾)

        Vector3 velocity;
        public Vector3 Velocity => velocity;
        public float Yaw { get; private set; }             // 航向 °
        public float YawRateCur { get; private set; }      // °/s
        public bool Landed { get; private set; } = true;
        public float GroundY = 0.55f;

        // ---------- 遥测 ----------
        public float Altitude => transform.position.y - GroundY;
        public float Speed => Velocity.magnitude;
        public float HorizSpeed => new Vector2(Velocity.x, Velocity.z).magnitude;
        public float VertSpeed => Velocity.y;
        public float HeadingDeg => Yaw;
        public float PitchDeg { get; private set; }
        public float RollDeg { get; private set; }
        public float Rpm01 { get; private set; } = 0.1f;
        public float DistanceFlown { get; private set; }
        public float MaxAlt { get; private set; }
        public float MaxSpeedReached { get; private set; }

        public Vector3 HomePos;             // 归航点(Build 时由模式设置)

        RotorSpin rotor;
        readonly List<SustainedForce> forces = new List<SustainedForce>(4);

        struct SustainedForce { public Vector3 Rate; public float Until; }

        void Start() => CacheParts();
        void OnEnable()                      // 域重载防御:重新缓存引用;P10 起自动登记复盘跟踪
        {
            CacheParts();
            ReplayService.Track(this);
        }
        void OnDisable() => ReplayService.Untrack(this);

        void CacheParts()
        {
            if (rotor == null) rotor = GetComponent<RotorSpin>();
        }

        // ---------- 外力接口(返航/避障/驱离/故障统一入口) ----------
        public void AddImpulse(Vector3 v) => velocity += v;

        public void AddSustained(Vector3 rateMps2, float durationSec) =>
            forces.Add(new SustainedForce { Rate = rateMps2, Until = DrillClock.SimTime + durationSec });

        /// <summary>瞬移归位(重置按钮/模式重排用)</summary>
        public void Teleport(Vector3 pos, float yawDeg)
        {
            transform.position = pos;
            velocity = Vector3.zero;
            Yaw = yawDeg; YawRateCur = 0f;
            PitchDeg = RollDeg = 0f;
            Landed = pos.y <= GroundY + 0.05f;
        }

        public void ResetStats() { DistanceFlown = MaxAlt = MaxSpeedReached = 0f; }

        void Update()
        {
            if (!DrillClock.CanSimulate) return;
            float dt = Time.deltaTime;

            // ---- 1. 指令 → 世界系目标速度(含风漂移) ----
            // 驻停:接地且无油门 → 停桨状态,不受风漂移、不滑动
            bool onGround = transform.position.y <= GroundY + 0.02f;
            bool parked = onGround && Mathf.Abs(Cmd.Throttle) < 0.1f;

            var yawRot = Quaternion.Euler(0f, Yaw, 0f);
            Vector3 target = yawRot * new Vector3(Cmd.Roll * MaxSpeed, Cmd.Throttle * MaxClimb, Cmd.Pitch * MaxSpeed);
            if (Cmd.Brake > 0f) target *= 1f - 0.85f * Cmd.Brake;   // 刹车:目标速度衰减

            WindField.Sample(transform.position, out var wind, out var gust);
            if (!parked) target += wind * WindResponse;              // 侧风漂移(输入回中仍会漂)

            // ---- 2. 惯性逼近 + 外力 ----
            float accel = Accel * (1f + Cmd.Brake * 1.6f);            // 刹车时减速度增大
            velocity = Vector3.MoveTowards(velocity, target, accel * dt);
            ApplyForces(dt);

            // ---- 3. 积分与地面约束 ----
            var prev = transform.position;
            var p = prev + velocity * dt;
            if (p.y <= GroundY)
            {
                p.y = GroundY;
                if (velocity.y < 0f) velocity.y = 0f;
            }
            if (parked)
            {
                velocity.x = 0f;
                velocity.z = 0f;
                if (velocity.y < 0f) velocity.y = 0f;
            }
            transform.position = p;
            float step = Vector3.Distance(prev, p);
            DistanceFlown += step;
            MaxAlt = Mathf.Max(MaxAlt, Altitude);
            MaxSpeedReached = Mathf.Max(MaxSpeedReached, Speed);
            Landed = Altitude <= 0.03f && Speed < 0.6f;

            // ---- 4. 偏航(角加速度平滑 + 故障漂移偏置) ----
            YawRateCur = Mathf.MoveTowards(YawRateCur, Cmd.YawRate * MaxYawRate, YawAccel * dt);
            Yaw = Mathf.Repeat(Yaw + (YawRateCur + YawBiasDeg) * dt, 360f);

            // ---- 5. 姿态视觉映射:速度→俯仰/横滚,转弯压坡度,阵风抖动 ----
            var vLocal = Quaternion.Euler(0f, -Yaw, 0f) * Velocity;
            float pitchT = Mathf.Clamp(vLocal.z / MaxSpeed, -1f, 1f) * MaxTilt;    // 前进低头
            float rollT = -Mathf.Clamp(vLocal.x / MaxSpeed, -1f, 1f) * MaxTilt     // 右移右倾
                          - Mathf.Clamp(YawRateCur / MaxYawRate, -1f, 1f) * BankAngle;
            pitchT += gust.z * TurbTilt;
            rollT -= gust.x * TurbTilt;
            rollT += RollBiasDeg;           // 电机故障侧倾偏置
            PitchDeg = Mathf.MoveTowardsAngle(PitchDeg, Mathf.Clamp(pitchT, -38f, 38f), TiltSpeed * dt);
            RollDeg = Mathf.MoveTowardsAngle(RollDeg, Mathf.Clamp(rollT, -38f, 38f), TiltSpeed * dt);
            transform.rotation = Quaternion.Euler(PitchDeg, Yaw, RollDeg);

            // ---- 6. 旋翼转速联动:基速+空速+油门;落地怠速 ----
            float speed01 = Mathf.Clamp01(HorizSpeed / MaxSpeed);
            float rpmT = 0.45f + 0.4f * speed01 + 0.25f * Mathf.Abs(Cmd.Throttle);
            if (Landed && Mathf.Abs(Cmd.Throttle) < 0.05f && HorizSpeed < 0.5f) rpmT = 0.08f;
            Rpm01 = Mathf.MoveTowards(Rpm01, Mathf.Min(rpmT, 1.2f), 0.7f * dt);
            if (rotor != null) rotor.SetRpm(Rpm01);
        }

        void ApplyForces(float dt)
        {
            if (forces.Count == 0) return;
            float now = DrillClock.SimTime;
            for (int i = forces.Count - 1; i >= 0; i--)
            {
                velocity += forces[i].Rate * dt;
                if (forces[i].Until <= now) forces.RemoveAt(i);
            }
        }
    }
}

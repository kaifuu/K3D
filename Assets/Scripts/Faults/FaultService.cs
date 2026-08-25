using UnityEngine;

namespace DroneSim
{
    public enum FaultKind { None, GpsJam, LowBattery, MotorFault, GyroDrift }

    /// <summary>
    /// 故障注入服务(模块8):单故障模型,Inject/Clear 全量可逆。
    /// Update 内施加物理效应并做滚动测量:
    /// GPS干扰→方波扰动力+速度抖动RMS;低电量→动力降级限速;
    /// 电机故障→横滚偏置+停转旋翼+横滚峰值;陀螺漂移→偏航角速度偏置+航向偏转量。
    /// </summary>
    public class FaultService : MonoBehaviour
    {
        public static FaultService I;

        public FlightBody Body;
        public RouteFollower Follower;

        public FaultKind Active { get; private set; }
        public float Elapsed;              // 当前故障已持续 s
        public float JitterRms;            // GPS干扰:水平速度抖动RMS m/s
        public float SpeedRef = 12f;       // 低电量:注入时巡航参考速度 m/s
        public float RollPeak;            // 电机故障:|横滚|峰值 °
        public float YawAtInject;          // 陀螺漂移:注入时刻航向 °

        static readonly string[] Names = { "无", "GPS干扰", "低电量", "电机故障", "陀螺漂移" };
        public string ActiveName => Names[(int)Active];

        float origMaxSpeed, origClimb, origAccel, savedCruise, cruisePeak;
        RotorSpin rotor;
        Vector3 emaVel;
        float jitterE, jamSw, pushSw;

        void OnEnable() => I = this;       // 域重载防御

        public void Bind(FlightBody body, RouteFollower follower)
        {
            Body = body;
            Follower = follower;
            rotor = body != null ? body.GetComponent<RotorSpin>() : null;
            if (body != null)
            {
                origMaxSpeed = body.MaxSpeed;
                origClimb = body.MaxClimb;
                origAccel = body.Accel;
            }
            savedCruise = follower != null ? follower.Cruise : 12f;
        }

        public void Inject(FaultKind k)
        {
            if (k == FaultKind.None || Active == k || Body == null) return;
            if (Active != FaultKind.None) Clear();
            Active = k;
            Elapsed = 0f;
            JitterRms = 0f; jitterE = 0f;
            emaVel = Body.Velocity;
            RollPeak = 0f;
            YawAtInject = Body.HeadingDeg;
            jamSw = pushSw = 0.5f;         // 立即触发首个扰动

            switch (k)
            {
                case FaultKind.GpsJam:
                    EventBus.Publish("故障", name, "注入:GPS 卫星信号受干扰,定位抖动加剧", EventGrade.Critical);
                    break;
                case FaultKind.LowBattery:
                    // 参考取无故障期巡航峰值(航线转角会瞬时掉速,瞬时值不可作基准)
                    SpeedRef = Mathf.Max(cruisePeak, 1f);
                    Body.MaxSpeed = origMaxSpeed * 0.4f;
                    Body.MaxClimb = origClimb * 0.5f;
                    Body.Accel = origAccel * 0.45f;
                    if (Follower != null)
                    {
                        savedCruise = Follower.Cruise;
                        Follower.Cruise = savedCruise * 0.35f;   // 自动降速返航策略
                    }
                    EventBus.Publish("故障", name, "注入:电池低电量,动力降级并自动限速", EventGrade.Critical);
                    break;
                case FaultKind.MotorFault:
                    Body.RollBiasDeg = 14f;
                    if (rotor != null) rotor.StoppedRotor = 1;
                    EventBus.Publish("故障", name, "注入:2号电机故障停转,机体向左失衡侧倾", EventGrade.Critical);
                    break;
                case FaultKind.GyroDrift:
                    Body.YawBiasDeg = 25f;
                    EventBus.Publish("故障", name, "注入:陀螺仪零偏漂移,航向持续缓慢偏转", EventGrade.Critical);
                    break;
            }
        }

        public void Clear()
        {
            if (Active == FaultKind.None || Body == null) return;
            var k = Active;
            Body.MaxSpeed = origMaxSpeed;
            Body.MaxClimb = origClimb;
            Body.Accel = origAccel;
            Body.RollBiasDeg = 0f;
            Body.YawBiasDeg = 0f;
            if (Follower != null) Follower.Cruise = savedCruise;
            if (rotor != null) rotor.StoppedRotor = -1;
            Active = FaultKind.None;
            EventBus.Publish("故障", name, $"解除:{Names[(int)k]} 已恢复(持续 {Elapsed:0.0}s)", EventGrade.Op);
        }

        void Update()
        {
            if (Body == null || !DrillClock.CanSimulate) return;

            // 无故障期持续刷新巡航峰值(低电量断言基准)
            if (Active == FaultKind.None)
            {
                cruisePeak = Mathf.Max(cruisePeak, Body.HorizSpeed);
                return;
            }

            float dt = Time.deltaTime;
            Elapsed += dt;

            if (Active == FaultKind.GpsJam)
            {
                // 方波扰动力:每 0.5s 换轴(黄金角步进,确定性),12 m/s²
                jamSw += dt;
                if (jamSw >= 0.5f)
                {
                    jamSw = 0f;
                    float a = Mathf.Floor(DrillClock.SimTime * 2f) * 2.39996f;
                    Body.AddSustained(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 12f, 0.5f);
                }
                // 速度抖动RMS:瞬时速度对 EMA(τ0.5s)的偏差,能量滚动平均(τ1.2s)
                float aEma = 1f - Mathf.Exp(-dt / 0.5f);
                emaVel = Vector3.Lerp(emaVel, Body.Velocity, aEma);
                var dv = Body.Velocity - emaVel;
                dv.y = 0f;
                jitterE = Mathf.Lerp(jitterE, dv.sqrMagnitude, 1f - Mathf.Exp(-dt / 1.2f));
                JitterRms = Mathf.Sqrt(jitterE);
            }
            else if (Active == FaultKind.MotorFault)
            {
                RollPeak = Mathf.Max(RollPeak, Mathf.Abs(Body.RollDeg));
                // 失衡侧向推力:机体右向 2.5 m/s² 方波(航线跟随器持续纠偏 → 可见侧偏拉锯)
                pushSw += dt;
                if (pushSw >= 0.4f)
                {
                    pushSw = 0f;
                    var right = Quaternion.Euler(0f, Body.HeadingDeg, 0f) * Vector3.right;
                    Body.AddSustained(right * 2.5f, 0.4f);
                }
            }
        }
    }
}

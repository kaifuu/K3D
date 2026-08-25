using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 航点自驾仪(比例控制):FlightBody 本身是速度伺服,这里只做
    /// 位置误差→期望速度→机体指令的换算。无头剧本验收与后续
    /// 返航逻辑共用;每过一个航点发布 Op 事件(复盘时间轴刻度)。
    /// </summary>
    public class FlightAutopilot : MonoBehaviour, ICommandSource
    {
        public FlightBody Body;
        public float Cruise = 9f;          // 巡航速度 m/s
        public float ArriveRadius = 3.5f;  // 到点判定半径
        public int VisitedCount { get; private set; }
        public bool Landing { get; private set; }
        public bool LandedDone { get; private set; }
        public Vector3 CurrentTarget { get; private set; }
        public bool HasWaypoint { get; private set; }
        /// <summary>航线飞完(所有航点出队,尚未开始降落)</summary>
        public bool RouteDone => HasWaypoint && wps.Count == 0 && !Landing;

        readonly Queue<Vector3> wps = new Queue<Vector3>();

        void Update()
        {
            if (Body == null || !DrillClock.CanSimulate) return;
            Apply(Body);
        }

        public void Enqueue(Vector3 wp) { wps.Enqueue(wp); HasWaypoint = true; }

        /// <summary>开始自动降落:回到归航点正上方缓降,接地后停桨</summary>
        public void BeginLanding() { Landing = true; }

        public void ResetRoute()
        {
            wps.Clear(); VisitedCount = 0;
            Landing = LandedDone = HasWaypoint = false;
        }

        public void Apply(FlightBody body)
        {
            var c = FlightCommand.Idle;
            var pos = body.transform.position;

            if (Landing)
            {
                // ---- 降落:水平对准归航点,缓降接地 ----
                var padXZ = new Vector3(body.HomePos.x, 0f, body.HomePos.z);
                var horiz = padXZ - new Vector3(pos.x, 0f, pos.z);
                float dH = horiz.magnitude;
                if (dH > 0.8f)
                {
                    var vh = Vector2.ClampMagnitude(new Vector2(horiz.x, horiz.z), 3f);
                    FromWorldVel(body, new Vector3(vh.x, 0f, vh.y), ref c);
                }
                float alt = body.Altitude;
                if (dH < 6f && alt > 0.3f)
                    c.Throttle = -Mathf.Clamp(alt * 0.25f, 0.15f, 0.55f);
                else if (alt > 3f)
                    c.Throttle = -0.3f;

                if (body.Landed)
                {
                    c = FlightCommand.Idle;
                    c.Brake = 1f;
                    if (!LandedDone)
                    {
                        LandedDone = true;
                        EventBus.Publish("飞行", name, "自动降落完成,任务结束", EventGrade.Op);
                    }
                }
                body.Cmd = c;
                return;
            }

            if (wps.Count == 0)
            {
                if (!HasWaypoint) return;   // 未领任务:保持悬停(指令回中)
                body.Cmd = c;               // 悬停(速度伺服自然减速)
                return;
            }

            // ---- 航点追踪 ----
            CurrentTarget = wps.Peek();
            var err = CurrentTarget - pos;
            float dHoriz = new Vector2(err.x, err.z).magnitude;

            if (dHoriz < ArriveRadius && Mathf.Abs(err.y) < 2.5f)
            {
                wps.Dequeue();
                VisitedCount++;
                EventBus.Publish("飞行", name, $"到达航点 {VisitedCount} ({CurrentTarget.x:0},{CurrentTarget.y:0},{CurrentTarget.z:0})", EventGrade.Op);
                return;
            }

            // 期望速度:比例趋近,远处巡航、近处减速
            var vHoriz = Vector2.ClampMagnitude(new Vector2(err.x, err.z) * 0.9f, Cruise);
            float vY = Mathf.Clamp(err.y * 0.7f, -body.MaxClimb * 0.8f, body.MaxClimb * 0.8f);
            FromWorldVel(body, new Vector3(vHoriz.x, vY, vHoriz.y), ref c);

            // 航向对准运动方向(远距离时才转向,避免终点处打转)
            if (dHoriz > 6f && vHoriz.sqrMagnitude > 1f)
            {
                float desired = Mathf.Atan2(vHoriz.x, vHoriz.y) * Mathf.Rad2Deg;
                float yawErr = Mathf.DeltaAngle(body.HeadingDeg, desired);
                c.YawRate = Mathf.Clamp(yawErr / 60f, -1f, 1f);
            }
            body.Cmd = c;
        }

        /// <summary>世界系期望速度 → 机体指令(公共换算)</summary>
        static void FromWorldVel(FlightBody body, Vector3 worldVel, ref FlightCommand c) =>
            FlightMath.WorldVelToCmd(body, worldVel, ref c);
    }
}

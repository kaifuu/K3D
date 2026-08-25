using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 编队中枢:持有领机(由航线跟随器驱动)+ 成员表,统一计算槽位世界坐标
    /// 与误差统计;构型切换只改槽位表,成员平滑过渡(无瞬移)。
    /// 收敛判定用"稳态误差"(剔除正在避障的成员),避障不算队形劣化。
    /// </summary>
    public class FormationController : MonoBehaviour
    {
        public FlightBody Leader;
        public readonly List<FormationHandle> Units = new List<FormationHandle>(16);
        public FormationShape Shape = FormationShape.Wedge;
        public float Spacing = 5f;
        public float Gain = 1.8f;             // 槽位误差 P 增益(近距精跟段)
        public float MaxUnitSpeed = 20f;      // 成员限速(追赶/避障裕量)

        public bool Active { get; private set; }
        public int Count => Units.Count + 1;
        public float LeaderYaw => Leader != null ? Leader.HeadingDeg : 0f;
        public Vector3 LeaderVel => Leader != null ? Leader.Velocity : Vector3.zero;
        public float SwitchAt { get; private set; } = -99f;
        public int SwitchCount { get; private set; }

        // ---- 误差/安全统计(Update 刷新) ----
        public bool Converged { get; private set; }        // 切换后稳态误差曾 < 阈值(闩锁到下次切换)
        public float MaxSlotError { get; private set; }     // 全体最大槽位误差
        public float MaxSteadyError { get; private set; }   // 剔除避障成员的最大误差
        public float AvgSlotError { get; private set; }
        public float MinClearance { get; private set; } = 999f;
        public bool AnyAvoiding { get; private set; }
        public const float ConvergeThreshold = 1.5f;

        public Vector3 SlotWorldPos(int i)
        {
            if (Leader == null) return Vector3.zero;
            var off = FormationLibrary.SlotOffset(Shape, i, Count, Spacing);
            return Leader.transform.position + Quaternion.Euler(0f, LeaderYaw, 0f) * off;
        }

        /// <summary>槽位世界速度 = 领机平移 + 航向旋转扫摆(ω×r)。
        /// 转弯时外侧槽位被旋转横向扫移,不补此项会有 稳态滞后=扫速/增益。</summary>
        public Vector3 SlotVel(int i)
        {
            if (Leader == null) return Vector3.zero;
            var r = SlotWorldPos(i) - Leader.transform.position;
            float omega = Leader.YawRateCur * Mathf.Deg2Rad;    // 左手系 y 轴:导数=ω*(rz,-rx)
            return LeaderVel + new Vector3(omega * r.z, 0f, -omega * r.x);
        }

        public void SetShape(FormationShape s)
        {
            if (s == Shape) return;
            Shape = s;
            SwitchCount++;
            SwitchAt = DrillClock.SimTime;
            Converged = false;
            EventBus.Publish("编队", name,
                $"切换构型:{FormationLibrary.Name(s)}({Count} 机 间距 {Spacing:0.0}m)", EventGrade.Op);
        }

        public void StartFormation()
        {
            Active = true;
            Converged = false;
            EventBus.Publish("编队", name,
                $"编队开始:{FormationLibrary.Name(Shape)}({Count} 机)", EventGrade.Op);
        }

        public void StopFormation()
        {
            if (!Active) return;
            Active = false;
            foreach (var u in Units) if (u != null) u.HoldPosition();
            EventBus.Publish("编队", name, "编队解除,成员原地悬停", EventGrade.Op);
        }

        void Update()
        {
            if (!DrillClock.CanSimulate || !Active) return;

            MaxSlotError = MaxSteadyError = AvgSlotError = 0f;
            AnyAvoiding = false;
            float sum = 0f;
            int n = 0;
            foreach (var u in Units)
            {
                if (u == null || u.Body == null) continue;
                if (u.SlotError > MaxSlotError) MaxSlotError = u.SlotError;
                if (u.Avoiding) AnyAvoiding = true;
                else if (u.SlotError > MaxSteadyError) MaxSteadyError = u.SlotError;
                sum += u.SlotError;
                n++;
                float cl = ObstacleAvoid.Clearance(u.transform.position);
                if (cl < MinClearance) MinClearance = cl;
            }
            AvgSlotError = n > 0 ? sum / n : 0f;
            if (Leader != null)
            {
                float cl = ObstacleAvoid.Clearance(Leader.transform.position);
                if (cl < MinClearance) MinClearance = cl;
            }
            if (!Converged && MaxSteadyError < ConvergeThreshold) Converged = true;
        }
    }
}

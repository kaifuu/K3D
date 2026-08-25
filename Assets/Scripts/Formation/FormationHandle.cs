using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 编队成员自驾:每帧取槽位世界坐标(领机位置绕领机航向旋转载),
    /// 指令 = 领机速度前馈 + 槽位误差 P 控制(带限速),跟随无稳态滞后;
    /// 航迹被障碍占据时目标点侧推绕行,通过后自动归位;偏航缓慢对齐领机。
    /// </summary>
    public class FormationHandle : MonoBehaviour
    {
        public FormationController Ctrl;
        public FlightBody Body;
        public int Index;                       // 1..n-1(0=领机)

        /// <summary>与真实槽位的距离(不含避让偏移,误差统计用)</summary>
        public float SlotError { get; private set; }
        /// <summary>本帧正在绕行障碍</summary>
        public bool Avoiding { get; private set; }

        public Vector3 SlotWorld => Ctrl != null ? Ctrl.SlotWorldPos(Index) : transform.position;

        void Update()
        {
            if (Body == null || Ctrl == null || !DrillClock.CanSimulate || !Ctrl.Active) return;

            var slot = Ctrl.SlotWorldPos(Index);
            SlotError = Vector3.Distance(transform.position, slot);
            var aim = ObstacleAvoid.AdjustTarget(transform.position, slot);
            Avoiding = (aim - slot).sqrMagnitude > 0.01f;

            // 双模控制:大误差拦截追踪(瞄准 槽位+槽速·τ 的前置点 —— 纯追踪侧移目标
            // 会画出外螺旋且速度被转向吃掉),小误差前馈(槽位速度=领机平移+旋转扫摆)
            // +P 精跟;2~10m 之间平滑混合
            var err = aim - transform.position;
            float em = err.magnitude;
            var vTrack = Ctrl.SlotVel(Index) + err * Ctrl.Gain;
            var lead = aim + Vector3.ClampMagnitude(Ctrl.SlotVel(Index) * 1.2f, 10f);
            var errI = lead - transform.position;
            float eim = errI.magnitude;
            // 截击限速:接近时按误差收油(槽速+误差),防止满速冲过槽位再折返
            float chaseSpd = Mathf.Min(Ctrl.MaxUnitSpeed, Ctrl.SlotVel(Index).magnitude + em);
            var vChase = eim > 0.01f ? errI / eim * chaseSpd : vTrack;
            var v = Vector3.Lerp(vTrack, vChase, Mathf.Clamp01((em - 2f) / 8f));
            var vh = Vector2.ClampMagnitude(new Vector2(v.x, v.z), Ctrl.MaxUnitSpeed);
            float vy = Mathf.Clamp(v.y, -Body.MaxClimb * 0.8f, Body.MaxClimb);

            // 近障防逼近四层(限速带宽、抵消带窄 —— 切向始终自由,不产生绕行锁死):
            // ① r+25 内限速 12(>外侧臂槽位速度 11.6,不掉队;<20 冲刺,任何入界速度都刹得住:
            //    20m/s 入界经机体 9m/s² 惯性收敛后最短停止距离仍留 >2m 余量);
            // ② 预测制动:当前径向速度按 7m/s²(保守)刹不住时,全力径向外甩;
            // ③ 净距<2m 纯径向弹出;
            // ④ 净距<5m(窄带!)才取消指向障碍的速度分量 —— 带宽了会吃掉弦向归位进度,卡在塔后
            if (ObstacleAvoid.Nearest(transform.position, out var c, out var r))
            {
                var p2 = new Vector2(transform.position.x, transform.position.z);
                float d = Vector2.Distance(p2, c);
                if (d > 0.01f && d < r + 25f)
                {
                    var radial = (p2 - c) / d;
                    vh = Vector2.ClampMagnitude(vh, 12f);
                    var bv2 = new Vector2(Body.Velocity.x, Body.Velocity.z);
                    float bodyIn = -Vector2.Dot(bv2, radial);          // 当前径向逼近速度
                    float brakeDist = bodyIn * bodyIn / 14f;           // v²/2a,a=7 保守
                    if (bodyIn > 0f && brakeDist > d - r - 2f)
                    {
                        vh = radial * Mathf.Max(6f, bodyIn * 0.8f);    // 刹不住 → 全力外甩
                    }
                    else if (d < r + 2f)
                    {
                        vh = radial * 6f;                              // 近距纯弹出
                    }
                    else if (d < r + 5f)
                    {
                        float inward = -Vector2.Dot(vh, radial);       // 指向障碍的分量
                        if (inward > 0f) vh += radial * inward;        // 抵消 → 切向滑行绕过
                    }
                }
            }

            var cmd = FlightCommand.Idle;
            FlightMath.WorldVelToCmd(Body, new Vector3(vh.x, vy, vh.y), ref cmd);

            // 起飞授权:机体在地面而槽位在上方 → 强制最低油门,
            // 防驻停判定(|Throttle|<0.1 时速度清零)把待飞成员按死在地面
            if (Body.Landed && err.y > 0.5f && cmd.Throttle < 0.35f)
                cmd.Throttle = 0.35f;

            float yawErr = Mathf.DeltaAngle(Body.HeadingDeg, Ctrl.LeaderYaw);
            cmd.YawRate = Mathf.Clamp(yawErr / 60f, -0.7f, 0.7f);
            cmd.Clamp();
            Body.Cmd = cmd;
        }

        /// <summary>编队解除/暂停:刹车悬停</summary>
        public void HoldPosition()
        {
            if (Body == null) return;
            var c = FlightCommand.Idle;
            c.Brake = 0.6f;
            Body.Cmd = c;
        }
    }
}

using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 航线跟随器:沿 RouteData 连续追踪前视"胡萝卜点"(Sample(dist+lookAhead)),
    /// 里程按机体投影推进(带环绕修正与回退护栏);支持 暂停→续飞(重投影断点)。
    /// 偏差=机体到航线最近投影距离,超限触发告警(红线/箭头由视觉层联动)。
    /// </summary>
    public class RouteFollower : MonoBehaviour, ICommandSource
    {
        public FlightBody Body;
        public RouteData Route;
        public float Cruise = 8f;            // 巡航速度 m/s
        public float LookAhead = 7f;        // 前视距离 m
        public float DeviationLimit = 6f;   // 偏差告警阈值 m

        public bool Active { get; private set; }
        public bool Started { get; private set; }
        public float Dist;                  // 沿线累计里程(跨圈连续)
        public float Deviation;             // 当前偏距 m
        public float MaxDeviation;          // 本次巡航最大偏距(导出用)
        public bool AlarmTriggered;         // 曾超偏(闩锁,断言/复盘用)

        /// <summary>告警条件:已入线收敛过(起飞爬升段不算)且当前超限</summary>
        public bool AlarmNow => Started && converged && Deviation > DeviationLimit;
        bool converged;
        float TotalLen => Route != null ? Route.TotalLength : 0f;
        public int Loops => TotalLen > 0.01f ? Mathf.FloorToInt(Dist / TotalLen) : 0;
        public float Progress01 => TotalLen > 0.01f ? Mathf.Repeat(Dist, TotalLen) / TotalLen : 0f;
        public Vector3 Carrot => Route != null && Route.Count >= 2
            ? Route.Sample(RepeatOrClamp(Dist + LookAhead)) : Vector3.zero;
        /// <summary>开线航线飞到终点</summary>
        public bool Finished => Started && !Active && Route != null && !Route.Loop && TotalLen > 0f && Dist >= TotalLen - 1f;

        float RepeatOrClamp(float d) => Route.Loop ? Mathf.Repeat(d, TotalLen) : Mathf.Clamp(d, 0f, TotalLen);

        void Update()
        {
            if (Body == null || !DrillClock.CanSimulate) return;
            Apply(Body);
        }

        public void Apply(FlightBody body)
        {
            var c = FlightCommand.Idle;
            if (Route == null || Route.Count < 2 || TotalLen < 1f || !Started)
                return;   // 未开始:不接管,保持玩家/其他源指令

            if (!Active)
            {
                // 暂停(断点):原地对地悬停
                c.Brake = 0.6f;
                body.Cmd = c;
                return;
            }

            // ---- 里程推进:投影 + 环绕修正 + 单调护栏 ----
            float proj = Route.ProjectDistance(body.transform.position, out var nearest);
            float total = TotalLen;
            float cur = Mathf.Repeat(Dist, total);
            float delta = proj - cur;
            if (delta < -total * 0.5f) delta += total;   // 越过闭合点
            if (delta > total * 0.5f) delta -= total;
            Dist += Mathf.Clamp(delta, -3f, Cruise * Time.deltaTime * 1.8f + 3f);

            // ---- 偏差与告警(首次入线收敛前不判定,排除起飞爬升段) ----
            Deviation = Vector3.Distance(body.transform.position, nearest);
            if (!converged)
            {
                if (Deviation < DeviationLimit * 0.66f) converged = true;
            }
            else
            {
                if (Deviation > MaxDeviation) MaxDeviation = Deviation;
                if (AlarmNow && !AlarmTriggered)
                {
                    AlarmTriggered = true;
                    EventBus.Publish("告警", name, $"偏离航线 {Deviation:0.0}m 超限({DeviationLimit:0}m)", EventGrade.Warn);
                }
            }

            // ---- 胡萝卜点追踪(P 控制 → FlightMath 换算) ----
            var pos = body.transform.position;
            var carrot = Carrot;
            var err = carrot - pos;
            var vHoriz = Vector2.ClampMagnitude(new Vector2(err.x, err.z) * 1.1f, Cruise);
            float vY = Mathf.Clamp(err.y * 0.8f, -body.MaxClimb * 0.8f, body.MaxClimb * 0.8f);
            FlightMath.WorldVelToCmd(body, new Vector3(vHoriz.x, vY, vHoriz.y), ref c);

            // 航向对准航线切线
            var tan = Route.Sample(RepeatOrClamp(cur + 1.6f)) - Route.Sample(cur);
            if (tan.sqrMagnitude > 0.01f)
            {
                float desired = Mathf.Atan2(tan.x, tan.z) * Mathf.Rad2Deg;
                float yawErr = Mathf.DeltaAngle(body.HeadingDeg, desired);
                c.YawRate = Mathf.Clamp(yawErr / 60f, -1f, 1f);
            }
            body.Cmd = c;
        }

        // ---------- 控制 ----------
        public void StartRoute()
        {
            if (Route == null || Route.Count < 2 || TotalLen < 1f) return;
            Dist = Route.ProjectDistance(Body.transform.position, out _);
            Deviation = MaxDeviation = 0f;
            AlarmTriggered = false;
            converged = false;
            Started = true;
            Active = true;
            EventBus.Publish("飞行", name, $"开始航线巡航({Route.Count} 点 {TotalLen:0}m{(Route.Loop ? " 闭环" : "")})", EventGrade.Op);
        }

        public void Pause()
        {
            if (!Active) return;
            Active = false;
            EventBus.Publish("飞行", name, $"航线暂停(断点 里程 {Mathf.Repeat(Dist, TotalLen):0}m)", EventGrade.Op);
        }

        /// <summary>断点续飞:从机体当前位置重新投影,就近接续航线</summary>
        public void Resume()
        {
            if (Started && !Active && Route != null && Route.Count >= 2)
            {
                Dist = Mathf.Floor(Dist / TotalLen) * TotalLen + Route.ProjectDistance(Body.transform.position, out _);
                Active = true;
                EventBus.Publish("飞行", name, "断点续飞,已重新接续航线", EventGrade.Op);
            }
        }

        public void StopRoute()
        {
            Started = false;
            Active = false;
            Deviation = 0f;
            EventBus.Publish("飞行", name, "航线巡航停止", EventGrade.Op);
        }
    }
}

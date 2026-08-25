using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 应急源①火情点:Ignite 后 火焰+烟柱+火光脉动(全 3D,批处理截图可见);
    /// Scanned 由模式在无人机到位侦察后标记。
    /// </summary>
    public class FireSite : MonoBehaviour
    {
        public bool Burning, Scanned;
        public float BurnTime { get; private set; }
        public int FireAlive => fire != null ? fire.particleCount : -1;
        public int SmokeAlive => smoke != null ? smoke.particleCount : -1;
        public bool FirePlaying => fire != null && fire.isPlaying;
        public float LightInt => fireLight != null ? fireLight.intensity : -1f;
        ParticleSystem fire, smoke;
        Light fireLight;
        Transform flameCore;

        void Update()
        {
            if (!Burning || !DrillClock.CanSimulate) return;
            BurnTime += Time.deltaTime;
            if (fireLight != null)
                fireLight.intensity = 2.0f + Mathf.Sin(DrillClock.SimTime * 9f) * 0.5f
                                      + Mathf.Sin(DrillClock.SimTime * 23f) * 0.25f;   // 火光跳动(确定性)
            if (flameCore != null)   // 火舌脉动(确定性)
            {
                float s = 1f + 0.14f * Mathf.Sin(DrillClock.SimTime * 11f)
                              + 0.06f * Mathf.Sin(DrillClock.SimTime * 27f);
                flameCore.localScale = new Vector3(8f * s, 11f * s, 8f * s);
            }
        }

        public void Ignite()
        {
            if (Burning) return;
            Burning = true;
            FXManager.I.FireAt(transform, transform.position, 1.8f);
            var pss = GetComponentsInChildren<ParticleSystem>();   // [0]=火焰 [1]=烟柱
            fire = pss.Length > 0 ? pss[0] : null;
            smoke = pss.Length > 1 ? pss[1] : null;
            flameCore = FXManager.I.BuildFlameCore(transform);
            var lgo = new GameObject("FireLight");
            lgo.transform.SetParent(transform, false);
            lgo.transform.localPosition = new Vector3(0f, 5.6f, 0f);
            fireLight = lgo.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.55f, 0.2f);
            fireLight.range = 55f;
            fireLight.intensity = 4.5f;
            EventBus.Publish("应急", name, "告警:B区仓库起火,浓烟扩散,请求空中侦察", EventGrade.Critical);
        }
    }

    /// <summary>
    /// 应急源②黑飞入侵:小机体(手工运动学)直线逼近禁区,
    /// 未被驱离抵达后绕禁区上空盘旋;被喊话 Deter 后调头加速离场。
    /// </summary>
    public class IntruderAlert : MonoBehaviour
    {
        public bool Active, Deterred, Left;
        public float Speed = 8f, FleeSpeed = 12f;
        public Vector3 Target = new Vector3(0f, 22f, 0f);   // 禁区上空
        Vector3 dir;
        float deterTimer, orbitA;

        public void Spawn(Vector3 from)
        {
            if (Active) return;
            Active = true;
            gameObject.SetActive(true);
            transform.position = from;
            dir = Target - from;
            dir.y = 0f;
            dir.Normalize();
            EventBus.Publish("应急", name, "发现黑飞无人机闯入管制空域,持续逼近禁区", EventGrade.Warn);
        }

        public void Deter()
        {
            if (!Active || Deterred) return;
            Deterred = true;
            EventBus.Publish("应急", name, "黑飞目标收到喊话警告,调头撤离", EventGrade.Op);
        }

        void Update()
        {
            if (!Active || Left || !DrillClock.CanSimulate) return;
            float dt = Time.deltaTime;

            if (Deterred)
            {
                deterTimer += dt;
                transform.position += (-dir * FleeSpeed + Vector3.up * 1.2f) * dt;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(-dir), dt * 3f);
                if (deterTimer > 8f)
                {
                    Left = true;
                    gameObject.SetActive(false);
                    EventBus.Publish("应急", name, "黑飞目标已离场,管制空域恢复", EventGrade.Info);
                }
                return;
            }

            var toT = Target - transform.position;
            toT.y = 0f;
            if (toT.magnitude > 5f)
            {
                transform.position += dir * Speed * dt;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), dt * 4f);
            }
            else
            {
                // 抵达禁区上空:绕圈示威盘旋
                orbitA += Speed / 9f * dt;
                var p = Target + new Vector3(Mathf.Cos(orbitA) * 9f, 0f, Mathf.Sin(orbitA) * 9f);
                var step = p - transform.position;
                transform.position += Vector3.ClampMagnitude(step, Speed * dt);
                if (step.sqrMagnitude > 0.04f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(step.normalized), dt * 4f);
            }
        }
    }

    /// <summary>
    /// 应急源③链路失联:断控后自驾停驶,机体进入失控保护
    /// (确定性伪噪声悬停偏差 + 风漂移),机顶双环反转示警;
    /// 计时到自动重连恢复。恢复时长/漂移量供导出。
    /// </summary>
    public class LinkLoss : MonoBehaviour
    {
        public FlightBody Body;
        public MonoBehaviour Autopilot;      // 失联时停自驾(FlightAutopilot 装箱禁用)
        public float Duration = 6f;
        public bool Lost { get; private set; }
        public bool Recovered { get; private set; }
        public float LostAt { get; private set; }
        public float DriftM { get; private set; }
        public float LostSeconds => Recovered ? Duration : Lost ? DrillClock.SimTime - LostAt : 0f;

        Transform ringA, ringB;
        Material ringMat;
        Vector3 lostPos;

        public void Begin()
        {
            if (Lost) return;
            Lost = true;
            Recovered = false;
            LostAt = DrillClock.SimTime;
            if (Body != null) lostPos = Body.transform.position;
            if (Autopilot != null) Autopilot.enabled = false;
            EventBus.Publish("应急", name, "紧急:数据链路中断,机体进入失控保护模式", EventGrade.Critical);
        }

        void BuildRings()
        {
            if (Body == null || ringA != null) return;
            ringMat = EnvironmentBuilder.UnlitMat(new Color(0.55f, 0.75f, 1f, 0.85f));
            ringA = MakeRing(2.2f, 2.2f);
            ringB = MakeRing(4.4f, 3.2f);
        }

        Transform MakeRing(float radius, float height)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            if (ring.GetComponent<Collider>() != null) Destroy(ring.GetComponent<Collider>());
            ring.name = "LostLinkRing";
            ring.transform.SetParent(Body.transform, false);
            ring.transform.localPosition = new Vector3(0f, height, 0f);
            ring.transform.localScale = new Vector3(radius, 0.04f, radius);
            ring.GetComponent<Renderer>().material = ringMat;
            return ring.transform;
        }

        void Update()
        {
            if (!DrillClock.CanSimulate) return;
            float dt = Time.deltaTime;

            if (Lost)
            {
                BuildRings();
                // 确定性伪噪声指令:小幅悬停偏差 → 视觉失控漂移(叠加风漂移)
                float n1 = Mathf.Sin(DrillClock.SimTime * 2.1f) + Mathf.Sin(DrillClock.SimTime * 3.7f) * 0.5f;
                float n2 = Mathf.Cos(DrillClock.SimTime * 1.7f) + Mathf.Cos(DrillClock.SimTime * 2.9f) * 0.5f;
                if (Body != null)
                {
                    var c = FlightCommand.Idle;
                    c.Roll = 0.18f * n1;
                    c.Pitch = 0.18f * n2;
                    c.Throttle = 0.06f * Mathf.Sin(DrillClock.SimTime * 1.3f);
                    c.Clamp();
                    Body.Cmd = c;
                    DriftM = Vector3.Distance(lostPos, Body.transform.position);
                }
                if (ringA != null) ringA.Rotate(0f, 95f * dt, 0f, Space.Self);
                if (ringB != null) ringB.Rotate(0f, -70f * dt, 0f, Space.Self);

                if (DrillClock.SimTime - LostAt >= Duration)
                {
                    Lost = false;
                    Recovered = true;
                    if (Autopilot != null) Autopilot.enabled = true;
                    if (Body != null) Body.Cmd = FlightCommand.Idle;
                    if (ringA != null) Destroy(ringA.gameObject);
                    if (ringB != null) Destroy(ringB.gameObject);
                    ringA = ringB = null;
                    EventBus.Publish("应急", name,
                        $"链路重连成功(失联 {Duration:0.0}s 漂移 {DriftM:0.0}m),恢复正常操控", EventGrade.Op);
                }
            }
        }
    }
}

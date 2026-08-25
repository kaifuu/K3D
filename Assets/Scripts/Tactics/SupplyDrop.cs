using UnityEngine;

namespace DroneSim
{
    /// <summary>投送区:位置锚 + 半径(落点误差以此为中心度量)</summary>
    public class DropZone : MonoBehaviour
    {
        public float Radius = 6f;
        public Vector3 Center => transform.position;
    }

    /// <summary>
    /// 物资箱:挂钩随机 → Release 开伞(终端速度缓降+伞摆回正)→
    /// 触地弹跳(能量衰减×0.4,≤3 次)→ 停稳;落点误差/弹跳次数导出。
    /// 手工运动学,不依赖物理引擎(无头确定性)。
    /// </summary>
    public class SupplyCrate : MonoBehaviour
    {
        public DropZone Zone;
        public bool Released, Settled;
        public int Bounces;
        public float ReleasedAt, SettledAt;
        public Vector3 LandPos { get; private set; }
        public float TerminalMps = 4.4f;

        Vector3 vel;
        Transform chute;

        public float ErrorM => Zone == null ? -1f
            : Vector2.Distance(new Vector2(LandPos.x, LandPos.z), new Vector2(Zone.Center.x, Zone.Center.z));

        public void Attach(Transform drone)
        {
            transform.SetParent(drone, false);
            transform.localPosition = new Vector3(0f, -1.35f, 0f);
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>脱离挂钩开伞:水平速度半继承(载机前冲),进入伞降</summary>
        public void Release(FlightBody carrier)
        {
            if (Released) return;
            Released = true;
            ReleasedAt = DrillClock.SimTime;
            transform.SetParent(null, true);
            var inherit = carrier != null ? carrier.Velocity : Vector3.zero;
            vel = new Vector3(inherit.x * 0.5f, 0f, inherit.z * 0.5f);
            BuildChute();
            EventBus.Publish("应急", name, "抵达投送点上空,物资箱释放(开伞减速)", EventGrade.Op);
        }

        void BuildChute()
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            if (c.GetComponent<Collider>() != null) Destroy(c.GetComponent<Collider>());
            c.name = "Chute";
            chute = c.transform;
            chute.SetParent(transform, false);
            chute.localPosition = new Vector3(0f, 1.9f, 0f);
            chute.localScale = new Vector3(2.8f, 1.5f, 2.8f);
            chute.GetComponent<Renderer>().material = EnvironmentBuilder.UnlitMat(new Color(1f, 0.5f, 0.22f, 0.92f));
        }

        void Update()
        {
            if (!Released || Settled || !DrillClock.CanSimulate) return;
            float dt = Time.deltaTime;

            // 伞降:垂直向终端速度过渡,水平气动衰减,姿态摆回正
            vel.y = Mathf.MoveTowards(vel.y, -TerminalMps, 9f * dt);
            vel.x *= 1f - 0.5f * dt;
            vel.z *= 1f - 0.5f * dt;
            transform.position += vel * dt;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.identity, dt * 2f);
            if (chute != null)
                chute.localRotation = Quaternion.Euler(6f * Mathf.Sin(DrillClock.SimTime * 1.8f), 0f, 5f * Mathf.Cos(DrillClock.SimTime * 1.5f));

            if (transform.position.y <= 0.5f)
            {
                transform.position = new Vector3(transform.position.x, 0.5f, transform.position.z);
                LandPos = transform.position;
                Bounces++;
                if (chute != null)
                {
                    Destroy(chute.gameObject);
                    chute = null;
                    FXManager.I?.DustPuff(transform.position + Vector3.up * 0.3f);
                }
                float vy = -vel.y;
                vel.x *= 0.55f;
                vel.z *= 0.55f;
                if (vy < 1.4f || Bounces >= 3)
                {
                    Settled = true;
                    SettledAt = DrillClock.SimTime;
                    vel = Vector3.zero;
                    EventBus.Publish("应急", name,
                        $"物资箱落地停稳:落点偏差 {ErrorM:0.0}m 弹跳 {Bounces} 次", EventGrade.Op);
                }
                else vel.y = vy * 0.4f;
            }
        }
    }
}

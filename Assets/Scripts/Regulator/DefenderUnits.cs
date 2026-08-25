using UnityEngine;

namespace DroneSim
{
    /// <summary>雷达站:扫描扇面可视化</summary>
    public class RadarStation : MonoBehaviour
    {
        Transform sweepBeam;

        void Awake()
        {
            sweepBeam = transform.Find("Sweep");
            if (sweepBeam == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                go.name = "Sweep";
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(SimConfig.RadarRange * 1.6f, 0.02f, 4f);
                go.transform.localPosition = new Vector3(0f, 0.15f, SimConfig.RadarRange * 0.8f);
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = new Color(0.2f, 1f, 0.5f, 0.25f);
                go.GetComponent<Renderer>().material = mat;
                sweepBeam = go.transform;
            }
        }

        void Update()
        {
            if (sweepBeam != null)
                sweepBeam.RotateAround(transform.position, Vector3.up, 150f * Time.deltaTime);
        }
    }

    /// <summary>核心禁飞区标记</summary>
    public class CoreZone : MonoBehaviour { }

    /// <summary>反制单元(干扰/捕获网/激光三选一),自动或手动开火;自动防御按威胁优先级选靶(P11)</summary>
    public class CounterUnit : MonoBehaviour
    {
        public enum Mode { Jammer, NetGun, Laser }
        public Mode mode;
        public float range = SimConfig.CounterRange;
        public float cooldown = 2.2f;

        /// <summary>威胁分级器(RegulatorMode 注入;空则退化为最近目标选靶)</summary>
        public static ThreatGrader Grader;

        float nextFire;
        Transform turretHead, barrel;
        LaserSystem laser;

        void Awake()
        {
            turretHead = transform.Find("Head");
            barrel = turretHead != null ? turretHead.Find("Barrel") : null;
            laser = GetComponent<LaserSystem>();
        }

        void Update()
        {
            if (!DrillClock.CanSimulate) return;
            if (Spawner.I == null) return;

            EnemyDrone auto = null;
            if (Grader != null && Grader.Ranked.Count > 0)
            {
                // 威胁优先级选靶:取射程内分级最高的目标
                for (int i = 0; i < Grader.Ranked.Count; i++)
                {
                    var d = Grader.Ranked[i].Drone;
                    if (d == null || d.State != DroneState.Approaching) continue;
                    if (Vector3.Distance(transform.position, d.transform.position) > range) continue;
                    if (mode == Mode.Laser && d.KillViolation) continue;   // 激光避免误击合规机
                    auto = d;
                    break;
                }
            }
            if (auto == null)
            {
                float best = float.MaxValue;
                foreach (var d in Spawner.I.Active)
                {
                    if (d == null || d.State != DroneState.Approaching) continue;
                    if (Vector3.Distance(transform.position, d.transform.position) > range) continue;
                    // 激光优先打黑飞机,避免误击合规机
                    if (mode == Mode.Laser && d.KillViolation) continue;
                    // 干扰与捕获网可以先用;警告合规机
                    float dCore = new Vector2(d.transform.position.x, d.transform.position.z).magnitude;
                    if (dCore < best) { best = dCore; auto = d; }
                }
            }

            EnemyDrone target = null;
            var sel = GameState.Selected;
            if (sel != null && sel.State == DroneState.Approaching &&
                Vector3.Distance(transform.position, sel.transform.position) <= range)
                target = sel;
            else if (GameState.AutoDefend)
                target = auto;

            if (target != null && Time.time >= nextFire)
                Fire(target);

            // 炮塔指向
            if (turretHead != null && target != null)
            {
                Vector3 dir = (target.transform.position - turretHead.position).normalized;
                turretHead.rotation = Quaternion.Slerp(
                    turretHead.rotation, Quaternion.LookRotation(dir), 6f * Time.deltaTime);
            }
        }

        void Fire(EnemyDrone d)
        {
            if (d == null) return;
            nextFire = Time.time + cooldown;
            Vector3 muzzle = barrel != null ? barrel.position : transform.position + Vector3.up * 2f;
            switch (mode)
            {
                case Mode.Jammer:
                    d.WarnRemoteId();                 // 先合规警告
                    if (d.State != DroneState.Approaching) break;  // 合规机已调头,不再叠加干扰
                    d.ApplyJamming();                 // 不听警告则干扰迫降
                    FXManager.I?.JamBurst(muzzle, d.transform.position);
                    break;
                case Mode.NetGun:
                    FXManager.I?.NetShot(muzzle, d.transform.position, d);
                    break;
                case Mode.Laser:
                    if (laser != null) laser.Fire(muzzle, d.transform.position);
                    d.LaserHit();
                    break;
            }
        }
    }
}

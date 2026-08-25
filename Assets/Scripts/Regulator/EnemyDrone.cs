using UnityEngine;

namespace DroneSim
{
    public enum DroneKind { Recon, Attack, Swarm }
    public enum DroneState { Approaching, TurnedBack, Descending, Falling, Captured, Dead }

    /// <summary>目标无人机:径向接近核心设施,可被警告/干扰/捕获/摧毁</summary>
    public class EnemyDrone : MonoBehaviour
    {
        public DroneKind Kind;
        public DroneState State = DroneState.Approaching;
        public string DroneId;
        public bool KillViolation;      // 击落是否算违规(合规误击)
        public bool RemoteIdCompliant;  // 是否响应RemoteID警告
        public float Speed = 14f;
        public int HP = 2;

        float idPhase;
        Renderer bodyRenderer;
        TrailRenderer trail;
        Transform selMark;
        static readonly Color[] kindColor =
        {
            new Color(0.35f, 0.75f, 1f),   // 侦察 蓝
            new Color(1f, 0.35f, 0.2f),    // 攻击 红
            new Color(1f, 0.8f, 0.2f)      // 蜂群 黄
        };

        // OnEnable:除首次激活外,Play 中途域重载恢复对象时也会重跑,可重新缓存子物体引用
        void OnEnable()
        {
            var body = transform.Find("Body");
            if (body != null) bodyRenderer = body.GetComponent<Renderer>();
            var tr = transform.Find("Trail");
            if (tr != null) trail = tr.GetComponent<TrailRenderer>();
            selMark = transform.Find("SelectionMark");
        }

        public void Init(DroneKind kind, int id)
        {
            Kind = kind;
            DroneId = $"UAV-{id:D3}";
            switch (kind)
            {
                case DroneKind.Recon:
                    Speed = Random.Range(10f, 13f); HP = 1;
                    RemoteIdCompliant = Random.value > 0.3f;
                    break;
                case DroneKind.Attack:
                    Speed = Random.Range(16f, 20f); HP = 2;
                    RemoteIdCompliant = Random.value > 0.75f;
                    break;
                default:
                    Speed = Random.Range(12f, 15f); HP = 1;
                    RemoteIdCompliant = false;
                    break;
            }
            KillViolation = RemoteIdCompliant;
            if (bodyRenderer != null) bodyRenderer.material.color = kindColor[(int)kind];
        }

        public void SetPhase(float p) => idPhase = p;

        void Update()
        {
            // 锁定指示球显隐
            if (selMark != null) selMark.gameObject.SetActive(GameState.Selected == this);

            float dt = Time.deltaTime;
            switch (State)
            {
                case DroneState.Approaching:
                {
                    Vector3 toC = -transform.position; toC.y = 0f;
                    float dist = toC.magnitude;
                    if (dist < SimConfig.NoFlyRadius) { Detonate(); return; }
                    if (dist > 0.5f)
                    {
                        Vector3 dir = toC / dist;
                        Vector3 targetPos = transform.position + dir * Speed * dt;
                        // 简单侧摆机动
                        targetPos += new Vector3(-dir.z, 0f, dir.x) * Mathf.Sin(Time.time * 1.7f + idPhase) * 2.2f * dt;
                        transform.position = targetPos;
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 4f * dt);
                    }
                    break;
                }
                case DroneState.TurnedBack:
                {
                    Vector3 away = transform.position; away.y = 0f;
                    away.Normalize();
                    transform.position += away * Speed * dt;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(away), 3f * dt);
                    if (new Vector2(transform.position.x, transform.position.z).magnitude > SimConfig.SpawnRadius + 30f)
                        Despawn();
                    break;
                }
                case DroneState.Descending:
                    transform.position += Vector3.down * 9f * dt;
                    transform.Rotate(40f * dt, 25f * dt, 0f, Space.Self);
                    if (transform.position.y <= 0.3f)
                    {
                        State = DroneState.Dead;
                        GameState.OnJamLanded(this);
                        FXManager.I?.Explode(transform.position, 0);
                        Despawn();
                    }
                    break;
                case DroneState.Falling:
                    transform.position += Vector3.down * 26f * dt;
                    transform.Rotate(140f * dt, 60f * dt, 30f * dt, Space.Self);
                    if (transform.position.y <= 0.3f)
                    {
                        State = DroneState.Dead;
                        FXManager.I?.Explode(transform.position, 0);
                        Despawn();
                    }
                    break;
            }
        }

        /// <summary>监管警告:合规机调头,黑飞机无视</summary>
        public void WarnRemoteId()
        {
            if (State != DroneState.Approaching) return;
            if (RemoteIdCompliant)
            {
                State = DroneState.TurnedBack;
                if (trail != null) trail.material.color = new Color(0.4f, 1f, 0.5f);
                GameState.OnTurnedBack(this);
            }
        }

        /// <summary>电磁干扰:约80%概率链路失锁迫降</summary>
        public void ApplyJamming()
        {
            if (State != DroneState.Approaching && State != DroneState.TurnedBack) return;
            if (Random.value < 0.8f)
            {
                State = DroneState.Descending;
                if (trail != null) trail.material.color = new Color(1f, 1f, 0.3f);
                SimEvents.Add($"[干扰] {DroneId} 图传/遥控链路失锁,强制迫降");
            }
        }

        /// <summary>区域信号阻断(模块10):确定性失联迫降(无概率),冰蓝尾迹</summary>
        public void SignalBlocked()
        {
            if (State != DroneState.Approaching && State != DroneState.TurnedBack) return;
            State = DroneState.Descending;
            if (trail != null) trail.material.color = new Color(0.55f, 0.85f, 1f);
            SimEvents.Add($"[阻断] {DroneId} 遭区域信号阻断,链路冻结强制迫降");
        }

        /// <summary>捕获网命中</summary>
        public void Capture()
        {
            if (State == DroneState.Dead || State == DroneState.Captured) return;
            State = DroneState.Captured;
            GameState.OnNetCaptured(this);
            StartCoroutine(FadeOut());
        }

        /// <summary>激光命中一次</summary>
        public void LaserHit()
        {
            if (State != DroneState.Approaching && State != DroneState.TurnedBack) return;
            HP--;
            if (HP <= 0)
            {
                State = DroneState.Falling;
                if (trail != null) trail.material.color = new Color(1f, 0.2f, 0.1f);
                GameState.OnLaserKilled(this);
            }
        }

        /// <summary>闯入核心禁飞区引爆</summary>
        void Detonate()
        {
            if (State == DroneState.Dead) return;
            State = DroneState.Dead;
            FXManager.I?.Explode(transform.position, 1);   // 特效缺失不阻塞判分与回收
            GameState.OnBreach(this);
            Despawn();
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<CoreZone>() != null) Detonate();
        }

        void Despawn()
        {
            Spawner.I?.NotifyRemoved(this);
            Destroy(gameObject);
        }

        System.Collections.IEnumerator FadeOut()
        {
            float t = 0f;
            Vector3 start = transform.position;
            Vector3 end = start + Vector3.down * Mathf.Min(start.y - 0.3f, 8f);
            while (t < 1f)
            {
                t += Time.deltaTime * 0.9f;
                transform.position = Vector3.Lerp(start, end, t);
                if (bodyRenderer != null)
                {
                    var c = bodyRenderer.material.color;
                    c.a = 1f - t;
                    bodyRenderer.material.color = c;
                }
                yield return null;
            }
            Despawn();
        }
    }
}

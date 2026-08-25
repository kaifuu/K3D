using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 要地防御战战斗单元(V4):来袭机(直扑核心,可被击落/冻结/EMP)、
    /// 防御炮塔(自动索敌+曳光弹)、曳光弹特效。行为全部走 DrillClock 门禁。
    /// </summary>
    public class BattleRaider : MonoBehaviour
    {
        public int HP = 3;
        public float Speed = 11f;
        public float Altitude = 20f;
        public int WaveIndex;
        public float Frozen;                 // EMP 冻结剩余秒
        public bool Alive => HP > 0;

        public static System.Action<BattleRaider> OnKilled;   // 由 BattleMode 订阅
        public static System.Action<BattleRaider> OnReached;  // 抵达核心(漏防)

        float phase;
        Renderer bodyRend;
        Color hitFlash = new Color(1f, 1f, 1f, 0f);

        void OnEnable()
        {
            var body = transform.Find("Body");
            if (body != null) bodyRend = body.GetComponent<Renderer>();
            phase = transform.position.x * 0.37f + transform.position.z * 0.11f;
        }

        /// <summary>受到伤害;死亡时回调 OnKilled 并自毁</summary>
        public void TakeDamage(int dmg, Vector3 from)
        {
            if (!Alive) return;
            HP -= dmg;
            hitFlash.a = 1f;
            if (!Alive)
            {
                FXManager.I?.Explode(transform.position, 1);
                OnKilled?.Invoke(this);
                Destroy(gameObject);
            }
        }

        /// <summary>EMP 冻结</summary>
        public void Freeze(float sec) => Frozen = Mathf.Max(Frozen, sec);

        void Update()
        {
            if (!DrillClock.CanSimulate) return;
            float dt = Time.deltaTime;

            // 受击白闪衰减(渲染在 Body 主材质上)
            if (hitFlash.a > 0f && bodyRend != null)
            {
                hitFlash.a = Mathf.Max(0f, hitFlash.a - dt * 6f);
                if (bodyRend.material != null)
                    bodyRend.material.color = Color.Lerp(new Color(1f, 0.32f, 0.22f), Color.white, hitFlash.a);
            }

            if (Frozen > 0f)
            {
                Frozen -= dt;
                transform.position += Vector3.up * Mathf.Sin(Time.time * 9f + phase) * 0.006f;  // 冻结悬停微颤
                return;
            }

            Vector3 toC = -transform.position; toC.y = 0f;
            float dist = toC.magnitude;
            if (dist < 9f)
            {
                OnReached?.Invoke(this);
                FXManager.I?.Explode(transform.position, 2);
                Destroy(gameObject);
                return;
            }
            Vector3 dir = toC / dist;
            Vector3 perp = new Vector3(-dir.z, 0f, dir.x);
            Vector3 step = dir * Speed * dt + perp * Mathf.Sin(DrillClock.SimTime * 1.6f + phase) * 2.4f * dt;

            // 高度剖面:远处保持,近核心压低突防
            float targetAlt = dist > 60f ? Altitude : Mathf.Lerp(7f, Altitude, dist / 60f);
            var p = transform.position + step;
            p.y = Mathf.MoveTowards(p.y, targetAlt, 4f * dt);
            transform.position = p;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(step), 5f * dt);
        }
    }

    /// <summary>防御炮塔:自动索敌最近来袭机,曳光弹点杀;超载模式(射程×/射速×)</summary>
    public class BattleTurret : MonoBehaviour
    {
        public float Range = 46f;
        public float FireInterval = 0.55f;
        public int Damage = 1;
        public float Overdrive;              // 超载剩余秒

        public Transform Head;
        public Vector3 MuzzleOffset = new Vector3(0f, 0.35f, 1.4f);

        float cooldown;
        int shots, hits;

        public int Shots => shots;
        public int Hits => hits;
        public float EffRange => Range * (Overdrive > 0f ? 1.7f : 1f);
        public float EffInterval => FireInterval * (Overdrive > 0f ? 0.5f : 1f);

        void Update()
        {
            if (!DrillClock.CanSimulate) return;
            float dt = Time.deltaTime;
            if (Overdrive > 0f) Overdrive -= dt;
            cooldown -= dt;

            BattleRaider target = null;
            float best = EffRange;
            var raiders = BattleMode.I != null ? BattleMode.I.Raiders : null;
            if (raiders != null)
            {
                for (int i = 0; i < raiders.Count; i++)
                {
                    var r = raiders[i];
                    if (r == null || !r.Alive) continue;
                    float d = Vector3.Distance(transform.position, r.transform.position);
                    if (d < best) { best = d; target = r; }
                }
            }

            if (Head != null && target != null)
            {
                var to = target.transform.position - Head.position;
                Head.rotation = Quaternion.Slerp(Head.rotation,
                    Quaternion.LookRotation(to.normalized), 6f * dt);
            }

            if (target != null && cooldown <= 0f)
            {
                cooldown = EffInterval;
                shots++;
                var from = transform.TransformPoint(MuzzleOffset);
                TracerFX.Spawn(from, target.transform.position,
                    Overdrive > 0f ? new Color(0.5f, 0.9f, 1f) : new Color(1f, 0.85f, 0.4f));
                // 命中判定:目标仍在有效射程内即命中(确定性,无随机散布)
                if (Vector3.Distance(transform.position, target.transform.position) <= EffRange + 2f)
                {
                    hits++;
                    target.TakeDamage(Damage, from);
                }
            }
        }
    }

    /// <summary>曳光弹:两点连线,短命自毁(全部 Unlit 语义色,像素可验)</summary>
    public static class TracerFX
    {
        static readonly List<Tracer> live = new List<Tracer>();

        public static int LiveCount => live.Count;

        public static void Spawn(Vector3 from, Vector3 to, Color color)
        {
            var go = new GameObject("Tracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = 0.09f;
            lr.endWidth = 0.03f;
            lr.material = EnvironmentBuilder.UnlitMat(color);
            var t = go.AddComponent<Tracer>();
            t.Life = 0.07f;
            live.Add(t);
        }

        /// <summary>等待全部曳光弹结束(截图前清屏用,当前为辅助 API)</summary>
        public static void ClearAll()
        {
            for (int i = live.Count - 1; i >= 0; i--)
                if (live[i] != null) Object.Destroy(live[i].gameObject);
            live.Clear();
        }

        class Tracer : MonoBehaviour
        {
            public float Life;
            void Update()
            {
                Life -= Time.deltaTime;
                if (Life <= 0f) Destroy(gameObject);
            }
            void OnDestroy() => live.Remove(this);
        }
    }
}

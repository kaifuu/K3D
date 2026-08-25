using System.Collections;
using UnityEngine;

namespace DroneSim
{
    /// <summary>特效管理:爆炸、干扰波、捕获网、激光束(P5/P8 扩展烟雾/火焰/声波)</summary>
    public class FXManager : MonoBehaviour
    {
        public static FXManager I { get; private set; }

        // 用 OnEnable 而非 Awake:Play 中途若发生域重载(脚本重编译),
        // 对象反序列化恢复后 OnEnable 会重新执行,静态单例得以重新注册
        void OnEnable() => I = this;

        public void Explode(Vector3 pos, int scale)
        {
            var go = new GameObject("Explosion");
            go.transform.position = pos;
            go.AddComponent<ExplosionFX>().Init(scale == 0 ? 6f : 14f);
            Destroy(go, 2.2f);
            if (scale > 0)
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(ring.GetComponent<Collider>());
                ring.name = "ShockRing";
                ring.transform.position = pos;
                ring.transform.localScale = new Vector3(2f, 0.05f, 2f);
                StartCoroutine(GrowRing(ring.transform));
            }
        }

        IEnumerator GrowRing(Transform ring)
        {
            float t = 0f;
            var mat = new Material(Shader.Find("Sprites/Default"));
            while (ring != null && t < 1f)
            {
                t += Time.deltaTime;
                float s = Mathf.Lerp(2f, 40f, t);
                if (ring != null) ring.localScale = new Vector3(s, 0.05f, s);
                yield return null;
            }
            if (ring != null) Destroy(ring.gameObject);
        }

        public void JamBurst(Vector3 from, Vector3 to)
        {
            var go = new GameObject("JamBurst");
            go.transform.position = from;
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = 0.4f; lr.endWidth = 2.5f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.useWorldSpace = true;
            StartCoroutine(FadeBeam(lr, 0.4f, new Color(1f, 1f, 0.3f), new Color(1f, 0.6f, 0f)));
        }

        /// <summary>手动激光光束</summary>
        public void LaserBeam(Vector3 from, Vector3 to)
        {
            var go = new GameObject("LaserBeam");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = 0.12f; lr.endWidth = 0.35f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.useWorldSpace = true;
            StartCoroutine(FadeBeam(lr, 0.22f, new Color(1f, 0.25f, 0.2f), new Color(1f, 0.55f, 0.3f)));
        }

        IEnumerator FadeBeam(LineRenderer lr, float dur, Color c0, Color c1)
        {
            float t = 0f;
            while (t < 1f && lr != null)
            {
                t += Time.deltaTime / dur;
                float a = 1f - t;
                lr.startColor = new Color(c0.r, c0.g, c0.b, a);
                lr.endColor = new Color(c1.r, c1.g, c1.b, a * 0.4f);
                yield return null;
            }
            if (lr != null) Destroy(lr.gameObject);
        }

        /// <summary>火场特效:火焰 + 烟柱(错层)成组建到 parent 下,返回火焰系统(供停止)</summary>
        public ParticleSystem FireAt(Transform parent, Vector3 pos, float scale = 2f)
        {
            var go = new GameObject("FireFX");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            var fire = VFXKit.MakeFire(go.transform);
            var smoke = VFXKit.MakeSmoke(go.transform);
            smoke.transform.localPosition = new Vector3(0f, 2.2f, 0f);   // 烟从火舌上方接续
            var smr = smoke.GetComponent<ParticleSystemRenderer>();
            if (smr != null)                                             // 深灰褐烟,日雾下保持对比
                smr.material.color = new Color(0.52f, 0.47f, 0.43f, 0.8f);
            VFXKit.SetRate(fire, 130f);
            VFXKit.SetRate(smoke, 70f);
            fire.Play();
            smoke.Play();
            return fire;
        }

        /// <summary>网格火焰核心(兜底可见火舌):橙红蛋形 + 亮黄内舌,由 FireSite 脉动缩放</summary>
        public Transform BuildFlameCore(Transform parent)
        {
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            if (core.GetComponent<Collider>() != null) Destroy(core.GetComponent<Collider>());
            core.name = "FlameCore";
            core.transform.SetParent(parent, false);
            core.transform.localPosition = new Vector3(0f, 4.5f, 0f);
            core.transform.localScale = new Vector3(8f, 11f, 8f);
            core.GetComponent<Renderer>().material = EnvironmentBuilder.UnlitMat(new Color(1f, 0.5f, 0.08f, 0.95f));

            var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            if (tip.GetComponent<Collider>() != null) Destroy(tip.GetComponent<Collider>());
            tip.name = "FlameTip";
            tip.transform.SetParent(core.transform, false);
            tip.transform.localPosition = new Vector3(0f, 0.64f, 0f);
            tip.transform.localScale = new Vector3(0.9f, 1.2f, 0.9f);
            tip.GetComponent<Renderer>().material = EnvironmentBuilder.UnlitMat(new Color(1f, 0.88f, 0.25f, 0.97f));
            return core.transform;
        }

        /// <summary>空中喊话声波:数道青色扩散环自机体铺开(3D,批处理截图可见)</summary>
        public void SoundWave(Vector3 origin, int waves = 3, float maxRadius = 34f)
        {
            StartCoroutine(WaveAnim(origin, waves, maxRadius));
        }

        IEnumerator WaveAnim(Vector3 origin, int waves, float maxRadius)
        {
            for (int i = 0; i < waves; i++)
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                if (ring.GetComponent<Collider>() != null) Destroy(ring.GetComponent<Collider>());
                ring.name = "SoundWave";
                ring.transform.position = origin + Vector3.down * 0.6f;
                ring.transform.localScale = new Vector3(2f, 0.05f, 2f);
                var mat = EnvironmentBuilder.UnlitMat(new Color(0.35f, 0.95f, 1f, 0.55f));
                ring.GetComponent<Renderer>().material = mat;
                float t = 0f;
                while (ring != null && t < 1f)
                {
                    t += Time.deltaTime / 1.5f;
                    float rr = Mathf.Lerp(2f, maxRadius, t);
                    if (ring != null)
                    {
                        ring.transform.localScale = new Vector3(rr, 0.05f, rr);
                        mat.SetColor("_Color", new Color(0.35f, 0.95f, 1f, 0.55f * (1f - t)));
                    }
                    yield return null;
                }
                if (ring != null) Destroy(ring);
                yield return new WaitForSeconds(0.4f);
            }
        }

        /// <summary>落点扬尘:一次性迸发,数秒后自毁</summary>
        public void DustPuff(Vector3 pos)
        {
            var go = new GameObject("DustPuff");
            go.transform.position = pos;
            var ps = VFXKit.MakePuff(go.transform);
            ps.Emit(26);
            Destroy(go, 2.6f);
        }

        public void NetShot(Vector3 from, Vector3 to, EnemyDrone target) =>
            StartCoroutine(NetFlight(from, to, target));

        IEnumerator NetFlight(Vector3 from, Vector3 to, EnemyDrone target)
        {
            var net = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(net.GetComponent<Collider>());
            net.name = "Net";
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(1f, 1f, 1f, 0.5f);
            net.GetComponent<Renderer>().material = mat;
            float t = 0f, dur = Mathf.Max(0.12f, Vector3.Distance(from, to) / 90f);
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                if (target != null) to = target.transform.position;
                if (net != null)
                {
                    net.transform.position = Vector3.Lerp(from, to, t);
                    net.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 3f, t);
                }
                yield return null;
            }
            if (target != null) target.Capture();
            if (net != null) Destroy(net);
        }

        /// <summary>简单爆炸:扩散球+衰减</summary>
        class ExplosionFX : MonoBehaviour
        {
            public void Init(float size)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(s.GetComponent<Collider>());
                s.transform.SetParent(transform, false);
                s.name = "Fireball";
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = new Color(1f, 0.55f, 0.1f, 0.9f);
                s.GetComponent<Renderer>().material = mat;
                StartCoroutine(Anim(s.transform, size));
            }

            IEnumerator Anim(Transform fb, float size)
            {
                float t = 0f;
                while (fb != null && t < 1f)
                {
                    t += Time.deltaTime * 1.6f;
                    fb.localScale = Vector3.one * Mathf.Lerp(0.5f, size, t);
                    var r = fb.GetComponent<Renderer>();
                    if (r != null)
                    {
                        var c = r.material.color;
                        c.a = 0.9f * (1f - t);
                        r.material.color = c;
                    }
                    yield return null;
                }
            }
        }
    }

    /// <summary>激光系统:光束烧灼,命中1~2次摧毁</summary>
    public class LaserSystem : MonoBehaviour
    {
        LineRenderer beam;
        float firingUntil;

        void Awake()
        {
            beam = GetComponent<LineRenderer>();
            if (beam == null) beam = gameObject.AddComponent<LineRenderer>();
            beam.enabled = false;
            beam.startWidth = 0.12f; beam.endWidth = 0.35f;
            beam.material = new Material(Shader.Find("Sprites/Default"));
            beam.startColor = new Color(1f, 0.2f, 0.2f);
            beam.endColor = new Color(1f, 0.5f, 0.3f, 0.4f);
            beam.useWorldSpace = true;
        }

        public void Fire(Vector3 from, Vector3 to)
        {
            firingUntil = Time.time + 0.18f;
            beam.enabled = true;
            beam.positionCount = 2;
            beam.SetPosition(0, from);
            beam.SetPosition(1, to);
        }

        void Update()
        {
            if (beam.enabled && Time.time > firingUntil) beam.enabled = false;
        }
    }
}

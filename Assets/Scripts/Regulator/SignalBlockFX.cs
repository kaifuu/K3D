using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 区域信号阻断特效(模块10):蓝白阻断穹顶扩散 + 地面冲击环,1.6s 自毁;
    /// 被冻结目标尾迹转冰蓝。静态 Play 即插即用。
    /// </summary>
    public class SignalBlockFX : MonoBehaviour
    {
        public static void Play(Vector3 center, float radius)
        {
            var go = new GameObject("SignalBlockFX");
            go.transform.position = center;
            var fx = go.AddComponent<SignalBlockFX>();
            fx.radius = radius;
        }

        float radius = 70f, t;
        Transform dome, ring;
        Material domeMat, ringMat;

        void Start()
        {
            dome = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            if (dome.GetComponent<Collider>() != null) Destroy(dome.GetComponent<Collider>());
            dome.name = "Dome";
            dome.SetParent(transform, false);
            dome.localPosition = Vector3.zero;
            domeMat = EnvironmentBuilder.UnlitMat(new Color(0.45f, 0.75f, 1f, 0.4f));
            dome.GetComponent<Renderer>().material = domeMat;

            ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            if (ring.GetComponent<Collider>() != null) Destroy(ring.GetComponent<Collider>());
            ring.name = "Ring";
            ring.SetParent(transform, false);
            ring.localPosition = Vector3.down * (transform.position.y - 0.1f);
            ringMat = EnvironmentBuilder.UnlitMat(new Color(0.5f, 0.85f, 1f, 0.55f));
            ring.GetComponent<Renderer>().material = ringMat;
        }

        void Update()
        {
            t += Time.deltaTime / 1.6f;
            if (t >= 1f || dome == null) { Destroy(gameObject); return; }
            float s = Mathf.Lerp(6f, radius, Mathf.Sqrt(t));
            dome.localScale = Vector3.one * s;
            domeMat.color = new Color(0.45f, 0.75f, 1f, 0.4f * (1f - t));
            ring.localScale = new Vector3(s, 0.06f, s);
            ringMat.color = new Color(0.5f, 0.85f, 1f, 0.55f * (1f - t));
        }
    }
}

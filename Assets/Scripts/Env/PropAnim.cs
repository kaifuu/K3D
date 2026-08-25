using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 场景动效组件库(V4):让实景"活"起来 —— 雷达搜索扫掠 / 警灯闪烁 /
    /// 风袋摆动 / 旗帜飘动 / 探照灯扫掠。全部 unscaled 时间驱动,
    /// Setup/暂停/回放态也在动(纯氛围,不参与任何行为断言)。
    /// </summary>
    public static class PropAnim
    {
        /// <summary>绕 Y 轴往复扫掠(雷达/探照灯:正弦扫角)</summary>
        public static SweepAt Sweep(Transform t, float amplitudeDeg, float period, float phase = 0f)
        {
            var s = t.gameObject.AddComponent<SweepAt>();
            s.AmplitudeDeg = amplitudeDeg;
            s.Period = period;
            s.Phase = phase;
            return s;
        }

        /// <summary>渲染器周期闪烁(警灯/告警灯)</summary>
        public static BlinkAt Blink(Renderer r, float onSec, float offSec, float phase = 0f)
        {
            var b = r.gameObject.AddComponent<BlinkAt>();
            b.Target = r;
            b.OnSec = onSec;
            b.OffSec = offSec;
            b.Phase = phase;
            return b;
        }

        /// <summary>绕 Y 轴小幅摆动(风袋/风向标)</summary>
        public static SwayAt Sway(Transform t, float amplitudeDeg, float period, float phase = 0f)
        {
            var s = t.gameObject.AddComponent<SwayAt>();
            s.AmplitudeDeg = amplitudeDeg;
            s.Period = period;
            s.Phase = phase;
            return s;
        }

        /// <summary>旗帜:绕挂杆轴波动 + 轻微展宽呼吸</summary>
        public static FlagWave Flag(Transform t, float period = 1.6f)
        {
            var f = t.gameObject.AddComponent<FlagWave>();
            f.Period = period;
            return f;
        }
    }

    /// <summary>正弦扫掠:rotation.y = base + sin(t) * amplitude</summary>
    public class SweepAt : MonoBehaviour
    {
        public float AmplitudeDeg = 110f;
        public float Period = 14f;
        public float Phase;
        float baseYaw;
        void Awake() { baseYaw = transform.localEulerAngles.y; }
        void Update()
        {
            float t = Time.unscaledTime / Mathf.Max(0.1f, Period) + Phase;
            float yaw = baseYaw + Mathf.Sin(t * Mathf.PI * 2f) * AmplitudeDeg;
            var e = transform.localEulerAngles;
            e.y = yaw;
            transform.localEulerAngles = e;
        }
    }

    /// <summary>渲染器开关闪烁</summary>
    public class BlinkAt : MonoBehaviour
    {
        public Renderer Target;
        public float OnSec = 0.12f;
        public float OffSec = 0.85f;
        public float Phase;
        void Update()
        {
            if (Target == null) return;
            float cycle = Mathf.Repeat(Time.unscaledTime + Phase, OnSec + OffSec);
            Target.enabled = cycle < OnSec;
        }
    }

    /// <summary>小幅正弦摆动(叠加在初始朝向上)</summary>
    public class SwayAt : MonoBehaviour
    {
        public float AmplitudeDeg = 28f;
        public float Period = 5f;
        public float Phase;
        float baseYaw;
        void Awake() { baseYaw = transform.localEulerAngles.y; }
        void Update()
        {
            float t = Time.unscaledTime / Mathf.Max(0.1f, Period) + Phase;
            float yaw = baseYaw + Mathf.Sin(t * Mathf.PI * 2f) * AmplitudeDeg;
            var e = transform.localEulerAngles;
            e.y = yaw;
            transform.localEulerAngles = e;
        }
    }

    /// <summary>旗帜波动:挂点固定,末端上下摆 + 绕杆小幅旋转</summary>
    public class FlagWave : MonoBehaviour
    {
        public float Period = 1.6f;
        Quaternion baseRot;
        Vector3 baseScale;
        void Awake()
        {
            baseRot = transform.localRotation;
            baseScale = transform.localScale;
        }
        void Update()
        {
            float t = Time.unscaledTime / Mathf.Max(0.1f, Period);
            float w = Mathf.Sin(t * Mathf.PI * 2f);
            transform.localRotation = baseRot * Quaternion.Euler(0f, w * 6f, w * 4f);
            transform.localScale = new Vector3(baseScale.x, baseScale.y * (1f + w * 0.05f), baseScale.z);
        }
    }
}

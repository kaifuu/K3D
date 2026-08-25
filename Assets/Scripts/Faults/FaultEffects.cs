using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 故障视觉表征(模块8):挂在机体上的示警组合。
    /// GPS干扰→红环快转闪;低电量→琥珀环呼吸;电机故障→灰烟尾迹+灰环;
    /// 陀螺漂移→紫双环反转。Dispose 全量回收。
    /// </summary>
    public class FaultEffects : MonoBehaviour
    {
        public FlightBody Body;

        Transform fxRoot;
        readonly List<Transform> rings = new List<Transform>(2);
        readonly List<float> spins = new List<float>(2);
        readonly List<Material> mats = new List<Material>(2);
        readonly List<Color> baseCols = new List<Color>(2);

        public bool Showing { get; private set; }

        public void Show(FaultKind k)
        {
            Dispose();
            if (Body == null || k == FaultKind.None) return;
            var root = new GameObject("FaultFX");
            root.transform.SetParent(Body.transform, false);
            fxRoot = root.transform;
            switch (k)
            {
                case FaultKind.GpsJam:
                    Ring(new Color(1f, 0.25f, 0.2f, 0.6f), 2.6f, 1.1f, 160f, 6f);
                    break;
                case FaultKind.LowBattery:
                    Ring(new Color(1f, 0.7f, 0.1f, 0.65f), 2.2f, 0.9f, -70f, 2f);
                    break;
                case FaultKind.MotorFault:
                    Smoke();
                    Ring(new Color(0.62f, 0.62f, 0.62f, 0.5f), 2f, 1.4f, 60f, 0f);
                    break;
                case FaultKind.GyroDrift:
                    Ring(new Color(0.72f, 0.4f, 1f, 0.6f), 2.4f, 2f, 120f, 0f);
                    Ring(new Color(0.72f, 0.4f, 1f, 0.42f), 3.6f, 2.8f, -85f, 0f);
                    break;
            }
            Showing = true;
        }

        void Ring(Color c, float radius, float height, float spinDeg, float blinkHz)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            if (ring.GetComponent<Collider>() != null) Destroy(ring.GetComponent<Collider>());
            ring.name = "FaultRing";
            ring.transform.SetParent(fxRoot, false);
            ring.transform.localPosition = new Vector3(0f, height, 0f);
            ring.transform.localScale = new Vector3(radius, 0.04f, radius);
            var mat = EnvironmentBuilder.UnlitMat(c);
            ring.GetComponent<Renderer>().material = mat;
            rings.Add(ring.transform);
            spins.Add(spinDeg);
            mats.Add(mat);
            baseCols.Add(new Color(c.r, c.g, c.b, blinkHz > 0f ? c.a : 0f));   // blinkHz=0 不闪烁
            if (blinkHz <= 0f) return;
            // 闪烁参数编码进色相无关通道:用第二个小环携带频率(简化:只支持两种频率,由 alpha 振荡实现)
            blink0 = blinkHz;   // 见 Update:所有闪烁环共用最高频率,足够示警
        }

        float blink0;

        void Smoke()
        {
            var go = new GameObject("FaultSmoke");
            go.transform.SetParent(fxRoot, false);
            go.transform.localPosition = new Vector3(0.9f, 0.2f, 0f);   // 2号电机方位
            var ps = VFXKit.MakeSmoke(go.transform);
            VFXKit.SetRate(ps, 26f);
            var smr = go.GetComponent<ParticleSystemRenderer>();
            if (smr != null) smr.material.color = new Color(0.45f, 0.44f, 0.44f, 0.7f);
        }

        void Update()
        {
            if (fxRoot == null || !DrillClock.CanSimulate) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < rings.Count; i++)
                if (rings[i] != null) rings[i].Rotate(0f, spins[i] * dt, 0f, Space.Self);
            if (blink0 > 0f && mats.Count > 0)
            {
                float a = 0.65f + 0.35f * Mathf.Sin(DrillClock.SimTime * blink0 * Mathf.PI * 2f);
                for (int i = 0; i < mats.Count; i++)
                    if (baseCols[i].a > 0f && mats[i] != null)
                        mats[i].color = new Color(baseCols[i].r, baseCols[i].g, baseCols[i].b, baseCols[i].a * a);
            }
        }

        public void Dispose()
        {
            if (fxRoot != null) Destroy(fxRoot.gameObject);
            fxRoot = null;
            rings.Clear(); spins.Clear(); mats.Clear(); baseCols.Clear();
            blink0 = 0f;
            Showing = false;
        }

        void OnDestroy() => Dispose();
    }
}

using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 对抗告警特效:核心防御区 3D 警戒环(红方进界时脉冲放大+提亮)、
    /// 进界红色暗角脉冲、命中瞬间全屏红闪。暂停安全(SimTime 驱动)。
    /// </summary>
    public class InterceptWarningFX : MonoBehaviour
    {
        public RedIntruderAI Red;
        public float ZoneRadius = 30f;

        Material ringMat;
        bool breachPublished;
        float hitFlash;

        /// <summary>在防御区边界建 24 段警戒环(挂 parent 下)</summary>
        public void InitZone(Transform parent, float radius)
        {
            ZoneRadius = radius;
            var ringGo = new GameObject("ZoneRing");
            ringGo.transform.SetParent(parent, false);
            ringMat = EnvironmentBuilder.UnlitMat(new Color(1f, 0.25f, 0.15f, 0.3f));
            for (int s = 0; s < 24; s++)
            {
                float a0 = s / 24f * Mathf.PI * 2f;
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (seg.GetComponent<Collider>() != null) Destroy(seg.GetComponent<Collider>());
                seg.name = "Seg";
                seg.transform.SetParent(ringGo.transform, false);
                seg.transform.localPosition = new Vector3(Mathf.Cos(a0) * radius, 0.06f, Mathf.Sin(a0) * radius);
                seg.transform.localRotation = Quaternion.Euler(0f, -a0 * Mathf.Rad2Deg + 90f, 0f);
                seg.transform.localScale = new Vector3(2.4f, 0.07f, 0.35f);
                seg.GetComponent<Renderer>().material = ringMat;
            }
        }

        /// <summary>命中全屏红闪(衰减 0.5s)</summary>
        public void FlashHit() => hitFlash = 0.5f;

        void Update()
        {
            if (!DrillClock.CanSimulate) return;
            float dt = Time.deltaTime;

            // ---- 命中红闪衰减 ----
            if (hitFlash > 0f)
            {
                hitFlash -= dt;
                Overlay.Vignette(new Color(1f, 0.2f, 0.1f), Mathf.Clamp01(hitFlash / 0.5f) * 0.5f);
            }

            if (Red == null) return;

            // ---- 红方进界:警戒环脉冲 + 暗角 + 一次性事件 ----
            float d = new Vector2(Red.transform.position.x, Red.transform.position.z).magnitude;
            bool inside = d < ZoneRadius && Red.Phase != RedPhase.Hit && Red.Phase != RedPhase.Escaped;
            if (inside)
            {
                if (!breachPublished)
                {
                    breachPublished = true;
                    EventBus.Publish("对抗", name, "警告:红方进入核心防御区!", EventGrade.Critical);
                }
                float pulse = 0.5f + 0.5f * Mathf.Sin(DrillClock.SimTime * 5f);
                if (ringMat != null)
                {
                    ringMat.SetColor("_Color", new Color(1f, 0.25f + 0.35f * pulse, 0.15f, 0.3f + 0.35f * pulse));
                    ringMat.renderQueue = 2500;
                }
                Overlay.Vignette(new Color(1f, 0.15f, 0.1f), 0.08f + 0.07f * pulse);
            }
            else if (ringMat != null && breachPublished)
            {
                ringMat.SetColor("_Color", new Color(1f, 0.25f, 0.15f, 0.3f));
            }
        }
    }
}

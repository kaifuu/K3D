using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 喊话驱离(挂载机):Broadcast → 声波扩散环(3D)+
    /// 半径内平民撤离 / 黑飞调头;次数计数。
    /// </summary>
    public class SpeakerDeter : MonoBehaviour
    {
        public float Range = 40f;
        public int Broadcasts { get; private set; }
        public readonly List<CivilianTarget> Civilians = new List<CivilianTarget>();
        public IntruderAlert Intruder;

        public void Broadcast()
        {
            Broadcasts++;
            FXManager.I?.SoundWave(transform.position);
            EventBus.Publish("应急", name,
                $"第 {Broadcasts} 次空中喊话:这里是管制空域,请立即离开!", EventGrade.Op);

            var p = transform.position;
            int moved = 0;
            foreach (var civ in Civilians)
            {
                if (civ == null) continue;
                if (HorizDist(p, civ.transform.position) < Range)
                {
                    civ.StartFlee(p);
                    moved++;
                }
            }
            if (Intruder != null && Intruder.Active && !Intruder.Deterred
                && HorizDist(p, Intruder.transform.position) < Range)
                Intruder.Deter();
        }

        static float HorizDist(Vector3 a, Vector3 b) =>
            Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }

    /// <summary>
    /// 地面人员目标:头顶状态环(黄=滞留 亮黄=撤离中 绿=安全);
    /// StartFlee 沿远离禁区中心方向撤离,轻微蛇形;走满 18m 判安全。
    /// </summary>
    public class CivilianTarget : MonoBehaviour
    {
        public enum CivState { Idle, Fleeing, Safe }
        public CivState State { get; private set; }
        public float FleeSpeed = 1.25f;
        public float Traveled { get; private set; }

        Vector3 fleeDir;
        Transform marker;

        public void BuildMarker()
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            if (ring.GetComponent<Collider>() != null) Destroy(ring.GetComponent<Collider>());
            ring.name = "CivMark";
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, 2.0f, 0f);
            ring.transform.localScale = new Vector3(1.1f, 0.03f, 1.1f);
            marker = ring.transform;
            SetMark(new Color(1f, 0.85f, 0.2f, 0.85f));
        }

        void SetMark(Color c) => marker.GetComponent<Renderer>().material = EnvironmentBuilder.UnlitMat(c);

        /// <summary>开始撤离:方向 = 远离禁区中心(威胁方位仅作事件记录)</summary>
        public void StartFlee(Vector3 threat)
        {
            if (State != CivState.Idle) return;
            State = CivState.Fleeing;
            var pos = transform.position;
            fleeDir = new Vector3(pos.x, 0f, pos.z).normalized;   // 场地中心即禁区中心
            if (fleeDir.sqrMagnitude < 0.01f) fleeDir = Vector3.forward;
            SetMark(new Color(1f, 1f, 0.35f, 0.95f));
            EventBus.Publish("应急", name, "地面人员听到警示,开始向安全方向撤离", EventGrade.Info);
        }

        void Update()
        {
            if (!DrillClock.CanSimulate) return;
            if (marker != null) marker.Rotate(0f, 60f * Time.deltaTime, 0f, Space.Self);
            if (State != CivState.Fleeing) return;

            float dt = Time.deltaTime;
            var step = fleeDir * FleeSpeed * dt;
            var sway = new Vector3(-fleeDir.z, 0f, fleeDir.x) * (Mathf.Sin(DrillClock.SimTime * 3f + Traveled * 0.5f) * 0.35f);
            transform.position += step + sway * dt;
            Traveled += step.magnitude;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fleeDir), dt * 5f);

            if (Traveled > 18f)
            {
                State = CivState.Safe;
                SetMark(new Color(0.35f, 1f, 0.5f, 0.95f));
                EventBus.Publish("应急", name, "人员已抵达安全区域", EventGrade.Info);
            }
        }
    }
}

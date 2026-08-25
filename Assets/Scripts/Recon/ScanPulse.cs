using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 扇形扫描波:绕机体匀速旋转的波束(亮线+半透明扇面+拖尾波纹),
    /// 波束掠过且在半径内的未识别目标即被识别;每满 360° 上报一轮战果。
    /// </summary>
    public class ScanPulse : MonoBehaviour
    {
        public Transform Origin;
        public float Radius = 160f;
        public float BeamHalfDeg = 17f;
        public float RateDeg = 22f;

        public bool Scanning { get; private set; }
        public float Yaw { get; private set; }          // 0°=+Z,顺 atan2(dx,dz) 方向
        public float Swept { get; private set; }        // 累计扫过角度
        public int IdentifiedCount { get; private set; }

        readonly List<ScannableTarget> targets = new List<ScannableTarget>(16);
        LineRenderer beam, trail;
        MeshRenderer fanR, trailFanR;
        Transform fan, trailFan;

        public void Setup(Transform origin, IEnumerable<ScannableTarget> scanTargets)
        {
            Origin = origin;
            targets.Clear();
            targets.AddRange(scanTargets);

            // 波束亮线
            beam = NewLine("Beam", new Color(0.3f, 1f, 0.9f, 0.85f), 0.45f);
            trail = NewLine("BeamTrail", new Color(0.3f, 1f, 0.9f, 0.3f), 0.3f);

            // 扇面(单位半径,GO 缩放即实际半径)
            fan = MakeFan("Fan", new Color(0.3f, 1f, 0.9f, 0.13f), out fanR);
            trailFan = MakeFan("TrailFan", new Color(0.3f, 1f, 0.9f, 0.06f), out trailFanR);
            ApplyVisual(0f);
        }

        public void StartScan()
        {
            Scanning = true;
            EventBus.Publish("侦察", "scan", "启动扇形扫描", EventGrade.Op);
        }

        public void StopScan()
        {
            Scanning = false;
            beam.gameObject.SetActive(false);
            trail.gameObject.SetActive(false);
            fan.gameObject.SetActive(false);
            trailFan.gameObject.SetActive(false);
        }

        public void ResetAll()
        {
            foreach (var t in targets) t.ResetScan();
            Yaw = 0f; Swept = 0f;
        }

        void Update()
        {
            if (!Scanning || Origin == null || !DrillClock.CanSimulate) return;
            float dt = Time.deltaTime;
            Yaw += RateDeg * dt;
            Swept += RateDeg * dt;

            // 满 360° 汇总一轮
            if (Swept >= 360f)
            {
                Swept -= 360f;
                IdentifiedCount = CountIdentified();
                EventBus.Publish("侦察", "scan", $"扫描一周完成:识别 {IdentifiedCount}/{targets.Count}", EventGrade.Op);
            }

            // 识别判定:方位角落进波束张角 且 在探测半径内
            float yawRad = Yaw * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
            var o = Origin.position;
            foreach (var t in targets)
            {
                if (t == null || t.Identified) continue;
                var d = t.transform.position - o;
                d.y = 0f;
                if (d.magnitude > Radius) continue;
                float bearing = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                if (Mathf.Abs(Mathf.DeltaAngle(bearing, Yaw)) <= BeamHalfDeg) t.Identify();
            }

            ApplyVisual(dt);
        }

        void ApplyVisual(float dt)
        {
            if (beam == null || Origin == null) return;
            bool on = Scanning;
            beam.gameObject.SetActive(on);
            trail.gameObject.SetActive(on);
            fan.gameObject.SetActive(on);
            trailFan.gameObject.SetActive(on);
            if (!on) return;

            var o = Origin.position + Vector3.up * 0.4f;
            float yawRad = Yaw * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
            beam.SetPosition(0, o);
            beam.SetPosition(1, o + dir * Radius);

            float tYaw = Yaw - BeamHalfDeg * 1.6f;
            float tr = tYaw * Mathf.Deg2Rad;
            var tdir = new Vector3(Mathf.Sin(tr), 0f, Mathf.Cos(tr));
            trail.SetPosition(0, o);
            trail.SetPosition(1, o + tdir * Radius * 0.96f);

            fan.position = o;
            fan.rotation = Quaternion.Euler(0f, Yaw, 0f);
            fan.localScale = new Vector3(Radius, 1f, Radius);
            trailFan.position = o;
            trailFan.rotation = Quaternion.Euler(0f, tYaw, 0f);
            trailFan.localScale = new Vector3(Radius * 0.96f, 1f, Radius * 0.96f);
        }

        int CountIdentified()
        {
            int n = 0;
            foreach (var t in targets) if (t != null && t.Identified) n++;
            return n;
        }

        LineRenderer NewLine(string name, Color c, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = width;
            lr.endWidth = width * 0.4f;
            lr.useWorldSpace = true;
            lr.material = EnvironmentBuilder.UnlitMat(c);
            return lr;
        }

        Transform MakeFan(string name, Color c, out MeshRenderer mr)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            const int seg = 10;
            var half = BeamHalfDeg * Mathf.Deg2Rad;
            var verts = new Vector3[seg + 2];
            verts[0] = Vector3.zero;
            for (int i = 0; i <= seg; i++)
            {
                float a = -half + 2f * half * i / seg;
                verts[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));   // 单位半径
            }
            var tris = new int[seg * 3];
            for (int i = 0; i < seg; i++)
            { tris[i * 3] = 0; tris[i * 3 + 1] = i + 2; tris[i * 3 + 2] = i + 1; }

            var mesh = new Mesh { vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = EnvironmentBuilder.UnlitMat(c);
            return go.transform;
        }
    }
}

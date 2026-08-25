using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 航线数据核:有序航点折线 + 累计里程缓存。
    /// Sample(d) 沿线取样(流光/胡萝卜点),ProjectDistance 投影求最近点(偏差/续飞)。
    /// Revision 在任何修改时自增,视觉层据此增量重建。
    /// </summary>
    public class RouteData
    {
        readonly List<Vector3> pts = new List<Vector3>(16);
        float[] cum = new float[1];      // cum[i] = 起点→pts[i] 里程
        float total;
        bool dirty = true;

        /// <summary>闭环航线(巡航绕圈,首尾自动闭合)</summary>
        public bool Loop;

        public int Count => pts.Count;
        public IReadOnlyList<Vector3> Points => pts;
        public int Revision { get; private set; }

        public float TotalLength { get { Ensure(); return total; } }

        // ---------- 编辑操作(全部 Touch 触发里程重算与视觉重建) ----------
        public void Add(Vector3 p) { pts.Add(p); Touch(); }
        public void Insert(int i, Vector3 p) { pts.Insert(Mathf.Clamp(i, 0, pts.Count), p); Touch(); }
        public void RemoveAt(int i) { if (i >= 0 && i < pts.Count) { pts.RemoveAt(i); Touch(); } }
        public void Move(int i, Vector3 p) { if (i >= 0 && i < pts.Count) { pts[i] = p; Touch(); } }
        public void Clear() { pts.Clear(); Touch(); }
        public Vector3 Get(int i) => pts[Mathf.Clamp(i, 0, pts.Count - 1)];
        public void RemoveLast() => RemoveAt(pts.Count - 1);

        void Touch() { dirty = true; Revision++; }

        void Ensure()
        {
            if (!dirty) return;
            int n = pts.Count;
            if (cum.Length < n) cum = new float[n];
            for (int i = 1; i < n; i++)
                cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            total = 0f;
            if (n == 1) total = 0f;
            else if (n >= 2)
                total = Loop ? cum[n - 1] + Vector3.Distance(pts[n - 1], pts[0]) : cum[n - 1];
            dirty = false;
        }

        // ---------- 沿线取样 ----------
        /// <summary>里程→世界坐标(闭环取模,开线夹取)</summary>
        public Vector3 Sample(float d)
        {
            Ensure();
            if (pts.Count == 0) return Vector3.zero;
            if (pts.Count == 1 || total <= 0.01f) return pts[0];
            if (Loop) d = Mathf.Repeat(d, total);
            else d = Mathf.Clamp(d, 0f, total);

            int n = pts.Count;
            int segs = Loop ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                int j = (i + 1) % n;
                float segLen = i == n - 1 ? total - cum[i] : cum[j] - cum[i];
                if (d <= cum[i] + segLen)
                {
                    float t = segLen > 1e-5f ? (d - cum[i]) / segLen : 0f;
                    return Vector3.LerpUnclamped(pts[i], pts[j], t);
                }
            }
            return Loop ? pts[0] : pts[n - 1];
        }

        /// <summary>里程→最近航点下标(视觉高亮当前目标用)</summary>
        public int IndexAt(float d)
        {
            Ensure();
            if (pts.Count == 0) return -1;
            if (Loop) d = Mathf.Repeat(d, total);
            else d = Mathf.Clamp(d, 0f, total);
            int n = pts.Count;
            int segs = Loop ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                int j = (i + 1) % n;
                float segLen = i == n - 1 ? total - cum[i] : cum[j] - cum[i];
                if (d <= cum[i] + segLen)
                    return (d - cum[i]) > segLen * 0.5f ? j : i;
            }
            return Loop ? 0 : n - 1;
        }

        // ---------- 投影 ----------
        /// <summary>点到折线最近投影:返回沿线里程(0..total),out 最近点。环路含闭合段。</summary>
        public float ProjectDistance(Vector3 p, out Vector3 nearest)
        {
            Ensure();
            nearest = Vector3.zero;
            int n = pts.Count;
            if (n == 0) return 0f;
            if (n == 1 || total <= 0.01f) { nearest = pts[0]; return 0f; }

            float bestD2 = float.MaxValue, bestAlong = 0f;
            int segs = Loop ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                int j = (i + 1) % n;
                var a = pts[i];
                var ab = pts[j] - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
                var q = a + ab * t;
                float d2 = (p - q).sqrMagnitude;
                if (d2 < bestD2)
                {
                    bestD2 = d2;
                    nearest = q;
                    bestAlong = cum[i] + Mathf.Sqrt(len2) * t;
                }
            }
            return bestAlong;
        }
    }
}

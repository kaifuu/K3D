using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 航点标记集:每航点一枚悬浮呼吸球,Revision 变化时增量重建。
    /// HighlightIndex=当前目标航点(放大金黄),其前序视为已过(暗绿)。
    /// </summary>
    public class WaypointMarker : MonoBehaviour
    {
        public RouteData Route;
        public int HighlightIndex = -1;

        readonly List<Transform> marks = new List<Transform>(16);
        readonly List<Material> mats = new List<Material>(16);
        int builtRev = -1;

        void Update()
        {
            if (Route == null) return;
            if (builtRev != Route.Revision) Rebuild();
            if (marks.Count == 0) return;

            // 呼吸缩放(realtimeSinceStartup:Setup/暂停态也保持活性,不死白)
            float t = Time.realtimeSinceStartup;
            for (int i = 0; i < marks.Count; i++)
            {
                bool hl = i == HighlightIndex;
                bool passed = HighlightIndex >= 0 && i < HighlightIndex;
                float s = (hl ? 1.6f : 1f) * (0.9f + 0.16f * Mathf.Sin(t * 2.2f + i * 0.9f));
                marks[i].localScale = Vector3.one * s;
                Color c = hl ? new Color(1f, 0.82f, 0.25f, 0.95f)
                       : passed ? new Color(0.35f, 0.75f, 0.45f, 0.4f)
                       : new Color(0.3f, 0.85f, 1f, 0.75f);
                mats[i].color = c;
            }
        }

        void Rebuild()
        {
            for (int i = marks.Count - 1; i >= 0; i--) Destroy(marks[i].gameObject);
            marks.Clear();
            mats.Clear();
            if (Route == null) return;

            foreach (var p in Route.Points)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                DestroyCollider(go);
                go.name = "WP";
                go.transform.SetParent(transform, false);
                go.transform.position = p;
                go.transform.localScale = Vector3.one * 0.9f;
                var mat = EnvironmentBuilder.UnlitMat(new Color(0.3f, 0.85f, 1f, 0.75f));
                go.GetComponent<Renderer>().material = mat;
                marks.Add(go.transform);
                mats.Add(mat);
            }
            builtRev = Route.Revision;
        }

        static void DestroyCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }
    }
}

using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 航线视觉:LineRenderer 基线(青色呼吸;超偏告警→红色 4Hz 方波闪烁)
    /// + 7 枚亮色光斑沿线匀速跑动(流光,无 shader 纯位移)。
    /// 基线仅在 Revision 变化时重建(打点/拖点/清空触发)。
    /// </summary>
    public class RouteVisual : MonoBehaviour
    {
        public RouteData Route;
        public bool Alarm;              // 由模式按偏差写入
        public float FlowSpeed = 9f;    // 流光速度 m/s

        const int RunnerCount = 7;
        LineRenderer line;
        Material lineMat;
        readonly Transform[] runners = new Transform[RunnerCount];
        readonly Material[] runnerMats = new Material[RunnerCount];
        int builtRev = -1;

        void Update()
        {
            if (Route == null || Route.Count < 2)
            {
                if (line != null) line.gameObject.SetActive(false);
                for (int i = 0; i < RunnerCount; i++)
                    if (runners[i] != null) runners[i].gameObject.SetActive(false);
                return;
            }
            if (line == null) BuildParts();
            if (builtRev != Route.Revision) RebuildLine();

            // ---- 基线配色:正常呼吸 / 告警红闪 ----
            float t = Time.realtimeSinceStartup;
            var baseCol = Alarm ? new Color(1f, 0.22f, 0.15f) : new Color(0.25f, 0.88f, 1f);
            float a = Alarm
                ? (Mathf.Sin(t * Mathf.PI * 2f * 4f) > 0f ? 0.95f : 0.3f)   // 4Hz 方波
                : 0.35f + 0.18f * Mathf.Sin(t * 1.6f * Mathf.PI * 2f);       // 慢呼吸
            lineMat.color = new Color(baseCol.r, baseCol.g, baseCol.b, a);

            // ---- 流光:沿线里程取点,朝向切线 ----
            float total = Route.TotalLength;
            if (total < 1f) return;
            float flowD = DrillClock.SimTime * FlowSpeed;   // 暂停即冻结,与演练时间一致
            for (int i = 0; i < RunnerCount; i++)
            {
                float d = Mathf.Repeat(flowD + i * total / RunnerCount, total);
                var p = Route.Sample(d);
                var next = Route.Sample(d + 0.9f);
                runners[i].position = p;
                var dir = next - p;
                if (dir.sqrMagnitude > 1e-4f) runners[i].rotation = Quaternion.LookRotation(dir);
                float s = 0.55f + 0.22f * Mathf.Sin(t * 3f * Mathf.PI * 2f + i * 1.1f);
                runners[i].localScale = Vector3.one * s;
                runnerMats[i].color = Alarm
                    ? new Color(1f, 0.5f, 0.3f, 0.95f)
                    : new Color(0.85f, 1f, 1f, 0.9f);
            }
        }

        void BuildParts()
        {
            var lineGo = new GameObject("Line");
            lineGo.transform.SetParent(transform, false);
            line = lineGo.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.45f;
            lineMat = EnvironmentBuilder.UnlitMat(new Color(0.25f, 0.88f, 1f, 0.5f));
            line.material = lineMat;
            line.positionCount = 0;
            line.sortingOrder = 5;

            for (int i = 0; i < RunnerCount; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCollider(go);
                go.name = $"Glow{i}";
                go.transform.SetParent(transform, false);
                var mat = EnvironmentBuilder.UnlitMat(new Color(0.85f, 1f, 1f, 0.9f));
                go.GetComponent<Renderer>().material = mat;
                runners[i] = go.transform;
                runnerMats[i] = mat;
            }
        }

        void RebuildLine()
        {
            int n = Route.Count;
            bool loop = Route.Loop;
            int cnt = loop ? n + 1 : n;
            line.positionCount = cnt;
            for (int i = 0; i < n; i++)
                line.SetPosition(i, Route.Points[i]);
            if (loop) line.SetPosition(n, Route.Points[0]);
            line.loop = loop;
            builtRev = Route.Revision;
        }

        static void DestroyCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }
    }
}

using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 偏差引导箭头:机体偏离航线 >2.5m 时,在 机体↔最近投影点 中点
    /// 显示脉冲放大的橙色 3D 箭头(纯 Cube 拼装),指向航线;恢复后隐藏。
    /// </summary>
    public class DeviationGuide : MonoBehaviour
    {
        public Transform Drone;
        public RouteData Route;
        public RouteFollower Follower;

        Transform arrow;

        void Update()
        {
            bool show = Drone != null && Route != null && Follower != null
                        && Route.Count >= 2 && Follower.Deviation > 2.5f;
            if (arrow == null)
            {
                if (!show) return;
                BuildArrow();
            }

            if (!show) { arrow.gameObject.SetActive(false); return; }
            arrow.gameObject.SetActive(true);

            Route.ProjectDistance(Drone.position, out var nearest);
            var from = Drone.position;
            var mid = (from + nearest) * 0.5f + Vector3.up * 1.4f;
            var dir = nearest - from;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            arrow.position = mid;
            arrow.rotation = Quaternion.LookRotation(dir.normalized);

            float pulse = 1f + 0.16f * Mathf.Sin(Time.realtimeSinceStartup * 6f);
            float len = Mathf.Clamp(Vector3.Distance(from, nearest) * 0.28f, 1f, 2.6f);
            arrow.localScale = Vector3.one * len * pulse;
        }

        void BuildArrow()
        {
            arrow = new GameObject("DeviationArrow").transform;
            arrow.SetParent(transform, false);
            var mat = EnvironmentBuilder.UnlitMat(new Color(1f, 0.6f, 0.15f, 0.85f));

            // 杆(z 向前)+ 双斜片箭头
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCollider(shaft);
            shaft.name = "Shaft";
            shaft.transform.SetParent(arrow, false);
            shaft.transform.localScale = new Vector3(0.16f, 0.16f, 1f);
            shaft.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            shaft.GetComponent<Renderer>().material = mat;

            for (int s = 0; s < 2; s++)
            {
                var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCollider(blade);
                blade.name = $"Head{s}";
                blade.transform.SetParent(arrow, false);
                blade.transform.localScale = new Vector3(0.14f, 0.14f, 0.42f);
                blade.transform.localPosition = new Vector3(0f, 0f, 0.45f);
                blade.transform.localRotation = Quaternion.Euler(0f, s == 0 ? 40f : -40f, 0f);
                blade.GetComponent<Renderer>().material = mat;
            }
        }

        static void DestroyCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }
    }
}

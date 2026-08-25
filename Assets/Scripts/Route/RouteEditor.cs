using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 航线编辑器(纯逻辑类,由模式 OnTick 驱动):
    /// 打点(单击地面加点)/ 手绘(按住左键按 6m 间距采样)/ 拖拽(按住航点球移动)。
    /// Z 撤销末点,X 清空;编辑仅在场内 95m 半径生效,巡航中禁用。
    /// </summary>
    public class RouteEditor
    {
        public enum Tool { Place, Draw, Drag }
        public Tool CurTool = Tool.Place;
        public float WaypointAlt = 18f;
        public bool EditingEnabled = true;

        readonly RouteData route;
        readonly Camera cam;
        readonly WaypointMarker markers;
        int dragIdx = -1;

        public RouteEditor(RouteData route, Camera cam, WaypointMarker markers)
        {
            this.route = route;
            this.cam = cam;
            this.markers = markers;
        }

        public void Tick()
        {
            if (route == null || cam == null) return;

            // 键盘(撤销/清空随时可用)
            if (Input.GetKeyDown(KeyCode.Z) && route.Count > 0)
            {
                route.RemoveLast();
                EventBus.Publish("航线", "", $"撤销航点,剩余 {route.Count} 点", EventGrade.Info);
            }
            if (Input.GetKeyDown(KeyCode.X) && route.Count > 0)
            {
                route.Clear();
                EventBus.Publish("航线", "", "航线已清空", EventGrade.Info);
            }

            if (!EditingEnabled) return;
            if (UIRoot.MouseOverGUI) { dragIdx = -1; return; }

            // 地面射线
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float enter)) return;
            var p = ray.GetPoint(enter);
            if (new Vector2(p.x, p.z).magnitude > 95f) { dragIdx = -1; return; }

            switch (CurTool)
            {
                case Tool.Place:
                    if (Input.GetMouseButtonDown(0)) AddPoint(p);
                    break;

                case Tool.Draw:
                    if (Input.GetMouseButton(0))
                    {
                        bool empty = route.Count == 0;
                        float gap = empty ? float.MaxValue : HorizontalDist(p, route.Get(route.Count - 1));
                        if (empty || gap > 6f) AddPoint(p);   // 手绘:按 6m 水平间距采样
                    }
                    break;

                case Tool.Drag:
                    if (Input.GetMouseButtonDown(0)) dragIdx = NearestWaypoint(p, 4.5f);
                    if (dragIdx >= 0 && Input.GetMouseButton(0))
                    {
                        var wp = route.Get(dragIdx);
                        route.Move(dragIdx, new Vector3(p.x, wp.y, p.z));   // 高度保持,只拖水平
                    }
                    if (Input.GetMouseButtonUp(0)) dragIdx = -1;
                    break;
            }
        }

        void AddPoint(Vector3 ground)
        {
            route.Add(new Vector3(ground.x, WaypointAlt, ground.z));
            EventBus.Publish("航线", "", $"打点 {route.Count} ({ground.x:0},{WaypointAlt:0},{ground.z:0})", EventGrade.Info);
        }

        int NearestWaypoint(Vector3 p, float radius)
        {
            int best = -1;
            float bestD = radius;
            for (int i = 0; i < route.Count; i++)
            {
                float d = HorizontalDist(p, route.Get(i));
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        static float HorizontalDist(Vector3 a, Vector3 b) =>
            new Vector2(a.x - b.x, a.z - b.z).magnitude;
    }
}

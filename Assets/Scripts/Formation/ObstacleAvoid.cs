using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 障碍规避服务:静态登记圆柱障碍(XZ 圆 + 顶高),
    /// 对"当前位置→槽位目标"线段做膨胀圆检测,把目标点沿垂直航迹方向侧推绕行;
    /// 通过后目标恢复原槽,误差自然收敛归位(不改构型、不瞬移)。
    /// </summary>
    public static class ObstacleAvoid
    {
        public struct Obstacle { public Vector3 Pos; public float Radius; public float Top; }

        static readonly List<Obstacle> items = new List<Obstacle>(8);
        public static IReadOnlyList<Obstacle> Items => items;
        /// <summary>累计避让触发次数(绕行激活的帧计数,统计/断言用)</summary>
        public static int AvoidEvents;

        public static void Clear() { items.Clear(); AvoidEvents = 0; }

        public static void AddCylinder(Vector3 basePos, float radius, float top) =>
            items.Add(new Obstacle { Pos = basePos, Radius = radius, Top = top });

        /// <summary>与线路相关:障碍顶高于线路两端较低点-2m(可能挡道)</summary>
        static bool Relevant(Obstacle o, float y0, float y1) => o.Top > Mathf.Min(y0, y1) - 2f;

        /// <summary>线段 from→target 若穿入膨胀圆(safe=半径+margin),
        /// 将 target 沿垂直航迹方向推出,使机体绕行而非穿墙。</summary>
        public static Vector3 AdjustTarget(Vector3 from, Vector3 target, float margin = 1.5f)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var o = items[i];
                if (!Relevant(o, from.y, target.y)) continue;

                var f = new Vector2(from.x, from.z);
                var t = new Vector2(target.x, target.z);
                var c = new Vector2(o.Pos.x, o.Pos.z);
                var d = t - f;
                float len = d.magnitude;
                if (len < 0.01f) continue;
                var dir = d / len;
                float u = Mathf.Clamp01(Vector2.Dot(c - f, dir));   // 投影长度(m)
                var near = f + dir * u;
                float dist = Vector2.Distance(near, c);
                float safe = o.Radius + margin;
                if (dist >= safe) continue;

                AvoidEvents++;
                // 垂直航迹方向,朝远离障碍一侧推(障碍在 perp+ 侧则向 perp- 推)
                var perp = new Vector2(-dir.y, dir.x);
                float side = Vector2.Dot(perp, c - near);
                float s = side >= 0f ? -1f : 1f;
                t += perp * (s * (safe - dist + 1.5f));
                // 目标点仍在膨胀圆内 → 沿径向强推到圆外(保证瞄准点永不在危险区)
                float dtc = Vector2.Distance(t, c);
                if (dtc < safe)
                {
                    var radial = dtc > 0.01f ? (t - c) / dtc : perp * -s;
                    t = c + radial * safe;
                }
                target = new Vector3(t.x, target.y, t.y);
            }
            return target;
        }

        /// <summary>当前位置距最近障碍面的净距(XZ;高于顶+1m 视为无碍返回大值)</summary>
        public static float Clearance(Vector3 pos)
        {
            float min = float.MaxValue;
            for (int i = 0; i < items.Count; i++)
            {
                var o = items[i];
                if (pos.y > o.Top + 1f) continue;
                float d = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(o.Pos.x, o.Pos.z)) - o.Radius;
                if (d < min) min = d;
            }
            return min;
        }

        /// <summary>最近障碍中心(净距<max 时),用于贴脸硬脱出方向</summary>
        public static bool Nearest(Vector3 pos, out Vector2 center, out float radius)
        {
            center = default; radius = 0f;
            float min = float.MaxValue;
            bool found = false;
            for (int i = 0; i < items.Count; i++)
            {
                var o = items[i];
                if (pos.y > o.Top + 1f) continue;
                float d = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(o.Pos.x, o.Pos.z));
                if (d < min) { min = d; center = new Vector2(o.Pos.x, o.Pos.z); radius = o.Radius; found = true; }
            }
            return found;
        }
    }
}

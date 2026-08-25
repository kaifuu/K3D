using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 复盘轨迹线(模块9):全程淡线一次构建 + 已飞亮线随回放游标重建(节流);
    /// 每个记录机体一条,调色板取色。挂在 ReplayService 物体下,退出回放全量回收。
    /// </summary>
    public static class TrajectoryDrawer
    {
        static readonly Dictionary<string, LineRenderer> faint = new Dictionary<string, LineRenderer>(8);
        static readonly Dictionary<string, LineRenderer> bright = new Dictionary<string, LineRenderer>(8);
        static Transform root;
        static float lastBrightT = -999f;

        static readonly Color[] palette =
        {
            new Color(0.35f, 0.95f, 1f), new Color(1f, 0.65f, 0.2f),
            new Color(1f, 0.4f, 0.8f), new Color(0.5f, 1f, 0.5f),
            new Color(1f, 0.9f, 0.3f), new Color(0.7f, 0.6f, 1f),
            new Color(1f, 0.5f, 0.4f), new Color(0.5f, 0.8f, 1f)
        };

        public static int LineCount => faint.Count;

        public static void BuildAll()
        {
            Clear();
            var rs = ReplayService.I;
            if (rs == null || !rs.HasData) return;
            var go = new GameObject("ReplayTrajectories");
            go.transform.SetParent(rs.transform, false);
            root = go.transform;

            var frames = rs.Frames;
            for (int fi = 0; fi < frames.Count; fi += 3)          // 每 3 帧取一点
            {
                var f = frames[fi];
                for (int i = 0; i < f.Names.Length; i++)
                {
                    var lr = FaintLine(f.Names[i], i);
                    var p = f.Samples[i].Pos;
                    lr.positionCount++;
                    lr.SetPosition(lr.positionCount - 1, p);
                }
            }
        }

        public static void UpdateBright(float upToT)
        {
            if (Mathf.Abs(upToT - lastBrightT) < 0.5f) return;    // 节流:游标移动>0.5s 才重建
            lastBrightT = upToT;
            var rs = ReplayService.I;
            if (rs == null) return;
            var frames = rs.Frames;
            foreach (var kv in bright) kv.Value.positionCount = 0;
            for (int fi = 0; fi < frames.Count; fi++)
            {
                var f = frames[fi];
                if (f.T > upToT) break;
                for (int i = 0; i < f.Names.Length; i++)
                {
                    var lr = BrightLine(f.Names[i], i);
                    lr.positionCount++;
                    lr.SetPosition(lr.positionCount - 1, f.Samples[i].Pos);
                }
            }
        }

        static LineRenderer FaintLine(string name, int idx) => GetLine(faint, name, idx, 0.3f, 0.25f);
        static LineRenderer BrightLine(string name, int idx) => GetLine(bright, name, idx, 0.6f, 0.9f);

        static LineRenderer GetLine(Dictionary<string, LineRenderer> store, string name, int idx, float width, float alpha)
        {
            if (idx < 0) idx = 0;
            if (store.TryGetValue(name, out var lr) && lr != null) return lr;
            var go = new GameObject("Traj_" + name);
            go.transform.SetParent(root, false);
            lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = width;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            var c = palette[Mathf.Abs(name.GetHashCode()) % palette.Length];
            lr.startColor = new Color(c.r, c.g, c.b, alpha);
            lr.endColor = new Color(c.r, c.g, c.b, alpha);
            store[name] = lr;
            return lr;
        }

        public static void Clear()
        {
            if (root != null) Object.Destroy(root.gameObject);
            root = null;
            faint.Clear();
            bright.Clear();
            lastBrightT = -999f;
        }
    }
}

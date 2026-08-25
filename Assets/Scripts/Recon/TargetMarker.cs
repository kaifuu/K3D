using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 目标悬浮标注:已识别目标画分类色角括号+名称距离;
    /// 被跟踪目标追加锁定圆环(旋转点阵)。IMGUI 无深度遮挡,接受穿透。
    /// </summary>
    public static class TargetMarker
    {
        public static Color ClassColor(ThermalClass c) => c switch
        {
            ThermalClass.Hot => new Color(1f, 0.5f, 0.2f),
            ThermalClass.Warm => new Color(1f, 0.85f, 0.3f),
            ThermalClass.Cold => new Color(0.65f, 0.55f, 1f),
            _ => new Color(0.6f, 0.65f, 0.7f),
        };

        public static void DrawAll(Camera cam, IList<ScannableTarget> targets, ScannableTarget tracked)
        {
            if (cam == null || targets == null) return;
            float now = Time.realtimeSinceStartup;

            foreach (var t in targets)
            {
                if (t == null || !t.Identified) continue;
                var pos = t.transform.position + Vector3.up * 1.1f;
                var c = ClassColor(t.Class);
                float dist = Vector3.Distance(cam.transform.position, t.transform.position);

                // 距离淡出:远小近大括号
                float size = Mathf.Lerp(52f, 30f, Mathf.Clamp01(dist / 200f));
                Overlay.Bracket(pos, size, size * 0.78f, c, tracked == t ? 0f : 1.5f);
                Overlay.Label(pos + Vector3.up * 1.3f, $"{t.Label} {dist:0}m", c);

                if (tracked == t)
                    Overlay.Ring(pos, 30f, 1f, (now * 0.6f) % 1f, c);   // 锁定环缓旋
            }
        }
    }
}

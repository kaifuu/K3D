using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>热学分类:热(发动机/排气)/温(人员/电子)/冷(设备箱/金属)/环境(地面/建筑)。</summary>
    public enum ThermalClass { Hot, Warm, Cold, Ambient }

    /// <summary>
    /// 渲染器热学注册表:模式构建期登记所有可见 Renderer 的热学分类,
    /// ThermalView 切换时按分类批量换纯色材质(红外视角),退出时还原。
    /// 每次模式 Build 先 Clear,防域重载残留死引用。
    /// </summary>
    public static class RendererRegistry
    {
        public struct Entry { public Renderer Rend; public ThermalClass Class; public Material[] Backup; }
        static readonly List<Entry> items = new List<Entry>(64);

        public static IReadOnlyList<Entry> Items => items;
        public static int Count => items.Count;

        public static void Register(Renderer r, ThermalClass c)
        {
            if (r == null) return;
            foreach (var e in items) if (e.Rend == r) return;   // 去重
            items.Add(new Entry { Rend = r, Class = c, Backup = null });
        }

        public static void Clear() => items.Clear();

        /// <summary>回写条目(ThermalView 备份/还原原材质用)</summary>
        public static void WriteBack(int index, Material[] backup)
        {
            var e = items[index];
            e.Backup = backup;
            items[index] = e;
        }
    }
}

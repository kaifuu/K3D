using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 红外视角(无后处理方案):按注册表分类批量把 Renderer 材质换成
    /// Sprites/Default 纯色(热=白橙/温=亮黄/冷=紫蓝/环境=深蓝灰),
    /// 同时关雾、相机背景换深蓝、叠冷色暗角;退出时完整还原。
    /// </summary>
    public static class ThermalView
    {
        public static bool On { get; private set; }

        static Color hotC = new Color(1f, 0.5f, 0.14f);
        static Color warmC = new Color(1f, 0.82f, 0.28f);
        static Color coldC = new Color(0.38f, 0.22f, 0.62f);
        static Color ambC = new Color(0.07f, 0.1f, 0.17f);

        static Material hotM, warmM, coldM, ambM;
        static Color savedBg;
        static bool savedFog;
        static Camera boundCam;

        public static void SetOn(Camera cam, bool enable)
        {
            if (enable == On) return;
            On = enable;
            EnsureMats();

            if (enable)
            {
                boundCam = cam;
                savedFog = RenderSettings.fog;
                RenderSettings.fog = false;                    // 红外不受气象雾衰减
                if (cam != null)
                {
                    savedBg = cam.backgroundColor;
                    cam.backgroundColor = new Color(0.03f, 0.05f, 0.11f);
                }
                var items = RendererRegistry.Items;
                for (int i = 0; i < items.Count; i++)
                {
                    var e = items[i];
                    if (e.Rend == null) continue;
                    RendererRegistry.WriteBack(i, e.Rend.sharedMaterials);
                    e.Rend.sharedMaterials = Give(e.Class);
                }
                EventBus.Publish("侦察", "thermal", "切换红外热成像视角", EventGrade.Op);
            }
            else
            {
                RenderSettings.fog = savedFog;
                if (boundCam != null) boundCam.backgroundColor = savedBg;
                var items = RendererRegistry.Items;
                for (int i = 0; i < items.Count; i++)
                {
                    var e = items[i];
                    if (e.Rend == null || e.Backup == null) continue;
                    e.Rend.sharedMaterials = e.Backup;
                    RendererRegistry.WriteBack(i, null);
                }
                EventBus.Publish("侦察", "thermal", "切回可见光视角", EventGrade.Op);
            }
        }

        /// <summary>红外常驻冷色暗角(模式 OnTick 每帧提交)</summary>
        public static void DrawFrameFx()
        {
            if (On) Overlay.Vignette(new Color(0.12f, 0.25f, 0.55f), 0.4f);
        }

        static Material[] Give(ThermalClass c)
        {
            var m = c switch
            {
                ThermalClass.Hot => hotM,
                ThermalClass.Warm => warmM,
                ThermalClass.Cold => coldM,
                _ => ambM,
            };
            return new[] { m };
        }

        static void EnsureMats()
        {
            if (hotM != null) return;
            hotM = EnvironmentBuilder.FlatMat(hotC);
            warmM = EnvironmentBuilder.FlatMat(warmC);
            coldM = EnvironmentBuilder.FlatMat(coldC);
            ambM = EnvironmentBuilder.FlatMat(ambC);
        }
    }
}

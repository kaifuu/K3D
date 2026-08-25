using System.Text;
using UnityEngine;

namespace DroneSim
{
    public enum DayPhase { Day, Dusk, Night }
    public enum WeatherKind { Clear, Rain, Snow, Fog, Dust }

    /// <summary>主菜单环境参数页产出,作用于所有模式</summary>
    public class ModeStartParams
    {
        public DayPhase Phase = DayPhase.Day;
        public WeatherKind Weather = WeatherKind.Clear;
        [Range(0f, 1f)] public float WeatherDensity = 0.5f;
        public float WindMps = 2f;
        /// <summary>无头批处理:进入模式后立即开始演练</summary>
        public bool AutoStart;
    }

    /// <summary>模式服务句柄袋(由 ModeManager 构建并注入)</summary>
    public class DrillContext
    {
        public Transform ModeRoot;      // 模式物件总根,切模式整树销毁
        public ModeStartParams Params;
        /// <summary>模式 Build 时注册自己的主相机</summary>
        public Camera MainCamera;
    }

    /// <summary>
    /// 演练模式基类:Build 过程式搭场景 → 用户/剧本开始 → OnTick 每帧驱动。
    /// 所有模式物件必须经 NewGo() 创建(自动挂 ModeRoot)。
    /// </summary>
    public abstract class DrillMode
    {
        public abstract string Id { get; }
        public abstract string Title { get; }
        public virtual string Brief => "";
        public DrillContext Ctx { get; internal set; }
        protected Transform Root => Ctx.ModeRoot;

        protected GameObject NewGo(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Ctx.ModeRoot, false);
            return go;
        }
        protected GameObject NewGo(string name, Vector3 worldPos)
        {
            var go = NewGo(name);
            go.transform.position = worldPos;
            return go;
        }

        public virtual void Build() { }                                   // Setup 态搭场景
        public virtual void OnStart() { }
        public virtual void OnStop() { }
        /// <summary>每帧驱动(仅演练运行态被调用;dt 已含倍速)</summary>
        public virtual void OnTick(float dt) { }
        /// <summary>模式专属侧板(IMGUI,右侧固定区域)</summary>
        public virtual void DrawSidePanel(Rect r) { }
        /// <summary>屏幕中央覆盖层(横幅等)</summary>
        public virtual void DrawOverlay() { }
        /// <summary>底部按键提示(追加到 StringBuilder)</summary>
        public virtual void DrawHint(StringBuilder sb) { }
        /// <summary>无头状态导出(追加到 state.txt)</summary>
        public virtual void WriteMetrics(StringBuilder sb) { }
        /// <summary>无头剧本编排(name 为 -scenario 参数,空=默认)</summary>
        public virtual void RunHeadlessScenario(string name) { }
    }
}

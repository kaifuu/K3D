using UnityEngine;

namespace DroneSim
{
    /// <summary>主菜单:模式目录 → 环境参数页 → 开始。未实装模式显示"建设中"。</summary>
    public static class MainMenuUI
    {
        struct ModeSpec { public string Id, Title, Brief; }
        static readonly ModeSpec[] specs =
        {
            new ModeSpec { Id="manual",    Title="无人机飞行操控", Brief="键鼠/手柄全向飞行:俯仰横滚偏航升降悬停返航,姿态倾斜回正、旋翼转速联动、侧风抖动漂移、惯性滑行。" },
            new ModeSpec { Id="route",     Title="动态航线巡航",   Brief="手绘/打点/拖拽航线,自动巡航与断点续飞;发光流光航线,偏离告警红闪与3D指引。" },
            new ModeSpec { Id="env",       Title="昼夜与天气适应", Brief="白昼/黄昏/夜晚平滑渐变,雨雪雾沙尘浓度可调,大风扰动联动飞行。" },
            new ModeSpec { Id="recon",     Title="侦察巡检",       Brief="可视/红外双视角,云台镜头平滑跟随,扇形扫描波纹,目标识别高亮标注。" },
            new ModeSpec { Id="tactics",   Title="应急战术处置",   Brief="火情/入侵/人员失联/障碍应急触发;物资投送坠落弹动,喊话驱离声波扩散。" },
            new ModeSpec { Id="formation", Title="集群编队飞行",   Brief="一字/三角/矩阵/环形编队一键切换,平滑插值换阵,主机带领协同巡航与避障归位。" },
            new ModeSpec { Id="combat",    Title="红蓝攻防对抗",   Brief="红方低空突防AI,蓝方锁定追踪拦截;锁定进度圈、驱离/迫降/逃逸结局动画。" },
            new ModeSpec { Id="fault",     Title="设备故障模拟",   Brief="GPS丢失/通信中断/电机故障/低电量一键注入与解除,故障状态联动表现。" },
            new ModeSpec { Id="full",      Title="综合演练与复盘", Brief="组合想定+全程录制:暂停/倍速/回溯复盘/轨迹重绘/关键节点回放。" },
            new ModeSpec { Id="regulator", Title="监管反制专项",   Brief="空域侦测预警与黑飞处置闭环:分级预警、电磁干扰/捕获网/激光反制、判分与空域复位。" },
        };

        static int sel = 9;          // 默认选中监管反制(唯一已实装)
        static bool envPage;
        static ModeStartParams draft = new ModeStartParams();

        public static void Draw()
        {
            var dim = new Rect(0, 0, Screen.width, Screen.height);
            var prev = GUI.color;
            GUI.color = new Color(0.04f, 0.06f, 0.09f, 0.92f);
            GUI.Box(dim, "");
            GUI.color = prev;

            GUI.Label(new Rect(Screen.width / 2 - 220, 26, 440, 34), "低空无人机综合演练平台", PanelKit.Header);
            GUI.Label(new Rect(Screen.width / 2 - 220, 58, 440, 20),
                $"共 {specs.Length} 个演练模式 · 选择模式后可配置昼夜/天气/风力", PanelKit.Small);

            if (!envPage) DrawCatalog();
            else DrawEnvPage();
        }

        static void DrawCatalog()
        {
            float gx = Screen.width / 2 - 460, gy = 96;
            const float bw = 220, bh = 52;
            for (int i = 0; i < specs.Length; i++)
            {
                int col = i % 4, row = i / 4;
                var r = new Rect(gx + col * (bw + 12), gy + row * (bh + 12), bw, bh);
                bool available = IsAvailable(specs[i].Id);
                bool cur = sel == i;
                var bg = GUI.backgroundColor;
                if (cur) GUI.backgroundColor = new Color(0.35f, 0.7f, 1f);
                else if (!available) GUI.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
                if (GUI.Button(r, $"{specs[i].Title}\n{(available ? "" : "(建设中)")}", PanelKit.Button))
                {
                    sel = i;
                    if (available) envPage = true;
                }
                GUI.backgroundColor = bg;
            }
        }

        static void DrawEnvPage()
        {
            var spec = specs[sel];
            float cx = Screen.width / 2 - 240, cy = 96;

            var box = new Rect(cx, cy, 480, 330);
            GUI.Box(box, "");
            GUI.Label(new Rect(cx + 14, cy + 10, 452, 24), spec.Title, PanelKit.Header);
            GUI.Label(new Rect(cx + 14, cy + 38, 452, 46), spec.Brief, PanelKit.Small);

            // 昼夜
            GUI.Label(new Rect(cx + 14, cy + 88, 100, 18), "时段", PanelKit.Label);
            string[] phases = { "白昼", "黄昏", "夜晚" };
            for (int i = 0; i < 3; i++)
                if (PanelKit.ToggleBtn(cx + 60 + i * 76, cy + 86, 70, 22, phases[i], (int)draft.Phase == i))
                    draft.Phase = (DayPhase)i;

            // 天气
            GUI.Label(new Rect(cx + 14, cy + 118, 100, 18), "天气", PanelKit.Label);
            string[] weathers = { "晴", "雨", "雪", "雾", "沙尘" };
            for (int i = 0; i < 5; i++)
                if (PanelKit.ToggleBtn(cx + 60 + i * 66, cy + 116, 60, 22, weathers[i], (int)draft.Weather == i))
                    draft.Weather = (WeatherKind)i;

            // 浓度/风力
            if (draft.Weather != WeatherKind.Clear)
                draft.WeatherDensity = GUI.HorizontalSlider(new Rect(cx + 70, cy + 152, 240, 20),
                    draft.WeatherDensity, 0.05f, 1f);
            GUI.Label(new Rect(cx + 320, cy + 148, 140, 18), $"天气浓度 {draft.WeatherDensity:P0}", PanelKit.Small);
            GUI.Label(new Rect(cx + 14, cy + 176, 56, 18), "风力", PanelKit.Label);
            draft.WindMps = GUI.HorizontalSlider(new Rect(cx + 70, cy + 180, 240, 20), draft.WindMps, 0f, 12f);
            GUI.Label(new Rect(cx + 320, cy + 176, 140, 18), $"{draft.WindMps:0.#} m/s", PanelKit.Small);

            GUI.Label(new Rect(cx + 14, cy + 210, 452, 18),
                "提示:时段/天气/风力对所选模式全程生效,进入后可在模式侧板实时调整", PanelKit.Mini);

            if (PanelKit.Btn(cx + 14, cy + 240, 120, 30, "← 返回目录")) envPage = false;
            if (PanelKit.Btn(cx + 146, cy + 240, 160, 30, "开始演练"))
            {
                envPage = false;
                var p = new ModeStartParams
                {
                    Phase = draft.Phase, Weather = draft.Weather,
                    WeatherDensity = draft.WeatherDensity, WindMps = draft.WindMps,
                };
                ModeManager.Enter(spec.Id, p);
            }
        }

        static bool IsAvailable(string id)
        {
            foreach (var e in ModeManager.Catalog)
                if (e.Id == id) return true;
            return false;
        }
    }
}

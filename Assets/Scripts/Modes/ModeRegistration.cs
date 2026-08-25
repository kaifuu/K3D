namespace DroneSim
{
    /// <summary>
    /// 模式注册表:每阶段实装后在此追加一行。
    /// 主菜单目录固定展示 10 个模式,未注册的显示"建设中"。
    /// </summary>
    public static class ModeRegistration
    {
        static bool done;

        public static void RegisterAll()
        {
            if (done) return;
            done = true;

            ModeManager.Register("regulator", "监管反制专项", () => new RegulatorMode());
            ModeManager.Register("manual", "无人机飞行操控", () => new ManualFlightMode());
            ModeManager.Register("route", "动态航线巡航", () => new RouteMode());
            ModeManager.Register("env", "昼夜与天气适应", () => new EnvAdaptMode());
            ModeManager.Register("recon", "侦察巡检", () => new ReconMode());
            ModeManager.Register("formation", "集群编队飞行", () => new FormationMode());
            ModeManager.Register("combat", "红蓝攻防对抗", () => new CombatMode());
            ModeManager.Register("tactics", "应急战术处置", () => new TacticsMode());
            ModeManager.Register("fault", "设备故障模拟", () => new FaultMode());
            ModeManager.Register("full", "综合演练与复盘", () => new FullExerciseMode());
        }
    }
}

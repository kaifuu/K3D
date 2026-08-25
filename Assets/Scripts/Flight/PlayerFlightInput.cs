using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 玩家飞行输入(键鼠 + 手柄,零配置 legacy Input):
    /// 前后/左右复用默认 Horizontal/Vertical 轴(已含 WASD 与左摇杆),
    /// 偏航/升降/刹车用独立按键与手柄数字键 —— 无需配置 InputManager。
    /// </summary>
    public class PlayerFlightInput : MonoBehaviour, ICommandSource
    {
        public FlightBody Body;
        /// <summary>自驾仪接管时关闭玩家输入</summary>
        public bool Enabled = true;

        void Update()
        {
            if (Body == null || !DrillClock.CanSimulate) return;
            if (!Enabled) { Body.Cmd = FlightCommand.Idle; return; }
            Apply(Body);
        }

        public void Apply(FlightBody body)
        {
            var c = FlightCommand.Idle;

            // ---- 前后/左右(默认轴=WASD+左摇杆,避免重复读键) ----
            c.Pitch = Input.GetAxis("Vertical");     // W/上=+前进
            c.Roll = Input.GetAxis("Horizontal");    // D/右=+右移

            // ---- 键盘:偏航/升降/刹车 ----
            if (Input.GetKey(KeyCode.Q)) c.YawRate -= 1f;
            if (Input.GetKey(KeyCode.E)) c.YawRate += 1f;
            if (Input.GetKey(KeyCode.Space)) c.Throttle += 1f;
            if (Input.GetKey(KeyCode.LeftShift)) c.Throttle -= 1f;
            if (Input.GetKey(KeyCode.LeftControl)) c.Brake = 1f;

            // ---- 手柄数字键(XInput:4=LB 5=RB 0=A 1=B 2=X) ----
            if (Input.GetKey(KeyCode.JoystickButton4)) c.YawRate -= 1f;
            if (Input.GetKey(KeyCode.JoystickButton5)) c.YawRate += 1f;
            if (Input.GetKey(KeyCode.JoystickButton0)) c.Throttle += 1f;
            if (Input.GetKey(KeyCode.JoystickButton1)) c.Throttle -= 1f;
            if (Input.GetKey(KeyCode.JoystickButton2)) c.Brake = 1f;

            c.Clamp();
            body.Cmd = c;
        }
    }
}

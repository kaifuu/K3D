using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 全向飞行指令(各轴 -1..1,0=回中):
    /// Pitch +前进/-后退(机体系+Z) Roll +右移/-左移(+X) YawRate +右转/-左转
    /// Throttle +上升/-下降 Brake 0..1 刹车悬停(衰减目标速度并增强减速)。
    /// </summary>
    public struct FlightCommand
    {
        public float Pitch, Roll, YawRate, Throttle, Brake;
        public static FlightCommand Idle => default;

        public void Clamp()
        {
            Pitch = Mathf.Clamp(Pitch, -1f, 1f);
            Roll = Mathf.Clamp(Roll, -1f, 1f);
            YawRate = Mathf.Clamp(YawRate, -1f, 1f);
            Throttle = Mathf.Clamp(Throttle, -1f, 1f);
            Brake = Mathf.Clamp01(Brake);
        }
    }

    /// <summary>指令源:玩家输入/自驾仪/航线跟随器统一实现,向 FlightBody 写入指令</summary>
    public interface ICommandSource
    {
        void Apply(FlightBody body);
    }

    /// <summary>飞行换算共用工具:世界系期望速度 → 机体指令</summary>
    public static class FlightMath
    {
        public static void WorldVelToCmd(FlightBody body, Vector3 worldVel, ref FlightCommand c)
        {
            var vLocal = Quaternion.Euler(0f, -body.HeadingDeg, 0f) * worldVel;
            c.Pitch = Mathf.Clamp(vLocal.z / body.MaxSpeed, -1f, 1f);
            c.Roll = Mathf.Clamp(vLocal.x / body.MaxSpeed, -1f, 1f);
            c.Throttle = Mathf.Clamp(vLocal.y / body.MaxClimb, -1f, 1f);
        }
    }
}

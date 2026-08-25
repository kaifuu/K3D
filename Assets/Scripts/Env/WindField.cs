using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 全局风场(唯一风源):定常风 + Perlin 湍流阵风。
    /// P2 提供基础版供飞行联动;P4 天气系统在其上扩展(菜单可调 0~12m/s)。
    /// 湍流用 DrillClock.SimTime 驱动:暂停即静止,无头可复现。
    /// </summary>
    public static class WindField
    {
        static Vector3 dir = new Vector3(1f, 0f, 0.35f).normalized;
        static float mps = 2f;

        public static Vector3 Direction => dir;
        public static float SpeedMps => mps;
        public static Vector3 Steady => dir * mps;

        /// <summary>模式进入时由 ModeManager 按菜单参数配置</summary>
        public static void Configure(Vector3 direction, float speedMps)
        {
            dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.right;
            mps = Mathf.Clamp(speedMps, 0f, 14f);
        }

        public static void ResetToDefault() => Configure(new Vector3(1f, 0f, 0.35f), 2f);

        /// <summary>Perlin 湍流阵风(幅度≈风速的40%,随位置与时间变化)</summary>
        public static Vector3 Gust(Vector3 pos)
        {
            if (mps < 0.1f) return Vector3.zero;
            float t = DrillClock.SimTime * 0.4f;
            float gx = Mathf.PerlinNoise(pos.x * 0.02f + t, pos.z * 0.02f) - 0.5f;
            float gz = Mathf.PerlinNoise(pos.x * 0.02f, pos.z * 0.02f + t) - 0.5f;
            float gy = Mathf.PerlinNoise(t * 0.7f, pos.x * 0.01f) - 0.5f;
            return new Vector3(gx * 2f, gy * 0.8f, gz * 2f) * (mps * 0.4f);
        }

        /// <summary>采样总风(定常+阵风)与阵风分量(阵风供姿态抖动用)</summary>
        public static void Sample(Vector3 pos, out Vector3 total, out Vector3 gust)
        {
            gust = Gust(pos);
            total = Steady + gust;
        }
    }
}

using UnityEngine;

namespace DroneSim
{
    /// <summary>旋翼旋转动画:转速可由飞行体动态驱动(P2 起 FlightBody 联动油门/速度)</summary>
    public class RotorSpin : MonoBehaviour
    {
        Transform[] rotors;
        /// <summary>0..1 归一化转速(1=全速),默认悬停基速</summary>
        public float Rpm01 = 0.6f;
        /// <summary>故障注入:停转旋翼索引(0..3,-1=全部正常,P9 电机故障)</summary>
        public int StoppedRotor = -1;

        void Start()
        {
            rotors = new Transform[4];
            for (int i = 0; i < 4; i++) rotors[i] = transform.Find($"Rotor{i}");
        }

        void OnEnable()
        {
            // 域重载后子物体引用重新缓存
            if (rotors == null)
            {
                rotors = new Transform[4];
                for (int i = 0; i < 4; i++) rotors[i] = transform.Find($"Rotor{i}");
            }
        }

        public void SetRpm(float rpm01) => Rpm01 = Mathf.Clamp(rpm01, 0f, 1.4f);

        void Update()
        {
            if (rotors == null) return;
            if (!DrillClock.CanSimulate && !DrillClock.InReplay) return;   // 回放态由 ReplayPlayer 设转速
            float spin = (900f + 1400f * Rpm01) * Time.deltaTime;   // 度/秒 × dt,帧率无关
            for (int i = 0; i < 4; i++)
                if (rotors[i] != null && i != StoppedRotor)
                    rotors[i].Rotate(0f, i % 2 == 0 ? spin : -spin, 0f, Space.Self);
        }
    }
}

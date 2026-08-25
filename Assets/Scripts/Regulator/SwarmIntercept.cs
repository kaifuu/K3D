using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 蜂群应对(模块10):同方位密集蜂群波次生成;区域信号阻断——
    /// 以锚点目标为中心半径内全部失联迫降(一次处置多目标)。
    /// </summary>
    public class SwarmIntercept : MonoBehaviour
    {
        public static SwarmIntercept I { get; private set; }
        public Spawner Spn;
        public float BlockRadius = 70f;

        public int BlockNeutralized { get; private set; }   // 区域阻断累计迫降数
        public int LastWaveSize { get; private set; }

        void OnEnable() => I = this;   // 域重载防御

        /// <summary>触发同方位蜂群集中来袭(演练编排/面板按钮)</summary>
        public void TriggerSwarmWave(int count)
        {
            if (Spn == null) return;
            LastWaveSize = Spn.SpawnWave(DroneKind.Swarm, count);
            EventBus.Publish("监管", "swarm",
                $"蜂群集中来袭:同方位 {LastWaveSize} 机集群,建议捕获网或区域信号阻断", EventGrade.Warn);
        }

        /// <summary>区域信号阻断:锚点半径内全部目标失联迫降,返回处置数</summary>
        public int AreaBlock(EnemyDrone anchor)
        {
            if (anchor == null || Spn == null) return 0;
            Vector3 c = anchor.transform.position;
            int n = 0;
            foreach (var d in Spn.Active)
            {
                if (d == null) continue;
                if (d.State != DroneState.Approaching && d.State != DroneState.TurnedBack) continue;
                if (Vector3.Distance(c, d.transform.position) > BlockRadius) continue;
                d.SignalBlocked();
                n++;
            }
            if (n == 0) return 0;
            BlockNeutralized += n;
            SignalBlockFX.Play(c, BlockRadius);
            GameState.OnBlockNeutralized(n);
            return n;
        }
    }
}

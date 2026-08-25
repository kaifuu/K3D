using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>波次生成器:按波次生成侦察/攻击/蜂群目标(演练时间由 DrillClock 统一管理)</summary>
    public class Spawner : MonoBehaviour
    {
        public static Spawner I { get; private set; }

        public GameObject dronePrefab;
        [Range(0f, 1f)] public float intensity = 0.6f;

        float nextSpawn;
        int droneCounter;
        readonly List<EnemyDrone> active = new List<EnemyDrone>();

        // OnEnable:域重载恢复后重新注册静态单例(Awake 不会重跑)
        void OnEnable() => I = this;

        public IReadOnlyList<EnemyDrone> Active => active;
        public int SpawnedCount => droneCounter;

        void Update()
        {
            if (!DrillClock.CanSimulate) return;   // 待开始/暂停/回放时不生成
            if (GameState.FacilityDown) return;
            if (Time.time < nextSpawn) return;

            int wave = GameState.Wave;
            float delay = Mathf.Lerp(6.5f, 2.2f, intensity);
            if (wave >= 3) delay *= 0.85f;
            nextSpawn = Time.time + delay * Random.Range(0.8f, 1.25f);

            DroneKind kind = PickKind(wave);
            int count = kind == DroneKind.Swarm ? Random.Range(3, 5) : 1;
            for (int i = 0; i < count; i++) SpawnOne(kind, i * 0.15f);
        }

        DroneKind PickKind(int wave)
        {
            float r = Random.value;
            if (wave >= 2 && r < 0.3f) return DroneKind.Swarm;
            if (wave >= 1 && r < 0.65f) return DroneKind.Attack;
            return DroneKind.Recon;
        }

        void SpawnOne(DroneKind kind, float delay)
        {
            Vector3 dir = Random.insideUnitCircle.normalized;
            Vector3 pos = new Vector3(dir.x, 0f, dir.y) * SimConfig.SpawnRadius
                        + new Vector3(Random.Range(-15f, 15f), 0f, Random.Range(-15f, 15f));
            pos.y = Random.Range(18f, 46f);
            SpawnAt(kind, pos);
        }

        /// <summary>
        /// 同方位集群波次(模块10 蜂群应对):指定方位生成紧密集群;
        /// 返回生成数量。正北为 0°,顺时针。
        /// </summary>
        public int SpawnWave(DroneKind kind, int count, float bearingDeg = -1f)
        {
            if (dronePrefab == null || count <= 0) return 0;
            float a = bearingDeg < 0f ? Random.value * 360f : bearingDeg;
            var dir = new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad)).normalized;
            var anchor = new Vector3(dir.x, 0f, dir.y) * SimConfig.SpawnRadius;
            anchor.y = Random.Range(24f, 34f);

            for (int i = 0; i < count; i++)
            {
                // 紧密集群:锚点周围 ±26m 扰动(蜂群散布远小于随机波)
                var pos = anchor + new Vector3(Random.Range(-26f, 26f), Random.Range(-6f, 6f), Random.Range(-26f, 26f));
                SpawnAt(kind, pos);
            }
            SimEvents.Add($"[监管] 同方位集群生成:{count} 机 方位{a:0}°(距离{SimConfig.SpawnRadius:0}m)");
            return count;
        }

        void SpawnAt(DroneKind kind, Vector3 pos)
        {
            var go = Instantiate(dronePrefab, pos, Quaternion.LookRotation(-pos.normalized));
            go.SetActive(true);   // 模板是禁用状态,克隆体需显式激活
            var d = go.GetComponent<EnemyDrone>();
            d.Init(kind, ++droneCounter);
            d.SetPhase(Random.value * 10f);
            active.Add(d);
        }

        public void NotifyRemoved(EnemyDrone d) => active.Remove(d);

        /// <summary>波次推进:每60秒或手动触发</summary>
        public void AdvanceWave()
        {
            GameState.Wave++;
            intensity = Mathf.Min(1f, 0.35f + GameState.Wave * 0.15f);
            SimEvents.Add($"[推演] === 第 {GameState.Wave} 波来袭,强度 {intensity:P0} ===");
        }

        void Start()
        {
            SimEvents.Add("[推演] 演练开始:进入无人机监管与反制推演");
            AdvanceWave();   // 从第1波开始,之后每60秒+1
            InvokeRepeating(nameof(TickWave), 60f, 60f);
        }

        void TickWave()
        {
            if (!GameState.FacilityDown) AdvanceWave();
        }
    }
}

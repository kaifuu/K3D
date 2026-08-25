using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 威胁分级器(模块10):按 类型权重/合规性/速度/接近度 计算每个来袭目标的
    /// 优先级分值并排序,产出 分级(Watch/Warn/Threat)+ 处置建议。
    /// 自动防御与预警面板共用此排序。
    /// </summary>
    public class ThreatGrader
    {
        public struct Grade
        {
            public EnemyDrone Drone;
            public float Priority;        // 0~120
            public ThreatLevel Level;
            public string Advice;         // 处置建议
            public float DistCore;        // 距核心 m
        }

        // 优先级权重(面板可调)
        public float WKind = 40f;         // 机型基础威胁
        public float WCompliance = 30f;   // 不合规加成
        public float WSpeed = 15f;        // 速度加成
        public float WProximity = 35f;    // 接近度加成

        public readonly List<Grade> Ranked = new List<Grade>(16);

        public void Rebuild(IReadOnlyList<EnemyDrone> active)
        {
            Ranked.Clear();
            if (active == null) return;
            for (int i = 0; i < active.Count; i++)
            {
                var d = active[i];
                if (d == null || d.State != DroneState.Approaching) continue;

                float kind = d.Kind == DroneKind.Attack ? 1f : d.Kind == DroneKind.Swarm ? 0.72f : 0.4f;
                float comp = d.RemoteIdCompliant ? 0.15f : 1f;
                float spd = Mathf.Clamp01(d.Speed / 20f);
                float distCore = new Vector2(d.transform.position.x, d.transform.position.z).magnitude;
                float prox = 1f - Mathf.Clamp01(distCore / SimConfig.SpawnRadius);

                float p = WKind * kind + WCompliance * comp + WSpeed * spd + WProximity * prox;
                Ranked.Add(new Grade
                {
                    Drone = d,
                    Priority = p,
                    Level = p > 70f ? ThreatLevel.Threat : p > 45f ? ThreatLevel.Warn : ThreatLevel.Watch,
                    Advice = Advice(d),
                    DistCore = distCore
                });
            }
            Ranked.Sort((a, b) => b.Priority.CompareTo(a.Priority));   // 优先级降序
        }

        static string Advice(EnemyDrone d)
        {
            if (d.Kind == DroneKind.Attack) return "激光硬摧毁";
            if (d.Kind == DroneKind.Swarm) return "捕获网/区域阻断";
            return d.RemoteIdCompliant ? "警告驱离" : "干扰迫降";
        }

        public int CountAt(ThreatLevel lv)
        {
            int n = 0;
            for (int i = 0; i < Ranked.Count; i++) if (Ranked[i].Level == lv) n++;
            return n;
        }
    }
}

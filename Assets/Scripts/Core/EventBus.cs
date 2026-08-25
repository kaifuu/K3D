using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    public enum EventGrade { Info = 0, Op = 1, Warn = 2, Critical = 3 }

    public readonly struct SimEventMsg
    {
        public readonly float Time;          // 演练时间(秒)
        public readonly string Category;     // 模式/监管/反制/故障/演练...
        public readonly string SubjectId;    // 关联对象(UAV-001 等,可空)
        public readonly string Message;
        public readonly EventGrade Grade;    // Op=关键操作节点(复盘时间轴标记)
        public readonly Vector3? Position;

        public SimEventMsg(float t, string cat, string subj, string msg, EventGrade g, Vector3? pos)
        { Time = t; Category = cat; SubjectId = subj; Message = msg; Grade = g; Position = pos; }
    }

    /// <summary>
    /// 全局事件总线:所有系统统一发布/订阅;复盘时间轴取 Op 级以上事件做刻度。
    /// 时间戳取 DrillClock.SimTime(受暂停/倍速正确影响)。
    /// </summary>
    public static class EventBus
    {
        static readonly List<SimEventMsg> log = new List<SimEventMsg>(256);
        const int kMax = 400;
        public static event Action<SimEventMsg> Published;

        public static void Publish(string category, string subjectId, string msg,
                                   EventGrade grade = EventGrade.Info, Vector3? pos = null)
        {
            var m = new SimEventMsg(DrillClock.SimTime, category, subjectId, msg, grade, pos);
            log.Add(m);
            if (log.Count > kMax) log.RemoveAt(0);
            try { Published?.Invoke(m); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>最新在前(供日志 UI/导出)</summary>
        public static List<SimEventMsg> Recent(int n)
        {
            var r = new List<SimEventMsg>(n);
            int start = Mathf.Max(0, log.Count - n);
            for (int i = log.Count - 1; i >= start; i--) r.Add(log[i]);
            return r;
        }

        public static IReadOnlyList<SimEventMsg> All => log;
        public static void Clear() => log.Clear();
    }
}

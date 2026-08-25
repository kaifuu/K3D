using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 无头剧本调度器:按演练时间触发动作,保证批处理截图时刻画面确定。
    /// 各模式在 RunHeadlessScenario 中用 At(t, action) 编排。
    /// </summary>
    public class ScenarioRunner : MonoBehaviour
    {
        public static ScenarioRunner I { get; private set; }

        struct Cue { public float Time; public Action Action; public bool Done; }
        struct RealCue { public float Wall; public Action Action; public bool Done; }
        readonly List<Cue> cues = new List<Cue>();
        readonly List<RealCue> realCues = new List<RealCue>();

        void OnEnable() => I = this;   // 域重载防御:OnEnable 重注册单例

        public void At(float simTime, Action a) => cues.Add(new Cue { Time = simTime, Action = a });
        public void After(float delay, Action a) => cues.Add(new Cue { Time = DrillClock.SimTime + delay, Action = a });

        /// <summary>墙钟剧本:按真实时间触发,回放态(CanSimulate=false)也可执行(P10 回放段编排)</summary>
        public void AtReal(float wallDelay, Action a) =>
            realCues.Add(new RealCue { Wall = Time.realtimeSinceStartup + wallDelay, Action = a });

        void Update()
        {
            // 墙钟剧本:任何非 Setup 状态都推进
            if (DrillClock.State != PlayState.Setup)
            {
                float now = Time.realtimeSinceStartup;
                for (int i = 0; i < realCues.Count; i++)
                {
                    if (realCues[i].Done || now < realCues[i].Wall) continue;
                    var rc = realCues[i];
                    rc.Done = true;
                    realCues[i] = rc;
                    try { rc.Action?.Invoke(); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }

            if (!DrillClock.CanSimulate) return;
            for (int i = 0; i < cues.Count; i++)
            {
                if (cues[i].Done || DrillClock.SimTime < cues[i].Time) continue;
                var c = cues[i];          // struct 不能直接经索引器改字段
                c.Done = true;
                cues[i] = c;
                try { c.Action?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}

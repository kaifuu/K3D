using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 无头验证断言:各系统在批处理运行中 Check(...),
    /// 结束时随 state.txt 导出报告,失败将退出码置 3。
    /// </summary>
    public static class HeadlessAssert
    {
        public struct Entry { public bool Pass; public string Msg; }
        public static readonly List<Entry> Entries = new List<Entry>();
        public static int FailCount { get; private set; }

        public static void Check(bool cond, string msg)
        {
            Entries.Add(new Entry { Pass = cond, Msg = msg });
            if (!cond) FailCount++;
            Debug.Log($"[Assert] {(cond ? "PASS" : "FAIL")}  {msg}");
        }

        public static void Report(StringBuilder sb)
        {
            sb.AppendLine($"---- 断言 {Entries.Count - FailCount}/{Entries.Count} 通过 ----");
            for (int i = 0; i < Entries.Count; i++)
                sb.AppendLine($"{(Entries[i].Pass ? "PASS" : "FAIL")}  {Entries[i].Msg}");
        }

        public static void ResetAll() { Entries.Clear(); FailCount = 0; }
    }
}

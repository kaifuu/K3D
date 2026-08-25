using System.Collections.Generic;
using UnityEngine;

namespace DroneSim
{
    public enum FormationShape { Wedge, Column, Line, Diamond }

    /// <summary>
    /// 编队构型库:给定构型/序号/机数/间距,返回领机坐标系(+Z 前 +X 右)槽位偏移。
    /// 序号 0 = 领机(原点)。纯函数,无头确定。
    /// </summary>
    public static class FormationLibrary
    {
        public static readonly FormationShape[] All =
        {
            FormationShape.Wedge, FormationShape.Column, FormationShape.Line, FormationShape.Diamond
        };

        public static string Name(FormationShape s) => s switch
        {
            FormationShape.Wedge => "楔形",
            FormationShape.Column => "纵队",
            FormationShape.Line => "横队",
            FormationShape.Diamond => "菱形",
            _ => "-"
        };

        /// <summary>槽位偏移(领机系:x 右 z 前,y 恒 0 同高)</summary>
        public static Vector3 SlotOffset(FormationShape s, int i, int n, float sp)
        {
            if (i <= 0) return Vector3.zero;                     // 领机
            switch (s)
            {
                case FormationShape.Wedge:                        // V 形双臂后掠 1,1 / 2,2 / 3,3...
                {
                    int k = (i - 1) / 2 + 1;
                    float side = i % 2 == 1 ? 1f : -1f;
                    return new Vector3(side * k * sp, 0f, -k * sp * 0.75f);
                }
                case FormationShape.Column:                       // 一路纵队(微交错防尾流)
                {
                    float stag = (i % 2 == 0 ? 0.5f : -0.5f) * sp * 0.4f;
                    return new Vector3(stag, 0f, -i * sp * 0.45f);
                }
                case FormationShape.Line:                         // 横队左右展开
                {
                    int k = (i + 1) / 2;
                    float side = i % 2 == 1 ? 1f : -1f;
                    return new Vector3(side * k * sp, 0f, 0f);
                }
                case FormationShape.Diamond:                      // 菱形:逐行加宽再收窄
                {
                    var rows = DiamondRows(n);
                    int idx = 1, row = 1;
                    for (int r = 1; r < rows.Count; r++)
                    {
                        int w = rows[r];
                        if (i < idx + w)
                        {
                            float off = (i - idx) - (w - 1) * 0.5f;   // 行内居中
                            return new Vector3(off * sp, 0f, -row * sp * 0.9f);
                        }
                        idx += w;
                        row++;
                    }
                    return new Vector3(0f, 0f, -row * sp * 0.9f);
                }
            }
            return Vector3.zero;
        }

        /// <summary>菱形行宽序列(首行 1=领机)。n=9 → 1,2,3,2,1 完美菱形;
        /// 非平方机数时尾部截断,行宽单调不减再收窄。</summary>
        static List<int> DiamondRows(int n)
        {
            var rows = new List<int> { 1 };
            int k = Mathf.Max(2, Mathf.RoundToInt(Mathf.Sqrt(n)));
            for (int w = 2; w <= k; w++) rows.Add(w);
            for (int w = k - 1; w >= 1; w--) rows.Add(w);
            int sum = 0;
            foreach (var w in rows) sum += w;
            while (sum > n && rows.Count > 1)
            {
                int last = rows[rows.Count - 1];
                int cut = Mathf.Min(last - 1, sum - n);
                if (cut <= 0) { rows.RemoveAt(rows.Count - 1); sum -= last; }
                else { rows[rows.Count - 1] = last - cut; sum -= cut; }
            }
            return rows;
        }
    }
}

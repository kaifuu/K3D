using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DroneSimEditor
{
    /// <summary>
    /// 无头运行入口(批处理验证管线):
    ///   Unity.exe -batchmode -projectPath &lt;工程&gt;
    ///     -executeMethod DroneSimEditor.SimRunner.SetupAndCapture
    ///     -dsMode=regulator -scenario=default -shots=4,15,35,65 -logFile run.log
    /// 流程:确保 Boot(PlatformBoot)场景 → 等编译空闲 → 进 Play →
    /// HeadlessBridge 传参使 PlatformBoot 直进模式并自动开始 →
    /// 按计划截图/导出状态(模式 WriteMetrics + 事件 + 断言) → 退出。
    /// 退出码:0=成功 2=看门狗超时 3=断言失败。
    /// 注意:进入 Play 会触发域重载,用 [InitializeOnLoad]+SessionState 跨越;
    /// Play 前禁用自动刷新并等待编译空闲,杜绝 Play 中途重编译(曾致静态单例全灭)。
    /// </summary>
    [InitializeOnLoad]
    public static class SimRunner
    {
        const int W = 1280, H = 720;
        static float[] shotTimes = { 4f, 15f, 35f, 65f };
        static string argMode = "regulator";
        static string argScenario = "";

        // SessionState 键 —— 域重载后仍保留(编辑器会话级)
        const string kActive = "SimRunner.Active";
        const string kPlayStart = "SimRunner.PlayStart";
        const string kShotIndex = "SimRunner.ShotIndex";
        const string kOutDir = "SimRunner.OutDir";
        const string kNextPing = "SimRunner.NextPing";
        const string kPendingPlay = "SimRunner.PendingPlay";
        const string kPrevAutoMode = "SimRunner.PrevAutoMode";
        const string kPrevAutoBool = "SimRunner.PrevAutoBool";
        const string kShotTimes = "SimRunner.ShotTimes";   // 静态数组过不了域重载,存 SessionState

        static SimRunner()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        [MenuItem("推演/在当前场景创建Boot并保存")]
        public static void CreateBootInScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            EnsureBoot();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/Main.unity");
            Debug.Log("[SimRunner] Boot(PlatformBoot) 已创建并保存到 Assets/Scenes/Main.unity,按 Play 进入主菜单");
        }

        /// <summary>Boot 物体只挂 PlatformBoot;旧 MainBoot 引用(可能 Missing)一并清除</summary>
        static void EnsureBoot()
        {
            var old = GameObject.Find("Boot");
            if (old != null) Object.DestroyImmediate(old);
            var boot = new GameObject("Boot");
            boot.AddComponent<DroneSim.PlatformBoot>();
            EditorSceneManager.MarkSceneDirty(boot.scene);
        }

        public static void SetupAndCapture()
        {
            ParseArgs();

            string outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Screenshots");
            Directory.CreateDirectory(outDir);
            SessionState.SetString(kOutDir, outDir);
            SessionState.SetBool(kActive, true);
            SessionState.SetFloat(kPlayStart, -1f);
            SessionState.SetInt(kShotIndex, 0);
            SessionState.SetFloat(kNextPing, 0f);

            // 1. 确保 Boot 场景存在并打开
            const string scenePath = "Assets/Scenes/Main.unity";
            Directory.CreateDirectory("Assets/Scenes");
            if (File.Exists(scenePath))
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            else
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, scenePath);
                Debug.Log($"[SimRunner] 已创建场景: {scenePath}");
            }
            EnsureBoot();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);

            // 2. 无头参数 → 运行时(经 SessionState 跨域重载)
            DroneSim.HeadlessBridge.SetHeadless(argMode, argScenario);
            SessionState.SetString(kShotTimes, string.Join(",", shotTimes));
            Debug.Log($"[SimRunner] 无头模式 dsMode={argMode} scenario={argScenario} shots={string.Join(",", shotTimes)}");

            // 3. 关闭自动刷新(Unity 6 无 AssetDatabase.autoRefresh,改走 EditorPrefs):
            //    "kAutoRefreshMode": 0=启用 1=仅Play外启用 2=禁用;旧键 "kAutoRefresh" 一并处理
            SessionState.SetInt(kPrevAutoMode, EditorPrefs.GetInt("kAutoRefreshMode", 0));
            SessionState.SetBool(kPrevAutoBool, EditorPrefs.GetBool("kAutoRefresh", true));
            EditorPrefs.SetInt("kAutoRefreshMode", 2);
            EditorPrefs.SetBool("kAutoRefresh", false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 4. 等待编译完全空闲后进入 Play(由 OnEditorUpdate 驱动)
            SessionState.SetBool(kPendingPlay, true);
            Debug.Log("[SimRunner] 场景就绪,等待编译空闲后进入 Play 模式");
        }

        static void ParseArgs()
        {
            // 兼容 "-dsMode=manual" 与 "-dsMode manual" 两种形式
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (!a.StartsWith("-")) continue;
                string val = null;
                int eq = a.IndexOf('=');
                if (eq > 0) { val = a.Substring(eq + 1); a = a.Substring(0, eq); }

                if (a == "-dsMode") argMode = TakeVal(ref val, args, ref i) ?? argMode;
                else if (a == "-scenario") argScenario = TakeVal(ref val, args, ref i) ?? argScenario;
                else if (a == "-shots")
                {
                    var s = TakeVal(ref val, args, ref i);
                    if (string.IsNullOrEmpty(s)) continue;
                    try
                    {
                        var parts = s.Split(',');
                        var list = new System.Collections.Generic.List<float>();
                        foreach (var p in parts)
                            if (float.TryParse(p.Trim(), out var v)) list.Add(v);
                        if (list.Count > 0) shotTimes = list.ToArray();
                    }
                    catch { Debug.LogWarning("[SimRunner] -shots 解析失败,用默认时刻"); }
                }
            }
        }

        /// <summary>取参值:等号内联优先,否则取下一个非开关参数</summary>
        static string TakeVal(ref string inline, string[] args, ref int i)
        {
            if (inline != null) return inline;
            if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
            {
                i++;
                return args[i];
            }
            return null;
        }

        static void OnEditorUpdate()
        {
            if (!SessionState.GetBool(kActive, false)) return;

            // 等待脚本编译/资源导入完全空闲后再进入 Play,杜绝 Play 中途域重载
            if (SessionState.GetBool(kPendingPlay, false))
            {
                if (EditorApplication.isPlaying)
                {
                    SessionState.SetBool(kPendingPlay, false);
                    LoadShotsFromSession();   // 域重载已把静态数组洗回默认,从 SessionState 恢复
                }
                else if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                {
                    EditorApplication.isPlaying = true;
                    Debug.Log("[SimRunner] 编译空闲,进入 Play 模式,开始按计划截图");
                }
                return;
            }

            if (!EditorApplication.isPlaying) return;

            string outDir = SessionState.GetString(kOutDir, "");
            if (string.IsNullOrEmpty(outDir)) return;

            if (SessionState.GetFloat(kPlayStart, -1f) < 0f)
            {
                SessionState.SetFloat(kPlayStart, Time.realtimeSinceStartup);
                SessionState.SetFloat(kNextPing, Time.realtimeSinceStartup + 30f);
            }

            float t = Time.realtimeSinceStartup - SessionState.GetFloat(kPlayStart, 0f);
            if (t < 0f)
            {
                SessionState.SetFloat(kPlayStart, Time.realtimeSinceStartup);
                return;
            }

            if (Time.realtimeSinceStartup > SessionState.GetFloat(kNextPing, 0f))
            {
                SessionState.SetFloat(kNextPing, Time.realtimeSinceStartup + 30f);
                Debug.Log($"[SimRunner] 运行中 t={t:F0}s sim={DroneSim.DrillClock.SimTime:F0}s state={DroneSim.DrillClock.State}");
            }

            int idx = SessionState.GetInt(kShotIndex, 0);
            if (idx < shotTimes.Length && t >= shotTimes[idx])
            {
                Capture(Path.Combine(outDir, $"shot_{(int)shotTimes[idx]:D2}s.png"));
                DumpState(outDir);
                idx++;
                SessionState.SetInt(kShotIndex, idx);
            }

            if (idx >= shotTimes.Length)
            {
                int exitCode = DroneSim.HeadlessAssert.FailCount > 0 ? 3 : 0;
                Debug.Log($"[SimRunner] 截图完成,退出编辑器(退出码 {exitCode})");
                SessionState.SetBool(kActive, false);
                RestoreAutoRefresh();
                EditorApplication.Exit(exitCode);
            }
            else if (t > shotTimes[shotTimes.Length - 1] + 60f)   // 看门狗:超时强退
            {
                Debug.LogError($"[SimRunner] 看门狗超时 t={t:F0}s,强制退出(退出码 2)");
                DumpState(outDir);
                SessionState.SetBool(kActive, false);
                RestoreAutoRefresh();
                EditorApplication.Exit(2);
            }
        }

        static void LoadShotsFromSession()
        {
            var s = SessionState.GetString(kShotTimes, "");
            if (string.IsNullOrEmpty(s)) return;
            var list = new System.Collections.Generic.List<float>();
            foreach (var p in s.Split(','))
                if (float.TryParse(p.Trim(), out var v)) list.Add(v);
            if (list.Count > 0) shotTimes = list.ToArray();
        }

        /// <summary>还原自动刷新偏好(批处理退出前必须调用,否则影响用户编辑器设置)</summary>
        static void RestoreAutoRefresh()
        {
            EditorPrefs.SetInt("kAutoRefreshMode", SessionState.GetInt(kPrevAutoMode, 0));
            EditorPrefs.SetBool("kAutoRefresh", SessionState.GetBool(kPrevAutoBool, true));
        }

        static void Capture(string file)
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[SimRunner] 找不到主相机,跳过截图"); return; }

            // V5:2× 超采样渲染再双线性降采样 —— RenderTexture 路径没有 MSAA,
            // 直接渲 1280×720 会满屏锯齿;降采样即高质量 AA
            const int ss = 2;
            var rt = new RenderTexture(W * ss, H * ss, 24, RenderTextureFormat.ARGB32);
            var small = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32);
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            Graphics.Blit(rt, small);
            RenderTexture.active = small;

            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());

            RenderTexture.active = prevActive;
            cam.targetTexture = null;
            rt.Release();
            small.Release();
            Object.DestroyImmediate(tex);
            var e = cam.transform.rotation.eulerAngles;
            Debug.Log($"[SimRunner] 截图已保存: {file}  cam={cam.transform.position:F1} rot=({e.x:F0},{e.y:F0},{e.z:F0}) fov={cam.fieldOfView:F0}");
        }

        static void DumpState(string outDir)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"simTime={DroneSim.DrillClock.SimTime:F0}s state={DroneSim.DrillClock.State} speed={DroneSim.DrillClock.Speed}x");
            sb.AppendLine($"mode={DroneSim.ModeManager.Current?.Id ?? "(菜单)"}");
            sb.AppendLine("---- 模式指标 ----");
            DroneSim.ModeManager.Current?.WriteMetrics(sb);
            sb.AppendLine("---- 事件(最新在前) ----");
            foreach (var e in DroneSim.EventBus.Recent(60))
                sb.AppendLine($"[{e.Time:F0}s][{e.Grade}][{e.Category}] {e.Message}");
            DroneSim.HeadlessAssert.Report(sb);
            File.WriteAllText(Path.Combine(outDir, "state.txt"), sb.ToString());
        }
    }
}

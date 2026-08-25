using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 锁定可视化(双通道):
    /// ① 3D 世界进度圈 —— 24 段环绕红方,按锁定度逐段点亮+游标旋转,
    ///    批处理截图可见(无头验收锁定 50%/100% 时刻);
    /// ② IMGUI 悬浮层 —— 进度圈点阵+四角锁定括号+文本(Play 模式 HUD)。
    /// 闪烁用 DrillClock.SimTime,截图时刻确定。
    /// </summary>
    public class LockVisualizer : MonoBehaviour
    {
        public BlueInterceptor Blue;
        public RedIntruderAI Red;
        public float WorldRadius = 4f;

        static readonly Color AcquireCol = new Color(1f, 0.85f, 0.25f);   // 充能 黄
        static readonly Color LockedCol = new Color(1f, 0.28f, 0.15f);    // 锁定 红

        Transform ringRoot;
        Renderer[] segRends;
        Material matDim, matBright;
        Color brightCol = AcquireCol;

        void OnEnable()
        {
            // ---- 3D 进度圈:24 段绕环 + 亮/暗双材质 ----
            ringRoot = new GameObject("LockRing3D").transform;
            ringRoot.SetParent(transform, false);
            matDim = EnvironmentBuilder.UnlitMat(new Color(1f, 0.85f, 0.25f, 0.10f));
            matBright = EnvironmentBuilder.UnlitMat(AcquireCol);
            segRends = new Renderer[24];
            for (int s = 0; s < 24; s++)
            {
                float a0 = s / 24f * Mathf.PI * 2f;
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (seg.GetComponent<Collider>() != null) Destroy(seg.GetComponent<Collider>());
                seg.name = $"Seg{s}";
                seg.transform.SetParent(ringRoot, false);
                seg.transform.localPosition = new Vector3(Mathf.Cos(a0) * WorldRadius, 0f, Mathf.Sin(a0) * WorldRadius);
                seg.transform.localRotation = Quaternion.Euler(0f, -a0 * Mathf.Rad2Deg + 90f, 0f);
                seg.transform.localScale = new Vector3(1.1f, 0.1f, 0.28f);
                segRends[s] = seg.GetComponent<Renderer>();
                segRends[s].material = matDim;
            }
            ringRoot.gameObject.SetActive(false);
        }

        void Update()
        {
            if (Blue == null || Red == null || !DrillClock.CanSimulate) return;

            float lock01 = Blue.Lock01;
            var rp = Red.transform.position;
            bool show = Red.Phase != RedPhase.Hit && Red.Phase != RedPhase.Escaped && (Blue.Engaged || lock01 > 0f);
            UpdateWorldRing(show, lock01);

            // ---- IMGUI 悬浮层 ----
            var cam = Camera.main;   // 模式相机由 CameraDirector 打 MainCamera tag
            if (cam == null) return;

            var col = Color.Lerp(AcquireCol, LockedCol, lock01);

            if (Red.Phase == RedPhase.Hit)
            {
                Overlay.Label(rp + Vector3.up * 2.2f, "红方 击落", LockedCol);
            }
            else if (Red.Phase == RedPhase.Escaped)
            {
                Overlay.Label(rp + Vector3.up * 2.2f, "红方 已逃逸", new Color(0.7f, 0.7f, 0.75f));
            }
            else
            {
                // 进度圈:半径随锁定度微涨,游标旋转
                float radiusPx = 30f + 12f * lock01;
                float spin = (DrillClock.SimTime * 0.12f) % 1f;
                Overlay.Ring(rp + Vector3.up * 1.2f, radiusPx, lock01, spin, col);

                // 锁定括号:充能中稳定 → 0.6 后 1.5Hz → 锁定 3Hz
                float blink = lock01 >= 1f ? 3f : (lock01 > 0.6f ? 1.5f : 0f);
                Overlay.Bracket(rp, 66f, 50f, col, blink);

                string state = lock01 >= 1f ? "已锁定·拦截中"
                    : Red.Evading ? $"红方反制中 锁定{lock01 * 100f:0}%"
                    : $"锁定 {lock01 * 100f:0}%";
                Overlay.Label(rp + Vector3.up * 2.6f, state, col);
            }

            if (Blue.Engaged)
                Overlay.Label(Blue.transform.position + Vector3.up * 2.2f,
                    $"蓝方拦截 距离{Blue.Range:0}m", new Color(0.5f, 0.8f, 1f));
        }

        /// <summary>3D 环:随动红方下方,亮段数=锁定度×24,游标段旋转,锁定满频闪</summary>
        void UpdateWorldRing(bool show, float lock01)
        {
            if (ringRoot == null) return;
            if (!show) { ringRoot.gameObject.SetActive(false); return; }
            ringRoot.gameObject.SetActive(true);
            ringRoot.position = Red.transform.position + Vector3.down * 0.8f;
            ringRoot.rotation = Quaternion.identity;   // 环平置不随机体姿态

            // 颜色:充能黄 → 锁定红;锁定满 3Hz 方波提亮
            brightCol = Color.Lerp(AcquireCol, LockedCol, lock01);
            float boost = 1f;
            if (lock01 >= 1f)
                boost = Mathf.Sin(DrillClock.SimTime * 3f * Mathf.PI * 2f) > 0f ? 1.6f : 0.8f;
            matBright.SetColor("_Color", new Color(
                Mathf.Clamp01(brightCol.r * boost), Mathf.Clamp01(brightCol.g * boost), Mathf.Clamp01(brightCol.b * boost), 0.95f));

            int lit = Mathf.RoundToInt(lock01 * 24f);
            // 旋转游标:锁定中绕环扫动,未满锁时作为“充能指针”走在填充前沿
            float cursorA = lock01 >= 1f
                ? DrillClock.SimTime * 1.6f * Mathf.PI * 2f
                : lock01 * Mathf.PI * 2f;
            for (int s = 0; s < 24; s++)
            {
                float a0 = s / 24f * Mathf.PI * 2f;
                float dAng = Mathf.Abs(Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, cursorA * Mathf.Rad2Deg));
                bool cursor = lock01 > 0.02f && dAng < 360f / 24f * 0.6f;
                bool on = s < lit || cursor;
                var want = on ? matBright : matDim;
                if (segRends[s].material != want) segRends[s].material = want;
                segRends[s].transform.localScale = cursor
                    ? new Vector3(1.5f, 0.14f, 0.42f) : new Vector3(1.1f, 0.1f, 0.28f);
            }
        }
    }
}

using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 侦察云台相机:挂机体前下方的稳定镜头,朝瞄准方向平滑转动(Slerp),
    /// FOV 平滑变焦;可锁定跟踪已识别目标。暂停时仍可手动环视(unscaled)。
    /// </summary>
    public class ReconCameraRig : MonoBehaviour
    {
        public Transform Drone;
        public ScannableTarget Track;
        public bool Tracking;
        public float FovTarget = 58f;
        public float AimSpeed = 4f;

        Quaternion aim;
        bool aimInit;

        public float AimErrorDeg(ScannableTarget t)
        {
            if (t == null) return 180f;
            return Vector3.Angle(transform.forward, t.transform.position + Vector3.up - transform.position);
        }

        void LateUpdate()
        {
            if (Drone == null) return;
            float dt = DrillClock.CanSimulate ? Time.deltaTime : Time.unscaledDeltaTime;

            // 位置:机体前上方吊舱
            transform.position = Drone.position + Drone.forward * 1.1f + Vector3.up * 0.75f;

            // 瞄准方向:跟踪目标 或 机体前下方
            var aimPoint = Tracking && Track != null
                ? Track.transform.position + Vector3.up * 0.8f
                : Drone.position + Drone.forward * 80f + Vector3.down * 6f;
            var targetRot = Quaternion.LookRotation(aimPoint - transform.position);
            if (!aimInit) { aim = targetRot; aimInit = true; }
            aim = Quaternion.Slerp(aim, targetRot, 1f - Mathf.Exp(-AimSpeed * dt));
            transform.rotation = aim;

            var cam = GetComponent<Camera>();
            if (cam != null) cam.fieldOfView = Mathf.MoveTowards(cam.fieldOfView, FovTarget, 40f * dt);
        }
    }
}

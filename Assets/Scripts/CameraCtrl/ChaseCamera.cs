using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 追尾跟随相机:平滑追踪机体,速度前视,右键环绕/俯仰,滚轮距离,
    /// C 键切换 追尾/自由视角。跟随快慢随倍速缩放(高速不脱靶),
    /// 暂停时输入响应不冻结。附 Shake 冲击震动接口(P8 应急复用)。
    /// </summary>
    public class ChaseCamera : MonoBehaviour
    {
        public Transform Target;
        public bool FollowBehind = true;    // 追尾(对准机体航向)
        public float Dist = 13f;
        public Transform LookOverride;      // 非空:相机看向该点(火场侦察等地面目标取景)

        float yawFree = 180f, pitchFree = 18f;
        float shakeAmp, shakeUntil;

        void LateUpdate()
        {
            if (Target == null) return;
            // 运行时随倍速跟随,暂停时 unscaled 保证视角可调
            float dt = Time.unscaledDeltaTime * (DrillClock.CanSimulate ? DrillClock.Speed : 1f);

            if (Input.GetKeyDown(KeyCode.C)) FollowBehind = !FollowBehind;

            float heading = Target.eulerAngles.y;
            if (FollowBehind && !Input.GetMouseButton(1))
                yawFree = Mathf.LerpAngle(yawFree, heading, 1f - Mathf.Exp(-dt * 2.2f));

            if (Input.GetMouseButton(1))
            {
                yawFree += Input.GetAxis("Mouse X") * 4.5f;
                pitchFree -= Input.GetAxis("Mouse Y") * 2.2f;
            }
            pitchFree = Mathf.Clamp(pitchFree, 6f, 72f);
            Dist = Mathf.Clamp(Dist * (1f - Input.GetAxis("Mouse ScrollWheel") * 1.1f), 5f, 45f);

            var rot = Quaternion.Euler(pitchFree, yawFree, 0f);
            var anchor = Target.position + Vector3.up * 0.6f;
            var desired = anchor - rot * Vector3.forward * Dist;

            var pos = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-dt * 6f));

            // 震动叠加
            if (shakeAmp > 0.001f && Time.realtimeSinceStartup < shakeUntil)
                pos += Random.insideUnitSphere * shakeAmp;

            transform.position = pos;
            var lookPoint = LookOverride != null && LookOverride.gameObject.activeInHierarchy
                ? LookOverride.position + Vector3.up * 1.5f : anchor;
            transform.rotation = Quaternion.LookRotation(lookPoint - pos, Vector3.up);
        }

        /// <summary>设定俯仰角(度,钳 6~72):大角度=相机低位仰拍,配 LookOverride 可俯瞰地面</summary>
        public void SetPitch(float p) => pitchFree = Mathf.Clamp(p, 6f, 72f);

        /// <summary>冲击震动(幅度 m / 时长 s)</summary>
        public void Shake(float amplitude, float duration)
        {
            shakeAmp = amplitude;
            shakeUntil = Time.realtimeSinceStartup + duration;
        }
    }
}

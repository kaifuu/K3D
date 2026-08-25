using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// RTS风格摄像机:WASD平移、右键旋转、滚轮缩放、F聚焦核心。
    /// 用 unscaledDeltaTime:暂停/倍速下视角操作手感一致。
    /// </summary>
    public class RTSCamera : MonoBehaviour
    {
        public Transform focus;
        float yaw = 45f, pitch = 42f, dist = 170f;

        void LateUpdate()
        {
            if (focus == null) return;
            float dt = Time.unscaledDeltaTime;

            // 平移(沿地面切向)
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) move -= Vector3.forward;
            if (Input.GetKey(KeyCode.A)) move -= Vector3.right;
            if (Input.GetKey(KeyCode.D)) move += Vector3.right;
            if (move.sqrMagnitude > 0f)
            {
                move = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * move.normalized;
                focus.position += move * (dist * 0.9f) * dt;
                float r = new Vector2(focus.position.x, focus.position.z).magnitude;
                if (r > 380f) { var p = focus.position; p *= 380f / r; focus.position = p; }
            }

            if (Input.GetKey(KeyCode.F))
                focus.position = Vector3.Lerp(focus.position, Vector3.zero, dt * 4f);

            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * 5f;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2f, 15f, 80f);
            }
            dist = Mathf.Clamp(dist * (1f - Input.GetAxis("Mouse ScrollWheel") * 1.2f), 40f, 420f);

            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = focus.position - rot * Vector3.forward * dist;
            transform.rotation = rot;
        }
    }
}

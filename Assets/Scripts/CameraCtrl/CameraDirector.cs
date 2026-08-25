using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 相机装配工具:统一创建模式相机并挂接操控组件。
    /// 相机物体挂 ModeRoot 下,切模式随树销毁。
    /// </summary>
    public static class CameraDirector
    {
        public static Camera CreateCamera(Transform parent, string name = "ModeCamera")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.07f, 0.1f);
            cam.fieldOfView = 58f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1500f;
            go.AddComponent<AudioListener>();
            return cam;
        }

        public static ChaseCamera Follow(Camera cam, Transform target, float dist = 13f)
        {
            var chase = cam.gameObject.AddComponent<ChaseCamera>();
            chase.Target = target;
            chase.Dist = dist;
            var behind = target.position - target.forward * dist + Vector3.up * 4f;
            cam.transform.position = behind;
            return chase;
        }

        public static RTSCamera RTS(Camera cam, out Transform focus)
        {
            var focusGo = new GameObject("CamFocus");
            focusGo.transform.SetParent(cam.transform.parent, false);
            focus = focusGo.transform;
            var rts = cam.gameObject.AddComponent<RTSCamera>();
            rts.focus = focus;
            return rts;
        }
    }
}

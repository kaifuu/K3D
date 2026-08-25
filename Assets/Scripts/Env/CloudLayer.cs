using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// V5 云层:程序云贴图公告板环(480~740m 高空),颜色/亮度跟随环境天光 ——
    /// 白昼亮白、黄昏自动偏暖粉、夜晚压暗;不投影、不注册热像、不参与演练逻辑。
    /// </summary>
    public class CloudLayer : MonoBehaviour
    {
        Material cloudMat;

        public static CloudLayer Build(Transform parent)
        {
            var root = new GameObject("CloudLayer");
            root.transform.SetParent(parent, false);
            var layer = root.AddComponent<CloudLayer>();
            layer.cloudMat = new Material(Shader.Find("Unlit/Transparent"))
            {
                mainTexture = MaterialLib.CloudTexture(),
            };

            for (int i = 0; i < 11; i++)
            {
                float h1 = Hash(i + 1f), h2 = Hash(i + 33f), h3 = Hash(i + 77f);
                float a = i / 11f * Mathf.PI * 2f + h1 * 0.5f;
                float r = 480f + h2 * 260f;
                var p = new Vector3(Mathf.Cos(a) * r, 180f + h3 * 160f, Mathf.Sin(a) * r);
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                DestroyCol(quad);
                quad.name = $"Cloud{i}";
                quad.transform.SetParent(root.transform, false);
                quad.transform.position = p;
                // Quad 正面朝自身 -Z:让 +Z 朝外、-Z 才面向场心(之前朝内被背面剔除,云全没画上)
                quad.transform.rotation = Quaternion.LookRotation(new Vector3(p.x, 0f, p.z).normalized);
                float w = 170f + h2 * 170f;
                quad.transform.localScale = new Vector3(w, w * 0.45f, 1f);
                var rend = quad.GetComponent<Renderer>();
                rend.sharedMaterial = layer.cloudMat;   // 共享材质,Update 改色全体生效
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }
            return layer;
        }

        static float Hash(float i)
        {
            float h = Mathf.Sin(i * 12.9898f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        static void DestroyCol(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }

        void Update()
        {
            // 云色 = 环境天色推亮(夜暗昼亮,黄昏随天色偏暖)
            var amb = RenderSettings.ambientSkyColor;
            float lum = Mathf.Clamp01(Mathf.Max(amb.r, Mathf.Max(amb.g, amb.b)) * 2.3f);
            float k = Mathf.Lerp(0.10f, 1f, lum);
            cloudMat.color = new Color(amb.r * 2.05f * k, amb.g * 2.05f * k, amb.b * 2.05f * k, 0.9f);
        }
    }
}

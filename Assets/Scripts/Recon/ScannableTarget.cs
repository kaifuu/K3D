using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 可扫描目标:过程式拼装 车/人/设备 三类热学实体,登记渲染器注册表;
    /// 被 ScanPulse 波束扫到即"识别"——点亮包壳描边 + 事件上报。
    /// Wander 目标绕锚点小幅游走(演练时间驱动,无头确定)。
    /// </summary>
    public class ScannableTarget : MonoBehaviour
    {
        public string Label = "目标";
        public ThermalClass Class = ThermalClass.Warm;
        public bool Wander;
        public bool Identified { get; private set; }
        public float IdentifiedAt { get; private set; } = -1f;

        Vector3 anchor;
        Renderer shellRend;

        public static ScannableTarget Create(Transform parent, string label, ThermalClass cls,
            Vector3 pos, bool wander = false)
        {
            var go = new GameObject($"Target_{label}");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var t = go.AddComponent<ScannableTarget>();
            t.Label = label;
            t.Class = cls;
            t.Wander = wander;
            t.anchor = pos;
            t.BuildBody();
            return t;
        }

        void BuildBody()
        {
            Renderer main = null;
            switch (Class)
            {
                case ThermalClass.Hot: main = BuildVehicle(); break;
                case ThermalClass.Warm: main = BuildPerson(); break;
                default: main = BuildEquipment(); break;
            }

            // 包壳描边(反壳法):不透明琥珀壳 ×1.07,队列 2001 画在车体(2000)之后,
            // 壳面比车面远 7% 被 ZTest 剔掉,只留轮廓一圈亮边
            var shellGo = new GameObject("OutlineShell");
            shellGo.transform.SetParent(transform, false);
            shellGo.transform.localPosition = Vector3.zero;
            shellGo.transform.localRotation = Quaternion.identity;
            shellGo.transform.localScale = Vector3.one * 1.07f;
            var mf = shellGo.AddComponent<MeshFilter>();
            mf.sharedMesh = main.GetComponent<MeshFilter>().sharedMesh;
            var mr = shellGo.AddComponent<MeshRenderer>();
            var m = EnvironmentBuilder.FlatMat(new Color(1f, 0.62f, 0.1f));
            m.renderQueue = 2001;
            mr.sharedMaterial = m;
            mr.enabled = false;
            shellRend = mr;
            CopyLocal(main.transform, shellGo.transform);
        }

        static void CopyLocal(Transform src, Transform dst)
        {
            dst.localPosition = src.localPosition;
            dst.localRotation = src.localRotation;
            dst.localScale = src.localScale * 1.07f;
        }

        Renderer BuildVehicle()
        {
            var bodyGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyGo.name = "Body";
            bodyGo.transform.SetParent(transform, false);
            bodyGo.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            bodyGo.transform.localScale = new Vector3(2.3f, 1f, 5f);
            var bodyR = bodyGo.GetComponent<Renderer>();
            bodyR.material = MaterialLib.Metal(new Color(0.24f, 0.26f, 0.3f), 3f);
            Object.Destroy(bodyGo.GetComponent<Collider>());

            var cabGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabGo.name = "Cab";
            cabGo.transform.SetParent(transform, false);
            cabGo.transform.localPosition = new Vector3(0f, 1.6f, -0.6f);
            cabGo.transform.localScale = new Vector3(2f, 0.8f, 1.8f);
            var cabR = cabGo.GetComponent<Renderer>();
            cabR.material = MaterialLib.Metal(new Color(0.2f, 0.22f, 0.26f), 2f);
            Object.Destroy(cabGo.GetComponent<Collider>());

            // 挡风玻璃 + 前后保险杠 + 货厢栏板(V2 细节)
            var glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(glass.GetComponent<Collider>());
            glass.name = "Windshield";
            glass.transform.SetParent(transform, false);
            glass.transform.localPosition = new Vector3(0f, 1.68f, 0.35f);
            glass.transform.localScale = new Vector3(1.8f, 0.5f, 0.12f);
            glass.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            var glassMat = new Material(Shader.Find("Standard")) { color = new Color(0.08f, 0.1f, 0.13f) };
            glassMat.SetFloat("_Glossiness", 0.9f);
            glass.GetComponent<Renderer>().material = glassMat;

            var bumperMat = MaterialLib.Metal(new Color(0.16f, 0.17f, 0.19f), 2f);
            var bumpF = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(bumpF.GetComponent<Collider>());
            bumpF.name = "BumperF";
            bumpF.transform.SetParent(transform, false);
            bumpF.transform.localPosition = new Vector3(0f, 0.55f, 2.6f);
            bumpF.transform.localScale = new Vector3(2.4f, 0.3f, 0.2f);
            bumpF.GetComponent<Renderer>().material = bumperMat;

            var railMat = MaterialLib.Metal(new Color(0.2f, 0.22f, 0.25f), 2f);
            for (int rl = 0; rl < 3; rl++)
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(rail.GetComponent<Collider>());
                rail.name = $"BedRail{rl}";
                rail.transform.SetParent(transform, false);
                bool side = rl < 2;
                rail.transform.localScale = side ? new Vector3(0.08f, 0.35f, 3f) : new Vector3(2.2f, 0.08f, 0.08f);
                rail.transform.localPosition = side
                    ? new Vector3(rl == 0 ? 1.12f : -1.12f, 1.5f, -1f)
                    : new Vector3(0f, 1.65f, -2.5f);
                rail.GetComponent<Renderer>().material = railMat;
            }

            // 前大灯(熄火状态微光,热像里发动机舱才是热源)
            for (int hl = 0; hl < 2; hl++)
            {
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(lamp.GetComponent<Collider>());
                lamp.name = $"Headlight{hl}";
                lamp.transform.SetParent(transform, false);
                lamp.transform.localPosition = new Vector3(hl == 0 ? 0.75f : -0.75f, 1.05f, 2.52f);
                lamp.transform.localScale = new Vector3(0.32f, 0.18f, 0.06f);
                lamp.GetComponent<Renderer>().material =
                    EnvironmentBuilder.UnlitMat(new Color(0.85f, 0.87f, 0.75f, 0.6f));
            }

            // 四轮(深色轮胎 + 轮毂)
            for (int i = 0; i < 4; i++)
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                w.name = $"Wheel{i}";
                w.transform.SetParent(transform, false);
                w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                w.transform.localPosition = new Vector3(i % 2 == 0 ? 1.2f : -1.2f, 0.42f, i < 2 ? 1.7f : -1.7f);
                w.transform.localScale = new Vector3(0.42f, 0.14f, 0.42f);
                w.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = new Color(0.07f, 0.07f, 0.08f) };
                Object.Destroy(w.GetComponent<Collider>());
            }

            RendererRegistry.Register(bodyR, ThermalClass.Hot);   // 发动机舱整体视为热
            RendererRegistry.Register(cabR, ThermalClass.Hot);
            return bodyR;
        }

        Renderer BuildPerson()
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            p.name = "Body";
            p.transform.SetParent(transform, false);
            p.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            p.transform.localScale = new Vector3(0.42f, 0.45f, 0.42f);
            var r = p.GetComponent<Renderer>();
            r.material = EnvironmentBuilder.StdMat(new Color(0.35f, 0.3f, 0.27f));
            Object.Destroy(p.GetComponent<Collider>());

            var h = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            h.name = "Head";
            h.transform.SetParent(transform, false);
            h.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            h.transform.localScale = Vector3.one * 0.3f;
            var hr = h.GetComponent<Renderer>();
            hr.material = EnvironmentBuilder.StdMat(new Color(0.4f, 0.34f, 0.3f));
            Object.Destroy(h.GetComponent<Collider>());

            // 双臂(V2 细节,与 PropKit.Person 同风格)
            for (int i = 0; i < 2; i++)
            {
                var arm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                arm.name = $"Arm{i}";
                arm.transform.SetParent(transform, false);
                arm.transform.localPosition = new Vector3(i == 0 ? 0.32f : -0.32f, 1.0f, 0f);
                arm.transform.localScale = new Vector3(0.13f, 0.32f, 0.13f);
                arm.GetComponent<Renderer>().material = r.material;
                Object.Destroy(arm.GetComponent<Collider>());
            }

            RendererRegistry.Register(r, ThermalClass.Warm);
            RendererRegistry.Register(hr, ThermalClass.Warm);
            return r;
        }

        Renderer BuildEquipment()
        {
            // 油桶(金属桶身 + 两道箍环)
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            c.name = "Crate";
            c.transform.SetParent(transform, false);
            c.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            c.transform.localScale = new Vector3(1.2f, 0.7f, 1.2f);
            var r = c.GetComponent<Renderer>();
            r.material = MaterialLib.Metal(new Color(0.32f, 0.38f, 0.34f), 2f);
            Object.Destroy(c.GetComponent<Collider>());

            var hoopMat = MaterialLib.Metal(new Color(0.22f, 0.26f, 0.24f), 2f);
            for (int i = 0; i < 2; i++)
            {
                var hoop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hoop.name = $"Hoop{i}";
                hoop.transform.SetParent(transform, false);
                hoop.transform.localPosition = new Vector3(0f, i == 0 ? 1.05f : 0.35f, 0f);
                hoop.transform.localScale = new Vector3(1.26f, 0.05f, 1.26f);
                hoop.GetComponent<Renderer>().material = hoopMat;
                Object.Destroy(hoop.GetComponent<Collider>());
            }

            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = "Box";
            b.transform.SetParent(transform, false);
            b.transform.localPosition = new Vector3(1.1f, 0.4f, 0.4f);
            b.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            var br = b.GetComponent<Renderer>();
            br.material = MaterialLib.Metal(new Color(0.26f, 0.32f, 0.3f), 2f);
            Object.Destroy(b.GetComponent<Collider>());

            RendererRegistry.Register(r, ThermalClass.Cold);
            RendererRegistry.Register(br, ThermalClass.Cold);
            return r;
        }

        public void Identify()
        {
            if (Identified) return;
            Identified = true;
            IdentifiedAt = DrillClock.SimTime;
            if (shellRend != null) shellRend.enabled = true;
            EventBus.Publish("侦察", "target", $"识别目标 {Label}({ClassName()})", EventGrade.Op, transform.position);
        }

        public void ResetScan()
        {
            Identified = false;
            IdentifiedAt = -1f;
            if (shellRend != null) shellRend.enabled = false;
        }

        public string ClassName() => Class switch
        {
            ThermalClass.Hot => "热源", ThermalClass.Warm => "温感", ThermalClass.Cold => "冷源", _ => "环境"
        };

        void Update()
        {
            if (!Wander || !DrillClock.CanSimulate) return;
            float t = DrillClock.SimTime * 0.25f;
            var off = new Vector3(Mathf.Sin(t * 1.3f) * 3f, 0f, Mathf.Cos(t) * 3f);
            transform.position = anchor + off;
        }
    }
}

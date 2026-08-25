using UnityEngine;

namespace DroneSim
{
    public enum DroneRole { Player, Blue, Red, Hostile, Civilian }

    /// <summary>
    /// 过程式机体工厂(V2 实物还原精雕版):
    /// 四旋翼真实构型 —— 碳纤机身+四臂+电机舱+双叶旋翼(Rotor0..3 旋转枢轴)+云台相机+起落架+航空灯。
    /// 对外契约保持不变:Rotor0..3 为根直接子物体(RotorSpin 按名查找)、
    /// Body 带放大 BoxCollider(点选判定)、SelectionMark/Trail 子物体、模板禁用。
    /// 未来扩展:CC0 fbx 可放 Resources/Art/Models/ 下,在本类开头加 TryModel() 双轨加载。
    /// </summary>
    public static class DroneFactory
    {
        static readonly Color[] roleTint =
        {
            new Color(0.3f, 0.9f, 1f),    // Player 蓝
            new Color(0.35f, 0.55f, 1f),  // Blue 深蓝
            new Color(1f, 0.3f, 0.25f),   // Red 红
            new Color(0.9f, 0.35f, 0.15f),// Hostile 橙红
            new Color(0.9f, 0.9f, 0.9f),  // Civilian 白
        };

        /// <summary>构建禁用状态的机体模板(挂 EnemyDrone+RotorSpin);克隆后 SetActive(true)。
        /// withEnemy=false 供可操控机体使用(不加监管玩法组件)。</summary>
        public static GameObject BuildTemplate(DroneRole role, string name = "DroneTemplate", bool withEnemy = true)
        {
            var root = new GameObject(name);
            var tint = roleTint[(int)role];

            Material carbon = MaterialLib.Metal(new Color(0.09f, 0.1f, 0.12f), 1f);
            Material podMat = MaterialLib.Metal(new Color(0.2f, 0.22f, 0.26f), 1f);
            Material bladeMat = new Material(Shader.Find("Standard")) { color = new Color(0.14f, 0.15f, 0.17f) };
            bladeMat.SetFloat("_Glossiness", 0.3f);
            Material accentMat = new Material(Shader.Find("Standard")) { color = tint };
            accentMat.SetFloat("_Glossiness", 0.45f);

            // ---- 机身:主板上盖 + 流线顶罩 + GPS 圆顶 + 腹板 ----
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.5f, 0.12f, 0.75f);
            body.GetComponent<Renderer>().material = carbon;
            var bc = body.GetComponent<BoxCollider>();
            // 命中判定放大到 ~2.9m(与旧版世界体积一致;size 为本地单位,除以机身缩放)
            bc.size = new Vector3(5.7f, 6.4f, 3.8f);

            var hump = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            DestroyCollider(hump);
            hump.name = "Hump";
            hump.transform.SetParent(root.transform, false);
            hump.transform.localScale = new Vector3(0.28f, 0.16f, 0.42f);
            hump.transform.localPosition = new Vector3(0f, 0.12f, 0.02f);
            hump.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hump.GetComponent<Renderer>().material = accentMat;

            var gps = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyCollider(gps);
            gps.name = "GpsDome";
            gps.transform.SetParent(root.transform, false);
            gps.transform.localScale = new Vector3(0.12f, 0.09f, 0.12f);
            gps.transform.localPosition = new Vector3(0f, 0.24f, -0.12f);
            gps.GetComponent<Renderer>().material = podMat;

            // ---- 航空灯:前白后红 + 顶部角色灯 ----
            var ledF = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCollider(ledF);
            ledF.name = "LedFront";
            ledF.transform.SetParent(root.transform, false);
            ledF.transform.localScale = new Vector3(0.06f, 0.03f, 0.03f);
            ledF.transform.localPosition = new Vector3(0f, 0.02f, 0.42f);
            ledF.GetComponent<Renderer>().material = EnvironmentBuilder.UnlitMat(new Color(0.95f, 0.97f, 1f, 0.95f));

            var ledR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCollider(ledR);
            ledR.name = "LedRear";
            ledR.transform.SetParent(root.transform, false);
            ledR.transform.localScale = new Vector3(0.06f, 0.03f, 0.03f);
            ledR.transform.localPosition = new Vector3(0f, 0.02f, -0.42f);
            ledR.GetComponent<Renderer>().material = EnvironmentBuilder.UnlitMat(new Color(1f, 0.12f, 0.1f, 0.95f));

            // ---- 四臂 + 电机舱 + 双叶旋翼(Rotor{i}=旋转枢轴,契约保持) ----
            var blurMat = new Material(Shader.Find("Sprites/Default"));
            blurMat.color = new Color(0.65f, 0.7f, 0.78f, 0.16f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                var armPos = new Vector3(Mathf.Cos(a) * 0.9f, 0f, Mathf.Sin(a) * 0.9f);

                var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCollider(arm);
                arm.name = $"Arm{i}";
                arm.transform.SetParent(root.transform, false);
                arm.transform.localScale = new Vector3(0.09f, 0.06f, 0.92f);
                arm.transform.localPosition = armPos * 0.5f;
                arm.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
                arm.GetComponent<Renderer>().material = carbon;

                // 臂端角色色帽(远距离敌我识别)
                var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCollider(cap);
                cap.name = $"ArmCap{i}";
                cap.transform.SetParent(root.transform, false);
                cap.transform.localScale = new Vector3(0.1f, 0.07f, 0.12f);
                cap.transform.localPosition = armPos * 0.97f;
                cap.transform.localRotation = arm.transform.localRotation;
                cap.GetComponent<Renderer>().material = accentMat;

                var pod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                DestroyCollider(pod);
                pod.name = $"MotorPod{i}";
                pod.transform.SetParent(root.transform, false);
                pod.transform.localScale = new Vector3(0.16f, 0.07f, 0.16f);
                pod.transform.localPosition = armPos + Vector3.up * 0.07f;
                pod.GetComponent<Renderer>().material = podMat;

                // 旋转枢轴:两片交叉桨叶 + 桨毂 + 高速残影盘
                var rotor = new GameObject($"Rotor{i}");
                rotor.transform.SetParent(root.transform, false);
                rotor.transform.localPosition = armPos + Vector3.up * 0.17f;

                for (int b = 0; b < 2; b++)
                {
                    var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    DestroyCollider(blade);
                    blade.name = $"Blade{b}";
                    blade.transform.SetParent(rotor.transform, false);
                    blade.transform.localScale = new Vector3(0.035f, 0.008f, 1.15f);
                    blade.transform.localRotation = Quaternion.Euler(0f, b * 90f, 0f);
                    blade.GetComponent<Renderer>().material = bladeMat;
                }
                var hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                DestroyCollider(hub);
                hub.name = "Hub";
                hub.transform.SetParent(rotor.transform, false);
                hub.transform.localScale = new Vector3(0.09f, 0.02f, 0.09f);
                hub.GetComponent<Renderer>().material = podMat;

                var blur = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                DestroyCollider(blur);
                blur.name = "Blur";
                blur.transform.SetParent(rotor.transform, false);
                blur.transform.localScale = new Vector3(1.18f, 0.004f, 1.18f);
                blur.transform.localPosition = new Vector3(0f, -0.015f, 0f);
                blur.GetComponent<Renderer>().material = blurMat;
            }

            // ---- 云台相机(机腹前部) ----
            var gimbal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyCollider(gimbal);
            gimbal.name = "Gimbal";
            gimbal.transform.SetParent(root.transform, false);
            gimbal.transform.localScale = new Vector3(0.16f, 0.14f, 0.16f);
            gimbal.transform.localPosition = new Vector3(0f, -0.13f, 0.22f);
            gimbal.GetComponent<Renderer>().material = podMat;

            var lens = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCollider(lens);
            lens.name = "Lens";
            lens.transform.SetParent(root.transform, false);
            lens.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            lens.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
            lens.transform.localPosition = new Vector3(0f, -0.13f, 0.31f);
            lens.GetComponent<Renderer>().material = EnvironmentBuilder.UnlitMat(new Color(0.1f, 0.12f, 0.16f, 1f));

            // ---- 起落架:两条滑橇 + 四根撑杆 ----
            var gearMat = podMat;
            for (int s = 0; s < 2; s++)
            {
                float sx = s == 0 ? -0.24f : 0.24f;
                var skid = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCollider(skid);
                skid.name = $"Skid{s}";
                skid.transform.SetParent(root.transform, false);
                skid.transform.localScale = new Vector3(0.04f, 0.035f, 0.62f);
                skid.transform.localPosition = new Vector3(sx, -0.28f, 0f);
                skid.GetComponent<Renderer>().material = gearMat;

                for (int st = 0; st < 2; st++)
                {
                    var strut = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    DestroyCollider(strut);
                    strut.name = $"Strut{s}{st}";
                    strut.transform.SetParent(root.transform, false);
                    strut.transform.localScale = new Vector3(0.025f, 0.1f, 0.025f);
                    strut.transform.localPosition = new Vector3(sx * 0.8f, -0.18f, st == 0 ? 0.22f : -0.22f);
                    strut.transform.localRotation = Quaternion.Euler(0f, 0f, s == 0 ? 18f : -18f);
                    strut.GetComponent<Renderer>().material = gearMat;
                }
            }

            // ---- 尾迹 + 选择指示球(契约保持) ----
            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(root.transform, false);
            trailGo.transform.localPosition = new Vector3(0f, 0f, -0.6f);
            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = 1.6f;
            trail.startWidth = 0.5f;
            trail.endWidth = 0f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(1f, 1f, 1f, 0.35f);
            trail.endColor = new Color(1f, 1f, 1f, 0f);
            trail.numCapVertices = 2;

            var selGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyCollider(selGo);
            selGo.name = "SelectionMark";
            selGo.transform.SetParent(root.transform, false);
            selGo.transform.localScale = Vector3.one * 2.8f;
            var smat = new Material(Shader.Find("Sprites/Default"));
            smat.color = new Color(1f, 1f, 1f, 0.14f);
            selGo.GetComponent<Renderer>().material = smat;
            selGo.SetActive(false);

            if (withEnemy) root.AddComponent<EnemyDrone>();
            root.AddComponent<RotorSpin>();

            root.SetActive(false);   // 模板永不参与运行
            return root;
        }

        /// <summary>一步到位生成场景机体:模板克隆→定位→启用→销毁模板</summary>
        public static GameObject Spawn(DroneRole role, Transform parent, Vector3 pos, string name, bool withEnemy = false)
        {
            var tpl = BuildTemplate(role, name + "Tpl", withEnemy);
            var go = Object.Instantiate(tpl, parent);
            Object.Destroy(tpl);
            go.name = name;
            go.transform.position = pos;
            go.SetActive(true);
            return go;
        }

        static void DestroyCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }
    }
}

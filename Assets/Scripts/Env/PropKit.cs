using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 过程式道具库(V2 实物还原):模式场景里的常见实物 ——
    /// 参照建筑 / 波纹铁仓库 / 空投供给箱 / 地面人员 / 桁架障碍塔。
    /// 全部走 MaterialLib 贴图材质(缺贴图回退纯色);契约:只造视觉,不带碰撞语义
    /// (碰撞/避障走各模式自有的解析判定)。
    /// </summary>
    public static class PropKit
    {
        static Material glassCache;

        /// <summary>幕墙窗带玻璃(整场共享一份,利于合批)</summary>
        static Material GlassMat()
        {
            if (glassCache == null)
            {
                glassCache = new Material(Shader.Find("Standard")) { color = new Color(0.12f, 0.15f, 0.19f) };
                glassCache.SetFloat("_Glossiness", 0.85f);
            }
            return glassCache;
        }

        /// <summary>参照建筑:立面贴图 + 楼层幕墙窗带 + 基座 + 檐口压顶 + 屋顶设备间/空调外机
        /// (远景点缀,替换单色方块;窗带为包裹式深色玻璃条,CS 写字楼观感)</summary>
        public static Transform Building(Transform parent, Vector3 pos, float w, float h, float d, int variant = -1)
        {
            if (variant < 0) variant = Mathf.Abs((int)(pos.x * 3f + pos.z)) % 3;
            var root = new GameObject($"Building{variant}");
            root.transform.SetParent(parent, false);
            root.transform.position = pos + Vector3.up * (h / 2f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(w, h, d);
            body.GetComponent<Renderer>().material =
                MaterialLib.Wall(variant, new Vector2(Mathf.Max(w, d) / 3.5f, h / 3.5f));

            // 基座(首层深色裙边)
            var plinth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(plinth);
            plinth.name = "Plinth";
            plinth.transform.SetParent(root.transform, false);
            plinth.transform.localScale = new Vector3(w + 0.16f, 1.1f, d + 0.16f);
            plinth.transform.localPosition = new Vector3(0f, -h / 2f + 0.55f, 0f);
            plinth.GetComponent<Renderer>().material = MaterialLib.Roof(new Vector2(3f, 1f));

            // 楼层幕墙窗带(2 层起,每层一条包裹式深色玻璃)
            int floors = Mathf.Clamp(Mathf.RoundToInt(h / 3.4f), 2, 8);
            for (int f = 1; f < floors; f++)
            {
                float bandY = -h / 2f + 1.1f + f * ((h - 1.1f) / floors);
                var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCol(band);
                band.name = $"WindowBand{f}";
                band.transform.SetParent(root.transform, false);
                band.transform.localScale = new Vector3(w + 0.1f, 1.15f, d + 0.1f);
                band.transform.localPosition = new Vector3(0f, bandY + 0.55f, 0f);
                band.GetComponent<Renderer>().material = GlassMat();
            }

            var cornice = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(cornice);
            cornice.name = "Cornice";
            cornice.transform.SetParent(root.transform, false);
            cornice.transform.localScale = new Vector3(w + 0.6f, 0.5f, d + 0.6f);
            cornice.transform.localPosition = new Vector3(0f, h / 2f - 0.25f, 0f);
            cornice.GetComponent<Renderer>().material = MaterialLib.Roof(new Vector2(4f, 4f));

            // 屋顶空调外机 ×2
            if (h > 10f)
            {
                Material acMat = MaterialLib.Metal(new Color(0.55f, 0.58f, 0.6f), 2f);
                for (int a = 0; a < 2; a++)
                {
                    var ac = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    DestroyCol(ac);
                    ac.name = $"RoofAC{a}";
                    ac.transform.SetParent(root.transform, false);
                    ac.transform.localScale = new Vector3(1.7f, 0.9f, 1.3f);
                    ac.transform.localPosition = new Vector3(a == 0 ? -w * 0.26f : w * 0.3f, h / 2f + 0.45f, a == 0 ? -d * 0.2f : d * 0.24f);
                    ac.GetComponent<Renderer>().material = acMat;
                }
            }

            if (h > 14f)   // 高层加屋顶设备间
            {
                var pent = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCol(pent);
                pent.name = "Penthouse";
                pent.transform.SetParent(root.transform, false);
                pent.transform.localScale = new Vector3(w * 0.35f, 2.2f, d * 0.35f);
                pent.transform.localPosition = new Vector3(w * 0.18f, h / 2f + 1.1f, -d * 0.18f);
                pent.GetComponent<Renderer>().material = MaterialLib.Roof(new Vector2(2f, 2f));
            }
            return root.transform;
        }

        /// <summary>波纹铁皮仓库:金属墙体 + 双坡屋面 + 卷帘门 + 屋顶通风器 + 装卸平台。
        /// pos 为建筑底部中心。火情建筑的英雄帧构图与原 12×14×12 方块一致。</summary>
        public static Transform Warehouse(Transform parent, Vector3 pos, float w, float h, float d, float rotY = 0f)
        {
            var root = new GameObject("Warehouse");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            Material wallMat = MaterialLib.Metal(new Color(0.62f, 0.6f, 0.55f), 6f);
            Material roofMat = MaterialLib.Metal(new Color(0.4f, 0.42f, 0.45f), 5f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, h / 2f - 1.2f, 0f);
            body.transform.localScale = new Vector3(w, h - 2.4f, d);
            body.GetComponent<Renderer>().material = wallMat;

            // 双坡屋面(两块斜板)
            float slope = 2.4f;
            for (int s = 0; s < 2; s++)
            {
                var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCol(slab);
                slab.name = $"RoofSlab{s}";
                slab.transform.SetParent(root.transform, false);
                slab.transform.localScale = new Vector3(w + 1.2f, 0.3f, Mathf.Sqrt((d / 2f + 0.6f) * (d / 2f + 0.6f) + slope * slope));
                slab.transform.localPosition = new Vector3(0f, h - 1.2f + slope / 2f, s == 0 ? d / 4f + 0.15f : -d / 4f - 0.15f);
                slab.transform.localRotation = Quaternion.Euler(s == 0 ? -Mathf.Atan2(slope, d / 2f) * Mathf.Rad2Deg : Mathf.Atan2(slope, d / 2f) * Mathf.Rad2Deg, 0f, 0f);
                slab.GetComponent<Renderer>().material = roofMat;
            }
            var ridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(ridge);
            ridge.name = "Ridge";
            ridge.transform.SetParent(root.transform, false);
            ridge.transform.localScale = new Vector3(w + 1.4f, 0.35f, 0.5f);
            ridge.transform.localPosition = new Vector3(0f, h - 1.2f + slope, 0f);
            ridge.GetComponent<Renderer>().material = roofMat;

            // 卷帘门(正面) + 门槛装卸平台
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(door);
            door.name = "ShutterDoor";
            door.transform.SetParent(root.transform, false);
            door.transform.localScale = new Vector3(w * 0.45f, h * 0.55f, 0.25f);
            door.transform.localPosition = new Vector3(0f, h * 0.275f, d / 2f + 0.05f);
            door.GetComponent<Renderer>().material = MaterialLib.Metal(new Color(0.3f, 0.33f, 0.38f), 2f);

            var dock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(dock);
            dock.name = "Dock";
            dock.transform.SetParent(root.transform, false);
            dock.transform.localScale = new Vector3(w * 0.55f, 0.7f, 1.6f);
            dock.transform.localPosition = new Vector3(0f, 0.35f, d / 2f + 0.9f);
            dock.GetComponent<Renderer>().material = MaterialLib.Roof(new Vector2(3f, 2f));

            // 屋顶通风器 ×3
            for (int v = 0; v < 3; v++)
            {
                var vent = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                DestroyCol(vent);
                vent.name = $"Vent{v}";
                vent.transform.SetParent(root.transform, false);
                vent.transform.localScale = new Vector3(1f, 0.8f, 1f);
                vent.transform.localPosition = new Vector3(-w / 2f + 2.5f + v * (w - 5f) / 2f, h - 0.6f + slope * 0.4f, 0f);
                vent.GetComponent<Renderer>().material = roofMat;
            }
            return root.transform;
        }

        /// <summary>空投供给箱:箱体+箱盖+捆扎带+护角(挂载/投放逻辑在 SupplyCrate 组件)</summary>
        public static GameObject SupplyCrate(Transform parent)
        {
            var root = new GameObject("SupplyCrate");
            root.transform.SetParent(parent, false);
            Material wood = MaterialLib.Dirt(4f);
            wood.color = new Color(0.55f, 0.42f, 0.28f);   // 贴图缺失/存在都偏军绿木色
            Material strapMat = MaterialLib.Metal(new Color(0.2f, 0.22f, 0.2f), 1f);

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Box";
            box.transform.SetParent(root.transform, false);
            box.transform.localScale = new Vector3(0.9f, 0.6f, 0.9f);
            box.GetComponent<Renderer>().material = wood;

            var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(lid);
            lid.name = "Lid";
            lid.transform.SetParent(root.transform, false);
            lid.transform.localScale = new Vector3(0.98f, 0.12f, 0.98f);
            lid.transform.localPosition = new Vector3(0f, 0.34f, 0f);
            lid.GetComponent<Renderer>().material = wood;

            for (int s = 0; s < 2; s++)
            {
                var strap = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCol(strap);
                strap.name = $"Strap{s}";
                strap.transform.SetParent(root.transform, false);
                strap.transform.localScale = new Vector3(s == 0 ? 0.12f : 1.0f, 0.66f, s == 0 ? 1.0f : 0.12f);
                strap.transform.localPosition = Vector3.zero;
                strap.GetComponent<Renderer>().material = strapMat;
            }
            return root;
        }

        /// <summary>地面人员:躯干+头+双臂(喊话驱离目标;CivilianTarget 挂返回根物体)</summary>
        public static GameObject Person(Transform parent, Vector3 pos, Color cloth)
        {
            var root = new GameObject("Civilian");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;

            Material clothMat = new Material(Shader.Find("Standard")) { color = cloth };
            clothMat.SetFloat("_Glossiness", 0.15f);
            Material skin = new Material(Shader.Find("Standard")) { color = new Color(0.78f, 0.62f, 0.5f) };
            skin.SetFloat("_Glossiness", 0.1f);

            var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            DestroyCol(torso);
            torso.name = "Torso";
            torso.transform.SetParent(root.transform, false);
            torso.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            torso.transform.localScale = new Vector3(0.7f, 0.42f, 0.45f);
            torso.GetComponent<Renderer>().material = clothMat;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyCol(head);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.42f, 0f);
            head.transform.localScale = new Vector3(0.3f, 0.34f, 0.3f);
            head.GetComponent<Renderer>().material = skin;

            for (int a = 0; a < 2; a++)
            {
                var arm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                DestroyCol(arm);
                arm.name = $"Arm{a}";
                arm.transform.SetParent(root.transform, false);
                arm.transform.localPosition = new Vector3(a == 0 ? -0.42f : 0.42f, 0.82f, 0f);
                arm.transform.localScale = new Vector3(0.16f, 0.36f, 0.16f);
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, a == 0 ? 12f : -12f);
                arm.GetComponent<Renderer>().material = clothMat;
            }
            return root;
        }

        /// <summary>桁架障碍塔:四柱+横撑斜撑+红白警示涂装+顶部平台(替换单色圆柱;
        /// 避障判定仍走 ObstacleAvoid.AddCylinder 解析圆柱)</summary>
        public static Transform ObstacleTower(Transform parent, Vector3 pos, float radius, float height)
        {
            var root = new GameObject("ObstacleTower");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;

            Material steel = MaterialLib.Metal(new Color(0.68f, 0.3f, 0.26f), 2f);
            Material steelLight = MaterialLib.Metal(new Color(0.88f, 0.86f, 0.82f), 2f);

            float half = radius * 0.72f;
            for (int c = 0; c < 4; c++)
            {
                var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                DestroyCol(col);
                col.name = $"Leg{c}";
                col.transform.SetParent(root.transform, false);
                col.transform.localScale = new Vector3(0.22f, height / 2f, 0.22f);
                col.transform.localPosition = new Vector3(c % 2 == 0 ? -half : half, height / 2f, c < 2 ? -half : half);
                col.GetComponent<Renderer>().material = steel;
            }

            int sections = Mathf.Max(4, (int)(height / 5f));
            for (int s = 1; s <= sections; s++)
            {
                float y = height * s / (sections + 1f);
                bool lightBand = s % 2 == 0;
                for (int e = 0; e < 4; e++)
                {
                    var brace = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    DestroyCol(brace);
                    brace.name = $"Brace{s}_{e}";
                    brace.transform.SetParent(root.transform, false);
                    bool alongX = e % 2 == 0;
                    brace.transform.localScale = alongX
                        ? new Vector3(half * 2f + 0.3f, 0.12f, 0.12f)
                        : new Vector3(0.12f, 0.12f, half * 2f + 0.3f);
                    brace.transform.localPosition = new Vector3(
                        alongX ? 0f : (e == 1 ? half : -half), y,
                        alongX ? (e == 0 ? -half : half) : 0f);
                    brace.GetComponent<Renderer>().material = lightBand ? steelLight : steel;
                }
            }

            var deck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCol(deck);
            deck.name = "Deck";
            deck.transform.SetParent(root.transform, false);
            deck.transform.localScale = new Vector3(radius * 1.5f, 0.25f, radius * 1.5f);
            deck.transform.localPosition = new Vector3(0f, height - 0.6f, 0f);
            deck.GetComponent<Renderer>().material = steel;

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyCol(beacon);
            beacon.name = "Beacon";
            beacon.transform.SetParent(root.transform, false);
            beacon.transform.localScale = Vector3.one * 0.4f;
            beacon.transform.localPosition = new Vector3(0f, height - 0.2f, 0f);
            beacon.GetComponent<Renderer>().material =
                EnvironmentBuilder.UnlitMat(new Color(1f, 0.25f, 0.15f, 0.95f));   // 障碍警灯(语义色)
            PropAnim.Blink(beacon.GetComponent<Renderer>(), 0.15f, 0.75f);          // 闪烁(V4 动效)
            return root.transform;
        }

        static void DestroyCol(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }
    }
}

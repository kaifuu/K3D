using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 过程式场景搭建工具库(自原 MainBoot 抽取)。
    /// 所有构建物挂到调用方给定的 parent(模式 ModeRoot)下,切模式整树销毁。
    /// </summary>
    public static class EnvironmentBuilder
    {
        // ---------- 环境默认态 ----------
        /// <summary>重置光照/雾/天色到白昼默认(每个模式 Build 开头调用,防止上个模式的环境泄漏)</summary>
        public static void ResetToDayDefault()
        {
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.47f, 0.64f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.47f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.21f, 0.2f, 0.18f);
            if (Camera.main != null) Camera.main.backgroundColor = new Color(0.05f, 0.07f, 0.1f);
        }

        public static Light BuildLighting(Transform parent)
        {
            // 阴影质量(CS 风格实景:全场软阴影 + 2 级联近距锐利)
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 170f;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowCascades = 2;

            var sun = new GameObject("DirectionalLight");
            sun.transform.SetParent(parent, false);
            sun.transform.rotation = Quaternion.Euler(38f, -24f, 0f);   // 低角度暖阳 → 长影子
            var li = sun.AddComponent<Light>();
            li.type = LightType.Directional;
            li.shadows = LightShadows.Soft;
            li.color = new Color(1f, 0.93f, 0.8f);
            li.intensity = 1.18f;
            return li;
        }

        // ---------- 地面与网格 ----------
        public static Transform CreateGround(Transform parent)
        {
            // 外围草场(900m,压在主地面下方,负责地平线观感)
            var field = GameObject.CreatePrimitive(PrimitiveType.Plane);
            field.name = "GrassField";
            DestroyCollider(field);
            field.transform.SetParent(parent, false);
            field.transform.localScale = new Vector3(90f, 1f, 90f);
            field.transform.position = new Vector3(0f, -0.05f, 0f);
            field.GetComponent<Renderer>().material = MaterialLib.GrassField(900f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.localScale = new Vector3(20f, 1f, 20f); // 200m x 200m
            ground.GetComponent<Renderer>().material = MaterialLib.Ground(200f);

            // 起降坪:柏油圆盘 + 四角白色角标(V1)
            var apron = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCollider(apron);
            apron.name = "Apron";
            apron.transform.SetParent(parent, false);
            apron.transform.localScale = new Vector3(44f, 0.012f, 44f);
            apron.transform.position = new Vector3(0f, 0.012f, 0f);
            apron.GetComponent<Renderer>().material = MaterialLib.Asphalt(44f);
            var markMat = UnlitMat(new Color(0.92f, 0.93f, 0.88f, 0.85f));
            for (int c = 0; c < 4; c++)
            {
                float cs = Mathf.Cos(c * Mathf.PI / 2f + Mathf.PI / 4f) * 18f;
                float sn = Mathf.Sin(c * Mathf.PI / 2f + Mathf.PI / 4f) * 18f;
                var m1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCollider(m1); m1.name = "PadMark";
                m1.transform.SetParent(parent, false);
                m1.transform.localScale = new Vector3(4f, 0.02f, 0.5f);
                m1.transform.position = new Vector3(cs, 0.045f, sn);
                m1.transform.rotation = Quaternion.Euler(0f, c * 90f, 0f);
                m1.GetComponent<Renderer>().material = markMat;
                var m2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCollider(m2); m2.name = "PadMark";
                m2.transform.SetParent(parent, false);
                m2.transform.localScale = new Vector3(0.5f, 0.02f, 4f);
                m2.transform.position = new Vector3(cs, 0.045f, sn);
                m2.transform.rotation = Quaternion.Euler(0f, c * 90f, 0f);
                m2.GetComponent<Renderer>().material = markMat;
            }

            var gridMat = new Material(Shader.Find("Sprites/Default"));
            gridMat.color = new Color(0.5f, 0.6f, 0.55f, 0.15f);
            for (int i = -10; i <= 10; i++)
            {
                if (i == 0) continue;
                var gx = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCollider(gx);
                gx.name = $"GridX{i}";
                gx.transform.SetParent(parent, false);
                gx.transform.localScale = new Vector3(200f, 0.02f, 0.15f);
                gx.transform.position = new Vector3(0f, 0.02f, i * 10f);
                gx.GetComponent<Renderer>().material = gridMat;

                var gz = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCollider(gz);
                gz.name = $"GridZ{i}";
                gz.transform.SetParent(parent, false);
                gz.transform.localScale = new Vector3(0.15f, 0.02f, 200f);
                gz.transform.position = new Vector3(i * 10f, 0.02f, 0f);
                gz.GetComponent<Renderer>().material = gridMat;
            }

            // 工业风场景陈设(V3):环路/围界/集装箱/油桶/托盘/护栏/绿篱/路灯
            StreetKit.DressYard(parent);
            return ground.transform;
        }

        // ---------- 分区圈 ----------
        public static void BuildZones(Transform parent)
        {
            MakeRing(parent, SimConfig.NoFlyRadius, new Color(1f, 0.15f, 0.1f, 0.55f), "NoFlyZone", 0.06f);
            MakeRing(parent, SimConfig.WarningRadius, new Color(1f, 0.65f, 0.1f, 0.4f), "WarningZone", 0.05f);
            MakeRing(parent, SimConfig.PerimeterRadius, new Color(0.2f, 0.75f, 1f, 0.3f), "PerimeterZone", 0.04f);
            MakeFlatDisc(parent, SimConfig.RadarRange, new Color(0.1f, 0.5f, 0.25f, 0.06f), "RadarCoverage");
        }

        public static void MakeRing(Transform parent, float radius, Color color, string name, float thickness)
        {
            int seg = 96;
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = color;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0) * radius, 0.04f, Mathf.Sin(a0) * radius);
                var p1 = new Vector3(Mathf.Cos(a1) * radius, 0.04f, Mathf.Sin(a1) * radius);
                var seg2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCollider(seg2);
                seg2.name = name;
                seg2.transform.SetParent(parent, false);
                seg2.transform.position = (p0 + p1) * 0.5f;
                seg2.transform.rotation = Quaternion.LookRotation(p1 - p0);
                seg2.transform.localScale = new Vector3(Vector3.Distance(p0, p1) + 0.1f, thickness * 4f, thickness);
                seg2.GetComponent<Renderer>().material = mat;
            }
        }

        public static void MakeFlatDisc(Transform parent, float radius, Color color, string name)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCollider(disc);
            disc.name = name;
            disc.transform.SetParent(parent, false);
            disc.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            disc.transform.position = new Vector3(0f, 0.03f, 0f);
            disc.GetComponent<Renderer>().material = UnlitMat(color);
        }

        // ---------- 核心设施 ----------
        public static Transform BuildCoreFacility(Transform parent)
        {
            var root = new GameObject("CoreFacility");
            root.transform.SetParent(parent, false);
            root.transform.position = Vector3.zero;

            var main = GameObject.CreatePrimitive(PrimitiveType.Cube);
            main.name = "MainBuilding";
            main.transform.SetParent(root.transform, false);
            main.transform.localScale = new Vector3(18f, 14f, 18f);
            main.transform.localPosition = new Vector3(0f, 7f, 0f);
            main.GetComponent<Renderer>().material = MaterialLib.Wall(0, new Vector2(5f, 4f));

            var roofMain = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCollider(roofMain);
            roofMain.name = "RoofMain";
            roofMain.transform.SetParent(root.transform, false);
            roofMain.transform.localScale = new Vector3(19f, 0.6f, 19f);
            roofMain.transform.localPosition = new Vector3(0f, 14.3f, 0f);
            roofMain.GetComponent<Renderer>().material = MaterialLib.Roof(new Vector2(5f, 5f));

            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "Tower";
            tower.transform.SetParent(root.transform, false);
            tower.transform.localScale = new Vector3(6f, 30f, 6f);
            tower.transform.localPosition = new Vector3(8f, 15f, -8f);
            tower.GetComponent<Renderer>().material = MaterialLib.Wall(1, new Vector2(2f, 9f));

            var roofTower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCollider(roofTower);
            roofTower.name = "RoofTower";
            roofTower.transform.SetParent(root.transform, false);
            roofTower.transform.localScale = new Vector3(6.8f, 0.5f, 6.8f);
            roofTower.transform.localPosition = new Vector3(8f, 30.25f, -8f);
            roofTower.GetComponent<Renderer>().material = MaterialLib.Roof(new Vector2(2f, 2f));

            // 附楼(设备间) + 主楼入口门檐(V2 细节)
            var annex = GameObject.CreatePrimitive(PrimitiveType.Cube);
            annex.name = "Annex";
            annex.transform.SetParent(root.transform, false);
            annex.transform.localScale = new Vector3(8f, 5f, 6f);
            annex.transform.localPosition = new Vector3(-12f, 2.5f, 6f);
            annex.GetComponent<Renderer>().material = MaterialLib.Wall(2, new Vector2(2.5f, 1.6f));

            var annexRoof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCollider(annexRoof);
            annexRoof.name = "AnnexRoof";
            annexRoof.transform.SetParent(root.transform, false);
            annexRoof.transform.localScale = new Vector3(8.6f, 0.4f, 6.6f);
            annexRoof.transform.localPosition = new Vector3(-12f, 5.2f, 6f);
            annexRoof.GetComponent<Renderer>().material = MaterialLib.Roof(new Vector2(2.5f, 2f));

            var canopy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCollider(canopy);
            canopy.name = "Canopy";
            canopy.transform.SetParent(root.transform, false);
            canopy.transform.localScale = new Vector3(6f, 0.35f, 2.4f);
            canopy.transform.localPosition = new Vector3(0f, 3.4f, 9.7f);
            canopy.GetComponent<Renderer>().material = MaterialLib.Metal(new Color(0.3f, 0.32f, 0.35f), 3f);

            // 主楼玻璃门斗 + 楼层窗带(V3,与参照建筑同语言)
            var entry = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCollider(entry);
            entry.name = "Entry";
            entry.transform.SetParent(root.transform, false);
            entry.transform.localScale = new Vector3(3.2f, 3.0f, 0.3f);
            entry.transform.localPosition = new Vector3(0f, 1.5f, 9.1f);
            var entryMat = new Material(Shader.Find("Standard")) { color = new Color(0.1f, 0.13f, 0.17f) };
            entryMat.SetFloat("_Glossiness", 0.85f);
            entry.GetComponent<Renderer>().material = entryMat;

            for (int fb = 0; fb < 3; fb++)
            {
                var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCollider(band);
                band.name = $"WindowBand{fb}";
                band.transform.SetParent(root.transform, false);
                band.transform.localScale = new Vector3(18.1f, 1.15f, 18.1f);
                band.transform.localPosition = new Vector3(0f, 4.6f + fb * 3.6f, 0f);
                band.GetComponent<Renderer>().material = entryMat;
            }

            var antenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCollider(antenna);
            antenna.name = "Antenna";
            antenna.transform.SetParent(root.transform, false);
            antenna.transform.localScale = new Vector3(0.25f, 10f, 0.25f);
            antenna.transform.localPosition = new Vector3(8f, 35f, -8f);
            antenna.GetComponent<Renderer>().material = MaterialLib.Metal(new Color(0.85f, 0.2f, 0.18f), 1f);

            // 核心禁飞触发区(EnemyDrone 以距离判断为主,此处冗余)
            var zone = new GameObject("CoreZone");
            zone.transform.SetParent(root.transform, false);
            zone.transform.localPosition = Vector3.zero;
            zone.AddComponent<BoxCollider>().size = new Vector3(20f, 40f, 20f);
            zone.GetComponent<BoxCollider>().isTrigger = true;
            zone.AddComponent<Rigidbody>().isKinematic = true;
            zone.AddComponent<CoreZone>();
            return root.transform;
        }

        // ---------- 雷达站 ----------
        public static Transform BuildRadarStation(Transform parent, Vector3 pos)
        {
            var root = new GameObject("RadarStation");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;

            var base_ = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            base_.name = "Base";
            base_.transform.SetParent(root.transform, false);
            base_.transform.localScale = new Vector3(4f, 3f, 4f);
            base_.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            base_.GetComponent<Renderer>().material = MaterialLib.Metal(new Color(0.3f, 0.33f, 0.37f), 3f);

            var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCollider(mast);
            mast.name = "Mast";
            mast.transform.SetParent(root.transform, false);
            mast.transform.localScale = new Vector3(0.4f, 6f, 0.4f);
            mast.transform.localPosition = new Vector3(0f, 6f, 0f);
            mast.GetComponent<Renderer>().material = MaterialLib.Metal(new Color(0.42f, 0.45f, 0.5f), 4f);

            var dish = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyCollider(dish);
            dish.name = "Dish";
            dish.transform.SetParent(root.transform, false);
            dish.transform.localScale = new Vector3(3.5f, 1.2f, 3.5f);
            dish.transform.localPosition = new Vector3(0f, 9f, 0f);
            dish.transform.localRotation = Quaternion.Euler(40f, 0f, 0f);   // 俯角朝场心
            dish.GetComponent<Renderer>().material = MaterialLib.Metal(new Color(0.82f, 0.86f, 0.92f), 2f);

            // 馈源杆(抛物面焦点)
            var feed = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCollider(feed);
            feed.name = "Feed";
            feed.transform.SetParent(dish.transform, false);
            feed.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            feed.transform.localScale = new Vector3(0.08f, 1.6f, 0.08f);
            feed.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            feed.GetComponent<Renderer>().material = MaterialLib.Metal(new Color(0.5f, 0.53f, 0.58f), 1f);

            // 桁架横撑(V2 细节)
            for (int b = 0; b < 3; b++)
            {
                var brace = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCollider(brace);
                brace.name = $"MastBrace{b}";
                brace.transform.SetParent(root.transform, false);
                brace.transform.localScale = new Vector3(1.4f - b * 0.3f, 0.08f, 0.08f);
                brace.transform.localPosition = new Vector3(0f, 3f + b * 2.4f, 0f);
                brace.GetComponent<Renderer>().material = MaterialLib.Metal(new Color(0.38f, 0.41f, 0.45f), 2f);
            }

            root.AddComponent<RadarStation>();
            return root.transform;
        }

        // ---------- 反制单元 ----------
        public static CounterUnit BuildCounterUnit(Transform parent, Vector3 pos, CounterUnit.Mode mode,
                                                   string name, Color accent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            var cu = root.AddComponent<CounterUnit>();
            cu.mode = mode;
            if (mode == CounterUnit.Mode.Laser) root.AddComponent<LaserSystem>();

            var base_ = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            base_.name = "Base";
            base_.transform.SetParent(root.transform, false);
            base_.transform.localScale = new Vector3(5f, 1.6f, 5f);
            base_.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            base_.GetComponent<Renderer>().material = MaterialLib.Metal(new Color(0.26f, 0.3f, 0.34f), 3f);

            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 2.4f, 0f);

            var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            housing.name = "Housing";
            housing.transform.SetParent(head.transform, false);
            housing.transform.localScale = new Vector3(2.4f, 1.6f, 2.8f);
            housing.GetComponent<Renderer>().material = MaterialLib.Metal(accent, 2f);

            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCollider(barrel);
            barrel.name = "Barrel";
            barrel.transform.SetParent(head.transform, false);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.3f, 3.2f, 0.3f);
            barrel.transform.localPosition = new Vector3(0f, 0.2f, 1.6f);
            barrel.GetComponent<Renderer>().material = MaterialLib.Metal(new Color(0.12f, 0.12f, 0.14f), 2f);

            // 转塔侧面护盾 + 座圈(V2 细节)
            var turretMat = MaterialLib.Metal(new Color(0.22f, 0.25f, 0.28f), 2f);
            for (int s = 0; s < 2; s++)
            {
                var shield = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCollider(shield);
                shield.name = $"Shield{s}";
                shield.transform.SetParent(head.transform, false);
                shield.transform.localScale = new Vector3(0.1f, 1.3f, 2.2f);
                shield.transform.localPosition = new Vector3(s == 0 ? -1.25f : 1.25f, 0.1f, 0.2f);
                shield.GetComponent<Renderer>().material = turretMat;
            }
            var yoke = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCollider(yoke);
            yoke.name = "Yoke";
            yoke.transform.SetParent(root.transform, false);
            yoke.transform.localScale = new Vector3(2.2f, 0.25f, 2.2f);
            yoke.transform.localPosition = new Vector3(0f, 1.75f, 0f);
            yoke.GetComponent<Renderer>().material = turretMat;

            // 弹箱(捕网)/干扰阵元(干扰)/聚焦鳍(激光)
            var boxMat = MaterialLib.Metal(accent * 0.85f, 1.5f);
            if (mode == CounterUnit.Mode.NetGun)
            {
                var mag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCollider(mag);
                mag.name = "Magazine";
                mag.transform.SetParent(head.transform, false);
                mag.transform.localScale = new Vector3(1.2f, 0.7f, 0.8f);
                mag.transform.localPosition = new Vector3(0f, 0.85f, -0.6f);
                mag.GetComponent<Renderer>().material = boxMat;
            }
            else if (mode == CounterUnit.Mode.Jammer)
            {
                for (int an = 0; an < 3; an++)
                {
                    var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    DestroyCollider(rod);
                    rod.name = $"Antenna{an}";
                    rod.transform.SetParent(head.transform, false);
                    rod.transform.localScale = new Vector3(0.05f, 1.5f, 0.05f);
                    rod.transform.localPosition = new Vector3(-0.6f + an * 0.6f, 1.2f, -0.7f);
                    rod.GetComponent<Renderer>().material = boxMat;
                }
            }
            else
            {
                var fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCollider(fin);
                fin.name = "FocusFin";
                fin.transform.SetParent(head.transform, false);
                fin.transform.localScale = new Vector3(0.9f, 0.5f, 0.5f);
                fin.transform.localPosition = new Vector3(0f, 0.95f, -0.6f);
                fin.GetComponent<Renderer>().material = boxMat;
            }

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCollider(ring);
            ring.name = "RangeRing";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localScale = new Vector3(SimConfig.CounterRange * 2f, 0.02f, SimConfig.CounterRange * 2f);
            ring.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            ring.GetComponent<Renderer>().material = UnlitMat(new Color(accent.r, accent.g, accent.b, 0.05f));
            return cu;
        }

        // ---------- 材质工具 ----------
        public static Material StdMat(Color c)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = c;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.2f);
            return m;
        }

        public static Material UnlitMat(Color c)
        {
            var m = new Material(Shader.Find("Sprites/Default"));
            m.color = c;
            return m;
        }

        /// <summary>不透明无光照纯色(Unlit/Color,ZWrite 开):
        /// 热像换装等需要正确深度遮挡的场合用这个,Sprites/Default 是透明无深度,
        /// 大平面(地面)按质心排序会把远处目标整片盖掉。</summary>
        public static Material FlatMat(Color c)
        {
            var m = new Material(Shader.Find("Unlit/Color"));
            m.color = c;
            return m;
        }

        static void DestroyCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }
    }
}

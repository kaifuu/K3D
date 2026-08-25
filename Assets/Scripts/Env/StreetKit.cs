using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 场景陈设库(V3 实景美化):CS 工业风庭院 —— 环场道路/标线/路灯、围界铁网、
    /// 集装箱堆场、油桶簇、托盘货物、混凝土护栏、绿篱。
    /// 全部手工布点(避开各模式已知建筑点位,集中在 r70~100 环带),只造视觉零碰撞体;
    /// 经 EnvironmentBuilder.CreateGround 统一挂载,10 个模式外观一致。
    /// </summary>
    public static class StreetKit
    {
        /// <summary>整个演练场地的统一陈设(每模式 Build 时随 CreateGround 调用)</summary>
        public static void DressYard(Transform parent)
        {
            var root = new GameObject("StreetDressing");
            root.transform.SetParent(parent, false);

            RingRoad(root.transform, 92f);
            PerimeterFence(root.transform, 98f);

            // 集装箱堆场(3 组)
            ContainerStack(root.transform, new Vector3(60f, 0f, 60f), 30f, 2);
            ContainerStack(root.transform, new Vector3(-55f, 0f, -70f), -20f, 1);
            ContainerStack(root.transform, new Vector3(58f, 0f, -75f), 15f, 2);

            // 油桶簇(4 处)
            Barrels(root.transform, new Vector3(30f, 0f, 85f), 3, 0);
            Barrels(root.transform, new Vector3(-30f, 0f, -88f), 4, 1);
            Barrels(root.transform, new Vector3(-85f, 0f, 5f), 3, 2);
            Barrels(root.transform, new Vector3(10f, 0f, -85f), 2, 0);

            // 托盘货物(2 处)
            PalletGoods(root.transform, new Vector3(0f, 0f, 88f), 10f);
            PalletGoods(root.transform, new Vector3(85f, 0f, -15f), -30f);

            // 混凝土护栏(3 排,围出内部车流动线)
            BarrierRow(root.transform, new Vector3(-40f, 0f, 78f), 25f, 3);
            BarrierRow(root.transform, new Vector3(70f, 0f, 25f), 100f, 3);
            BarrierRow(root.transform, new Vector3(-25f, 0f, 70f), 60f, 3);

            // 绿篱(围界四角内侧软化)
            Hedge(root.transform, new Vector3(80f, 0f, 80f), 10f, 1.6f);
            Hedge(root.transform, new Vector3(-80f, 0f, 80f), 8f, 1.4f);
            Hedge(root.transform, new Vector3(82f, 0f, -78f), 9f, 1.5f);
            Hedge(root.transform, new Vector3(-82f, 0f, -78f), 7f, 1.4f);

            // 环路路灯 ×12
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f + 0.26f;
                var p = new Vector3(Mathf.Cos(a) * 95.5f, 0f, Mathf.Sin(a) * 95.5f);
                LampPost(root.transform, p, -a * Mathf.Rad2Deg + 90f);
            }

            // 起降坪边风袋(风向/风感)
            Windsock(root.transform, new Vector3(27f, 0f, 5f));

            // 远景城市街区(北侧天际线,所有模式可见)
            CityKit.CityDistrict(root.transform, new Vector3(0f, 0f, 185f), 120f);
        }

        /// <summary>风袋:杆 + 摆动枢轴 + 橙白相间三节</summary>
        static void Windsock(Transform parent, Vector3 pos)
        {
            var root = new GameObject("Windsock");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            Material poleMat = MaterialLib.Metal(new Color(0.75f, 0.77f, 0.8f), 1f);

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCol(pole);
            pole.name = "Pole";
            pole.transform.SetParent(root.transform, false);
            pole.transform.localScale = new Vector3(0.05f, 1.6f, 0.05f);
            pole.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            pole.GetComponent<Renderer>().material = poleMat;

            var pivot = new GameObject("SockPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 3.1f, 0f);

            Color[] bands = { new Color(0.9f, 0.45f, 0.1f), new Color(0.92f, 0.92f, 0.9f), new Color(0.9f, 0.45f, 0.1f) };
            for (int s = 0; s < 3; s++)
            {
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                DestroyCol(seg);
                seg.name = $"Sock{s}";
                seg.transform.SetParent(pivot.transform, false);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                seg.transform.localScale = new Vector3(0.16f - s * 0.035f, 0.28f, 0.16f - s * 0.035f);
                seg.transform.localPosition = new Vector3(0.14f + s * 0.56f, 0f, 0f);
                seg.GetComponent<Renderer>().material = EnvironmentBuilder.FlatMat(bands[s]);
            }
            PropAnim.Sway(pivot.transform, 24f, 4.5f);
        }

        // ---------- 环形道路 ----------
        /// <summary>环形柏油路(弧段拼圆)+ 中心虚线标线</summary>
        static void RingRoad(Transform parent, float radius)
        {
            var roadGo = new GameObject("RingRoad");
            roadGo.transform.SetParent(parent, false);
            Material asphalt = MaterialLib.Asphalt(10f);
            Material line = EnvironmentBuilder.UnlitMat(new Color(0.85f, 0.82f, 0.6f, 0.8f));

            int seg = 48;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0) * radius, 0.015f, Mathf.Sin(a0) * radius);
                var p1 = new Vector3(Mathf.Cos(a1) * radius, 0.015f, Mathf.Sin(a1) * radius);
                float chord = Vector3.Distance(p0, p1);

                var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCol(s);
                s.name = $"RoadSeg{i}";
                s.transform.SetParent(roadGo.transform, false);
                s.transform.position = (p0 + p1) * 0.5f;
                s.transform.rotation = Quaternion.LookRotation(p1 - p0);
                s.transform.localScale = new Vector3(chord + 0.1f, 0.06f, 7f);
                s.GetComponent<Renderer>().material = asphalt;

                if (i % 2 == 0)   // 中心虚线
                {
                    var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    DestroyCol(d);
                    d.name = $"Dash{i}";
                    d.transform.SetParent(roadGo.transform, false);
                    d.transform.position = (p0 + p1) * 0.5f + Vector3.up * 0.028f;
                    d.transform.rotation = Quaternion.LookRotation(p1 - p0);
                    d.transform.localScale = new Vector3(chord * 0.55f, 0.015f, 0.3f);
                    d.GetComponent<Renderer>().material = line;
                }
            }
        }

        // ---------- 围界 ----------
        /// <summary>方形围界:立柱 + 三道横杆(铁丝网暗示)</summary>
        static void PerimeterFence(Transform parent, float half)
        {
            var fenceGo = new GameObject("PerimeterFence");
            fenceGo.transform.SetParent(parent, false);
            Material postMat = MaterialLib.Metal(new Color(0.24f, 0.26f, 0.28f), 1f);
            Material railMat = MaterialLib.Metal(new Color(0.34f, 0.36f, 0.38f), 1f);

            for (int side = 0; side < 4; side++)
            {
                bool alongX = side % 2 == 0;
                float fixedCoord = side < 2 ? half : -half;
                float len = half * 2f;
                int posts = 15;
                for (int p = 0; p <= posts; p++)
                {
                    float t = -half + p / (float)posts * len;
                    // 角柱只造一次(每侧末柱留给下一侧)
                    if (p == posts && side != 3 && (side == 0 || side == 2)) continue;
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    DestroyCol(post);
                    post.name = $"FencePost{side}_{p}";
                    post.transform.SetParent(fenceGo.transform, false);
                    post.transform.localScale = new Vector3(0.12f, 1.9f, 0.12f);
                    post.transform.position = alongX
                        ? new Vector3(t, 0.95f, fixedCoord)
                        : new Vector3(fixedCoord, 0.95f, t);
                    post.GetComponent<Renderer>().material = postMat;
                }
                // 三道通长横杆
                float[] railY = { 0.45f, 1.1f, 1.78f };
                foreach (float ry in railY)
                {
                    var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    DestroyCol(rail);
                    rail.name = $"FenceRail{side}_{ry:0.0}";
                    rail.transform.SetParent(fenceGo.transform, false);
                    rail.transform.localScale = alongX
                        ? new Vector3(len, 0.05f, 0.05f)
                        : new Vector3(0.05f, 0.05f, len);
                    rail.transform.position = alongX
                        ? new Vector3(0f, ry, fixedCoord)
                        : new Vector3(fixedCoord, ry, 0f);
                    rail.GetComponent<Renderer>().material = railMat;
                }
            }
        }

        // ---------- 集装箱 ----------
        static readonly Color[] ContainerTints =
        {
            new Color(0.52f, 0.26f, 0.2f),    // 锈红
            new Color(0.2f, 0.34f, 0.46f),    // 工业蓝
            new Color(0.28f, 0.38f, 0.26f),   // 军绿
            new Color(0.62f, 0.4f, 0.18f),    // 橙
        };

        /// <summary>集装箱堆:标准 20 尺箱(6.1×2.6×2.4)+ 角柱 + 门杆,可叠两层</summary>
        static void ContainerStack(Transform parent, Vector3 pos, float rotY, int stacked)
        {
            var root = new GameObject("ContainerStack");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            for (int i = 0; i < Mathf.Clamp(stacked, 1, 2); i++)
            {
                var c = Container(root.transform, ContainerTints[i % ContainerTints.Length]);
                c.localPosition = new Vector3(i * 0.15f, 1.3f + i * 2.6f, 0f);
            }
            // 旁边斜靠一只空箱(破对称,更像堆场)
            var spare = Container(root.transform, ContainerTints[2]);
            spare.localPosition = new Vector3(4.2f, 1.3f, 1.6f);
            spare.localRotation = Quaternion.Euler(0f, 18f, 0f);
        }

        static Transform Container(Transform parent, Color tint)
        {
            var go = new GameObject("Container");
            go.transform.SetParent(parent, false);
            Material bodyMat = MaterialLib.Metal(tint, 4f);
            Material cornerMat = MaterialLib.Metal(tint * 0.6f, 1.5f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(body);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(6.1f, 2.6f, 2.44f);
            body.GetComponent<Renderer>().material = bodyMat;

            for (int cx = 0; cx < 4; cx++)
            {
                var corner = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCol(corner);
                corner.name = $"CornerPost{cx}";
                corner.transform.SetParent(go.transform, false);
                corner.transform.localScale = new Vector3(0.18f, 2.68f, 0.18f);
                corner.transform.localPosition = new Vector3(cx % 2 == 0 ? -3.0f : 3.0f, 0f, cx < 2 ? -1.2f : 1.2f);
                corner.GetComponent<Renderer>().material = cornerMat;
            }
            // 端门锁杆 ×2
            for (int r = 0; r < 2; r++)
            {
                var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                DestroyCol(rod);
                rod.name = $"LockRod{r}";
                rod.transform.SetParent(go.transform, false);
                rod.transform.localScale = new Vector3(0.05f, 2.4f, 0.05f);
                rod.transform.localPosition = new Vector3(r == 0 ? -1.6f : 1.6f, 0f, 1.25f);
                rod.GetComponent<Renderer>().material = cornerMat;
            }
            return go.transform;
        }

        // ---------- 油桶 ----------
        static readonly Color[] BarrelTints =
        {
            new Color(0.5f, 0.2f, 0.16f),    // 锈红
            new Color(0.18f, 0.3f, 0.42f),   // 蓝
            new Color(0.4f, 0.42f, 0.44f),   // 灰
        };

        static void Barrels(Transform parent, Vector3 pos, int n, int tintIdx)
        {
            var root = new GameObject("Barrels");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            Material mat = MaterialLib.Metal(BarrelTints[tintIdx % BarrelTints.Length], 2f);
            Material ringMat = MaterialLib.Metal(new Color(0.2f, 0.21f, 0.22f), 2f);

            for (int i = 0; i < Mathf.Clamp(n, 1, 5); i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                var b = new GameObject($"Barrel{i}");
                b.transform.SetParent(root.transform, false);
                b.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.65f, 0.45f, Mathf.Sin(a) * 0.65f);

                var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                DestroyCol(body);
                body.name = "Body";
                body.transform.SetParent(b.transform, false);
                body.transform.localScale = new Vector3(0.6f, 0.45f, 0.6f);
                body.GetComponent<Renderer>().material = mat;

                for (int rg = 0; rg < 2; rg++)
                {
                    var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    DestroyCol(ring);
                    ring.name = $"Ring{rg}";
                    ring.transform.SetParent(b.transform, false);
                    ring.transform.localScale = new Vector3(0.63f, 0.03f, 0.63f);
                    ring.transform.localPosition = new Vector3(0f, rg == 0 ? 0.2f : -0.2f, 0f);
                    ring.GetComponent<Renderer>().material = ringMat;
                }
            }
        }

        // ---------- 托盘货物 ----------
        static void PalletGoods(Transform parent, Vector3 pos, float rotY)
        {
            var root = new GameObject("PalletGoods");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            Material wood = MaterialLib.Dirt(3f);
            wood.color = new Color(0.52f, 0.4f, 0.26f);
            Material crateMat = MaterialLib.Dirt(2f);
            crateMat.color = new Color(0.44f, 0.35f, 0.24f);

            // 托盘(垫木 + 面板)
            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(deck);
            deck.name = "Deck";
            deck.transform.SetParent(root.transform, false);
            deck.transform.localScale = new Vector3(1.4f, 0.08f, 1.1f);
            deck.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            deck.GetComponent<Renderer>().material = wood;

            // 箱货两层
            var c1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(c1);
            c1.name = "Crate1";
            c1.transform.SetParent(root.transform, false);
            c1.transform.localScale = new Vector3(1.1f, 0.7f, 0.95f);
            c1.transform.localPosition = new Vector3(-0.1f, 0.53f, 0f);
            c1.GetComponent<Renderer>().material = crateMat;

            var c2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(c2);
            c2.name = "Crate2";
            c2.transform.SetParent(root.transform, false);
            c2.transform.localScale = new Vector3(0.7f, 0.5f, 0.6f);
            c2.transform.localPosition = new Vector3(0.75f, 0.43f, 0.2f);
            c2.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
            c2.GetComponent<Renderer>().material = crateMat;

            // 相邻空托盘斜靠
            var spare = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(spare);
            spare.name = "SparePallet";
            spare.transform.SetParent(root.transform, false);
            spare.transform.localScale = new Vector3(1.4f, 0.08f, 1.1f);
            spare.transform.localPosition = new Vector3(2.2f, 0.7f, -0.4f);
            spare.transform.localRotation = Quaternion.Euler(-72f, 0f, 0f);
            spare.GetComponent<Renderer>().material = wood;
        }

        // ---------- 混凝土护栏 ----------
        static void BarrierRow(Transform parent, Vector3 pos, float rotY, int count)
        {
            var root = new GameObject("BarrierRow");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            Material mat = MaterialLib.Wall(0, new Vector2(1.2f, 0.6f));
            mat.color = new Color(0.82f, 0.8f, 0.76f);

            for (int i = 0; i < count; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCol(b);
                b.name = $"Jersey{i}";
                b.transform.SetParent(root.transform, false);
                b.transform.localScale = new Vector3(2.4f, 0.85f, 0.55f);
                b.transform.localPosition = new Vector3(i * 2.55f - (count - 1) * 1.275f, 0.42f, 0f);
                b.GetComponent<Renderer>().material = mat;
            }
        }

        // ---------- 绿篱 ----------
        static void Hedge(Transform parent, Vector3 pos, float w, float h)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(go);
            go.name = "Hedge";
            go.transform.SetParent(parent, false);
            go.transform.position = pos + Vector3.up * (h / 2f);
            go.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(pos.z, pos.x) * Mathf.Rad2Deg, 0f);
            go.transform.localScale = new Vector3(w, h, 1.2f);
            go.GetComponent<Renderer>().material = MaterialLib.GrassField(6f);
        }

        // ---------- 路灯 ----------
        public static void LampPost(Transform parent, Vector3 pos, float rotY)
        {
            var root = new GameObject("LampPost");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            Material poleMat = MaterialLib.Metal(new Color(0.2f, 0.21f, 0.24f), 1f);

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCol(pole);
            pole.name = "Pole";
            pole.transform.SetParent(root.transform, false);
            pole.transform.localScale = new Vector3(0.09f, 3.4f, 0.09f);
            pole.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            pole.GetComponent<Renderer>().material = poleMat;

            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(arm);
            arm.name = "Arm";
            arm.transform.SetParent(root.transform, false);
            arm.transform.localScale = new Vector3(1.3f, 0.08f, 0.08f);
            arm.transform.localPosition = new Vector3(0.65f, 3.35f, 0f);
            arm.GetComponent<Renderer>().material = poleMat;

            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(head);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localScale = new Vector3(0.6f, 0.14f, 0.28f);
            head.transform.localPosition = new Vector3(1.25f, 3.3f, 0f);
            var headMat = new Material(Shader.Find("Standard")) { color = new Color(0.75f, 0.74f, 0.68f) };
            headMat.SetFloat("_Glossiness", 0.7f);
            head.GetComponent<Renderer>().material = headMat;
        }

        static void DestroyCol(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }
    }
}

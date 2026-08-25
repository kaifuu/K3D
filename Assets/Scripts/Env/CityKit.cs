using UnityEngine;

namespace DroneSim
{
    /// <summary>
    /// 城市街区与国防工事库(V4 实景还原):
    /// - CityDistrict:4×4 街区(楼宇/公园/广场 + 人行道 + 斑马线 + 路灯),远景区天际线
    /// - DefenseWorks:要地防御工事环(沙袋弧 + 瞭望塔 + 蛇腹铁丝网 + 探照灯 + 旗杆)
    /// 布局确定性:用坐标哈希代替随机,无头截图帧帧可复现。
    /// </summary>
    public static class CityKit
    {
        // ---------- 确定性哈希(0..1) ----------
        static float Hash01(int x, int y, int salt)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f + salt * 74.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        // ================= 城市街区 =================

        /// <summary>城市街区:span×span 范围 4×4 地块,块间 8m 道路。
        /// 地块按哈希分为 楼宇(约2/3)/公园/广场;含人行道边带与路口斑马线。</summary>
        public static void CityDistrict(Transform parent, Vector3 center, float span)
        {
            var root = new GameObject("CityDistrict");
            root.transform.SetParent(parent, false);
            root.transform.position = center;

            // 区域基底(柏油)
            var base_ = GameObject.CreatePrimitive(PrimitiveType.Plane);
            DestroyCol(base_);
            base_.name = "CityBase";
            base_.transform.SetParent(root.transform, false);
            base_.transform.localPosition = new Vector3(0f, 0.004f, 0f);
            base_.transform.localScale = new Vector3(span / 10f, 1f, span / 10f);
            base_.GetComponent<Renderer>().material = MaterialLib.Asphalt(span);

            const int n = 4;
            float cell = span / n;
            float road = 8f;
            float block = cell - road;

            for (int gx = 0; gx < n; gx++)
            for (int gz = 0; gz < n; gz++)
            {
                float bx = (gx - (n - 1) / 2f) * cell;
                float bz = (gz - (n - 1) / 2f) * cell;
                var blockT = new GameObject($"Block_{gx}_{gz}");
                blockT.transform.SetParent(root.transform, false);
                blockT.transform.localPosition = new Vector3(bx, 0f, bz);

                // 人行道边带(浅灰,块四周)
                var walk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCol(walk);
                walk.name = "Sidewalk";
                walk.transform.SetParent(blockT.transform, false);
                walk.transform.localScale = new Vector3(block, 0.12f, block);
                walk.transform.localPosition = new Vector3(0f, 0.06f, 0f);
                walk.GetComponent<Renderer>().material = MaterialLib.Ground(block);

                float kind = Hash01(gx, gz, 7);
                if (kind < 0.66f) BuildCityLot(blockT.transform, block, gx, gz);
                else if (kind < 0.87f) Park(blockT.transform, block, gx, gz);
                else Plaza(blockT.transform, block, gx, gz);

                // 街角路灯(每块西南角)
                StreetKit.LampPost(blockT.transform,
                    new Vector3(-block / 2f - road / 2f + 1.2f, 0f, -block / 2f - road / 2f + 1.2f), 45f);
            }

            // 中央十字路口斑马线 ×4 侧
            Crosswalk(root.transform, new Vector3(0f, 0.02f, -cell / 2f), 0f);
            Crosswalk(root.transform, new Vector3(0f, 0.02f, cell / 2f), 0f);
            Crosswalk(root.transform, new Vector3(-cell / 2f, 0.02f, 0f), 90f);
            Crosswalk(root.transform, new Vector3(cell / 2f, 0.02f, 0f), 90f);
        }

        static void BuildCityLot(Transform parent, float block, int gx, int gz)
        {
            // 1~2 栋楼:主楼居中较高 + 副楼角落较矮,高度/贴图变体由哈希定
            // (V4:加高加胖 —— 14~40m 塔楼才有天际线,远景不再压成一条线)
            float h1 = 14f + Hash01(gx, gz, 1) * 26f;
            float h2 = 9f + Hash01(gx, gz, 2) * 12f;
            float lot = block - 6f;
            PropKit.Building(parent, new Vector3(-lot * 0.18f, 0f, -lot * 0.14f),
                lot * 0.85f, h1, lot * 0.85f, Mathf.RoundToInt(Hash01(gx, gz, 3) * 3f));
            if (Hash01(gx, gz, 4) > 0.35f)
                PropKit.Building(parent, new Vector3(lot * 0.3f, 0f, lot * 0.32f),
                    lot * 0.55f, h2, lot * 0.55f, Mathf.RoundToInt(Hash01(gx, gz, 5) * 3f));
        }

        static void Park(Transform parent, float block, int gx, int gz)
        {
            var lawn = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(lawn);
            lawn.name = "Lawn";
            lawn.transform.SetParent(parent, false);
            lawn.transform.localScale = new Vector3(block - 5f, 0.14f, block - 5f);
            lawn.transform.localPosition = new Vector3(0f, 0.07f, 0f);
            lawn.GetComponent<Renderer>().material = MaterialLib.GrassField(block);

            int trees = 3 + Mathf.RoundToInt(Hash01(gx, gz, 6) * 3f);
            for (int t = 0; t < trees; t++)
                Tree(parent, new Vector3(
                    (Hash01(gx * 9 + t, gz, 8) - 0.5f) * (block - 8f), 0f,
                    (Hash01(gx, gz * 9 + t, 9) - 0.5f) * (block - 8f)), 0.8f + Hash01(t, gx, 10) * 0.6f);
        }

        static void Tree(Transform parent, Vector3 pos, float size)
        {
            var root = new GameObject("Tree");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCol(trunk);
            trunk.name = "Trunk";
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localScale = new Vector3(0.22f * size, 1.1f * size, 0.22f * size);
            trunk.transform.localPosition = new Vector3(0f, 1.1f * size, 0f);
            trunk.GetComponent<Renderer>().material = TrunkMat();

            Material leaf = LeafMat();
            for (int s = 0; s < 2; s++)
            {
                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                DestroyCol(crown);
                crown.name = $"Crown{s}";
                crown.transform.SetParent(root.transform, false);
                crown.transform.localScale = Vector3.one * (1.9f - s * 0.5f) * size;
                crown.transform.localPosition = new Vector3(s * 0.25f * size, (2.1f + s * 0.9f) * size, 0f);
                crown.GetComponent<Renderer>().material = leaf;
            }
        }

        static Material trunkCache, leafCache;
        static Material TrunkMat()
        {
            if (trunkCache == null)
            {
                trunkCache = new Material(Shader.Find("Standard")) { color = new Color(0.32f, 0.24f, 0.17f) };
                trunkCache.SetFloat("_Glossiness", 0.1f);
            }
            return trunkCache;
        }
        static Material LeafMat()
        {
            if (leafCache == null)
            {
                leafCache = new Material(Shader.Find("Standard")) { color = new Color(0.22f, 0.38f, 0.18f) };
                leafCache.SetFloat("_Glossiness", 0.3f);
            }
            return leafCache;
        }

        static void Plaza(Transform parent, float block, int gx, int gz)
        {
            // 硬化广场:中央旗台 + 四角树池
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(pad);
            pad.name = "PlazaPad";
            pad.transform.SetParent(parent, false);
            pad.transform.localScale = new Vector3(block - 5f, 0.14f, block - 5f);
            pad.transform.localPosition = new Vector3(0f, 0.07f, 0f);
            pad.GetComponent<Renderer>().material = MaterialLib.Ground(block);

            for (int c = 0; c < 4; c++)
                Tree(parent, new Vector3(
                    (c % 2 == 0 ? -1f : 1f) * (block / 2f - 4f), 0f,
                    (c < 2 ? -1f : 1f) * (block / 2f - 4f)), 0.7f);
        }

        static void Crosswalk(Transform parent, Vector3 pos, float rotY)
        {
            Material white = EnvironmentBuilder.UnlitMat(new Color(0.88f, 0.89f, 0.86f, 0.85f));
            var root = new GameObject("Crosswalk");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            for (int i = 0; i < 5; i++)
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCol(bar);
                bar.name = $"Stripe{i}";
                bar.transform.SetParent(root.transform, false);
                bar.transform.localScale = new Vector3(0.55f, 0.02f, 4.5f);
                bar.transform.localPosition = new Vector3((i - 2) * 1.15f, 0.02f, 0f);
                bar.GetComponent<Renderer>().material = white;
            }
        }

        // ================= 国防工事 =================

        /// <summary>要地防御工事环:沙袋弧 ×n + 瞭望塔 ×4 + 蛇腹铁丝网 + 探照灯 ×2 + 旗杆。
        /// 全部无碰撞体(战斗判定在 BattleMode 内解析处理)。</summary>
        public static void DefenseWorks(Transform parent, float radius)
        {
            var root = new GameObject("DefenseWorks");
            root.transform.SetParent(parent, false);

            const int arcs = 8;
            for (int i = 0; i < arcs; i++)
            {
                float a = i / (float)arcs * Mathf.PI * 2f + 0.39f;
                var p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                SandbagArc(root.transform, p, -a * Mathf.Rad2Deg + 90f);
                if (i % 2 == 1)
                    RazorWire(root.transform, p, -a * Mathf.Rad2Deg + 90f);
            }

            for (int t = 0; t < 4; t++)
            {
                float a = t / 4f * Mathf.PI * 2f + Mathf.PI / 4f;
                var p = new Vector3(Mathf.Cos(a) * (radius + 4f), 0f, Mathf.Sin(a) * (radius + 4f));
                WatchTower(root.transform, p);
            }

            FloodlightMast(root.transform, new Vector3(-radius * 0.8f, 0f, radius * 0.6f), -0.4f);
            FloodlightMast(root.transform, new Vector3(radius * 0.8f, 0f, -radius * 0.6f), 0.9f);
            FlagPole(root.transform, new Vector3(0f, 0f, radius + 2f));
        }

        static Material bagCache;
        static Material BagMat()
        {
            if (bagCache == null)
            {
                bagCache = new Material(Shader.Find("Standard")) { color = new Color(0.52f, 0.47f, 0.33f) };
                bagCache.SetFloat("_Glossiness", 0.08f);
            }
            return bagCache;
        }

        /// <summary>沙袋弧:7 袋 ×2 层错缝</summary>
        static void SandbagArc(Transform parent, Vector3 pos, float rotY)
        {
            var root = new GameObject("SandbagArc");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            Material mat = BagMat();

            for (int layer = 0; layer < 2; layer++)
            {
                int count = layer == 0 ? 7 : 6;
                for (int i = 0; i < count; i++)
                {
                    var bag = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    DestroyCol(bag);
                    bag.name = $"Bag{layer}_{i}";
                    bag.transform.SetParent(root.transform, false);
                    bag.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    float off = (i - (count - 1) / 2f) * 0.62f + (layer == 1 ? 0.31f : 0f);
                    bag.transform.localPosition = new Vector3(off, 0.18f + layer * 0.3f, 0f);
                    bag.transform.localScale = new Vector3(0.55f, 0.16f, 0.3f);
                    bag.GetComponent<Renderer>().material = mat;
                }
            }
        }

        /// <summary>蛇腹型铁丝网:双立柱 + 三道水平刺丝</summary>
        static void RazorWire(Transform parent, Vector3 pos, float rotY)
        {
            var root = new GameObject("RazorWire");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            Material postMat = MaterialLib.Metal(new Color(0.2f, 0.21f, 0.23f), 1f);
            Material wireMat = MaterialLib.Metal(new Color(0.55f, 0.57f, 0.6f), 1f);

            for (int p = 0; p < 2; p++)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                DestroyCol(post);
                post.name = $"Post{p}";
                post.transform.SetParent(root.transform, false);
                post.transform.localScale = new Vector3(0.07f, 0.55f, 0.07f);
                post.transform.localPosition = new Vector3(p == 0 ? -1.6f : 1.6f, 0.55f, 0f);
                post.GetComponent<Renderer>().material = postMat;
            }
            for (int w = 0; w < 3; w++)
            {
                var coil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                DestroyCol(coil);
                coil.name = $"Coil{w}";
                coil.transform.SetParent(root.transform, false);
                coil.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                coil.transform.localScale = new Vector3(0.3f, 1.65f, 0.3f);
                coil.transform.localPosition = new Vector3(0f, 0.45f + w * 0.35f, 0f);
                coil.GetComponent<Renderer>().material = wireMat;
            }
        }

        /// <summary>瞭望塔:四腿 + 平台 + 护栏 + 顶棚(木质)</summary>
        static void WatchTower(Transform parent, Vector3 pos)
        {
            var root = new GameObject("WatchTower");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            Material wood = MaterialLib.Dirt(3f);
            wood.color = new Color(0.45f, 0.36f, 0.24f);

            for (int l = 0; l < 4; l++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                DestroyCol(leg);
                leg.name = $"Leg{l}";
                leg.transform.SetParent(root.transform, false);
                leg.transform.localScale = new Vector3(0.12f, 1.9f, 0.12f);
                leg.transform.localPosition = new Vector3(l % 2 == 0 ? -0.9f : 0.9f, 1.9f, l < 2 ? -0.9f : 0.9f);
                leg.GetComponent<Renderer>().material = wood;
            }

            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(deck);
            deck.name = "Deck";
            deck.transform.SetParent(root.transform, false);
            deck.transform.localScale = new Vector3(2.6f, 0.14f, 2.6f);
            deck.transform.localPosition = new Vector3(0f, 3.85f, 0f);
            deck.GetComponent<Renderer>().material = wood;

            for (int r = 0; r < 4; r++)
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCol(rail);
                rail.name = $"Rail{r}";
                rail.transform.SetParent(root.transform, false);
                bool alongX = r % 2 == 0;
                rail.transform.localScale = alongX ? new Vector3(2.6f, 0.07f, 0.07f) : new Vector3(0.07f, 0.07f, 2.6f);
                rail.transform.localPosition = alongX
                    ? new Vector3(0f, 4.75f, r == 0 ? -1.25f : 1.25f)
                    : new Vector3(r == 2 ? -1.25f : 1.25f, 4.75f, 0f);
                rail.GetComponent<Renderer>().material = wood;
            }

            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(roof);
            roof.name = "Roof";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localScale = new Vector3(3.0f, 0.12f, 3.0f);
            roof.transform.localPosition = new Vector3(0f, 5.5f, 0f);
            roof.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            roof.GetComponent<Renderer>().material = MaterialLib.Metal(new Color(0.3f, 0.33f, 0.32f), 2f);
        }

        /// <summary>探照灯灯塔:杆 + 双灯头 + 扫掠光锥(半透明 Unlit,语义光效)</summary>
        static void FloodlightMast(Transform parent, Vector3 pos, float phase)
        {
            var root = new GameObject("FloodlightMast");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            Material poleMat = MaterialLib.Metal(new Color(0.18f, 0.19f, 0.21f), 1f);

            var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCol(mast);
            mast.name = "Mast";
            mast.transform.SetParent(root.transform, false);
            mast.transform.localScale = new Vector3(0.14f, 4.5f, 0.14f);
            mast.transform.localPosition = new Vector3(0f, 4.5f, 0f);
            mast.GetComponent<Renderer>().material = poleMat;

            // 灯头(可绕杆扫掠)
            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 8.7f, 0f);

            for (int h = 0; h < 2; h++)
            {
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyCol(lamp);
                lamp.name = $"LampHead{h}";
                lamp.transform.SetParent(head.transform, false);
                lamp.transform.localScale = new Vector3(0.55f, 0.35f, 0.3f);
                lamp.transform.localPosition = new Vector3(h == 0 ? -0.5f : 0.5f, 0f, 0.25f);
                lamp.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
                lamp.GetComponent<Renderer>().material =
                    EnvironmentBuilder.UnlitMat(new Color(1f, 0.95f, 0.72f, 0.95f));
            }

            // 扫掠光锥(长条半透明面,朝外下方)
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(beam);
            beam.name = "Beam";
            beam.transform.SetParent(head.transform, false);
            beam.transform.localScale = new Vector3(1.0f, 0.05f, 26f);
            beam.transform.localPosition = new Vector3(0f, -1.6f, 13.5f);
            beam.transform.localRotation = Quaternion.Euler(-6f, 0f, 0f);
            beam.GetComponent<Renderer>().material =
                EnvironmentBuilder.UnlitMat(new Color(1f, 0.93f, 0.65f, 0.08f));

            PropAnim.Sweep(head.transform, 65f, 11f, phase);
        }

        /// <summary>旗杆:杆 + 旗面(PropAnim.Flag 波动)</summary>
        static void FlagPole(Transform parent, Vector3 pos)
        {
            var root = new GameObject("FlagPole");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            Material poleMat = MaterialLib.Metal(new Color(0.7f, 0.72f, 0.75f), 1f);

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyCol(pole);
            pole.name = "Pole";
            pole.transform.SetParent(root.transform, false);
            pole.transform.localScale = new Vector3(0.06f, 4.5f, 0.06f);
            pole.transform.localPosition = new Vector3(0f, 4.5f, 0f);
            pole.GetComponent<Renderer>().material = poleMat;

            var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyCol(flag);
            flag.name = "Flag";
            flag.transform.SetParent(root.transform, false);
            // 挂点在旗左缘:localPosition 左偏半个旗宽,波动绕挂点
            flag.transform.localScale = new Vector3(1.5f, 0.9f, 0.04f);
            flag.transform.localPosition = new Vector3(0.75f, 8.4f, 0f);
            flag.GetComponent<Renderer>().material = EnvironmentBuilder.FlatMat(new Color(0.72f, 0.12f, 0.12f));
            PropAnim.Flag(flag.transform);
        }

        static void DestroyCol(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }
    }
}

using UnityEditor;

namespace DroneSimEditor
{
    /// <summary>
    /// 贴图导入修正:按文件名后缀自动设置导入参数,免编辑器手点。
    /// _n = 法线贴图(GL, 线性), _r = 粗糙度(线性), 其余(_d 反照率)保持 sRGB。
    /// 只处理 Assets/ 下的贴图,避免影响其它资源。
    /// </summary>
    public class TextureImportFixup : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var n = assetPath.ToLowerInvariant();
            if (!n.StartsWith("assets/art/textures/") && !n.StartsWith("assets/resources/art/textures/"))
                return;
            var imp = (TextureImporter)assetImporter;
            if (n.EndsWith("_n.jpg"))
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.sRGBTexture = false;
            }
            else if (n.EndsWith("_r.jpg"))
            {
                imp.sRGBTexture = false;
            }
            else
            {
                imp.sRGBTexture = true;
            }
            imp.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            imp.filterMode = UnityEngine.FilterMode.Trilinear;
            imp.anisoLevel = 8;
            // OnPreprocessTexture 里设置的参数作用于本次导入,无需 SaveAndReimport(避免重入)
        }
    }
}

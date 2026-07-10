#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class CustomTexturePacker : EditorWindow
{
    [MenuItem("Tools/Custom Texture Packer")]
    public static void ShowWindow()
    {
        GetWindow<CustomTexturePacker>("Texture Packer");
    }

    private void OnGUI()
    {
        GUILayout.Label("HƯỚNG DẪN BỔ SUNG ẢNH:", EditorStyles.boldLabel);
        GUILayout.Label("Giữ Ctrl/Shift để chọn FILE ATLAS CŨ cùng với CÁC ẢNH MỚI cần thêm,\nsau đó bấm nút bên dưới.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Gộp / Thêm Ảnh Vào Atlas", GUILayout.Height(50)))
        {
            PackTextures();
        }
    }

    private void PackTextures()
    {
        // 1. Lấy tất cả các Object đang được chọn trong tab Project
        Object[] selectedObjects = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);
        if (selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("Lỗi", "Vui lòng chọn ảnh mới và/hoặc file Atlas cũ trước!", "OK");
            return;
        }

        List<Texture2D> texturesToPack = new List<Texture2D>();
        string atlasPath = "";

        foreach (var obj in selectedObjects)
        {
            if (obj is Texture2D tex)
            {
                string path = AssetDatabase.GetAssetPath(tex);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                // KIỂM TRA: Nếu ảnh được chọn là một Atlas cũ (Multiple Sprite)
                if (importer != null && importer.spriteImportMode == SpriteImportMode.Multiple)
                {
                    atlasPath = path; // Ghi nhớ đường dẫn để tí nữa ghi đè vào đây

                    // Bật Read/Write cho atlas cũ TRƯỚC khi extract — nếu không sẽ ArgumentException
                    // "texture data is not readable". Reimport synchronously rồi reload reference.
                    MakeTextureReadable(tex);
                    tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

                    // Đọc tất cả các ảnh con từ Atlas cũ này ra
                    Object[] subSprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
                    foreach (var subObj in subSprites)
                    {
                        if (subObj is Sprite sprite)
                        {
                            // Tạo lại Texture2D riêng lẻ từ Sprite con này
                            Texture2D extractedTex = ExtractTextureFromSprite(sprite);
                            if (extractedTex == null) continue;
                            extractedTex.name = sprite.name;
                            texturesToPack.Add(extractedTex);
                        }
                    }
                }
                // Nếu là ảnh đơn lẻ bình thường (Single Sprite hoặc Texture)
                else if (importer != null && importer.spriteImportMode != SpriteImportMode.Multiple)
                {
                    MakeTextureReadable(tex);
                    texturesToPack.Add(tex);
                }
            }
        }

        if (texturesToPack.Count == 0)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy ảnh hợp lệ nào trong vùng chọn!", "OK");
            return;
        }

        // Nếu người dùng không chọn Atlas cũ nào, mặc định lưu thành file mới
        if (string.IsNullOrEmpty(atlasPath))
        {
            atlasPath = "Assets/PackedAtlas.png";
        }

        // 2. Tạo tấm ảnh lớn và dùng hàm PackTextures để tự sắp xếp tự động
        Texture2D atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Rect[] rects = atlas.PackTextures(texturesToPack.ToArray(), 4, 4096, false);

        // 3. Ghi đè file PNG (Atlas mới bao gồm cả cũ và mới)
        byte[] bytes = atlas.EncodeToPNG();
        File.WriteAllBytes(atlasPath, bytes);
        AssetDatabase.Refresh();

        // 4. Cấu hình lại file Atlas tổng
        TextureImporter atlasImporter = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
        if (atlasImporter != null)
        {
            atlasImporter.textureType = TextureImporterType.Sprite;
            atlasImporter.spriteImportMode = SpriteImportMode.Multiple;
            atlasImporter.textureCompression = TextureImporterCompression.Uncompressed; // Chống mờ góc bo
            atlasImporter.filterMode = FilterMode.Bilinear;

            // 5. Thiết lập lại tọa độ cắt cho toàn bộ ảnh con mới và cũ
            SpriteMetaData[] metaDataArray = new SpriteMetaData[texturesToPack.Count];
            for (int i = 0; i < texturesToPack.Count; i++)
            {
                SpriteMetaData meta = new SpriteMetaData();
                meta.name = texturesToPack[i].name;
                meta.alignment = (int)SpriteAlignment.Center;
                meta.rect = new Rect(
                    rects[i].x * atlas.width,
                    rects[i].y * atlas.height,
                    rects[i].width * atlas.width,
                    rects[i].height * atlas.height
                );
                metaDataArray[i] = meta;
            }

            atlasImporter.spritesheet = metaDataArray;
            atlasImporter.SaveAndReimport();

            EditorUtility.DisplayDialog("Thành công", $"Đã gộp tổng cộng {texturesToPack.Count} ảnh vào file: {atlasPath}", "OK");
        }
    }

    // Hàm bổ trợ: Trích xuất dữ liệu pixel của một Sprite nằm trong Atlas cũ để biến nó thành ảnh lẻ độc lập.
    // Thử GetPixels (fast path khi texture readable) → nếu fail, fallback Graphics.Blit qua RenderTexture
    // (không yêu cầu Read/Write — work với mọi texture).
    private Texture2D ExtractTextureFromSprite(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null) return null;

        var rect = sprite.rect;
        int w = Mathf.Max(1, (int)rect.width);
        int h = Mathf.Max(1, (int)rect.height);
        int x = (int)rect.x;
        // Unity Sprite rect dùng gốc Y bottom-left của texture — GetPixels cũng vậy nên OK.
        int y = (int)rect.y;

        // FAST PATH: GetPixels nếu texture readable.
        if (sprite.texture.isReadable)
        {
            try
            {
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var pixels = sprite.texture.GetPixels(x, y, w, h);
                tex.SetPixels(pixels);
                tex.Apply();
                return tex;
            }
            catch (System.ArgumentException)
            {
                // Fall through tới Blit fallback bên dưới.
            }
        }

        // FALLBACK: copy GPU-side qua RenderTexture — không cần Read/Write.
        // Blit toàn bộ atlas vào RT cùng size, sau đó ReadPixels chỉ vùng sprite.
        var src = sprite.texture;
        var prev = RenderTexture.active;
        RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        try
        {
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            // ReadPixels(srcRect, destX, destY): srcRect dùng gốc Y bottom-left của RT.
            outTex.ReadPixels(new Rect(x, y, w, h), 0, 0, false);
            outTex.Apply();
            return outTex;
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    private void MakeTextureReadable(Texture2D texture)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }
}
#endif
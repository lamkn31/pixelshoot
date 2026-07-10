#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Wayfu.Lamkn
{
public class ResizeToMultipleOf4 : EditorWindow
{
    #region Data Types

    private class FolderEntry
    {
        public string path = "";
        public bool resize = true;
        public bool crunchETC2 = true;
        public int crunchQuality = 50;
        public int maxSize = 2048;
        public TextureResizeAlgorithm resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
        public bool packAtlas = true;
        public string atlasName = "";
        public bool foldout = true;
        // Default ON: any non-Sprite texture in the folder (typically Default 2D) is converted
        // to Sprite (2D and UI) so it can be packed into a SpriteAtlas / used by UI Image.
        public bool forceSpriteType = true;
    }

    private struct ImageInfo
    {
        public string path;
        public int originalW, originalH, newW, newH;
        public bool needsResize;
    }

    #endregion

    #region State

    private readonly List<FolderEntry> _folders = new List<FolderEntry>();
    private readonly List<ImageInfo> _preview = new List<ImageInfo>();

    private bool _previewOnly = false;
    private Vector2 _scrollFolders;
    private Vector2 _scrollResults;

    // Optional shared "Common" folder auto-added to packables of every atlas the tool creates.
    // This makes each popup atlas self-contained: it packs its own sprites + all common sprites
    // so any popup using a Common icon (coin, button, frame...) renders with only ONE atlas
    // texture -> no batch break between popup-specific and common sprites.
    private DefaultAsset _commonFolder;

    // ImageConversion.LoadImage only reliably supports PNG / JPG / EXR.
    // Other formats (PSD / TIFF / WEBP / BMP / TGA / GIF) can fail or crash on LoadImage.
    private static readonly string[] SupportedExt =
        { ".png", ".jpg", ".jpeg", ".exr" };

    private static readonly int[] MaxSizeOptions =
        { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };

    #endregion

    #region Menu

    [MenuItem("Tools/Resize + Compress + Atlas (x4)")]
    public static void ShowWindow()
    {
        var win = GetWindow<ResizeToMultipleOf4>("Img Tools x4");
        win.minSize = new Vector2(600, 600);
    }

    #endregion

    #region GUI - Main

    private void OnGUI()
    {
        GUILayout.Space(8);
        EditorGUILayout.LabelField(
            "Image Tools  -  Resize x4 / ETC2 RGBA Crunch / Sprite Atlas",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Add one or more folders. Each folder has independent settings.\n" +
            "- Resize: rounds UP to multiple of 4 (size only increases).\n" +
            "- ETC2 RGBA8 Crunched: Android TextureImporter override.\n" +
            "- Atlas: packs all sprites in the folder into one SpriteAtlas (reduces drawcalls).",
            MessageType.Info);

        GUILayout.Space(6);

        DrawToolbar();
        GUILayout.Space(4);
        DrawFolderList();
        GUILayout.Space(6);
        DrawActionButtons();
        DrawResults();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginVertical("box");

        // Row 1: Preview toggle + Add Folder
        EditorGUILayout.BeginHorizontal();
        _previewOnly = EditorGUILayout.ToggleLeft(
            "Preview / Dry-run (no files written)", _previewOnly, GUILayout.Width(240));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+ Add Folder", GUILayout.Width(100), GUILayout.Height(22)))
            AddFolder();
        EditorGUILayout.EndHorizontal();

        // Row 2: Common folder (auto-included in every atlas this tool creates)
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            new GUIContent("Common Folder",
                "Optional. If set, every atlas the tool creates will also pack this folder " +
                "as a second packable -> each popup atlas becomes self-contained (popup sprites " +
                "+ common sprites in ONE texture) so mixing them does not break batching."),
            GUILayout.Width(120));
        _commonFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            _commonFolder, typeof(DefaultAsset), false);
        if (_commonFolder != null && !IsAssetFolder(_commonFolder))
        {
            EditorGUILayout.LabelField("(not a folder!)", EditorStyles.miniLabel, GUILayout.Width(90));
            _commonFolder = null;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private static bool IsAssetFolder(Object asset)
    {
        if (asset == null) return false;
        string path = AssetDatabase.GetAssetPath(asset);
        return !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path);
    }

    private void DrawFolderList()
    {
        float listHeight = Mathf.Clamp(_folders.Count * 175f, 120f, 340f);
        _scrollFolders = EditorGUILayout.BeginScrollView(_scrollFolders, GUILayout.Height(listHeight));

        for (int i = 0; i < _folders.Count; i++)
            DrawFolderEntry(i);

        if (_folders.Count == 0)
            EditorGUILayout.HelpBox("Click '+ Add Folder' to begin.", MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    private void DrawActionButtons()
    {
        GUI.enabled = _folders.Count > 0;
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Scan All", GUILayout.Height(32)))
            RunAll(writeFiles: false);

        GUI.color = _previewOnly ? Color.white : new Color(0.4f, 0.85f, 0.4f);
        if (GUILayout.Button(_previewOnly ? "Preview All" : "Run All", GUILayout.Height(32)))
            RunAll(writeFiles: !_previewOnly);
        GUI.color = Color.white;

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawResults()
    {
        if (_preview.Count == 0) return;

        GUILayout.Space(6);
        int needCount = _preview.Count(p => p.needsResize);
        EditorGUILayout.LabelField(
            $"Scan results: {_preview.Count} image(s)  -  {needCount} need resize",
            EditorStyles.boldLabel);

        _scrollResults = EditorGUILayout.BeginScrollView(_scrollResults);
        foreach (var img in _preview)
        {
            EditorGUILayout.BeginHorizontal(
                img.needsResize ? "helpbox" : EditorStyles.inspectorDefaultMargins);
            EditorGUILayout.LabelField(Path.GetFileName(img.path), GUILayout.Width(210));

            if (img.needsResize)
            {
                GUI.color = new Color(1f, 0.7f, 0.2f);
                EditorGUILayout.LabelField(
                    $"{img.originalW}x{img.originalH}  ->  {img.newW}x{img.newH}",
                    EditorStyles.boldLabel);
            }
            else
            {
                GUI.color = new Color(0.5f, 0.9f, 0.5f);
                EditorGUILayout.LabelField($"{img.originalW}x{img.originalH}  (ok)");
            }
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    #endregion

    #region GUI - Folder Entry

    private void DrawFolderEntry(int idx)
    {
        var e = _folders[idx];

        EditorGUILayout.BeginVertical("box");

        // Header
        EditorGUILayout.BeginHorizontal();
        string label = string.IsNullOrEmpty(e.path) ? "(no folder selected)" : Path.GetFileName(e.path);
        e.foldout = EditorGUILayout.Foldout(e.foldout, $"  {label}", true, EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("X", GUILayout.Width(22), GUILayout.Height(18)))
        {
            _folders.RemoveAt(idx);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        EditorGUILayout.EndHorizontal();

        if (!e.foldout)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUI.indentLevel++;

        // Path
        EditorGUILayout.BeginHorizontal();
        e.path = EditorGUILayout.TextField("Path", e.path);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string chosen = EditorUtility.OpenFolderPanel("Select Image Folder", e.path, "");
            if (!string.IsNullOrEmpty(chosen))
            {
                e.path = chosen;
                if (string.IsNullOrEmpty(e.atlasName))
                    e.atlasName = Path.GetFileName(chosen);
                _preview.Clear();
            }
        }
        EditorGUILayout.EndHorizontal();

        // Force Sprite texture type — runs as the very first step so all subsequent steps
        // (resize / ETC2 / atlas) see Sprite (2D and UI) importers.
        GUILayout.Space(3);
        e.forceSpriteType = EditorGUILayout.ToggleLeft(
            new GUIContent("Force Texture Type → Sprite (2D and UI)",
                "Tự động đổi mọi texture trong folder sang Sprite (2D and UI). Bật để các bước Resize/Atlas hoạt động đúng với UI Image."),
            e.forceSpriteType);

        // Resize
        GUILayout.Space(3);
        e.resize = EditorGUILayout.ToggleLeft(
            "Resize to multiple of 4 (round UP, size only increases)", e.resize);

        // Crunch ETC2 RGBA
        GUILayout.Space(3);
        e.crunchETC2 = EditorGUILayout.ToggleLeft(
            "Apply ETC2 RGBA8 Crunched (Android override)", e.crunchETC2);

        if (e.crunchETC2)
        {
            EditorGUI.indentLevel++;

            e.crunchQuality = EditorGUILayout.IntSlider(
                new GUIContent("Compressor Quality", "0 = smallest, 100 = best quality"),
                e.crunchQuality, 0, 100);

            string[] maxSizeLabels = System.Array.ConvertAll(MaxSizeOptions, x => x.ToString());
            int curIdx = System.Array.IndexOf(MaxSizeOptions, e.maxSize);
            if (curIdx < 0) curIdx = 6;
            curIdx = EditorGUILayout.Popup(
                new GUIContent("Max Size", "Maximum texture dimension for Android"),
                curIdx, maxSizeLabels);
            e.maxSize = MaxSizeOptions[curIdx];

            e.resizeAlgorithm = (TextureResizeAlgorithm)EditorGUILayout.EnumPopup(
                new GUIContent("Resize Algorithm", "Mitchell = high quality. Bilinear = faster."),
                e.resizeAlgorithm);

            EditorGUILayout.HelpBox(
                "Format: ETC2_RGBA8Crunched (full alpha + crunch). " +
                "Textures must be inside Assets/.",
                MessageType.None);

            EditorGUI.indentLevel--;
        }

        // Pack Atlas
        GUILayout.Space(3);
        e.packAtlas = EditorGUILayout.ToggleLeft(
            "Pack folder into Sprite Atlas (reduce drawcalls)", e.packAtlas);

        if (e.packAtlas)
        {
            EditorGUI.indentLevel++;
            e.atlasName = EditorGUILayout.TextField(
                new GUIContent("Atlas Name", "Name of the .spriteatlasv2 asset"),
                e.atlasName);
            EditorGUILayout.HelpBox(
                "Creates / updates a SpriteAtlas at the folder root. " +
                "The entire folder is added as a packable source.",
                MessageType.None);
            EditorGUI.indentLevel--;
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
        GUILayout.Space(4);
    }

    #endregion

    #region Pipeline

    private void AddFolder()
    {
        string chosen = EditorUtility.OpenFolderPanel("Select Image Folder", "Assets", "");
        if (string.IsNullOrEmpty(chosen)) return;
        _folders.Add(new FolderEntry
        {
            path = chosen,
            atlasName = Path.GetFileName(chosen)
        });
    }

    private void RunAll(bool writeFiles)
    {
        _preview.Clear();
        int total = _folders.Count;

        // Step 1: Resize on disk (no AssetDatabase work yet).
        try
        {
            for (int i = 0; i < total; i++)
            {
                var e = _folders[i];
                if (string.IsNullOrEmpty(e.path) || !Directory.Exists(e.path))
                {
                    Debug.LogWarning($"[ImgTools] Skipping invalid path: {e.path}");
                    continue;
                }

                EditorUtility.DisplayProgressBar(
                    "Resizing...", Path.GetFileName(e.path), (float)i / total);

                if (e.resize)
                    StepResize(e, writeFiles);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (!writeFiles)
        {
            Repaint();
            return;
        }

        // Sync disk changes into AssetDatabase before reimport / atlas work.
        AssetDatabase.Refresh();

        // Step 2 + 3: ETC2 + Atlas, bracketed so Unity does ONE batched import at the end.
        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < total; i++)
            {
                var e = _folders[i];
                if (string.IsNullOrEmpty(e.path) || !Directory.Exists(e.path)) continue;

                EditorUtility.DisplayProgressBar(
                    "Compress / Atlas...", Path.GetFileName(e.path), (float)i / total);

                // Type conversion runs first so ETC2 + Atlas see Sprite importers.
                if (e.forceSpriteType)
                    StepForceSpriteType(e);

                if (e.crunchETC2)
                    StepCrunchETC2(e);

                if (e.packAtlas)
                    StepCreateAtlas(e);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // NOTE: we intentionally do NOT call SpriteAtlasUtility.PackAtlases here.
        // Packing from editor scripts was observed to crash the editor on certain
        // projects. The atlas assets are fully configured (Include in Build = true,
        // padding 8, alpha dilation, Android ETC2_RGBA8Crunched override) so Unity
        // will pack them at build time. To pack manually now, use the "Pack Preview"
        // button on the SpriteAtlas inspector.

        int resized = _preview.Count(p => p.needsResize);
        EditorUtility.DisplayDialog("Done",
            $"Finished processing {total} folder(s).\n" +
            $"Resized: {resized} image(s).\n" +
            "See Console for ETC2 / Atlas details.",
            "OK");

        Repaint();
    }

    #endregion

    #region Step 1 - Resize

    private void StepResize(FolderEntry e, bool writeFiles)
    {
        foreach (string file in Directory.GetFiles(e.path, "*.*", SearchOption.TopDirectoryOnly))
        {
            if (!IsSupportedExt(file)) continue;
            ResizeSingleImage(file, writeFiles);
        }
    }

    private void ResizeSingleImage(string filePath, bool writeFile)
    {
        Texture2D tex = null;
        Texture2D dst = null;

        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            if (!ImageConversion.LoadImage(tex, bytes))
            {
                Debug.LogWarning($"[Resize] Could not decode '{Path.GetFileName(filePath)}' - skipped.");
                return;
            }

            int origW = tex.width, origH = tex.height;
            int newW = RoundUpMul4(origW), newH = RoundUpMul4(origH);
            bool needs = newW != origW || newH != origH;

            _preview.Add(new ImageInfo
            {
                path = filePath,
                originalW = origW,
                originalH = origH,
                newW = newW,
                newH = newH,
                needsResize = needs
            });

            if (!writeFile || !needs) return;

            // Safety cap: skip enormous textures that would OOM on GetPixels.
            const long MaxPixels = 8192L * 8192L;
            if ((long)newW * newH > MaxPixels)
            {
                Debug.LogWarning($"[Resize] '{Path.GetFileName(filePath)}' too large ({newW}x{newH}) - skipped.");
                return;
            }

            dst = new Texture2D(newW, newH, TextureFormat.RGBA32, false);
            Color[] srcPx = tex.GetPixels();
            Color[] dstPx = new Color[newW * newH]; // transparent black padding

            for (int y = 0; y < origH; y++)
                for (int x = 0; x < origW; x++)
                    dstPx[y * newW + x] = srcPx[y * origW + x];

            dst.SetPixels(dstPx);
            dst.Apply(false, false);

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            byte[] encoded;
            if (ext == ".jpg" || ext == ".jpeg")
                encoded = ImageConversion.EncodeToJPG(dst, 95);
            else if (ext == ".exr")
                encoded = ImageConversion.EncodeToEXR(dst);
            else
                encoded = ImageConversion.EncodeToPNG(dst);

            File.WriteAllBytes(filePath, encoded);
            Debug.Log($"[Resize] {Path.GetFileName(filePath)} : {origW}x{origH} -> {newW}x{newH}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Resize] Failed on '{filePath}': {ex.Message}");
        }
        finally
        {
            if (dst != null) DestroyImmediate(dst);
            if (tex != null) DestroyImmediate(tex);
        }
    }

    #endregion

    #region Step 1b - Force Sprite (2D and UI) Texture Type

    /// <summary>
    /// Convert every direct-child texture in the folder to Sprite (2D and UI). No-op if the
    /// texture is already Sprite. Runs before ETC2 + Atlas so those steps see Sprite importers.
    /// </summary>
    private void StepForceSpriteType(FolderEntry e)
    {
        string rel = AbsToAssetPath(e.path);
        if (rel == null)
        {
            Debug.LogWarning($"[ForceSprite] '{e.path}' is outside Assets/ - skipping.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { rel });
        int count = 0;

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            // Only direct children of this folder
            if (NormDir(Path.GetDirectoryName(assetPath)) != NormDir(rel))
                continue;

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) continue;
            if (importer.textureType == TextureImporterType.Sprite) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            count++;
        }

        Debug.Log($"[ForceSprite] {count} texture(s) converted to Sprite (2D and UI) in '{rel}'.");
    }

    #endregion

    #region Step 2 - ETC2 RGBA Crunched (Android)

    private void StepCrunchETC2(FolderEntry e)
    {
        string rel = AbsToAssetPath(e.path);
        if (rel == null)
        {
            Debug.LogWarning($"[ETC2] '{e.path}' is outside Assets/ - skipping.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { rel });
        int count = 0;

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            // Only direct children of this folder
            if (NormDir(Path.GetDirectoryName(assetPath)) != NormDir(rel))
                continue;

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) continue;

            TextureImporterPlatformSettings ps = importer.GetPlatformTextureSettings("Android");

            ps.overridden = true;
            ps.compressionQuality = e.crunchQuality;
            ps.maxTextureSize = e.maxSize;
            ps.resizeAlgorithm = e.resizeAlgorithm;
            ps.format = TextureImporterFormat.ETC2_RGBA8Crunched;

            importer.SetPlatformTextureSettings(ps);
            importer.SaveAndReimport();
            count++;
        }

        Debug.Log($"[ETC2 RGBA Crunched] {count} texture(s) updated in '{rel}'  Quality={e.crunchQuality}");
    }

    #endregion

    #region Step 3 - Sprite Atlas

    private void StepCreateAtlas(FolderEntry e)
    {
        string rel = AbsToAssetPath(e.path);
        if (rel == null)
        {
            Debug.LogWarning($"[Atlas] '{e.path}' is outside Assets/ - skipping.");
            return;
        }

        string name = string.IsNullOrWhiteSpace(e.atlasName)
            ? Path.GetFileName(e.path)
            : e.atlasName;
        name = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
        if (string.IsNullOrEmpty(name)) name = "SpriteAtlas";

        // IMPORTANT: place the atlas asset in the PARENT folder, never inside the
        // folder it packs. An atlas living inside its own packable folder causes
        // Unity to re-scan itself on every import -> import loops / crashes.
        string relNorm = NormDir(rel);
        string parentDir = NormDir(Path.GetDirectoryName(relNorm));
        if (string.IsNullOrEmpty(parentDir) || !parentDir.StartsWith("Assets"))
            parentDir = "Assets"; // fallback if folder is directly under Assets root

        string atlasPath = $"{parentDir}/{name}.spriteatlas";

        SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
        bool created = false;
        if (atlas == null)
        {
            atlas = new SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, atlasPath);
            created = true;
            Debug.Log($"[Atlas] Created: {atlasPath}");
        }
        else
        {
            Debug.Log($"[Atlas] Updating: {atlasPath}");
        }

        // Always (re)apply the canonical settings so existing atlases get updated too.
        atlas.SetIncludeInBuild(true);

        var packing = new SpriteAtlasPackingSettings
        {
            blockOffset = 1,
            enableRotation = false,
            enableTightPacking = false,
            enableAlphaDilation = true,
            padding = 8,
        };
        atlas.SetPackingSettings(packing);

        var texSettings = new SpriteAtlasTextureSettings
        {
            readable = false,
            generateMipMaps = false,
            sRGB = true,
            filterMode = FilterMode.Bilinear,
        };
        atlas.SetTextureSettings(texSettings);

        // Android platform override on the atlas itself: RGBA Crunched ETC2.
        var androidPS = new TextureImporterPlatformSettings
        {
            name = "Android",
            overridden = true,
            maxTextureSize = e.maxSize,
            format = TextureImporterFormat.ETC2_RGBA8Crunched,
            textureCompression = TextureImporterCompression.Compressed,
            compressionQuality = e.crunchQuality,
            crunchedCompression = true,
            resizeAlgorithm = e.resizeAlgorithm,
            allowsAlphaSplitting = false,
        };
        atlas.SetPlatformSettings(androidPS);

        Object folderObj = AssetDatabase.LoadAssetAtPath<Object>(rel);
        if (folderObj != null)
        {
            Object[] existing = atlas.GetPackables();
            if (!System.Array.Exists(existing, o => o == folderObj))
                atlas.Add(new Object[] { folderObj });
        }

        // Auto-pack the global Common folder so the atlas is self-contained.
        // Skip if the current folder IS the common folder (don't pack twice).
        if (_commonFolder != null)
        {
            string commonPath = AssetDatabase.GetAssetPath(_commonFolder);
            if (!string.IsNullOrEmpty(commonPath) &&
                NormDir(commonPath) != NormDir(rel))
            {
                Object[] existing = atlas.GetPackables();
                if (!System.Array.Exists(existing, o => o == _commonFolder))
                {
                    atlas.Add(new Object[] { _commonFolder });
                    Debug.Log($"[Atlas] Common folder '{commonPath}' merged into '{name}'.");
                }
            }
        }

        EditorUtility.SetDirty(atlas);
        Debug.Log($"[Atlas] '{name}' configured (padding=8, alphaDilation, Android ETC2_RGBA8Crunched).");
    }

    #endregion

    #region Helpers

    private static int RoundUpMul4(int n)
    {
        int r = n % 4;
        return r == 0 ? n : n + (4 - r);
    }

    private static bool IsSupportedExt(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return System.Array.Exists(SupportedExt, e => e == ext);
    }

    private static string NormDir(string p) =>
        p?.Replace('\\', '/').TrimEnd('/') ?? "";

    /// <summary>
    /// Converts an absolute filesystem path to a project-relative "Assets/..." path.
    /// Returns null if outside the project.
    /// </summary>
    private static string AbsToAssetPath(string absPath)
    {
        string dataPath = Application.dataPath.Replace('\\', '/').TrimEnd('/');
        string norm = absPath.Replace('\\', '/').TrimEnd('/');

        if (!norm.StartsWith(dataPath))
            return null;

        return "Assets" + norm.Substring(dataPath.Length);
    }

    #endregion
}
}
#endif

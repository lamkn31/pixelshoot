using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Level Tool 3 khung: trái = files/GameSettings/list level; giữa = màn hình (chiếu theo camera,
    /// zoom/pan, kéo waypoint & 4 góc grid); phải = data level. Block xếp thành GRID (4 góc + cong,
    /// nhiều grid/level). Config gun/path ở GameSettings. Mở qua Wayfu ▸ Level Tool.
    /// </summary>
    public class LevelBuilderWindow : EditorWindow
    {
        private enum ListOp { None, MoveUp, MoveDown, Duplicate, Delete }

        private const string FolderPrefKey = "Wayfu.LevelTool.Folder";
        private const float Pad = 24f;
        private static readonly GUILayoutOption BtnW = GUILayout.Width(24);

        private LevelData _target;
        private SerializedObject _so;
        private SerializedObject _gsSO;
        private SerializedObject _listSO;

        private string _levelsFolder = "Assets";
        private readonly List<LevelData> _levels = new List<LevelData>();
        private Gun _defaultGunPrefab;
        private Block _defaultBlockPrefab;

        private Vector2 _leftScroll, _rightScroll;
        private bool _showPath = true, _showSlots = true, _showBlocks = true, _showFrame = true, _showRange = true, _showDir = true;
        // Ghost hàng đợi của cell Spawner — tắt cho đỡ rối khi không sửa spawner.
        private bool _showQueue = true;

        // Bề rộng 2 panel — kéo được.
        private float _leftW = 250f, _rightW = 360f;
        private int _dragSplitter; // 0 none, 1 = trái, 2 = phải

        private Camera _previewCamera;

        // View transform.
        private Rect _viewContent;
        private Vector2 _viewMin;
        private float _viewScale = 1f;
        private bool _camMode;
        private float _zoom = 1f;
        private Vector2 _pan = Vector2.zero;
        private bool _panning;

        // Kéo.
        private int _dragWaypoint = -1;
        private int _dragGrid = -1, _dragHandle = -1; // handle: 0=Center, 1=Left tip, 2=Right tip
        private int _dragCellGrid = -1, _dragCellIndex = -1; // xoay hướng 1 cell
        private Vector3 _dragCellCenter;
        private int _dragSplineGrid = -1, _dragSplineWp = -1; // kéo waypoint của Spline grid

        // Generate grid.
        private TypeColor _genColor = TypeColor.Red;
        private int _genStack = 3;
        private BlockGridShape _genShape = BlockGridShape.Spline; // loại grid khi bấm "+ Grid"

        // Màu tô cell bằng click trong khung giữa. None → click để CHỌN xem/sửa thông số.
        private TypeColor _paintColor = TypeColor.None;

        // Brush tô màu: giữ chuột rê qua nhiều cell thay vì chấm từng ô.
        private bool _painting;
        private Vector2 _lastPaintPos;

        // Live sync: sửa grid/path lúc đang Play → dựng lại level ngay để thấy vị trí thật.
        private bool _liveSync = true;
        private bool _liveDirty;

        // Đối tượng đang chọn trong khung giữa (chỉ khi Paint = None).
        private readonly List<(int grid, int cell)> _selCells = new List<(int, int)>();
        private int _selSlot = -1, _selGun = -1;    // gun (chọn 1)
        // Cell trong hàng đợi Spawner đang chọn (chỉ 1). qi < 0 = không chọn gì.
        private (int grid, int cell, int qi) _selQueue = (-1, -1, -1);

        // Giá trị áp dụng CHUNG cho nhiều cell đang chọn.
        private TypeColor _multiColor = TypeColor.Red;
        private BlockCellType _multiType = BlockCellType.Normal;
        private int _multiStack = 3;

        // Đường bo góc của path, dựng 1 lần mỗi lần vẽ (dùng chung với runtime qua BuildSamples).
        private Vector3[] _pathSamples;

        // Quét chọn (marquee) + vùng click của cell/gun, gom lại trong lúc vẽ.
        private Vector2 _marqueeStart, _marqueeEnd;
        private bool _marqueeOn;
        private readonly List<(Rect rect, int grid, int flat)> _hitCells = new List<(Rect, int, int)>();
        private readonly List<(Rect rect, int slot, int gun)> _hitGuns = new List<(Rect, int, int)>();
        // Ghost hàng đợi của cell Spawner. qi = index trong Queue; qi == Queue.Count là ô "+" (thêm mới).
        private readonly List<(Rect rect, int grid, int flat, int qi)> _hitQueue = new List<(Rect, int, int, int)>();

        private bool IsCellSelected(int gi, int flat) => _selCells.Contains((gi, flat));

        private void SelectGun(int si, int gi) { _selSlot = si; _selGun = gi; _selCells.Clear(); ClearQueueSel(); }

        private void ClearQueueSel() => _selQueue = (-1, -1, -1);
        private bool IsQueueSelected(int gi, int flat, int qi) => _selQueue == (gi, flat, qi);

        private void SelectQueue(int gi, int flat, int qi)
        {
            _selQueue = (gi, flat, qi);
            _selCells.Clear(); _selSlot = -1; _selGun = -1;
        }

        // Ctrl/Cmd = chọn thêm (bấm lại thì bỏ chọn).
        private void ToggleCell(int gi, int flat, bool additive)
        {
            _selSlot = -1; _selGun = -1; ClearQueueSel();
            var key = (gi, flat);
            if (!additive) { _selCells.Clear(); _selCells.Add(key); return; }
            if (!_selCells.Remove(key)) _selCells.Add(key);
        }

        // Foldout: thu gọn từng nhóm cho panel phải ngắn lại (chỉ GRIDS mở sẵn).
        private bool _foldMeta, _foldPath, _foldPrefabs, _foldWaypoints, _foldSlots, _foldGrids = true;
        private readonly List<bool> _foldGrid = new List<bool>();   // theo từng grid
        private readonly List<bool> _foldSlot = new List<bool>();   // theo từng slot

        // Foldout theo index, tự nới list. Mặc định đóng để panel gọn.
        private static bool FoldAt(List<bool> flags, int i, string label)
        {
            while (flags.Count <= i) flags.Add(false);
            flags[i] = EditorGUILayout.Foldout(flags[i], label, true);
            return flags[i];
        }

        [MenuItem("Wayfu/Level Tool")]
        public static void Open()
        {
            var w = GetWindow<LevelBuilderWindow>("Level Tool");
            w.minSize = new Vector2(960, 520);
        }

        [MenuItem("CONTEXT/LevelData/Open in Level Tool")]
        private static void OpenFromContext(MenuCommand cmd)
        {
            var w = GetWindow<LevelBuilderWindow>("Level Tool");
            w.Select(cmd.context as LevelData);
        }

        private void OnEnable()
        {
            _levelsFolder = EditorPrefs.GetString(FolderPrefKey, "Assets");
            EnsurePreviewCamera();
            RefreshLevels();
        }

        // WYSIWYG: luôn chiếu bằng camera thật. Nếu chưa gán (null vì domain-reload/scene chưa sẵn lúc
        // OnEnable) thì resolve lại: ưu tiên Camera.main, fallback camera bất kỳ trong scene.
        private void EnsurePreviewCamera()
        {
            if (_previewCamera != null) return;
            _previewCamera = Camera.main;
            if (_previewCamera == null)
            {
                var cams = Object.FindObjectsOfType<Camera>();
                if (cams.Length > 0) _previewCamera = cams[0];
            }
        }

        private void OnGUI()
        {
            EnsurePreviewCamera();
            DrawToolbar();

            if (_target != null)
            {
                if (_so == null || _so.targetObject != _target) _so = new SerializedObject(_target);
                _so.Update();
            }

            EditorGUILayout.BeginHorizontal();
            DrawLeftPanel();
            DrawSplitter(1, +1f, ref _leftW);
            DrawCenterPanel();
            DrawSplitter(2, -1f, ref _rightW);
            DrawRightPanel();
            EditorGUILayout.EndHorizontal();

            if (_target != null && _so != null && _so.ApplyModifiedProperties()) _liveDirty = true;
            TryLiveRebuild();
        }

        private bool IsDragging => _dragGrid >= 0 || _dragWaypoint >= 0 || _dragCellGrid >= 0 || _painting || _panning;

        /// <summary>
        /// Sửa grid/path/cell lúc đang Play → dựng lại level để thấy ngay vị trí THẬT trong runtime,
        /// khỏi phải thoát Play rồi vào lại. Đợi thả chuột mới dựng: kéo handle sinh thay đổi mỗi frame,
        /// dựng lại cả board 60 lần/giây thì giật và reset gameplay liên tục.
        /// </summary>
        private void TryLiveRebuild()
        {
            if (!_liveDirty || IsDragging) return;
            _liveDirty = false;
            if (!_liveSync || !Application.isPlaying || _target == null) return;
            // Không dùng LevelController.Instance: Singleton.Instance log error khi scene chưa có nó.
            var lc = Object.FindObjectOfType<LevelController>();
            if (lc != null && lc.Level == _target) lc.Build();
        }

        // Thanh kéo dọc để resize panel. sign = chiều tăng width theo delta.x của chuột.
        private void DrawSplitter(int id, float sign, ref float width)
        {
            var rect = GUILayoutUtility.GetRect(5f, 5f, GUILayout.Width(5f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.35f));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);

            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (rect.Contains(e.mousePosition)) { _dragSplitter = id; e.Use(); }
                    break;
                case EventType.MouseDrag:
                    if (_dragSplitter == id)
                    {
                        width = Mathf.Clamp(width + sign * e.delta.x, 170f, position.width - 430f);
                        e.Use(); Repaint();
                    }
                    break;
                case EventType.MouseUp:
                    if (_dragSplitter == id) { _dragSplitter = 0; e.Use(); }
                    break;
            }
        }

        #region Toolbar

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("PixelShoot Level Tool", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            _showPath = GUILayout.Toggle(_showPath, "Path", EditorStyles.toolbarButton);
            _showSlots = GUILayout.Toggle(_showSlots, "Slots", EditorStyles.toolbarButton);
            _showBlocks = GUILayout.Toggle(_showBlocks, "Grids", EditorStyles.toolbarButton);
            _showDir = GUILayout.Toggle(_showDir, "Dir", EditorStyles.toolbarButton);
            _showQueue = GUILayout.Toggle(_showQueue, new GUIContent("Queue",
                "Hiện các cell hàng đợi nằm SAU cell Spawner (ô mờ + ô \"+\"). Tắt cho đỡ rối khi không sửa spawner — " +
                "tắt rồi thì click cũng không còn trúng chúng nữa."), EditorStyles.toolbarButton);
            _showFrame = GUILayout.Toggle(_showFrame, "Cam", EditorStyles.toolbarButton);
            _showRange = GUILayout.Toggle(_showRange, "Range", EditorStyles.toolbarButton);
            _liveSync = GUILayout.Toggle(_liveSync, new GUIContent("Live",
                "Đang Play mà sửa grid/path/cell thì dựng lại level ngay trong runtime (thả chuột mới dựng). " +
                "Chỉ chạy khi LevelController đang gán ĐÚNG level đang mở."), EditorStyles.toolbarButton);
            if (GUILayout.Button("Fit", EditorStyles.toolbarButton)) { _zoom = 1f; _pan = Vector2.zero; Repaint(); }
            if (GUILayout.Button("Add Preview", EditorStyles.toolbarButton) && _target != null) AddPreviewToScene();
            if (GUILayout.Button("Save", EditorStyles.toolbarButton)) AssetDatabase.SaveAssets();
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Left panel

        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(_leftW));
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            EditorGUILayout.LabelField("Files", EditorStyles.boldLabel);
            var folderObj = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_levelsFolder);
            var newFolder = (DefaultAsset)EditorGUILayout.ObjectField("Levels Folder", folderObj, typeof(DefaultAsset), false);
            if (newFolder != folderObj)
            {
                string p = AssetDatabase.GetAssetPath(newFolder);
                if (!string.IsNullOrEmpty(p) && AssetDatabase.IsValidFolder(p)) { _levelsFolder = p; EditorPrefs.SetString(FolderPrefKey, p); RefreshLevels(); }
            }
            _defaultGunPrefab = (Gun)EditorGUILayout.ObjectField("Def. Gun", _defaultGunPrefab, typeof(Gun), false);
            _defaultBlockPrefab = (Block)EditorGUILayout.ObjectField("Def. Block", _defaultBlockPrefab, typeof(Block), false);
            _previewCamera = (Camera)EditorGUILayout.ObjectField("Scene Camera", _previewCamera, typeof(Camera), true);

            EditorGUILayout.Space(4);
            DrawGameSettings();

            EditorGUILayout.Space(4);
            DrawLevelList();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Level")) CreateNewLevel();
            if (GUILayout.Button("Reload")) RefreshLevels();
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Create Scene Slots (0-4)")) CreateSceneSlots();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Levels ({_levels.Count})", EditorStyles.boldLabel);
            for (int i = 0; i < _levels.Count; i++)
            {
                var lv = _levels[i];
                if (lv == null) continue;
                EditorGUILayout.BeginHorizontal();
                bool sel = lv == _target;
                if (GUILayout.Toggle(sel, $"{i}. {lv.name}", "Button") && !sel) Select(lv);
                // ▶ = chơi thẳng level này (trước đây chỉ Add Preview — trùng nút trên toolbar).
                if (GUILayout.Button(new GUIContent("▶", "Chơi level này ngay (tự vào Play mode nếu đang tắt)."), BtnW))
                { Select(lv); PlayLevel(lv); GUIUtility.ExitGUI(); }
                if (GUILayout.Button("X", BtnW)) { DeleteLevel(lv); GUIUtility.ExitGUI(); }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // Thứ tự chơi: LevelController lấy level theo index trong asset này (UserProgress.currentLevelIndex).
        private void DrawLevelList()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("LEVEL LIST (thứ tự chơi)", EditorStyles.boldLabel);

            var list = LevelList.Instance;
            EditorGUILayout.ObjectField("Asset", list, typeof(LevelList), false);
            if (list == null)
            {
                EditorGUILayout.HelpBox("Chưa có LevelList. Tạo asset để LevelController load level theo index.",
                    MessageType.Info);
                if (GUILayout.Button("Create LevelList")) CreateLevelList();
                EditorGUILayout.EndVertical();
                return;
            }

            if (_listSO == null || _listSO.targetObject != list) _listSO = new SerializedObject(list);
            _listSO.Update();
            var levels = _listSO.FindProperty("Levels");

            EditorGUILayout.HelpBox($"{list.Count} level. Index 0 = level đầu. Chơi hết list thì LẶP LẠI, "
                + "bỏ qua level có bật Skip Level Loop (level tutorial chỉ chơi 1 lần).", MessageType.None);

            int pend = -1; ListOp op = ListOp.None;
            for (int i = 0; i < levels.arraySize; i++)
            {
                var el = levels.GetArrayElementAtIndex(i);
                var lv = el.objectReferenceValue as LevelData;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(i.ToString(), GUILayout.Width(18));
                // Bấm tên = mở level đó trong tool; ô trống thì gán bằng ObjectField.
                if (lv != null)
                {
                    bool sel = lv == _target;
                    string tag = lv.SkipLevelLoop ? " (skip loop)" : "";
                    if (GUILayout.Toggle(sel, lv.name + tag, "Button") && !sel) Select(lv);
                }
                else el.objectReferenceValue = EditorGUILayout.ObjectField(null, typeof(LevelData), false);
                var o = MiniButtons(i, levels.arraySize);
                if (o != ListOp.None) { pend = i; op = o; }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_target == null || list.IndexOf(_target) >= 0))
                if (GUILayout.Button("+ Level đang mở")) levels.GetArrayElementAtIndex(AddArray(levels)).objectReferenceValue = _target;
            if (GUILayout.Button(new GUIContent("Fill from folder",
                "Nạp mọi LevelData trong Levels Folder theo thứ tự tên, ghi đè list hiện tại.")))
                FillLevelListFromFolder(levels);
            if (GUILayout.Button("Clear")) levels.arraySize = 0;
            EditorGUILayout.EndHorizontal();

            if (pend >= 0) ApplyOp(levels, pend, op);
            _listSO.ApplyModifiedProperties();

            // Nhảy thẳng tới level đang mở, không cần đợi thắng từng màn để tiến trình bò tới đó.
            using (new EditorGUI.DisabledScope(_target == null))
                if (GUILayout.Button(new GUIContent("▶ Play level đang mở",
                    "Chơi đúng level này, bỏ qua tiến trình đã lưu. Đang tắt Play thì tự vào Play mode.")))
                { PlayLevel(_target); GUIUtility.ExitGUI(); }

            EditorGUILayout.EndVertical();
        }

        private void CreateLevelList()
        {
            const string dir = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "Resources");
            var asset = CreateInstance<LevelList>();
            foreach (var lv in _levels) if (lv != null) asset.Levels.Add(lv);
            AssetDatabase.CreateAsset(asset, dir + "/LevelList.asset");
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(asset);
        }

        private void FillLevelListFromFolder(SerializedProperty levels)
        {
            RefreshLevels();
            levels.arraySize = _levels.Count;
            for (int i = 0; i < _levels.Count; i++) levels.GetArrayElementAtIndex(i).objectReferenceValue = _levels[i];
        }

        /// <summary>
        /// Bấm ▶ = chơi ĐÚNG level này, bất kể tiến trình đang ở đâu.
        /// Đang Play thì nạp thẳng vào LevelController. Chưa Play thì gửi level qua SessionState rồi vào
        /// Play mode — LevelController.Start() sẽ nhặt lên (xem LevelController.PlayLevelKey).
        /// </summary>
        private void PlayLevel(LevelData lv)
        {
            if (lv == null) return;

            if (Application.isPlaying)
            {
                var lc = Object.FindObjectOfType<LevelController>();
                if (lc == null) { Debug.LogWarning("[Level Tool] Scene không có LevelController."); return; }
                // PlayLevelNow chứ không phải LoadLevel(index): LoadLevel đi qua Resolve() nên Level
                // Override (nếu scene có gán) sẽ thắng, bấm level nào cũng ra đúng cái override đó.
                lc.PlayLevelNow(lv);
                return;
            }

            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(lv));
            if (string.IsNullOrEmpty(guid)) { Debug.LogWarning("[Level Tool] Level chưa lưu thành asset."); return; }
            AssetDatabase.SaveAssets(); // chơi đúng cái vừa sửa, không phải bản cũ còn trên đĩa
            SessionState.SetString(LevelController.PlayLevelKey, guid);
            EditorApplication.EnterPlaymode();
        }

        private void DrawGameSettings()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("GAME SETTINGS (config chung)", EditorStyles.boldLabel);
            var gs = GameSettings.Instance;
            EditorGUILayout.ObjectField("Asset", gs, typeof(GameSettings), false);
            if (gs == null)
            {
                if (GUILayout.Button("Create GameSettings")) CreateGameSettings();
                EditorGUILayout.EndVertical();
                return;
            }
            if (_gsSO == null || _gsSO.targetObject != gs) _gsSO = new SerializedObject(gs);
            _gsSO.Update();
            foreach (var name in new[] { "CoreType", "SlotGunSpacing", "MaxGunOnPath", "GunSpeed", "GunSpacing",
                "FireInterval", "FireMode", "BurstSpawnStacked", "BurstRowLead", "GunFireRange",
                "GunFireAngle", "FrontStationDistance", "BulletSpeed", "BlockStackSpacing",
                "BlockCollapseDuration" })
                EditorGUILayout.PropertyField(_gsSO.FindProperty(name));
            _gsSO.ApplyModifiedProperties();
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Center panel

        private void DrawCenterPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var rect = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.14f));
            if (_target != null) DrawSchematicView(rect);
            else GUI.Label(rect, "Chọn 1 level ở panel trái", EditorStyles.centeredGreyMiniLabel);
            GUI.Label(new Rect(rect.x + 6, rect.yMax - 18, 300, 16),
                $"zoom {(_zoom * 100f):0}%  (scroll: zoom, chuột giữa/Alt+kéo: pan)", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawSchematicView(Rect area)
        {
            Camera cam = _previewCamera;
            _camMode = cam != null;

            float aspW = _target.ScreenAspect.x, aspH = _target.ScreenAspect.y;
            Rect content = area;
            if (aspW > 0f && aspH > 0f)
            {
                float ta = aspW / aspH;
                if (area.width / area.height > ta) { float w = area.height * ta; content = new Rect(area.center.x - w / 2f, area.y, w, area.height); }
                else { float h = area.width / ta; content = new Rect(area.x, area.center.y - h / 2f, area.width, h); }
            }
            _viewContent = content;

            // Flat bounds (khi chưa gán camera).
            Vector2 min = new Vector2(-5, -5), max = new Vector2(5, 5);
            if (!_camMode)
            {
                if (_target.CameraOrthoSize > 0f)
                {
                    float hH = _target.CameraOrthoSize, hW = aspH > 0f ? hH * aspW / aspH : hH;
                    Vector2 cc = new Vector2(_target.CameraCenter.x, _target.CameraCenter.z);
                    min = cc + new Vector2(-hW, -hH); max = cc + new Vector2(hW, hH);
                }
                else
                {
                    var pts = new List<Vector3>();
                    if (_target.PathWaypoints != null) pts.AddRange(_target.PathWaypoints);
                    CollectGridPoints(pts); CollectSlotPoints(pts);
                    if (pts.Count > 0) { min = max = new Vector2(pts[0].x, pts[0].z); foreach (var p in pts) { var q = new Vector2(p.x, p.z); min = Vector2.Min(min, q); max = Vector2.Max(max, q); } }
                }
            }
            float spanX = Mathf.Max(0.001f, max.x - min.x), spanY = Mathf.Max(0.001f, max.y - min.y);
            float flatScale = Mathf.Min((content.width - 2 * Pad) / spanX, (content.height - 2 * Pad) / spanY);
            if (flatScale <= 0 || float.IsInfinity(flatScale)) flatScale = 1f;
            _viewMin = min; _viewScale = flatScale;

            HandleZoomPan(area, content.center);
            // Ép aspect = ScreenAspect (resolution gate) — physical cam + gate-fit tự dựng đúng khung
            // như game render trên thiết bị. KHÔNG tắt usePhysicalProperties để giữ WYSIWYG.
            if (_camMode && aspW > 0f && aspH > 0f) cam.aspect = aspW / aspH;

            Vector2 C = content.center;
            Vector2 ViewT(Vector2 p) => C + (p - C) * _zoom + _pan;
            Vector2 ProjBase(Vector3 w)
            {
                if (_camMode) { Vector3 vp = cam.WorldToViewportPoint(w); return new Vector2(content.x + vp.x * content.width, content.yMax - vp.y * content.height); }
                return new Vector2(content.x + Pad + (w.x - min.x) * flatScale, content.yMax - Pad - (w.z - min.y) * flatScale);
            }
            Vector2 Proj(Vector3 w) => ViewT(ProjBase(w));
            bool Front(Vector3 w) => !_camMode || cam.WorldToViewportPoint(w).z > 0f;
            float PixSize(Vector3 w, float world) => Mathf.Clamp((Proj(w + Vector3.right * world) - Proj(w)).magnitude, 3f, 160f);
            void Line(Vector2 a, Vector2 b) { if (ClipSegment(ref a, ref b, area)) Handles.DrawLine(a, b); }
            void FillRect(Rect rc, Color col) { var ir = RectIntersect(rc, area); if (ir.width > 0 && ir.height > 0) EditorGUI.DrawRect(ir, col); }
            void ArrowHead(Vector2 from, Vector2 to)
            {
                Vector2 d = to - from; float m = d.magnitude; if (m < 1e-3f) return; d /= m;
                Vector2 n = new Vector2(-d.y, d.x); float h = Mathf.Min(7f, m * 0.5f);
                Line(to, to - d * h + n * h * 0.55f);
                Line(to, to - d * h - n * h * 0.55f);
            }

            // Khung camera = 4 góc frustum cắt y=0 (đúng vùng camera nhìn trên sàn).
            if (_showFrame) DrawCameraFrame(cam, aspW, aspH, Proj, Front, Line);

            // Đường bo góc dùng CHUNG với runtime; dựng 1 lần rồi cả path lẫn vòng range đều xài.
            _pathSamples = _target.PathWaypoints != null && _target.PathWaypoints.Count >= 2
                ? RoundedPolylinePath.BuildSamples(_target.PathWaypoints, _target.IsClosed,
                                                   _target.CornerRadius, 8, _target.PathStyle)
                : null;

            // Path: vẽ ĐÚNG đường bo góc, kèm 2 mép theo PathWidth để thấy mặt đường rộng bao nhiêu.
            if (_showPath)
            {
                var samples = _pathSamples;
                if (samples != null && samples.Length >= 2)
                {
                    float half = Mathf.Max(0f, _target.PathWidth) * 0.5f;
                    for (int i = 1; i < samples.Length; i++)
                    {
                        Vector3 p0 = samples[i - 1], p1 = samples[i];
                        if (!Front(p0) || !Front(p1)) continue;

                        Handles.color = new Color(0.3f, 0.8f, 1f, 0.55f);
                        Line(Proj(p0), Proj(p1)); // tim đường

                        if (half <= 0f) continue;
                        Vector3 dir = p1 - p0; dir.y = 0f;
                        if (dir.sqrMagnitude < 1e-8f) continue;
                        Vector3 side = Vector3.Cross(Vector3.up, dir.normalized) * half;
                        Handles.color = new Color(0.3f, 0.8f, 1f, 0.95f);
                        Line(Proj(p0 + side), Proj(p1 + side)); // mép trái
                        Line(Proj(p0 - side), Proj(p1 - side)); // mép phải
                    }
                }
            }

            // HAI quạt CHỌN TARGET của gun tại ĐIỂM VÀO path — mọi gun xuất phát từ đây rồi chạy dọc path.
            // Quạt chỉ lọc lúc CHỌN: chốt được cell rồi thì nòng bắn dứt điểm hết stack kể cả khi cell đã
            // trôi ra ngoài quạt, nên tầm bắn THỰC TẾ rộng hơn hình vẽ này.
            // Gun KHÔNG quay mặt về target: thân luôn hướng theo đường ray. Mỗi nòng quét TỪ hướng trước
            // mặt rồi toả sang sườn của nó đúng GunFireAngle độ — 2 quạt chung mép ở trục trước mặt, nòng
            // phải (R) toả sang phải, nòng trái (L) sang trái. Mỗi nòng có target riêng và không bắn trùng
            // cell với nòng kia (xem Gun.TickBarrel). GunFireAngle = 180 thì 2 quạt phủ kín vòng tròn.
            if (_showRange && _pathSamples != null && _pathSamples.Length >= 2)
            {
                var gs = GameSettings.Instance;
                float rng = gs != null ? gs.GunFireRange : 3f;
                float spread = Mathf.Clamp(gs != null ? gs.GunFireAngle : 360f, 0f, 180f);
                float front = gs != null ? gs.FrontStationDistance : 0f;
                Vector3 ctr = PathPointAt(front);
                Vector2 c = Proj(ctr);

                // Hướng path tại điểm vào: look-ahead 0.05 y hệt RoundedPolylineFollower.
                Vector3 fwd = PathPointAt(front + 0.05f) - ctr; fwd.y = 0f;
                if (fwd.sqrMagnitude > 1e-6f) fwd.Normalize(); else fwd = Vector3.forward;

                var sideLbl = new GUIStyle(EditorStyles.miniBoldLabel);
                sideLbl.normal.textColor = new Color(1f, 0.9f, 0.4f);
                const int SEG = 18;
                for (int s = 0; s < 2; s++)
                {
                    float sign = s == 0 ? 1f : -1f; // +1 = nòng phải, −1 = nòng trái
                    Vector3 D(int k) => Quaternion.AngleAxis(spread * sign * k / SEG, Vector3.up) * fwd;

                    Handles.color = new Color(1f, 0.85f, 0.2f, 0.5f);
                    Vector2 prev = Proj(ctr + D(0) * rng);
                    for (int k = 1; k <= SEG; k++) { Vector2 p = Proj(ctr + D(k) * rng); Line(prev, p); prev = p; }
                    Line(c, Proj(ctr + D(SEG) * rng)); // mép NGOÀI; mép trong trùng trục trước mặt, vẽ dưới

                    Handles.color = new Color(1f, 0.9f, 0.4f, 0.6f);
                    Vector2 outTip = Proj(ctr + D(SEG) * rng);
                    if (area.Contains(outTip))
                        GUI.Label(new Rect(outTip.x + 4f, outTip.y - 8f, 14f, 14f), s == 0 ? "R" : "L", sideLbl);
                }

                // Trục TRƯỚC MẶT = mép chung của 2 quạt, cũng là hướng gun chạy.
                Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
                Vector2 fTip = Proj(ctr + fwd * rng);
                Line(c, fTip); ArrowHead(c, fTip);

                FillRect(new Rect(c.x - 3, c.y - 3, 6, 6), Color.yellow);
            }

            if (_showPath) HandleWaypointDrag(Proj, area);

            // Grids (fan vòng cung).
            if (_showBlocks && _target.Grids != null)
            {
                var blockLbl = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
                blockLbl.normal.textColor = Color.white;

                // Thu vùng click MỌI frame: brush tô màu chạy trên MouseDrag, không chỉ MouseDown/Up.
                _hitCells.Clear();
                _hitQueue.Clear();

                for (int gi = 0; gi < _target.Grids.Count; gi++)
                {
                    var grid = _target.Grids[gi];
                    if (grid == null) continue;
                    int last = Mathf.Max(0, grid.Rows - 1);

                    // Viền: Rect = 4 cạnh; Arc = 2 cạnh bên. Spline bỏ qua — dải uốn lượn không có "cạnh"
                    // nối thẳng được, vẽ vào chỉ thành đường chém ngang qua grid; đã có đường tím thay thế.
                    Handles.color = new Color(0.7f, 0.7f, 0.7f);
                    if (grid.Shape == BlockGridShape.Rect)
                    {
                        Vector3 p0 = grid.CellPos(0, 0), p1 = grid.CellPos(0, grid.ElementsInRow(0) - 1);
                        Vector3 p2 = grid.CellPos(last, grid.ElementsInRow(last) - 1), p3 = grid.CellPos(last, 0);
                        Line(Proj(p0), Proj(p1)); Line(Proj(p1), Proj(p2)); Line(Proj(p2), Proj(p3)); Line(Proj(p3), Proj(p0));
                    }
                    else if (grid.Shape == BlockGridShape.Arc)
                    {
                        Line(Proj(grid.CellPos(0, 0)), Proj(grid.CellPos(last, grid.ElementsInRow(last) - 1)));
                        Line(Proj(grid.CellPos(0, grid.ElementsInRow(0) - 1)), Proj(grid.CellPos(last, 0)));
                    }

                    for (int r = 0; r < grid.Rows; r++)
                    {
                        int count = grid.ElementsInRow(r);
                        for (int e = 0; e < count; e++)
                        {
                            var cell = grid.GetCell(r, e);
                            if (cell == null) continue;
                            bool empty = cell.BlockStackCt <= 0;
                            // Ô đã XOÁ (stack 0): chỉ hiện khi đang tô màu, để click phục hồi + đổi màu.
                            if (empty && _paintColor == TypeColor.None) continue;
                            Vector3 wp = grid.CellPos(r, e);
                            if (!Front(wp)) continue;
                            // Kích thước vẽ = kích thước THẬT của block (prefab 1x1 × CellScale của grid).
                            Vector2 bp = Proj(wp);
                            float sz = PixSize(wp, Mathf.Max(0.05f, grid.CellScale.x));
                            var cellRect = new Rect(bp.x - sz / 2, bp.y - sz / 2, sz, sz);
                            int flatIdx = grid.CellIndex(r, e);
                            _hitCells.Add((cellRect, gi, flatIdx));
                            if (empty)
                            {
                                // Ghost ô trống: fill rất mờ + viền, click (tô màu) sẽ phục hồi stack.
                                FillRect(cellRect, new Color(1f, 1f, 1f, 0.06f));
                                DrawOutline(cellRect,
                                    IsCellSelected(gi, flatIdx) ? Color.yellow : new Color(1f, 1f, 1f, 0.4f), area);
                                continue;
                            }
                            bool isSpawner = cell.Type == BlockCellType.Spawner;
                            FillRect(cellRect, GlobalConfigManager.ColorOf(cell.Color));
                            // Viền vàng = cell đang chọn; viền cam = cell Spawner (còn hàng đợi phía sau).
                            if (IsCellSelected(gi, flatIdx)) DrawOutline(cellRect, Color.yellow, area);
                            else if (isSpawner) DrawOutline(cellRect, SpawnerCol, area);
                            // Số block trong stack (giống nhãn số đạn của gun).
                            if (sz >= 10f && area.Contains(bp))
                                GUI.Label(new Rect(bp.x - sz / 2, bp.y - sz / 2, sz, sz), cell.BlockStackCt.ToString(), blockLbl);

                            // Hướng (rotate) của cell: mũi tên; kéo đầu mũi tên để xoay.
                            // Spawner: mũi tên CAM + dài hơn = hướng nhả cell ra, phân biệt ngay với cell thường.
                            // Rect/Spline: hướng do hình dạng grid quyết định → vẽ theo hướng TÍNH TỪ GRID
                            // (không đọc data) để xoay grid / uốn đường là mũi tên theo ngay, khỏi phải
                            // Generate Cells lại; và không cho kéo xoay từng cell. Arc thì đọc data.
                            if (_showDir)
                            {
                                bool autoDir = grid.CellAngleFromShape;
                                Vector3 dirV = autoDir
                                    ? Quaternion.Euler(0f, grid.DefaultCellAngle(r, e), 0f) * Vector3.forward
                                    : cell.DirectionVector;
                                Vector3 tipW = wp + dirV * (isSpawner ? 0.95f : 0.55f);
                                if (Front(tipW))
                                {
                                    Vector2 tip = Proj(tipW);
                                    Handles.color = isSpawner ? SpawnerCol : new Color(1f, 1f, 1f, 0.9f);
                                    Line(bp, tip); ArrowHead(bp, tip);
                                    if (!autoDir) CellRotateHandle(gi, grid.CellIndex(r, e), wp, tip, area);
                                }
                            }
                        }
                    }

                    // Hàng đợi Spawner vẽ SAU toàn bộ cell thật: ghost xếp lùi về phía hàng sâu hơn, vẽ
                    // xen trong vòng trên thì bị cell hàng sau đè lên — mà click lại ưu tiên ghost, thành
                    // ra nhìn một đằng bấm một nẻo. Tắt Queue = không vẽ → _hitQueue rỗng → ghost cũng
                    // hết ăn click, không còn cướp chuột của cell thật nằm dưới.
                    if (_showQueue)
                        for (int r = 0; r < grid.Rows; r++)
                        {
                            int count = grid.ElementsInRow(r);
                            for (int e = 0; e < count; e++)
                            {
                                var cell = grid.GetCell(r, e);
                                if (cell == null || cell.BlockStackCt <= 0 || cell.Type != BlockCellType.Spawner) continue;
                                Vector3 wp = grid.CellPos(r, e);
                                if (!Front(wp)) continue;
                                DrawQueueGhosts(gi, grid, r, e, count, wp, grid.CellIndex(r, e), cell, area,
                                                Proj, Front, PixSize);
                            }
                        }

                    // Spline: hình dạng do waypoint quyết định → vẽ đường + handle waypoint, và BỎ 2 handle
                    // đầu cạnh (chúng chỉnh BaseRadius/ArcAngle, vô nghĩa ở đây).
                    if (grid.Shape == BlockGridShape.Spline) DrawSplineHandles(gi, grid, area, Proj, Line);

                    // Handle: tâm + 2 đầu cạnh + xoay (kéo được).
                    GridHandle(gi, 0, Proj(grid.Center), area);
                    if (grid.Shape != BlockGridShape.Spline)
                    {
                        GridHandle(gi, 1, Proj(grid.CellPos(0, 0)), area);                             // trái
                        GridHandle(gi, 2, Proj(grid.CellPos(0, grid.ElementsInRow(0) - 1)), area);     // phải
                    }

                    // Handle xoay: nằm trên hướng "sâu dần" của grid, ra ngoài hàng cuối 1 bậc.
                    Vector3 rotW = grid.Center + grid.Forward *
                                   (grid.BaseRadius + Mathf.Max(1, grid.Rows) * grid.RowSpacing);
                    if (Front(rotW))
                    {
                        Handles.color = new Color(0.3f, 1f, 0.4f, 0.9f);
                        Line(Proj(grid.Center), Proj(rotW));
                        GridHandle(gi, 3, Proj(rotW), area);
                    }

                }
                ApplyGridHandleDrag();
                ApplySplineDrag();
                if (_showDir) ApplyCellRotateDrag();
                // Tô màu chạy SAU toàn bộ handle (handle đã e.Use() thì tới đây không còn MouseDown nữa)
                // và sau cả vòng vẽ, vì brush cần _hitCells/_hitQueue của MỌI grid đã gom đủ.
                HandleQueuePaint(area);
                HandleCellPaint(area);
            }

            // Slots.
            if (_showSlots && _target.Slots != null)
            {
                var sceneSlots = GetSceneSlots();
                if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp) _hitGuns.Clear();
                float spacing = GameSettings.Instance != null ? GameSettings.Instance.SlotGunSpacing : 1f;
                var lbl = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
                lbl.normal.textColor = Color.white;
                for (int si = 0; si < sceneSlots.Count && si < _target.Slots.Count; si++)
                {
                    var slot = sceneSlots[si]; var guns = _target.Slots[si]?.Guns;
                    if (slot == null || guns == null) continue;
                    Vector3 basePos = slot.transform.position;
                    for (int i = 0; i < guns.Count; i++)
                    {
                        var g = guns[i]; if (g == null) continue;
                        Vector3 wp = basePos - Vector3.forward * spacing * i; // index 0 phía trước (+Z)
                        if (!Front(wp)) continue;
                        Vector2 gp = Proj(wp); float sz = PixSize(wp, 0.6f);
                        var gunRect = new Rect(gp.x - sz / 2, gp.y - sz / 2, sz, sz);
                        FillRect(gunRect, GlobalConfigManager.ColorOf(g.Color));
                        if (area.Contains(gp)) GUI.Label(gunRect, g.CountBullet.ToString(), lbl);
                        if (_selSlot == si && _selGun == i) DrawOutline(gunRect, Color.yellow, area);
                        // Vùng click gun — chọn xử lý ở HandleMarquee (click ngắn = chọn 1 gun).
                        if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp)
                            _hitGuns.Add((gunRect, si, i));
                    }
                }
            }

            // Cuối cùng: quét chọn / click chọn. Đặt sau mọi handle nên nếu handle đã e.Use() thì bỏ qua.
            HandleMarquee(area);
            HandleDeleteKey(area);

            if (_camMode) cam.ResetAspect();
        }

        // Paint = None: kéo chuột = QUÉT chọn nhiều cell; click ngắn = chọn 1 cell/gun.
        // Giữ Ctrl (hoặc Cmd) = chọn THÊM (click lại vào cell đã chọn thì bỏ chọn).
        private void HandleMarquee(Rect area)
        {
            if (_paintColor != TypeColor.None) return; // đang tô màu → không quét chọn
            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button != 0 || e.alt || !area.Contains(e.mousePosition)) break;
                    _marqueeStart = _marqueeEnd = e.mousePosition;
                    _marqueeOn = true;
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (!_marqueeOn) break;
                    _marqueeEnd = e.mousePosition;
                    e.Use(); Repaint();
                    break;

                case EventType.MouseUp:
                    if (!_marqueeOn) break;
                    _marqueeOn = false;
                    _marqueeEnd = e.mousePosition;
                    bool additive = e.control || e.command;
                    var r = MarqueeRect();
                    if (r.width < 3f && r.height < 3f) ClickSelect(e.mousePosition, additive);
                    else RectSelect(r, additive);
                    e.Use(); Repaint();
                    break;

                case EventType.Repaint:
                    if (!_marqueeOn) break;
                    var mr = MarqueeRect();
                    EditorGUI.DrawRect(RectIntersect(mr, area), new Color(0.3f, 0.7f, 1f, 0.15f));
                    DrawOutline(mr, new Color(0.4f, 0.8f, 1f, 0.9f), area);
                    break;
            }
        }

        private Rect MarqueeRect()
        {
            float x = Mathf.Min(_marqueeStart.x, _marqueeEnd.x), y = Mathf.Min(_marqueeStart.y, _marqueeEnd.y);
            return new Rect(x, y, Mathf.Abs(_marqueeEnd.x - _marqueeStart.x), Mathf.Abs(_marqueeEnd.y - _marqueeStart.y));
        }

        private void ClickSelect(Vector2 pos, bool additive)
        {
            // Ghost hàng đợi xét TRƯỚC cell thật: nó vẽ đè lên vùng của các hàng sâu hơn, không ưu tiên
            // thì click trúng ghost lại chọn nhầm cell nằm dưới. Ô "+" (qi >= Queue.Count) không chọn được.
            foreach (var h in _hitQueue)
                if (h.rect.Contains(pos) && h.qi < QueueSize(h.grid, h.flat))
                { SelectQueue(h.grid, h.flat, h.qi); return; }
            foreach (var h in _hitCells)
                if (h.rect.Contains(pos)) { ToggleCell(h.grid, h.flat, additive); return; }
            foreach (var h in _hitGuns)
                if (h.rect.Contains(pos)) { SelectGun(h.slot, h.gun); return; }
            // click ra chỗ trống = bỏ chọn
            if (!additive) { _selCells.Clear(); _selSlot = -1; _selGun = -1; ClearQueueSel(); }
        }

        private int QueueSize(int gi, int flat)
        {
            if (_target?.Grids == null || gi < 0 || gi >= _target.Grids.Count) return 0;
            var cells = _target.Grids[gi]?.Cells;
            if (cells == null || flat < 0 || flat >= cells.Count) return 0;
            return cells[flat]?.Queue?.Count ?? 0;
        }

        /// <summary>
        /// Phím Delete/Backspace: xoá thứ đang chọn. Cell trong hàng đợi Spawner → rút khỏi Queue. Cell
        /// trên grid → đặt stack = 0, tức thành LỖ: runtime không dựng cell VÀ không cho cell nào dồn vào
        /// (xem GridBlockManager.CanEnter) — grid giữ nguyên hình dạng, chỉ thủng đúng ô đó.
        /// </summary>
        private void HandleDeleteKey(Rect area)
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if (e.keyCode != KeyCode.Delete && e.keyCode != KeyCode.Backspace) return;
            if (!area.Contains(e.mousePosition)) return; // chỉ ăn khi chuột đang ở khung giữa

            var grids = _so.FindProperty("Grids");
            if (_selQueue.qi >= 0)
            {
                var q = QueueProp(grids, _selQueue.grid, _selQueue.cell);
                if (q != null && _selQueue.qi < q.arraySize) q.DeleteArrayElementAtIndex(_selQueue.qi);
                ClearQueueSel();
                e.Use(); Repaint();
                return;
            }

            if (_selCells.Count == 0) return;
            foreach (var (gi, ci) in _selCells)
            {
                if (gi < 0 || gi >= grids.arraySize) continue;
                var cells = grids.GetArrayElementAtIndex(gi).FindPropertyRelative("Cells");
                if (ci < 0 || ci >= cells.arraySize) continue;
                cells.GetArrayElementAtIndex(ci).FindPropertyRelative("BlockStackCt").intValue = 0;
            }
            _selCells.Clear();
            e.Use(); Repaint();
        }

        private static SerializedProperty QueueProp(SerializedProperty grids, int gi, int flat)
        {
            if (gi < 0 || gi >= grids.arraySize) return null;
            var cells = grids.GetArrayElementAtIndex(gi).FindPropertyRelative("Cells");
            if (flat < 0 || flat >= cells.arraySize) return null;
            return cells.GetArrayElementAtIndex(flat).FindPropertyRelative("Queue");
        }

        // Quét: mọi cell có ô GIAO với vùng quét đều được chọn.
        private void RectSelect(Rect r, bool additive)
        {
            if (!additive) _selCells.Clear();
            _selSlot = -1; _selGun = -1;
            foreach (var h in _hitCells)
            {
                var ir = RectIntersect(h.rect, r);
                if (ir.width <= 0f || ir.height <= 0f) continue;
                if (!_selCells.Contains((h.grid, h.flat))) _selCells.Add((h.grid, h.flat));
            }
        }

        // Vẽ khung camera bằng 4 góc viewport chiếu ray xuống y=0.
        private void DrawCameraFrame(Camera cam, float aspW, float aspH, System.Func<Vector3, Vector2> Proj,
            System.Func<Vector3, bool> Front, System.Action<Vector2, Vector2> Line)
        {
            Handles.color = new Color(1f, 0.55f, 0.1f);
            if (cam != null)
            {
                var plane = new Plane(Vector3.up, Vector3.zero);
                Vector2[] vps = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                Vector3[] g = new Vector3[4];
                bool[] ok = new bool[4];
                for (int i = 0; i < 4; i++)
                {
                    Ray ray = cam.ViewportPointToRay(vps[i]);
                    if (plane.Raycast(ray, out float e)) { g[i] = ray.GetPoint(e); ok[i] = true; }
                }
                for (int i = 0; i < 4; i++)
                {
                    int j = (i + 1) % 4;
                    if (ok[i] && ok[j] && Front(g[i]) && Front(g[j])) Line(Proj(g[i]), Proj(g[j]));
                }
            }
            else if (_target.CameraOrthoSize > 0f)
            {
                float hH = _target.CameraOrthoSize, hW = aspH > 0f ? hH * aspW / aspH : hH;
                Vector3 c = _target.CameraCenter;
                Vector3 a = c + new Vector3(-hW, 0, -hH), b = c + new Vector3(hW, 0, -hH), d = c + new Vector3(hW, 0, hH), e2 = c + new Vector3(-hW, 0, hH);
                Line(Proj(a), Proj(b)); Line(Proj(b), Proj(d)); Line(Proj(d), Proj(e2)); Line(Proj(e2), Proj(a));
            }
        }

        private void GridHandle(int gi, int hid, Vector2 p, Rect area)
        {
            if (!area.Contains(p)) return;
            float hr = hid == 0 ? 7f : 6f;
            var hrect = new Rect(p.x - hr, p.y - hr, hr * 2, hr * 2);
            bool active = _dragGrid == gi && _dragHandle == hid;
            Color col = hid == 0 ? new Color(1f, 0.5f, 0.9f, 0.9f)          // tâm: hồng
                      : hid == 3 ? new Color(0.3f, 1f, 0.4f, 0.9f)          // xoay: xanh lá
                                 : new Color(0.3f, 0.9f, 1f, 0.9f);         // đầu cạnh: xanh dương
            EditorGUI.DrawRect(hrect, active ? Color.yellow : col);
            EditorGUIUtility.AddCursorRect(hrect, hid == 3 ? MouseCursor.RotateArrow : MouseCursor.MoveArrow);
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && hrect.Contains(e.mousePosition)) { _dragGrid = gi; _dragHandle = hid; e.Use(); }
        }

        // Kéo tâm (0) hoặc 2 đầu cạnh (1/2): đầu cạnh set BaseRadius (khoảng cách) + ArcAngle (góc).
        private void ApplyGridHandleDrag()
        {
            if (_dragGrid < 0 || _so == null) return;
            var grids = _so.FindProperty("Grids");
            if (_dragGrid >= grids.arraySize) { _dragGrid = -1; return; }
            var e = Event.current;
            if (e.type == EventType.MouseDrag)
            {
                Vector3 nw = InverseV(e.mousePosition);
                var g = grids.GetArrayElementAtIndex(_dragGrid);
                if (_dragHandle == 0)
                {
                    var cp = g.FindPropertyRelative("Center");
                    Vector3 old = cp.vector3Value;
                    cp.vector3Value = new Vector3(nw.x, old.y, nw.z);
                }
                else if (_dragHandle == 3)
                {
                    // Handle xoay: hướng từ tâm tới chuột = hướng "sâu dần" (local +Z) của grid.
                    Vector3 center = g.FindPropertyRelative("Center").vector3Value;
                    Vector3 v = nw - center; v.y = 0f;
                    if (v.sqrMagnitude > 1e-6f)
                        g.FindPropertyRelative("Rotation").floatValue =
                            Mathf.Repeat(Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg, 360f);
                }
                else
                {
                    Vector3 center = g.FindPropertyRelative("Center").vector3Value;
                    Vector3 v = nw - center; v.y = 0f;
                    // Quy về hệ LOCAL của grid để công thức dưới đúng cả khi grid đã xoay.
                    v = Quaternion.Euler(0f, -g.FindPropertyRelative("Rotation").floatValue, 0f) * v;
                    if (g.FindPropertyRelative("Shape").enumValueIndex == (int)BlockGridShape.Rect)
                    {
                        // Rect: đầu cạnh đặt khoảng cách tới hàng 0 (trục Z) + số cột (trục X).
                        g.FindPropertyRelative("BaseRadius").floatValue = Mathf.Max(0.1f, Mathf.Abs(v.z));
                        float step = Mathf.Max(0.01f, g.FindPropertyRelative("BlockWidth").floatValue
                                                    + g.FindPropertyRelative("Spacing").floatValue);
                        g.FindPropertyRelative("Columns").intValue =
                            Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(v.x) * 2f / step) + 1, 1, 64);
                    }
                    else
                    {
                        g.FindPropertyRelative("BaseRadius").floatValue = Mathf.Max(0.1f, v.magnitude);
                        float angle = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
                        // Trần 360 (không phải 350): 360 = VÒNG KÍN, kéo tới đó mới ra được level kiểu ring.
                        g.FindPropertyRelative("ArcAngle").floatValue = Mathf.Clamp(Mathf.Abs(angle) * 2f, 1f, 360f);
                    }
                }
                e.Use(); Repaint();
            }
            else if (e.type == EventType.MouseUp) { _dragGrid = -1; _dragHandle = -1; e.Use(); }
        }

        private static readonly Color SpawnerCol = new Color(1f, 0.6f, 0.1f, 1f);

        /// <summary>
        /// Vẽ hàng đợi cell PHÍA SAU của 1 cell Spawner. Hướng "phía sau" lấy từ chính grid
        /// (CellPosAt(row+1) − CellPosAt(row)) nên đúng cả Arc lẫn Rect, và khớp chỗ runtime nhả cell ra
        /// (xem GridBlockManager.FeedSources). Ghost nhỏ + mờ hơn cell thật, bước ngắn hơn 1 hàng để không
        /// đè lên cell của hàng sâu hơn. Ô cuối là "+" (chỉ hiện khi đang chọn màu) để thêm nhanh.
        /// </summary>
        private void DrawQueueGhosts(int gi, BlockGridData grid, int r, int e, int count, Vector3 wp, int flatIdx,
                                     BlockCellData cell, Rect area,
                                     System.Func<Vector3, Vector2> Proj, System.Func<Vector3, bool> Front,
                                     System.Func<Vector3, float, float> PixSize)
        {
            Vector3 back = grid.CellPosAt(r + 1, e, count) - wp; back.y = 0f;
            if (back.sqrMagnitude < 1e-6f) return;
            back.Normalize();

            int qn = cell.Queue != null ? cell.Queue.Count : 0;
            int slots = _paintColor != TypeColor.None ? qn + 1 : qn; // slot dư = ô "+"
            if (slots == 0) return;

            float step = Mathf.Max(0.3f, (grid.BlockWidth + grid.Spacing) * 0.75f);
            for (int qi = 0; qi < slots; qi++)
            {
                Vector3 qw = wp + back * step * (qi + 1);
                if (!Front(qw)) continue;
                Vector2 qp = Proj(qw);
                float qsz = Mathf.Max(4f, PixSize(qw, Mathf.Max(0.05f, grid.CellScale.x)) * 0.6f);
                var qr = new Rect(qp.x - qsz * 0.5f, qp.y - qsz * 0.5f, qsz, qsz);

                if (qi < qn)
                {
                    var q = cell.Queue[qi];
                    var col = GlobalConfigManager.ColorOf(q != null ? q.Color : TypeColor.None);
                    col.a = 0.5f; // mờ = block chưa nhả ra, phân biệt với cell thật
                    var ir = RectIntersect(qr, area);
                    if (ir.width > 0f && ir.height > 0f) EditorGUI.DrawRect(ir, col);
                    DrawOutline(qr, IsQueueSelected(gi, flatIdx, qi) ? Color.yellow : SpawnerCol, area);
                }
                else DrawOutline(qr, new Color(1f, 0.6f, 0.1f, 0.5f), area); // ô "+" rỗng

                _hitQueue.Add((qr, gi, flatIdx, qi));
            }
        }

        // Viền 1px quanh ô (đánh dấu cell đang chọn / cell Spawner).
        private static void DrawOutline(Rect r, Color col, Rect clip)
        {
            void Bar(Rect b) { var ir = RectIntersect(b, clip); if (ir.width > 0 && ir.height > 0) EditorGUI.DrawRect(ir, col); }
            Bar(new Rect(r.x, r.y, r.width, 1f));
            Bar(new Rect(r.x, r.yMax - 1f, r.width, 1f));
            Bar(new Rect(r.x, r.y, 1f, r.height));
            Bar(new Rect(r.xMax - 1f, r.y, 1f, r.height));
        }

        // Đầu mũi tên hướng của 1 cell — kéo để xoay SpawnerDirectionAngleZ.
        private void CellRotateHandle(int gi, int flatIndex, Vector3 centerWorld, Vector2 tip, Rect area)
        {
            if (!area.Contains(tip)) return;
            const float hr = 5f;
            var hrect = new Rect(tip.x - hr, tip.y - hr, hr * 2, hr * 2);
            bool active = _dragCellGrid == gi && _dragCellIndex == flatIndex;
            EditorGUI.DrawRect(hrect, active ? Color.yellow : new Color(1f, 1f, 1f, 0.9f));
            EditorGUIUtility.AddCursorRect(hrect, MouseCursor.RotateArrow);
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && hrect.Contains(e.mousePosition))
            {
                _dragCellGrid = gi; _dragCellIndex = flatIndex; _dragCellCenter = centerWorld; e.Use();
            }
        }

        /// <summary>
        /// Tô màu cell khi Paint Color != None: click 1 ô, hoặc GIỮ chuột rê qua nhiều ô (brush) — khỏi
        /// phải chấm từng cell. Test cả ĐOẠN chuột đi được trong frame (ClipSegment) chứ không chỉ điểm
        /// cuối, nên rê nhanh cũng không nhảy cóc bỏ sót ô. Paint = None thì nhường HandleMarquee chọn ô.
        /// </summary>
        private void HandleCellPaint(Rect area)
        {
            var e = Event.current;
            if (e.type == EventType.MouseUp) _painting = false;
            if (_paintColor == TypeColor.None) return;

            bool start = e.type == EventType.MouseDown && e.button == 0 && !e.alt && area.Contains(e.mousePosition);
            bool cont = e.type == EventType.MouseDrag && _painting;
            if (!start && !cont) return;

            Vector2 from = start ? e.mousePosition : _lastPaintPos;
            Vector2 to = e.mousePosition;
            _lastPaintPos = to;
            if (start) _painting = true;

            var grids = _so.FindProperty("Grids");
            bool hit = false;
            foreach (var h in _hitCells)
            {
                Vector2 a = from, b = to;
                if (!ClipSegment(ref a, ref b, h.rect)) continue;
                if (h.grid < 0 || h.grid >= grids.arraySize) continue;
                var cells = grids.GetArrayElementAtIndex(h.grid).FindPropertyRelative("Cells");
                if (h.flat < 0 || h.flat >= cells.arraySize) continue;
                var cp = cells.GetArrayElementAtIndex(h.flat);
                cp.FindPropertyRelative("Color").enumValueIndex = (int)_paintColor;
                // Tô vào ô đã xoá (stack 0) = phục hồi cell với stack = Hole Capacity.
                var stackP = cp.FindPropertyRelative("BlockStackCt");
                if (stackP.intValue <= 0) stackP.intValue = Mathf.Max(1, _target.HoleCapacity);
                hit = true;
            }
            if (hit || start) { e.Use(); Repaint(); }
        }

        /// <summary>
        /// Ghost hàng đợi của cell Spawner: click trái = tô màu đang chọn, click vào ô "+" ở cuối đuôi =
        /// thêm 1 cell vào hàng đợi, click PHẢI = xoá. Chạy TRƯỚC HandleCellPaint vì ghost nằm đè lên
        /// vùng của hàng sâu hơn — không ưu tiên thì tô trúng cell thật bên dưới.
        /// </summary>
        private void HandleQueuePaint(Rect area)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.alt || !area.Contains(e.mousePosition)) return;
            bool paint = e.button == 0 && _paintColor != TypeColor.None;
            bool del = e.button == 1;
            if (!paint && !del) return;

            foreach (var h in _hitQueue)
            {
                if (!h.rect.Contains(e.mousePosition)) continue;
                var grids = _so.FindProperty("Grids");
                if (h.grid < 0 || h.grid >= grids.arraySize) return;
                var cells = grids.GetArrayElementAtIndex(h.grid).FindPropertyRelative("Cells");
                if (h.flat < 0 || h.flat >= cells.arraySize) return;
                var q = cells.GetArrayElementAtIndex(h.flat).FindPropertyRelative("Queue");

                if (del)
                {
                    if (h.qi < 0 || h.qi >= q.arraySize) return;
                    q.DeleteArrayElementAtIndex(h.qi);
                }
                else if (h.qi >= q.arraySize) // ô "+" cuối đuôi → thêm cell mới
                {
                    var it = q.GetArrayElementAtIndex(AddArray(q));
                    it.FindPropertyRelative("Color").enumValueIndex = (int)_paintColor;
                    it.FindPropertyRelative("BlockStackCt").intValue = Mathf.Max(1, _target.HoleCapacity);
                }
                else q.GetArrayElementAtIndex(h.qi).FindPropertyRelative("Color").enumValueIndex = (int)_paintColor;

                e.Use(); Repaint();
                return;
            }
        }

        private void ApplyCellRotateDrag()
        {
            if (_dragCellGrid < 0 || _so == null) return;
            var grids = _so.FindProperty("Grids");
            if (_dragCellGrid >= grids.arraySize) { _dragCellGrid = -1; return; }
            var e = Event.current;
            if (e.type == EventType.MouseDrag)
            {
                var cells = grids.GetArrayElementAtIndex(_dragCellGrid).FindPropertyRelative("Cells");
                if (_dragCellIndex < 0 || _dragCellIndex >= cells.arraySize) { _dragCellGrid = -1; return; }
                Vector3 v = InverseV(e.mousePosition) - _dragCellCenter; v.y = 0f;
                if (v.sqrMagnitude > 1e-6f)
                {
                    float ang = Mathf.Repeat(Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg, 360f);
                    cells.GetArrayElementAtIndex(_dragCellIndex).FindPropertyRelative("SpawnerDirectionAngleZ").floatValue = ang;
                }
                e.Use(); Repaint();
            }
            else if (e.type == EventType.MouseUp) { _dragCellGrid = -1; _dragCellIndex = -1; e.Use(); }
        }

        /// <summary>
        /// Vẽ đường spline + handle waypoint của 1 Spline grid. Waypoint lưu ở LOCAL nên phải qua
        /// Center+Rotation mới ra world; lúc kéo thì làm ngược lại.
        /// </summary>
        private void DrawSplineHandles(int gi, BlockGridData g, Rect area,
                                       System.Func<Vector3, Vector2> Proj, System.Action<Vector2, Vector2> Line)
        {
            if (g.SplineWaypoints == null || g.SplineWaypoints.Count == 0) return;

            var rot = Quaternion.Euler(0f, g.Rotation, 0f);
            Vector3 ToW(Vector3 local) => g.Center + rot * local;

            // Đường tim (chưa lệch ra hàng nào) — cho thấy grid đang bám cái gì.
            var s = RoundedPolylinePath.BuildSamples(g.SplineWaypoints, g.SplineClosed,
                                                     g.SplineCornerRadius, 8, g.SplineStyle);
            if (s != null && s.Length >= 2)
            {
                Handles.color = new Color(0.8f, 0.4f, 1f, 0.7f);
                for (int i = 1; i < s.Length; i++) Line(Proj(ToW(s[i - 1])), Proj(ToW(s[i])));
            }

            var e = Event.current;
            for (int i = 0; i < g.SplineWaypoints.Count; i++)
            {
                Vector2 p = Proj(ToW(g.SplineWaypoints[i]));
                if (!area.Contains(p)) continue;
                var hr = new Rect(p.x - 6f, p.y - 6f, 12f, 12f);
                bool active = _dragSplineGrid == gi && _dragSplineWp == i;
                EditorGUI.DrawRect(hr, active ? Color.yellow : new Color(0.8f, 0.4f, 1f, 0.95f));
                EditorGUIUtility.AddCursorRect(hr, MouseCursor.MoveArrow);
                if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && hr.Contains(e.mousePosition))
                { _dragSplineGrid = gi; _dragSplineWp = i; e.Use(); }
            }
        }

        private void ApplySplineDrag()
        {
            if (_dragSplineGrid < 0 || _so == null) return;
            var grids = _so.FindProperty("Grids");
            if (_dragSplineGrid >= grids.arraySize) { _dragSplineGrid = -1; return; }

            var e = Event.current;
            if (e.type == EventType.MouseDrag)
            {
                var g = grids.GetArrayElementAtIndex(_dragSplineGrid);
                var wp = g.FindPropertyRelative("SplineWaypoints");
                if (_dragSplineWp < 0 || _dragSplineWp >= wp.arraySize) { _dragSplineGrid = -1; return; }

                // Chuột cho ra toạ độ WORLD → quy ngược về LOCAL của grid để lưu.
                Vector3 world = InverseV(e.mousePosition);
                Vector3 center = g.FindPropertyRelative("Center").vector3Value;
                float rotY = g.FindPropertyRelative("Rotation").floatValue;
                Vector3 local = Quaternion.Euler(0f, -rotY, 0f) * (world - center);

                var prop = wp.GetArrayElementAtIndex(_dragSplineWp);
                prop.vector3Value = new Vector3(local.x, prop.vector3Value.y, local.z);
                e.Use(); Repaint();
            }
            else if (e.type == EventType.MouseUp) { _dragSplineGrid = -1; _dragSplineWp = -1; e.Use(); }
        }

        private void HandleWaypointDrag(System.Func<Vector3, Vector2> V, Rect area)
        {
            var wp = _so?.FindProperty("PathWaypoints");
            if (wp == null) return;
            var e = Event.current;
            const float hr = 6f;
            for (int i = 0; i < wp.arraySize; i++)
            {
                Vector2 p = V(wp.GetArrayElementAtIndex(i).vector3Value);
                if (!area.Contains(p)) continue;
                var hrect = new Rect(p.x - hr, p.y - hr, hr * 2, hr * 2);
                EditorGUI.DrawRect(hrect, _dragWaypoint == i ? Color.yellow : new Color(1, 1, 1, 0.85f));
                EditorGUIUtility.AddCursorRect(hrect, MouseCursor.MoveArrow);
                if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && hrect.Contains(e.mousePosition)) { _dragWaypoint = i; e.Use(); }
            }
            if (_dragWaypoint >= 0 && _dragWaypoint < wp.arraySize)
            {
                if (e.type == EventType.MouseDrag)
                {
                    Vector3 nw = InverseV(e.mousePosition);
                    var prop = wp.GetArrayElementAtIndex(_dragWaypoint);
                    Vector3 old = prop.vector3Value;
                    prop.vector3Value = new Vector3(nw.x, old.y, nw.z);
                    e.Use(); Repaint();
                }
                else if (e.type == EventType.MouseUp) { _dragWaypoint = -1; e.Use(); }
            }
        }

        private void HandleZoomPan(Rect area, Vector2 center)
        {
            var e = Event.current;
            if (e.type == EventType.ScrollWheel && area.Contains(e.mousePosition))
            {
                float old = _zoom;
                _zoom = Mathf.Clamp(_zoom * (1f - e.delta.y * 0.05f), 0.1f, 20f);
                Vector2 m = e.mousePosition, baseM = center + (m - _pan - center) / old;
                _pan = m - center - (baseM - center) * _zoom;
                e.Use(); Repaint();
            }
            else if (e.type == EventType.MouseDown && (e.button == 2 || (e.button == 0 && e.alt)) && area.Contains(e.mousePosition)) { _panning = true; e.Use(); }
            else if (_panning && e.type == EventType.MouseDrag) { _pan += e.delta; e.Use(); Repaint(); }
            else if (_panning && e.type == EventType.MouseUp) { _panning = false; e.Use(); }
        }

        private Vector3 InverseV(Vector2 s)
        {
            Vector2 C = _viewContent.center;
            Vector2 baseS = C + (s - _pan - C) / _zoom;
            if (_camMode && _previewCamera != null)
            {
                float vx = (baseS.x - _viewContent.x) / _viewContent.width;
                float vy = (_viewContent.yMax - baseS.y) / _viewContent.height;
                Ray ray = _previewCamera.ViewportPointToRay(new Vector3(vx, vy, 0f));
                if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float e)) return ray.GetPoint(e);
                return Vector3.zero;
            }
            float wx = _viewMin.x + (baseS.x - _viewContent.x - Pad) / _viewScale;
            float wz = _viewMin.y + (_viewContent.yMax - Pad - baseS.y) / _viewScale;
            return new Vector3(wx, 0f, wz);
        }

        private void CollectGridPoints(List<Vector3> pts)
        {
            if (_target.Grids == null) return;
            foreach (var g in _target.Grids)
            {
                if (g == null) continue;
                int last = Mathf.Max(0, g.Rows - 1);
                pts.Add(g.Center);
                pts.Add(g.CellPos(0, 0));
                pts.Add(g.CellPos(0, g.ElementsInRow(0) - 1));
                pts.Add(g.CellPos(last, 0));
                pts.Add(g.CellPos(last, g.ElementsInRow(last) - 1));
            }
        }

        private void CollectSlotPoints(List<Vector3> pts)
        {
            if (_target.Slots == null) return;
            var sceneSlots = GetSceneSlots();
            float spacing = GameSettings.Instance != null ? GameSettings.Instance.SlotGunSpacing : 1f;
            for (int si = 0; si < sceneSlots.Count && si < _target.Slots.Count; si++)
            {
                var guns = _target.Slots[si]?.Guns;
                if (sceneSlots[si] == null || guns == null) continue;
                Vector3 basePos = sceneSlots[si].transform.position;
                for (int i = 0; i < guns.Count; i++) pts.Add(basePos + Vector3.forward * spacing * i);
            }
        }

        #endregion

        #region Right panel

        private void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(_rightW));
            if (_target == null) { EditorGUILayout.HelpBox("Chưa chọn level.", MessageType.Info); EditorGUILayout.EndVertical(); return; }
            if (_so == null || _so.targetObject != _target) { _so = new SerializedObject(_target); _so.Update(); }

            EditorGUILayout.LabelField("Level: " + _target.name, EditorStyles.boldLabel);
            bool ok0 = _target.ValidateColorBalance(out string rep0);
            EditorGUILayout.HelpBox(rep0, ok0 ? MessageType.Info : MessageType.Warning);

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            DrawSelectionSection();
            EditorGUILayout.Space(4); DrawMetaSection();
            EditorGUILayout.Space(4); DrawPathSection();
            EditorGUILayout.Space(4); DrawPrefabsSection();
            EditorGUILayout.Space(4); DrawSlotsSection();
            EditorGUILayout.Space(4); DrawGridsSection();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // Đã bỏ khỏi UI (vẫn còn trong LevelData, code chiếu vẫn dùng): CAMERA FRAME (Ortho Size / Aspect /
        // Center) — không cần chỉnh tay nữa vì đang WYSIWYG theo Scene Camera.
        // Tạm ẩn: NumberOfColors, MechanicNames, PROPS/OBSTACLES.
        private void DrawMetaSection()
        {
            EditorGUILayout.BeginVertical("box");
            _foldMeta = EditorGUILayout.Foldout(_foldMeta, "META", true, EditorStyles.foldoutHeader);
            if (_foldMeta)
            {
                EditorGUILayout.PropertyField(_so.FindProperty("CurGameDifficulty"));
                EditorGUILayout.PropertyField(_so.FindProperty("HoleCapacity"),
                    new GUIContent("Hole Capacity", "Stack mỗi cell khi Generate Cells."));
            }
            EditorGUILayout.EndVertical();
        }

        // Thông số của gun/cell đang click chọn trong khung giữa (chỉ hiện khi Paint Color = None).
        private void DrawSelectionSection()
        {
            if (_selQueue.qi >= 0) { DrawSelectedQueueCell(); return; }
            if (_selGun >= 0) { DrawSelectedGun(); return; }
            if (_selCells.Count == 1) { DrawSelectedCell(_selCells[0].grid, _selCells[0].cell); return; }
            if (_selCells.Count > 1) { DrawMultiCellEdit(); return; }
            if (_paintColor == TypeColor.None)
                EditorGUILayout.HelpBox("Paint = None → click chọn 1 GUN/CELL (kể cả ô mờ hàng đợi Spawner) · "
                    + "KÉO để quét chọn nhiều cell · giữ CTRL để chọn thêm · DELETE để xoá.", MessageType.None);
        }

        // 1 cell trong hàng đợi Spawner (ô mờ phía sau). Chọn bằng click khi Paint = None.
        private void DrawSelectedQueueCell()
        {
            var grids = _so.FindProperty("Grids");
            var q = QueueProp(grids, _selQueue.grid, _selQueue.cell);
            if (q == null || _selQueue.qi >= q.arraySize) { ClearQueueSel(); return; }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"CELL SAU SPAWNER — Grid {_selQueue.grid} · #{_selQueue.cell} · thứ {_selQueue.qi + 1}",
                EditorStyles.boldLabel);
            if (GUILayout.Button("✕", BtnW)) { ClearQueueSel(); EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); return; }
            EditorGUILayout.EndHorizontal();

            var it = q.GetArrayElementAtIndex(_selQueue.qi);
            EditorGUILayout.PropertyField(it.FindPropertyRelative("Color"), new GUIContent("Màu"));
            EditorGUILayout.PropertyField(it.FindPropertyRelative("BlockStackCt"),
                new GUIContent("Số đạn (stack)", "Số block của cell này khi spawner đẩy nó ra — cũng là số " +
                               "đạn cần để phá nó."));

            EditorGUILayout.HelpBox("Ấn DELETE (chuột đang ở khung giữa) để xoá cell này khỏi hàng đợi.",
                MessageType.None);
            if (GUILayout.Button("Xoá cell này"))
            {
                q.DeleteArrayElementAtIndex(_selQueue.qi);
                ClearQueueSel();
            }
            EditorGUILayout.EndVertical();
        }

        // Đặt giá trị CHUNG cho tất cả cell đang quét chọn.
        private void DrawMultiCellEdit()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"CELLS — đang chọn {_selCells.Count} ô", EditorStyles.boldLabel);
            if (GUILayout.Button("✕", BtnW)) _selCells.Clear();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Bấm Áp dụng để set giá trị chung cho toàn bộ ô đang chọn.", MessageType.None);

            EditorGUILayout.BeginHorizontal();
            _multiColor = (TypeColor)EditorGUILayout.EnumPopup("Màu", _multiColor);
            if (GUILayout.Button("Áp dụng", GUILayout.Width(70))) ApplyToSelected("Color", (int)_multiColor);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _multiType = (BlockCellType)EditorGUILayout.EnumPopup("Type", _multiType);
            if (GUILayout.Button("Áp dụng", GUILayout.Width(70))) ApplyToSelected("Type", (int)_multiType);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _multiStack = Mathf.Max(1, EditorGUILayout.IntField("Stack", _multiStack));
            if (GUILayout.Button("Áp dụng", GUILayout.Width(70))) ApplyToSelected("BlockStackCt", _multiStack);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void ApplyToSelected(string propName, int value)
        {
            var grids = _so.FindProperty("Grids");
            foreach (var (gi, ci) in _selCells)
            {
                if (gi < 0 || gi >= grids.arraySize) continue;
                var cells = grids.GetArrayElementAtIndex(gi).FindPropertyRelative("Cells");
                if (ci < 0 || ci >= cells.arraySize) continue;
                var p = cells.GetArrayElementAtIndex(ci).FindPropertyRelative(propName);
                if (p == null) continue;
                if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = value;
                else p.intValue = value;
            }
        }

        private void DrawSelectedGun()
        {
            var slots = _so.FindProperty("Slots");
            if (_selSlot < 0 || _selSlot >= slots.arraySize) { _selGun = -1; return; }
            var guns = slots.GetArrayElementAtIndex(_selSlot).FindPropertyRelative("Guns");
            if (_selGun >= guns.arraySize) { _selGun = -1; return; }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"GUN — Slot {_selSlot} · #{_selGun}", EditorStyles.boldLabel);
            if (GUILayout.Button("✕", BtnW)) { _selGun = -1; _selSlot = -1; }
            EditorGUILayout.EndHorizontal();

            var g = guns.GetArrayElementAtIndex(_selGun);
            EditorGUILayout.PropertyField(g.FindPropertyRelative("Color"), new GUIContent("Màu"));
            EditorGUILayout.PropertyField(g.FindPropertyRelative("CountBullet"), new GUIContent("Số lượng đạn"));
            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedCell(int gridIdx, int cellIdx)
        {
            var grids = _so.FindProperty("Grids");
            if (gridIdx < 0 || gridIdx >= grids.arraySize) { _selCells.Clear(); return; }
            var cells = grids.GetArrayElementAtIndex(gridIdx).FindPropertyRelative("Cells");
            if (cellIdx < 0 || cellIdx >= cells.arraySize) { _selCells.Clear(); return; }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"CELL — Grid {gridIdx} · #{cellIdx}", EditorStyles.boldLabel);
            if (GUILayout.Button("✕", BtnW)) _selCells.Clear();
            EditorGUILayout.EndHorizontal();

            var c = cells.GetArrayElementAtIndex(cellIdx);
            EditorGUILayout.PropertyField(c.FindPropertyRelative("Color"), new GUIContent("Màu"));
            EditorGUILayout.PropertyField(c.FindPropertyRelative("BlockStackCt"), new GUIContent("Stack"));

            var typeProp = c.FindPropertyRelative("Type");
            EditorGUILayout.PropertyField(typeProp, new GUIContent("Type"));
            if (typeProp.enumValueIndex == (int)BlockCellType.Spawner) DrawCellQueue(c);
            else EditorGUILayout.HelpBox("Normal: phá hết stack là cell biến mất.", MessageType.None);
            EditorGUILayout.EndVertical();
        }

        // Hàng đợi cell PHÍA SAU của 1 cell Spawner: phá hết stack hiện tại → đẩy mục kế ra tại chỗ.
        private void DrawCellQueue(SerializedProperty cell)
        {
            var q = cell.FindPropertyRelative("Queue");
            if (q == null) return;

            int total = 0;
            for (int i = 0; i < q.arraySize; i++)
                total += Mathf.Max(0, q.GetArrayElementAtIndex(i).FindPropertyRelative("BlockStackCt").intValue);
            EditorGUILayout.LabelField($"Cell phía sau — {q.arraySize} mục · ∑{total} block", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox("Spawner: phá hết stack hiện tại → đẩy mục kế ra ĐÚNG vị trí này (đổi màu/stack). Hết hàng đợi mới biến mất. Block ở đây CÓ tính vào cân bằng bullet↔block.", MessageType.None);

            int pend = -1; ListOp op = ListOp.None;
            for (int i = 0; i < q.arraySize; i++)
            {
                var it = q.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField((i + 1) + ".", GUILayout.Width(20));
                EditorGUILayout.PropertyField(it.FindPropertyRelative("Color"), GUIContent.none, GUILayout.MinWidth(70));
                EditorGUILayout.PropertyField(it.FindPropertyRelative("BlockStackCt"), GUIContent.none, GUILayout.Width(50));
                var o = MiniButtons(i, q.arraySize);
                if (o != ListOp.None) { pend = i; op = o; }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Cell sau"))
            {
                var e = q.GetArrayElementAtIndex(AddArray(q));
                e.FindPropertyRelative("Color").enumValueIndex = _paintColor != TypeColor.None ? (int)_paintColor : (int)_genColor;
                e.FindPropertyRelative("BlockStackCt").intValue = Mathf.Max(1, _target.HoleCapacity);
            }
            if (GUILayout.Button("Clear")) q.arraySize = 0;
            EditorGUILayout.EndHorizontal();

            if (pend >= 0) ApplyOp(q, pend, op);
        }

        private void DrawPrefabsSection()
        {
            EditorGUILayout.BeginVertical("box");
            _foldPrefabs = EditorGUILayout.Foldout(_foldPrefabs, "PREFABS", true, EditorStyles.foldoutHeader);
            if (_foldPrefabs)
            {
                EditorGUILayout.PropertyField(_so.FindProperty("GunPrefab"));
                EditorGUILayout.PropertyField(_so.FindProperty("BlockPrefab"));
                EditorGUILayout.PropertyField(_so.FindProperty("BulletPrefab"));
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawPathSection()
        {
            EditorGUILayout.BeginVertical("box");
            _foldPath = EditorGUILayout.Foldout(_foldPath, "PATH SHAPE (riêng level)", true, EditorStyles.foldoutHeader);
            if (!_foldPath) { EditorGUILayout.EndVertical(); return; }

            EditorGUILayout.PropertyField(_so.FindProperty("IsClosed"));
            EditorGUILayout.PropertyField(_so.FindProperty("PathStyle"), new GUIContent("Path Style"));
            // CornerRadius chỉ có tác dụng với RoundedCorner — Bezier bỏ qua nó hoàn toàn.
            using (new EditorGUI.DisabledScope(_target.PathStyle != PathStyle.RoundedCorner))
                EditorGUILayout.PropertyField(_so.FindProperty("CornerRadius"));
            if (!_target.IsClosed)
                EditorGUILayout.HelpBox("Path HỞ → PathManager sinh TunnelIn ở đầu và TunnelOut ở cuối "
                    + "(gán prefab trên PathManager trong scene).", MessageType.None);
            EditorGUILayout.PropertyField(_so.FindProperty("PathWidth"), new GUIContent("Path Width"));

            var wp = _so.FindProperty("PathWaypoints");
            _foldWaypoints = EditorGUILayout.Foldout(_foldWaypoints, $"Waypoints — {wp.arraySize}", true);
            if (_foldWaypoints)
            {
                int pend = -1; ListOp op = ListOp.None;
                for (int i = 0; i < wp.arraySize; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(wp.GetArrayElementAtIndex(i), new GUIContent("WP " + i));
                    var o = MiniButtons(i, wp.arraySize);
                    if (o != ListOp.None) { pend = i; op = o; }
                    EditorGUILayout.EndHorizontal();
                }
                if (GUILayout.Button("+ Waypoint"))
                {
                    int idx = wp.arraySize; wp.arraySize++;
                    wp.GetArrayElementAtIndex(idx).vector3Value = idx > 0 ? wp.GetArrayElementAtIndex(idx - 1).vector3Value + Vector3.right : Vector3.zero;
                }
                if (pend >= 0) ApplyOp(wp, pend, op);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSlotsSection()
        {
            var slots = _so.FindProperty("Slots");
            EditorGUILayout.BeginHorizontal();
            _foldSlots = EditorGUILayout.Foldout(_foldSlots, $"SLOTS — {slots.arraySize}", true, EditorStyles.foldoutHeader);
            if (GUILayout.Button("+ Slot", GUILayout.Width(58))) slots.GetArrayElementAtIndex(AddArray(slots)).FindPropertyRelative("Guns").arraySize = 0;
            EditorGUILayout.EndHorizontal();
            if (!_foldSlots) return;
            EditorGUILayout.HelpBox("Vị trí slot lấy từ GunSlot trên scene (Slot0..4). Slot[i] có gun = slot i active.", MessageType.None);

            int pend = -1; ListOp op = ListOp.None;
            for (int i = 0; i < slots.arraySize; i++)
            {
                var guns = slots.GetArrayElementAtIndex(i).FindPropertyRelative("Guns");
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                bool open = FoldAt(_foldSlot, i, $"Slot {i} — {guns.arraySize} guns");
                var o = MiniButtons(i, slots.arraySize);
                if (o != ListOp.None) { pend = i; op = o; }
                EditorGUILayout.EndHorizontal();
                if (open) DrawGunList(guns);
                EditorGUILayout.EndVertical();
            }
            if (pend >= 0) ApplyOp(slots, pend, op);
        }

        private void DrawGunList(SerializedProperty guns)
        {
            EditorGUILayout.LabelField($"Guns — thứ tự ({guns.arraySize})", EditorStyles.miniBoldLabel);
            int pend = -1; ListOp op = ListOp.None;
            for (int i = 0; i < guns.arraySize; i++)
            {
                var g = guns.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField((i + 1) + ".", GUILayout.Width(20));
                EditorGUILayout.PropertyField(g.FindPropertyRelative("Color"), GUIContent.none, GUILayout.MinWidth(70));
                EditorGUILayout.PropertyField(g.FindPropertyRelative("CountBullet"), GUIContent.none, GUILayout.Width(50));
                var o = MiniButtons(i, guns.arraySize);
                if (o != ListOp.None) { pend = i; op = o; }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ Gun")) guns.GetArrayElementAtIndex(AddArray(guns)).FindPropertyRelative("CountBullet").intValue = 5;
            if (pend >= 0) ApplyOp(guns, pend, op);
        }

        private void DrawGridsSection()
        {
            var grids = _so.FindProperty("Grids");
            EditorGUILayout.BeginHorizontal();
            _foldGrids = EditorGUILayout.Foldout(_foldGrids, $"GRIDS — {grids.arraySize}", true, EditorStyles.foldoutHeader);
            _genShape = (BlockGridShape)EditorGUILayout.EnumPopup(_genShape, GUILayout.Width(60));
            if (GUILayout.Button("+ Grid", GUILayout.Width(58))) AddGrid(grids);
            EditorGUILayout.EndHorizontal();
            if (!_foldGrids) return;
            EditorGUILayout.HelpBox("Chọn loại grid (Arc = vòng cung, Rect = lưới chữ nhật) rồi bấm + Grid.\nKhung giữa: kéo TÂM (hồng) · 2 ĐẦU CẠNH (xanh dương) · XOAY grid (xanh lá). Row 0 = hàng gần path.\nClick ô cell = tô màu đang chọn ở Paint Color · kéo đầu mũi tên = xoay hướng cell.", MessageType.None);

            EditorGUILayout.BeginHorizontal();
            _genColor = (TypeColor)EditorGUILayout.EnumPopup("Gen Color", _genColor);
            _genStack = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Refill Stack", "Stack cho hàng đợi Refill (Generate Cells dùng Hole Capacity)."), _genStack, GUILayout.Width(120)));
            EditorGUILayout.EndHorizontal();

            DrawPaintPalette();

            int pend = -1; ListOp op = ListOp.None;
            for (int i = 0; i < grids.arraySize; i++)
            {
                var grid = grids.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                int rows = grid.FindPropertyRelative("Rows").intValue;
                int gridBlocks = SumStack(grid.FindPropertyRelative("Cells"));
                int pendBlocks = SumStack(grid.FindPropertyRelative("PendingRefill"));
                string blkTxt = pendBlocks > 0 ? $"{gridBlocks}(+{pendBlocks})" : gridBlocks.ToString();
                bool openGrid = FoldAt(_foldGrid, i, $"Grid {i} — {rows} rows · {blkTxt} block");
                var o = MiniButtons(i, grids.arraySize);
                if (o != ListOp.None) { pend = i; op = o; }
                EditorGUILayout.EndHorizontal();
                if (!openGrid) { EditorGUILayout.EndVertical(); continue; }

                var shapeProp = grid.FindPropertyRelative("Shape");
                bool isRect = shapeProp.enumValueIndex == (int)BlockGridShape.Rect;
                bool isSpline = shapeProp.enumValueIndex == (int)BlockGridShape.Spline;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(shapeProp, new GUIContent("Shape"));
                EditorGUILayout.PropertyField(grid.FindPropertyRelative("Side"), new GUIContent("Side",
                    "Left/Right = chỉ nòng cùng bên của gun bắn được grid này. Any = theo quạt gun."));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(grid.FindPropertyRelative("Center"));
                EditorGUILayout.PropertyField(grid.FindPropertyRelative("Rotation"),
                    new GUIContent("Rotation (Y°)", "Xoay cả grid quanh trục Y. Kéo handle XANH LÁ trong khung giữa."));
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(grid.FindPropertyRelative("BaseRadius"),
                    new GUIContent(isRect ? "Dist Row 0" : isSpline ? "Lệch Row 0" : "Base Radius",
                        isSpline ? "Khoảng cách từ đường spline tới hàng 0, theo pháp tuyến." : null));
                EditorGUILayout.PropertyField(grid.FindPropertyRelative("RowSpacing"), new GUIContent("Row Spacing"));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(grid.FindPropertyRelative("Rows"), new GUIContent("Rows"));
                // ArcAngle / Columns / SpiralGrowth vô nghĩa với Spline — hình dạng do waypoint quyết định.
                if (isRect) EditorGUILayout.PropertyField(grid.FindPropertyRelative("Columns"), new GUIContent("Columns"));
                else if (!isSpline) EditorGUILayout.PropertyField(grid.FindPropertyRelative("ArcAngle"), new GUIContent("Arc Angle"));
                EditorGUILayout.EndHorizontal();
                if (!isRect && !isSpline)
                    EditorGUILayout.PropertyField(grid.FindPropertyRelative("SpiralGrowth"), new GUIContent("Spiral Growth (xoắn)"));
                if (isSpline) DrawSplineSection(grid, i);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(grid.FindPropertyRelative("BlockWidth"), new GUIContent("Block W"));
                EditorGUILayout.PropertyField(grid.FindPropertyRelative("Spacing"), new GUIContent("Spacing"));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(grid.FindPropertyRelative("CellScale"),
                    new GUIContent("Cell Scale", "Scale của mọi BLOCK trong grid này."));
                if (!isRect) EditorGUILayout.PropertyField(grid.FindPropertyRelative("Layout"), new GUIContent("Layout"));
                EditorGUILayout.HelpBox(DescribeLayout(i), MessageType.None);

                if (GUILayout.Button($"Generate Cells (Gen Color · stack = Hole Capacity {Mathf.Max(1, _target.HoleCapacity)})")) GenerateGridCells(i);
                EditorGUILayout.PropertyField(grid.FindPropertyRelative("Cells"), new GUIContent($"Cells ({grid.FindPropertyRelative("Cells").arraySize})"), true);

                EditorGUILayout.Space(2);
                DrawRefillQueue(grid, i);
                EditorGUILayout.EndVertical();
            }
            if (pend >= 0) ApplyOp(grids, pend, op);
        }

        #endregion

        #region Grid ops

        // Đường uốn lượn mà Spline grid bám theo. Waypoint là toạ độ LOCAL so với Center + Rotation.
        private void DrawSplineSection(SerializedProperty grid, int gridIndex)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("SPLINE — đường grid bám theo", EditorStyles.miniBoldLabel);

            var styleProp = grid.FindPropertyRelative("SplineStyle");
            EditorGUILayout.PropertyField(grid.FindPropertyRelative("SplineClosed"), new GUIContent("Khép kín"));
            EditorGUILayout.PropertyField(styleProp, new GUIContent("Style"));
            using (new EditorGUI.DisabledScope(styleProp.enumValueIndex != (int)PathStyle.RoundedCorner))
                EditorGUILayout.PropertyField(grid.FindPropertyRelative("SplineCornerRadius"),
                    new GUIContent("Corner Radius"));

            var wp = grid.FindPropertyRelative("SplineWaypoints");
            EditorGUILayout.LabelField($"Waypoints — {wp.arraySize}", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox("Kéo ô VUÔNG TÍM trong khung giữa để chỉnh đường. Cần ít nhất 2 waypoint "
                + "thì grid mới hiện.", MessageType.None);

            int pend = -1; ListOp op = ListOp.None;
            for (int i = 0; i < wp.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(wp.GetArrayElementAtIndex(i), new GUIContent("WP " + i));
                var o = MiniButtons(i, wp.arraySize);
                if (o != ListOp.None) { pend = i; op = o; }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ Waypoint"))
            {
                int idx = AddArray(wp);
                // Nối tiếp waypoint trước cho khỏi đè lên nhau; rỗng thì bắt đầu tại gốc local.
                wp.GetArrayElementAtIndex(idx).vector3Value = idx > 0
                    ? wp.GetArrayElementAtIndex(idx - 1).vector3Value + Vector3.right * 2f
                    : Vector3.zero;
            }
            if (pend >= 0) ApplyOp(wp, pend, op);
            EditorGUILayout.EndVertical();
        }

        private void AddGrid(SerializedProperty grids)
        {
            int idx = grids.arraySize; grids.arraySize++;
            var g = grids.GetArrayElementAtIndex(idx);
            bool spline = _genShape == BlockGridShape.Spline;

            g.FindPropertyRelative("Shape").enumValueIndex = (int)_genShape;
            g.FindPropertyRelative("Center").vector3Value = Vector3.zero;
            g.FindPropertyRelative("Rotation").floatValue = 0f;
            g.FindPropertyRelative("Rows").intValue = 3;
            g.FindPropertyRelative("Columns").intValue = 5;
            g.FindPropertyRelative("ArcAngle").floatValue = 90f;
            g.FindPropertyRelative("SpiralGrowth").floatValue = 0f;
            g.FindPropertyRelative("CellScale").vector3Value = Vector3.one;
            g.FindPropertyRelative("Layout").enumValueIndex = (int)BlockGridLayout.ArcLength;
            g.FindPropertyRelative("Cells").arraySize = 0;

            // Default riêng cho Spline (dải uốn lượn) khác Arc/Rect.
            g.FindPropertyRelative("BaseRadius").floatValue = spline ? 0f : 3f;      // Lệch Row 0
            g.FindPropertyRelative("RowSpacing").floatValue = spline ? 1f : 1.2f;
            g.FindPropertyRelative("BlockWidth").floatValue = spline ? 0f : 0.8f;
            g.FindPropertyRelative("Spacing").floatValue = spline ? 0.8f : 0.2f;

            var scr = g.FindPropertyRelative("SplineCornerRadius");
            if (scr != null) scr.floatValue = 5f;

            // Spline: sinh sẵn 2 waypoint để có 1 đoạn đường ngay.
            var wp = g.FindPropertyRelative("SplineWaypoints");
            if (wp != null)
            {
                wp.arraySize = spline ? 2 : 0;
                if (spline)
                {
                    wp.GetArrayElementAtIndex(0).vector3Value = Vector3.zero;
                    wp.GetArrayElementAtIndex(1).vector3Value = new Vector3(4f, 0f, 0f);
                }
            }
        }

        // Hàng đợi SPAWNER refill: mỗi mục = 1 stack (màu + số block) sẽ được nhả bù ở ring ngoài cùng
        // khi front bị thu hết. "+ Refill Ring" nạp nguyên 1 vòng (= số phần tử hàng ngoài cùng).
        private void DrawRefillQueue(SerializedProperty grid, int gridIndex)
        {
            var pending = grid.FindPropertyRelative("PendingRefill");
            if (pending == null) return;

            int total = 0;
            for (int i = 0; i < pending.arraySize; i++)
                total += Mathf.Max(0, pending.GetArrayElementAtIndex(i).FindPropertyRelative("BlockStackCt").intValue);

            EditorGUILayout.LabelField($"Refill Queue (spawner) — {pending.arraySize} mục · ∑{total} block", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox("Ring front bị thu hết → collapse → spawner nhả 1 ring mới ở NGOÀI CÙNG, lấy lần lượt từ hàng đợi này. Block ở đây CÓ tính vào cân bằng bullet↔block.", MessageType.None);

            int pend = -1; ListOp op = ListOp.None;
            for (int i = 0; i < pending.arraySize; i++)
            {
                var it = pending.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField((i + 1) + ".", GUILayout.Width(20));
                EditorGUILayout.PropertyField(it.FindPropertyRelative("Color"), GUIContent.none, GUILayout.MinWidth(70));
                EditorGUILayout.PropertyField(it.FindPropertyRelative("BlockStackCt"), GUIContent.none, GUILayout.Width(50));
                var o = MiniButtons(i, pending.arraySize);
                if (o != ListOp.None) { pend = i; op = o; }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Refill"))
            {
                var e = pending.GetArrayElementAtIndex(AddArray(pending));
                e.FindPropertyRelative("Color").enumValueIndex = (int)_genColor;
                e.FindPropertyRelative("BlockStackCt").intValue = _genStack;
            }
            if (GUILayout.Button("+ Refill Ring")) AddRefillRing(gridIndex);
            if (GUILayout.Button("Clear")) pending.arraySize = 0;
            EditorGUILayout.EndHorizontal();

            if (pend >= 0) ApplyOp(pending, pend, op);
        }

        // Nạp nguyên 1 vòng vào hàng đợi: số mục = số phần tử hàng NGOÀI CÙNG (theo chiều dài cung).
        private void AddRefillRing(int gridIndex)
        {
            _so.ApplyModifiedProperties(); // sync field vừa sửa để ElementsInRow tính đúng
            if (gridIndex < 0 || gridIndex >= _target.Grids.Count) return;
            var g = _target.Grids[gridIndex];
            int outerRow = Mathf.Max(0, g.Rows - 1);
            int count = Mathf.Max(1, g.ElementsInRow(outerRow));

            _so.Update();
            var pending = _so.FindProperty("Grids").GetArrayElementAtIndex(gridIndex).FindPropertyRelative("PendingRefill");
            for (int k = 0; k < count; k++)
            {
                var e = pending.GetArrayElementAtIndex(AddArray(pending));
                e.FindPropertyRelative("Color").enumValueIndex = (int)_genColor;
                e.FindPropertyRelative("BlockStackCt").intValue = _genStack;
            }
        }

        // Bảng màu tô cell: None (mặc định) = click cell để chọn/xem thông số. Chọn 1 màu → click cell trong
        // khung giữa sẽ tô cell đó. Danh sách màu lấy từ GlobalConfigManager (chỉ hiện màu đã cấu hình).
        private void DrawPaintPalette()
        {
            var cfg = GlobalConfigManager.Instance;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Paint Color", GUILayout.Width(72));
            if (GUILayout.Toggle(_paintColor == TypeColor.None, "None", EditorStyles.miniButton, GUILayout.Width(44)))
                _paintColor = TypeColor.None;

            var bg = GUI.backgroundColor;
            if (cfg != null && cfg.listColor != null)
                foreach (var co in cfg.listColor)
                {
                    if (co == null || co.typeColor == TypeColor.None) continue;
                    bool sel = _paintColor == co.typeColor;
                    GUI.backgroundColor = GlobalConfigManager.ColorOf(co.typeColor);
                    if (GUILayout.Toggle(sel, sel ? "●" : "", EditorStyles.miniButton, GUILayout.Width(24)) && !sel)
                        _paintColor = co.typeColor;
                }
            GUI.backgroundColor = bg;
            EditorGUILayout.EndHorizontal();

            if (cfg == null)
            {
                EditorGUILayout.HelpBox("Không tìm thấy GlobalConfigManager → không có bảng màu.", MessageType.Warning);
                return;
            }
            EditorGUILayout.HelpBox(_paintColor == TypeColor.None
                ? "Paint = None: click cell/gun để XEM & SỬA thông số (không đổi màu) · kéo chuột = quét chọn nhiều cell."
                : $"Đang tô màu {_paintColor} — click, hoặc GIỮ CHUỘT RÊ qua nhiều cell để tô cả vùng. "
                  + "Ô đã XOÁ hiện dạng khung mờ; tô vào đó sẽ PHỤC HỒI cell (stack = Hole Capacity).\n"
                  + (_showQueue
                      ? "Cell Spawner: các ô mờ phía sau = hàng đợi · click ô mờ để tô · click ô \"+\" cuối đuôi "
                        + "để thêm cell · CLICK PHẢI vào ô mờ để xoá. (Tắt nút Queue trên toolbar để ẩn.)"
                      : "Hàng đợi Spawner đang ẩn (nút Queue trên toolbar đang tắt) — bật lại để sửa bằng chuột."),
                MessageType.None);
        }

        // Mô tả số cell từng hàng + kiểu chặn, để thấy ngay cột có thẳng hay không.
        private string DescribeLayout(int index)
        {
            if (index < 0 || index >= _target.Grids.Count) return "";
            var g = _target.Grids[index];
            var sb = new System.Text.StringBuilder();
            sb.Append("Cell/hàng: ");
            for (int r = 0; r < Mathf.Max(1, g.Rows); r++) sb.Append(g.ElementsInRow(r)).Append(r < g.Rows - 1 ? " / " : "");
            if (g.Shape == BlockGridShape.Rect)
                sb.Append("\nRect: lưới chữ nhật, mọi hàng = Columns → cột THẲNG, cell sau bị đúng 1 cell trước chặn.");
            else
                sb.Append(g.Layout == BlockGridLayout.Uniform
                    ? "\nArc/Uniform: mọi hàng bằng nhau → cột THẲNG, cell sau bị đúng 1 cell trước chặn."
                    : "\nArc/ArcLength: hàng ra xa nhiều cell hơn → cột LỆCH, cell giữa bị 2 cell trước chặn.");

            if (g.IsFullRing)
                sb.Append("\n★ VÒNG KÍN (ArcAngle 360): mỗi hàng là 1 vòng tròn quanh Center, Rows = số vòng "
                        + "lồng nhau. Nên để Layout = Uniform — ArcLength cho mỗi vòng số cell khác nhau, "
                        + "map dồn hàng không vòng qua được mối nối.");
            else if (g.Shape == BlockGridShape.Arc && g.SpiralGrowth > 0f)
                sb.Append("\n★ XOẮN ỐC (SpiralGrowth > 0): bán kính lớn dần dọc theo sweep. Để ArcAngle > 360 "
                        + "là cuộn nhiều vòng.");
            return sb.ToString();
        }

        private void GenerateGridCells(int index)
        {
            _so.ApplyModifiedProperties(); // sync các field vừa sửa
            if (index < 0 || index >= _target.Grids.Count) return;
            var grid = _target.Grids[index];
            int stack = Mathf.Max(1, _target.HoleCapacity); // stack mỗi cell = Hole Capacity của level

            var cells = _so.FindProperty("Grids").GetArrayElementAtIndex(index).FindPropertyRelative("Cells");
            cells.arraySize = grid.TotalCells(); // tính theo chiều dài cong
            int i = 0;
            for (int r = 0; r < grid.Rows; r++)
            {
                int count = grid.ElementsInRow(r);
                for (int el = 0; el < count; el++)
                {
                    var c = cells.GetArrayElementAtIndex(i++);
                    c.FindPropertyRelative("Color").enumValueIndex = (int)_genColor;
                    c.FindPropertyRelative("BlockStackCt").intValue = stack;
                    c.FindPropertyRelative("CellScale").vector3Value = Vector3.one;
                    c.FindPropertyRelative("BlockCol").intValue = el;   // index cột trong hàng
                    c.FindPropertyRelative("SpawnerDepth").intValue = r; // 0 = hàng sát path
                    // Hướng dồn: Rect = mọi cell chung 1 hướng của grid; Arc = từng cell về tâm.
                    c.FindPropertyRelative("SpawnerDirectionAngleZ").floatValue = grid.DefaultCellAngle(r, el);
                }
            }
        }

        #endregion

        #region Level list ops

        private void Select(LevelData lv) { _target = lv; _so = null; if (lv != null) EditorGUIUtility.PingObject(lv); Repaint(); }

        private void RefreshLevels()
        {
            _levels.Clear();
            string folder = AssetDatabase.IsValidFolder(_levelsFolder) ? _levelsFolder : "Assets";
            foreach (var guid in AssetDatabase.FindAssets("t:LevelData", new[] { folder }))
            {
                var lv = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guid));
                if (lv != null) _levels.Add(lv);
            }
        }

        private void CreateNewLevel()
        {
            string folder = AssetDatabase.IsValidFolder(_levelsFolder) ? _levelsFolder : "Assets";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/Level{_levels.Count + 1}.asset");
            var asset = CreateInstance<LevelData>();
            asset.GunPrefab = _defaultGunPrefab; asset.BlockPrefab = _defaultBlockPrefab;
            AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssets();
            RefreshLevels(); Select(asset);
        }

        private void DeleteLevel(LevelData lv)
        {
            if (lv == null) return;
            if (!EditorUtility.DisplayDialog("Delete Level", $"Xoá asset '{lv.name}'?", "Delete", "Cancel")) return;
            string path = AssetDatabase.GetAssetPath(lv);
            if (_target == lv) { _target = null; _so = null; }
            AssetDatabase.DeleteAsset(path); RefreshLevels();
        }

        private void AddPreviewToScene()
        {
            var go = new GameObject("LevelPreview_" + _target.name);
            go.AddComponent<LevelPreview>().level = _target;
            Undo.RegisterCreatedObjectUndo(go, "Add Level Preview");
            Selection.activeGameObject = go;
        }

        private void CreateGameSettings()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var asset = CreateInstance<GameSettings>();
            AssetDatabase.CreateAsset(asset, "Assets/Resources/GameSettings.asset"); AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(asset);
        }

        private void CreateSceneSlots()
        {
            var mgr = FindObjectOfType<SlotManager>();
            if (mgr == null) { var go = new GameObject("SlotManager"); Undo.RegisterCreatedObjectUndo(go, "Create SlotManager"); mgr = go.AddComponent<SlotManager>(); }
            var so = new SerializedObject(mgr);
            var arr = so.FindProperty("sceneSlots");
            arr.arraySize = 5;
            for (int i = 0; i < 5; i++)
            {
                var go = new GameObject("Slot" + i);
                Undo.RegisterCreatedObjectUndo(go, "Create Slot");
                go.transform.SetParent(mgr.transform);
                go.transform.position = new Vector3((i - 2) * 1.2f, 0f, -3f);
                var slot = go.AddComponent<GunSlot>(); slot.SlotIndex = i;
                arr.GetArrayElementAtIndex(i).objectReferenceValue = slot;
            }
            so.ApplyModifiedProperties();
            Selection.activeGameObject = mgr.gameObject;
        }

        #endregion

        #region Helpers

        private static int AddArray(SerializedProperty arr) { int idx = arr.arraySize; arr.arraySize++; return idx; }

        // Tổng BlockStackCt của 1 mảng phần tử (Cells hoặc PendingRefill — đều có field BlockStackCt).
        private static int SumStack(SerializedProperty arr)
        {
            if (arr == null) return 0;
            int s = 0;
            for (int i = 0; i < arr.arraySize; i++)
            {
                var p = arr.GetArrayElementAtIndex(i).FindPropertyRelative("BlockStackCt");
                if (p != null) s += Mathf.Max(0, p.intValue);
            }
            return s;
        }

        private ListOp MiniButtons(int index, int count)
        {
            ListOp op = ListOp.None;
            using (new EditorGUI.DisabledScope(index <= 0)) if (GUILayout.Button("↑", BtnW)) op = ListOp.MoveUp;
            using (new EditorGUI.DisabledScope(index >= count - 1)) if (GUILayout.Button("↓", BtnW)) op = ListOp.MoveDown;
            if (GUILayout.Button("⧉", BtnW)) op = ListOp.Duplicate;
            if (GUILayout.Button("✕", BtnW)) op = ListOp.Delete;
            return op;
        }

        private void ApplyOp(SerializedProperty list, int i, ListOp op)
        {
            switch (op)
            {
                case ListOp.MoveUp: list.MoveArrayElement(i, i - 1); break;
                case ListOp.MoveDown: list.MoveArrayElement(i, i + 1); break;
                case ListOp.Duplicate: list.InsertArrayElementAtIndex(i); break;
                case ListOp.Delete:
                    // Mảng OBJECT REFERENCE (vd LevelList.Levels): lần gọi đầu chỉ set phần tử = null,
                    // phải gọi lần 2 mới rút nó ra. Mảng class [Serializable] thì 1 lần là đủ.
                    var el = list.GetArrayElementAtIndex(i);
                    if (el.propertyType == SerializedPropertyType.ObjectReference && el.objectReferenceValue != null)
                        list.DeleteArrayElementAtIndex(i);
                    list.DeleteArrayElementAtIndex(i);
                    break;
            }
        }

        private List<GunSlot> GetSceneSlots()
        {
            var list = new List<GunSlot>(Object.FindObjectsOfType<GunSlot>(true));
            list.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
            return list;
        }

        // Điểm trên path theo arc-length — chạy trên ĐƯỜNG BO GÓC (_pathSamples) đúng như runtime, không
        // phải đoạn thẳng nối waypoint. Wrap khoảng cách bất kể IsClosed, khớp GetPointAtDistance.
        private Vector3 PathPointAt(float dist)
        {
            var s = _pathSamples;
            if (s == null || s.Length == 0) return Vector3.zero;
            if (s.Length == 1) return s[0];

            float total = 0f;
            for (int i = 1; i < s.Length; i++) total += Vector3.Distance(s[i - 1], s[i]);
            if (total < 1e-4f) return s[0];

            dist = Mathf.Repeat(dist, total);
            float acc = 0f;
            for (int i = 1; i < s.Length; i++)
            {
                float len = Vector3.Distance(s[i - 1], s[i]);
                if (acc + len >= dist) return Vector3.Lerp(s[i - 1], s[i], len > 1e-4f ? (dist - acc) / len : 0f);
                acc += len;
            }
            return s[s.Length - 1];
        }

        private static bool ClipSegment(ref Vector2 a, ref Vector2 b, Rect r)
        {
            float xmin = r.xMin, xmax = r.xMax, ymin = r.yMin, ymax = r.yMax;
            int Code(Vector2 p) { int c = 0; if (p.x < xmin) c |= 1; else if (p.x > xmax) c |= 2; if (p.y < ymin) c |= 4; else if (p.y > ymax) c |= 8; return c; }
            int ca = Code(a), cb = Code(b);
            for (int guard = 0; guard < 16; guard++)
            {
                if ((ca | cb) == 0) return true;
                if ((ca & cb) != 0) return false;
                int co = ca != 0 ? ca : cb;
                Vector2 p = Vector2.zero;
                if ((co & 8) != 0) { p.x = a.x + (b.x - a.x) * (ymax - a.y) / (b.y - a.y); p.y = ymax; }
                else if ((co & 4) != 0) { p.x = a.x + (b.x - a.x) * (ymin - a.y) / (b.y - a.y); p.y = ymin; }
                else if ((co & 2) != 0) { p.y = a.y + (b.y - a.y) * (xmax - a.x) / (b.x - a.x); p.x = xmax; }
                else { p.y = a.y + (b.y - a.y) * (xmin - a.x) / (b.x - a.x); p.x = xmin; }
                if (co == ca) { a = p; ca = Code(a); } else { b = p; cb = Code(b); }
            }
            return true;
        }

        private static Rect RectIntersect(Rect a, Rect b)
        {
            float x = Mathf.Max(a.xMin, b.xMin), y = Mathf.Max(a.yMin, b.yMin);
            float xM = Mathf.Min(a.xMax, b.xMax), yM = Mathf.Min(a.yMax, b.yMax);
            return new Rect(x, y, Mathf.Max(0f, xM - x), Mathf.Max(0f, yM - y));
        }

        #endregion
    }
}

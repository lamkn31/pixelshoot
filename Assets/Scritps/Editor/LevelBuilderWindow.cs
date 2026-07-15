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

        private string _levelsFolder = "Assets";
        private readonly List<LevelData> _levels = new List<LevelData>();
        private Gun _defaultGunPrefab;
        private Block _defaultBlockPrefab;

        private Vector2 _leftScroll, _rightScroll;
        private bool _showPath = true, _showSlots = true, _showBlocks = true, _showFrame = true, _showRange = true, _showDir = true;

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

        // Generate grid.
        private BlockColor _genColor = BlockColor.Red;
        private int _genStack = 3;
        private BlockGridShape _genShape = BlockGridShape.Arc; // loại grid khi bấm "+ Grid"

        // Bảng màu tô cell bằng click trong khung giữa. -1 = None → click KHÔNG đổi màu cell.
        private int _paintColorIdx = -1;

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

            if (_target != null && _so != null) _so.ApplyModifiedProperties();
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
            _showFrame = GUILayout.Toggle(_showFrame, "Cam", EditorStyles.toolbarButton);
            _showRange = GUILayout.Toggle(_showRange, "Range", EditorStyles.toolbarButton);
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
                if (GUILayout.Button("▶", BtnW)) { Select(lv); AddPreviewToScene(); }
                if (GUILayout.Button("X", BtnW)) { DeleteLevel(lv); GUIUtility.ExitGUI(); }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
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
            foreach (var name in new[] { "SlotGunSpacing", "MaxGunOnPath", "GunSpeed", "GunSpacing",
                "FireInterval", "GunFireRange", "FrontStationDistance", "BulletSpeed", "BlockStackSpacing" })
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

            // Path.
            if (_showPath && _target.PathWaypoints != null && _target.PathWaypoints.Count >= 2)
            {
                Handles.color = new Color(0.3f, 0.8f, 1f);
                int n = _target.PathWaypoints.Count, lastSeg = _target.IsClosed ? n : n - 1;
                for (int i = 0; i < lastSeg; i++)
                {
                    Vector3 w0 = _target.PathWaypoints[i], w1 = _target.PathWaypoints[(i + 1) % n];
                    if (Front(w0) && Front(w1)) Line(Proj(w0), Proj(w1));
                }
            }

            // Vòng PHÁT HIỆN của gun tại các vị trí xuất phát trên path (không phải điều kiện bắn).
            if (_showRange && _target.PathWaypoints != null && _target.PathWaypoints.Count >= 2)
            {
                var gs = GameSettings.Instance;
                int maxGun = gs != null ? gs.MaxGunOnPath : 5;
                float rng = gs != null ? gs.GunFireRange : 3f;
                float front = gs != null ? gs.FrontStationDistance : 0f, sp = gs != null ? gs.GunSpacing : 1.2f;
                Handles.color = new Color(1f, 0.85f, 0.2f, 0.9f);
                for (int gi = 0; gi < maxGun; gi++)
                {
                    Vector3 ctr = PathPointAt(front + gi * sp); // khớp index đảo của PathManager
                    Vector2 prev = Proj(ctr + new Vector3(rng, 0, 0));
                    for (int k = 1; k <= 26; k++) { float a = k / 26f * Mathf.PI * 2f; Vector2 s = Proj(ctr + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * rng); Line(prev, s); prev = s; }
                    Vector2 c = Proj(ctr); FillRect(new Rect(c.x - 3, c.y - 3, 6, 6), Color.yellow);
                }
            }

            if (_showPath) HandleWaypointDrag(Proj, area);

            // Grids (fan vòng cung).
            if (_showBlocks && _target.Grids != null)
            {
                var blockLbl = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
                blockLbl.normal.textColor = Color.white;

                // Chỉ thu thập vùng click khi thật sự đang tô màu (Paint != None) và có MouseDown.
                bool collectHits = _paintColorIdx >= 0 && Event.current.type == EventType.MouseDown;
                List<(Rect rect, int flat)> cellHits = collectHits ? new List<(Rect, int)>() : null;

                for (int gi = 0; gi < _target.Grids.Count; gi++)
                {
                    var grid = _target.Grids[gi];
                    if (grid == null) continue;
                    int last = Mathf.Max(0, grid.Rows - 1);
                    cellHits?.Clear();

                    // Viền: Rect = 4 cạnh; Arc = 2 cạnh bên.
                    Handles.color = new Color(0.7f, 0.7f, 0.7f);
                    if (grid.Shape == BlockGridShape.Rect)
                    {
                        Vector3 p0 = grid.CellPos(0, 0), p1 = grid.CellPos(0, grid.ElementsInRow(0) - 1);
                        Vector3 p2 = grid.CellPos(last, grid.ElementsInRow(last) - 1), p3 = grid.CellPos(last, 0);
                        Line(Proj(p0), Proj(p1)); Line(Proj(p1), Proj(p2)); Line(Proj(p2), Proj(p3)); Line(Proj(p3), Proj(p0));
                    }
                    else
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
                            if (cell == null || cell.BlockStackCt <= 0) continue;
                            Vector3 wp = grid.CellPos(r, e);
                            if (!Front(wp)) continue;
                            Vector2 bp = Proj(wp); float sz = PixSize(wp, 0.5f);
                            var cellRect = new Rect(bp.x - sz / 2, bp.y - sz / 2, sz, sz);
                            FillRect(cellRect, BlockColorPalette.ToColor(cell.Color));
                            cellHits?.Add((cellRect, grid.CellIndex(r, e)));
                            // Số block trong stack (giống nhãn số đạn của gun).
                            if (sz >= 10f && area.Contains(bp))
                                GUI.Label(new Rect(bp.x - sz / 2, bp.y - sz / 2, sz, sz), cell.BlockStackCt.ToString(), blockLbl);

                            // Hướng (rotate) của cell: mũi tên; kéo đầu mũi tên để xoay.
                            if (_showDir)
                            {
                                Vector3 tipW = wp + cell.DirectionVector * 0.55f;
                                if (Front(tipW))
                                {
                                    Vector2 tip = Proj(tipW);
                                    Handles.color = new Color(1f, 1f, 1f, 0.9f);
                                    Line(bp, tip); ArrowHead(bp, tip);
                                    CellRotateHandle(gi, grid.CellIndex(r, e), wp, tip, area);
                                }
                            }
                        }
                    }

                    // Handle: tâm + 2 đầu cạnh (kéo được).
                    GridHandle(gi, 0, Proj(grid.Center), area);
                    GridHandle(gi, 1, Proj(grid.CellPos(0, 0)), area);                               // trái
                    GridHandle(gi, 2, Proj(grid.CellPos(0, grid.ElementsInRow(0) - 1)), area);         // phải

                    // Click cell = tô màu Gen Color (sau handle xoay/resize để nhường ưu tiên).
                    if (cellHits != null) PaintCellClick(gi, cellHits, area);
                }
                ApplyGridHandleDrag();
                if (_showDir) ApplyCellRotateDrag();
            }

            // Slots.
            if (_showSlots && _target.Slots != null)
            {
                var sceneSlots = GetSceneSlots();
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
                        FillRect(new Rect(gp.x - sz / 2, gp.y - sz / 2, sz, sz), BlockColorPalette.ToColor(g.Color));
                        if (area.Contains(gp)) GUI.Label(new Rect(gp.x - sz / 2, gp.y - sz / 2, sz, sz), g.CountBullet.ToString(), lbl);
                    }
                }
            }

            if (_camMode) cam.ResetAspect();
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
            Color col = hid == 0 ? new Color(1f, 0.5f, 0.9f, 0.9f) : new Color(0.3f, 0.9f, 1f, 0.9f);
            EditorGUI.DrawRect(hrect, active ? Color.yellow : col);
            EditorGUIUtility.AddCursorRect(hrect, MouseCursor.MoveArrow);
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
                else
                {
                    Vector3 center = g.FindPropertyRelative("Center").vector3Value;
                    Vector3 v = nw - center; v.y = 0f;
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
                        g.FindPropertyRelative("ArcAngle").floatValue = Mathf.Clamp(Mathf.Abs(angle) * 2f, 1f, 350f);
                    }
                }
                e.Use(); Repaint();
            }
            else if (e.type == EventType.MouseUp) { _dragGrid = -1; _dragHandle = -1; e.Use(); }
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

        // Click trái vào ô cell → tô thành màu đang chọn ở bảng Paint Color (None thì bỏ qua).
        private void PaintCellClick(int gi, List<(Rect rect, int flat)> hits, Rect area)
        {
            if (_paintColorIdx < 0) return; // None → không tô
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || e.alt || !area.Contains(e.mousePosition)) return;
            var grids = _so.FindProperty("Grids");
            if (gi >= grids.arraySize) return;
            var cells = grids.GetArrayElementAtIndex(gi).FindPropertyRelative("Cells");
            for (int i = 0; i < hits.Count; i++)
            {
                if (!hits[i].rect.Contains(e.mousePosition)) continue;
                int flat = hits[i].flat;
                if (flat >= 0 && flat < cells.arraySize)
                    cells.GetArrayElementAtIndex(flat).FindPropertyRelative("Color").enumValueIndex = _paintColorIdx;
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
            DrawMetaSection();
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
            EditorGUILayout.PropertyField(_so.FindProperty("CornerRadius"));

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
            EditorGUILayout.HelpBox("Chọn loại grid (Arc = vòng cung, Rect = lưới chữ nhật) rồi bấm + Grid.\nKéo TÂM (hồng) + 2 ĐẦU CẠNH (xanh) trong khung giữa. Row 0 = hàng ngoài cùng gần path.\nClick ô cell = tô màu đang chọn ở Paint Color · kéo đầu mũi tên = xoay hướng cell.", MessageType.None);

            EditorGUILayout.BeginHorizontal();
            _genColor = (BlockColor)EditorGUILayout.EnumPopup("Gen Color", _genColor);
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
                EditorGUILayout.PropertyField(shapeProp, new GUIContent("Shape"));
                EditorGUILayout.PropertyField(grid.FindPropertyRelative("Center"));
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(grid.FindPropertyRelative("BaseRadius"),
                    new GUIContent(isRect ? "Dist Row 0" : "Base Radius"));
                EditorGUILayout.PropertyField(grid.FindPropertyRelative("RowSpacing"), new GUIContent("Row Spacing"));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(grid.FindPropertyRelative("Rows"), new GUIContent("Rows"));
                if (isRect) EditorGUILayout.PropertyField(grid.FindPropertyRelative("Columns"), new GUIContent("Columns"));
                else EditorGUILayout.PropertyField(grid.FindPropertyRelative("ArcAngle"), new GUIContent("Arc Angle"));
                EditorGUILayout.EndHorizontal();
                if (!isRect) EditorGUILayout.PropertyField(grid.FindPropertyRelative("SpiralGrowth"), new GUIContent("Spiral Growth (xoắn)"));
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(grid.FindPropertyRelative("BlockWidth"), new GUIContent("Block W"));
                EditorGUILayout.PropertyField(grid.FindPropertyRelative("Spacing"), new GUIContent("Spacing"));
                EditorGUILayout.EndHorizontal();
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

        private void AddGrid(SerializedProperty grids)
        {
            int idx = grids.arraySize; grids.arraySize++;
            var g = grids.GetArrayElementAtIndex(idx);
            g.FindPropertyRelative("Shape").enumValueIndex = (int)_genShape;
            g.FindPropertyRelative("Center").vector3Value = Vector3.zero;
            g.FindPropertyRelative("BaseRadius").floatValue = 3f;
            g.FindPropertyRelative("RowSpacing").floatValue = 1.2f;
            g.FindPropertyRelative("Rows").intValue = 3;
            g.FindPropertyRelative("Columns").intValue = 5;
            g.FindPropertyRelative("ArcAngle").floatValue = 90f;
            g.FindPropertyRelative("SpiralGrowth").floatValue = 0f;
            g.FindPropertyRelative("BlockWidth").floatValue = 0.8f;
            g.FindPropertyRelative("Spacing").floatValue = 0.2f;
            g.FindPropertyRelative("Layout").enumValueIndex = (int)BlockGridLayout.ArcLength;
            g.FindPropertyRelative("Cells").arraySize = 0;
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

        // Bảng màu tô cell: None (mặc định) = click cell không đổi gì. Chọn 1 màu → click cell trong khung
        // giữa sẽ tô cell đó thành màu đang chọn.
        private void DrawPaintPalette()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Paint Color", GUILayout.Width(72));
            if (GUILayout.Toggle(_paintColorIdx < 0, "None", EditorStyles.miniButton, GUILayout.Width(44)))
                _paintColorIdx = -1;

            int count = System.Enum.GetValues(typeof(BlockColor)).Length;
            var bg = GUI.backgroundColor;
            for (int i = 0; i < count; i++)
            {
                bool sel = _paintColorIdx == i;
                GUI.backgroundColor = BlockColorPalette.ToColor((BlockColor)i);
                if (GUILayout.Toggle(sel, sel ? "●" : "", EditorStyles.miniButton, GUILayout.Width(24)) && !sel)
                    _paintColorIdx = i;
            }
            GUI.backgroundColor = bg;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(_paintColorIdx < 0
                ? "Paint = None: click cell KHÔNG đổi màu (chỉ kéo handle/xoay hướng)."
                : $"Đang tô màu {(BlockColor)_paintColorIdx} — click cell trong khung giữa để đổi màu cell đó.",
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
                    // Hướng mặc định: dồn về TÂM grid (≈ về phía path).
                    Vector3 v = grid.Center - grid.CellPos(r, el); v.y = 0f;
                    c.FindPropertyRelative("SpawnerDirectionAngleZ").floatValue =
                        v.sqrMagnitude > 1e-6f ? Mathf.Repeat(Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg, 360f) : 0f;
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
                case ListOp.Delete: list.DeleteArrayElementAtIndex(i); break;
            }
        }

        private List<GunSlot> GetSceneSlots()
        {
            var list = new List<GunSlot>(Object.FindObjectsOfType<GunSlot>(true));
            list.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
            return list;
        }

        private Vector3 PathPointAt(float dist)
        {
            var wp = _target.PathWaypoints;
            int n = wp.Count;
            if (n == 0) return Vector3.zero;
            if (n == 1) return wp[0];
            int segs = _target.IsClosed ? n : n - 1;
            float total = 0f;
            for (int i = 0; i < segs; i++) total += Vector3.Distance(wp[i], wp[(i + 1) % n]);
            if (total < 1e-4f) return wp[0];
            dist = _target.IsClosed ? Mathf.Repeat(dist, total) : Mathf.Clamp(dist, 0f, total);
            float acc = 0f;
            for (int i = 0; i < segs; i++)
            {
                Vector3 a = wp[i], b = wp[(i + 1) % n];
                float len = Vector3.Distance(a, b);
                if (acc + len >= dist) return Vector3.Lerp(a, b, len > 1e-4f ? (dist - acc) / len : 0f);
                acc += len;
            }
            return wp[_target.IsClosed ? 0 : n - 1];
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

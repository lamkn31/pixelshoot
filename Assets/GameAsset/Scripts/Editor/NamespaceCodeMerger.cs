using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// Tool gộp toàn bộ file .cs trong một thư mục nguồn thành các file lớn,
    /// mỗi file tương ứng với một namespace. Hữu ích để xuất code chia sẻ /
    /// review / nạp context cho AI mà vẫn giữ nguyên cấu trúc namespace.
    ///
    /// Mở qua menu: Tools ▸ Code ▸ Namespace Code Merger.
    /// </summary>
    public sealed class NamespaceCodeMerger : EditorWindow
    {
        // ──────────────────────────────── Cấu hình ────────────────────────────────
        private string _sourceFolder = "Assets/Scripts";
        private string _outputFolder = "Assets/_Merged";
        private bool _recursive = true;
        private bool _mergeUsings = true;          // Gộp & loại trùng using lên đầu file.
        private bool _includeSourceHeader = true;  // Thêm dòng comment // ── file gốc.
        private bool _flattenFileName = true;       // Tên file = namespace đầy đủ (a.b.c.cs).
        private string _fileExtension = ".cs.txt";  // Đuôi xuất; .txt để không bị Unity compile.

        private Vector2 _scroll;
        private string _lastReport;

        [MenuItem("Tools/Code/Namespace Code Merger")]
        private static void Open()
        {
            var win = GetWindow<NamespaceCodeMerger>("Namespace Merger");
            win.minSize = new Vector2(460, 360);
            win.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Nguồn & đích", EditorStyles.boldLabel);

            DrawFolderField("Thư mục nguồn", ref _sourceFolder);
            DrawFolderField("Thư mục xuất", ref _outputFolder);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tùy chọn", EditorStyles.boldLabel);

            _recursive = EditorGUILayout.Toggle(
                new GUIContent("Quét đệ quy", "Quét cả các thư mục con."), _recursive);
            _mergeUsings = EditorGUILayout.Toggle(
                new GUIContent("Gộp using", "Dồn mọi using lên đầu file và loại trùng."), _mergeUsings);
            _includeSourceHeader = EditorGUILayout.Toggle(
                new GUIContent("Ghi tên file gốc", "Chèn comment đánh dấu mỗi đoạn từ file nào."), _includeSourceHeader);
            _flattenFileName = EditorGUILayout.Toggle(
                new GUIContent("Tên file = namespace", "ConveyorPath.Core → ConveyorPath.Core.cs.txt."), _flattenFileName);

            _fileExtension = EditorGUILayout.TextField(
                new GUIContent("Đuôi file xuất", "Dùng .txt để Unity không compile file đã gộp."), _fileExtension);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_sourceFolder)))
            {
                if (GUILayout.Button("Gộp code theo namespace", GUILayout.Height(32)))
                    Run();
            }

            if (!string.IsNullOrEmpty(_lastReport))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_lastReport, MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawFolderField(string label, ref string path)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                path = EditorGUILayout.TextField(label, path);
                if (GUILayout.Button("…", GUILayout.Width(28)))
                {
                    string abs = EditorUtility.OpenFolderPanel(label, ToAbsolute(path), "");
                    if (!string.IsNullOrEmpty(abs))
                        path = ToProjectRelative(abs);
                }
            }
        }

        // ──────────────────────────────── Xử lý chính ────────────────────────────────

        private void Run()
        {
            string srcAbs = ToAbsolute(_sourceFolder);
            if (!Directory.Exists(srcAbs))
            {
                EditorUtility.DisplayDialog("Lỗi", $"Không tìm thấy thư mục nguồn:\n{srcAbs}", "OK");
                return;
            }

            var search = _recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] files = Directory.GetFiles(srcAbs, "*.cs", search);

            // namespace -> dữ liệu gộp.
            var groups = new Dictionary<string, NamespaceBucket>(StringComparer.Ordinal);

            foreach (string file in files)
            {
                // Bỏ qua chính file tool và các file generated/assembly.
                string name = Path.GetFileName(file);
                if (name.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)) continue;

                string code = File.ReadAllText(file);
                foreach (ParsedNamespace ns in ParseFile(code))
                {
                    if (!groups.TryGetValue(ns.Name, out var bucket))
                    {
                        bucket = new NamespaceBucket(ns.Name);
                        groups[ns.Name] = bucket;
                    }

                    foreach (string u in ns.Usings) bucket.Usings.Add(u);
                    bucket.Members.Add(new MemberBlock(
                        ToProjectRelative(file), ns.Body));
                }
            }

            if (groups.Count == 0)
            {
                _lastReport = "Không tìm thấy namespace nào trong thư mục nguồn.";
                Repaint();
                return;
            }

            string outAbs = ToAbsolute(_outputFolder);
            Directory.CreateDirectory(outAbs);

            int written = 0;
            var sb = new StringBuilder();
            foreach (NamespaceBucket bucket in groups.Values.OrderBy(b => b.Name, StringComparer.Ordinal))
            {
                sb.Clear();
                BuildFile(sb, bucket);

                string fileName = (_flattenFileName ? bucket.Name : SafeShortName(bucket.Name)) + _fileExtension;
                fileName = Sanitize(fileName);
                File.WriteAllText(Path.Combine(outAbs, fileName), sb.ToString(), new UTF8Encoding(false));
                written++;
            }

            AssetDatabase.Refresh();

            _lastReport =
                $"Đã gộp {files.Length} file nguồn → {written} file namespace.\n" +
                $"Xuất tại: {_outputFolder}\n\n" +
                string.Join("\n", groups.Values
                    .OrderBy(b => b.Name, StringComparer.Ordinal)
                    .Select(b => $"• {b.Name}  ({b.Members.Count} khối)"));

            EditorUtility.RevealInFinder(outAbs);
            Repaint();
        }

        private void BuildFile(StringBuilder sb, NamespaceBucket bucket)
        {
            sb.Append("// <auto-generated> Gộp bởi NamespaceCodeMerger — ")
              .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
              .AppendLine(" </auto-generated>");
            sb.Append("// Namespace: ").AppendLine(bucket.Name);
            sb.Append("// Số khối nguồn: ").Append(bucket.Members.Count).AppendLine();
            sb.AppendLine();

            if (_mergeUsings && bucket.Usings.Count > 0)
            {
                foreach (string u in SortUsings(bucket.Usings))
                    sb.Append(u).AppendLine(";");
                sb.AppendLine();
            }

            sb.Append("namespace ").AppendLine(bucket.Name);
            sb.AppendLine("{");

            for (int i = 0; i < bucket.Members.Count; i++)
            {
                MemberBlock m = bucket.Members[i];
                if (_includeSourceHeader)
                {
                    sb.AppendLine("    // ───────────────────────────────────────────────");
                    sb.Append("    // ").AppendLine(m.SourcePath);
                    sb.AppendLine("    // ───────────────────────────────────────────────");
                }

                sb.AppendLine(Indent(m.Body.Trim('\r', '\n'), "    "));
                if (i < bucket.Members.Count - 1) sb.AppendLine();
            }

            sb.AppendLine("}");
        }

        // ──────────────────────────────── Parser ────────────────────────────────

        // Lấy tên namespace cho cả block (namespace X { }) và file-scoped (namespace X;).
        private static readonly Regex NamespaceRegex =
            new Regex(@"(^|\s)namespace\s+([A-Za-z_][\w\.]*)\s*(\{|;)", RegexOptions.Compiled);

        // Lấy dòng using ở cấp ngoài cùng (không phải using bên trong thân hàm).
        private static readonly Regex UsingRegex =
            new Regex(@"^\s*(using\s+(?:static\s+)?[A-Za-z_][\w\.]*(?:\s*=\s*[\w\.<>,\s]+)?)\s*;",
                RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// Tách một file thành danh sách (namespace, usings, body).
        /// Code nằm ngoài mọi namespace (global) được gom vào namespace rỗng "" → file "global".
        /// </summary>
        private static IEnumerable<ParsedNamespace> ParseFile(string code)
        {
            string noComments = StripCommentsAndStrings(code);
            var result = new List<ParsedNamespace>();

            var matches = NamespaceRegex.Matches(noComments);
            if (matches.Count == 0)
            {
                // Không có namespace: trả toàn bộ như global.
                var globalUsings = CollectUsings(code, 0, code.Length);
                string body = RemoveUsings(code).Trim();
                if (body.Length > 0)
                    result.Add(new ParsedNamespace("(global)", globalUsings, body));
                return result;
            }

            foreach (Match m in matches)
            {
                string nsName = m.Groups[2].Value;
                bool fileScoped = m.Groups[3].Value == ";";
                int bodyStart, bodyEnd;

                if (fileScoped)
                {
                    bodyStart = m.Index + m.Length;
                    bodyEnd = code.Length;
                }
                else
                {
                    int open = noComments.IndexOf('{', m.Index);
                    bodyStart = open + 1;
                    bodyEnd = FindMatchingBrace(noComments, open);
                    if (bodyEnd < 0) bodyEnd = code.Length;
                }

                var usings = CollectUsings(code, m.Index, code.Length);
                string raw = code.Substring(bodyStart, Math.Min(bodyEnd, code.Length) - bodyStart);
                string body = RemoveUsings(raw).Trim();
                if (body.Length > 0)
                    result.Add(new ParsedNamespace(nsName, usings, body));
            }

            return result;
        }

        private static List<string> CollectUsings(string original, int from, int to)
        {
            // Quét using ở phần đầu file (trước namespace) + using ngay trong namespace.
            var list = new List<string>();
            string head = original.Substring(0, Math.Min(from, original.Length));
            foreach (Match u in UsingRegex.Matches(head))
                list.Add(u.Groups[1].Value.Trim());
            return list;
        }

        private static string RemoveUsings(string body) =>
            UsingRegex.Replace(body, string.Empty);

        // Tìm dấu } khớp với { tại vị trí openIndex (chuỗi đã bỏ comment/string).
        private static int FindMatchingBrace(string s, int openIndex)
        {
            int depth = 0;
            for (int i = openIndex; i < s.Length; i++)
            {
                if (s[i] == '{') depth++;
                else if (s[i] == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Thay comment và nội dung string/char bằng khoảng trắng để việc đếm ngoặc
        /// không bị nhiễu bởi { } nằm trong chuỗi. Giữ nguyên độ dài để index khớp file gốc.
        /// </summary>
        private static string StripCommentsAndStrings(string s)
        {
            var sb = new StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                char n = i + 1 < s.Length ? s[i + 1] : '\0';

                if (c == '/' && n == '/')
                {
                    while (i < s.Length && s[i] != '\n') { sb.Append(' '); i++; }
                }
                else if (c == '/' && n == '*')
                {
                    while (i < s.Length && !(s[i] == '*' && i + 1 < s.Length && s[i + 1] == '/'))
                    { sb.Append(s[i] == '\n' ? '\n' : ' '); i++; }
                    if (i < s.Length) { sb.Append("  "); i += 2; }
                }
                else if (c == '@' && n == '"') // verbatim string
                {
                    sb.Append("  "); i += 2;
                    while (i < s.Length)
                    {
                        if (s[i] == '"' && i + 1 < s.Length && s[i + 1] == '"') { sb.Append("  "); i += 2; continue; }
                        if (s[i] == '"') { sb.Append(' '); i++; break; }
                        sb.Append(s[i] == '\n' ? '\n' : ' '); i++;
                    }
                }
                else if (c == '"' || c == '\'')
                {
                    char quote = c;
                    sb.Append(' '); i++;
                    while (i < s.Length && s[i] != quote)
                    {
                        if (s[i] == '\\' && i + 1 < s.Length) { sb.Append("  "); i += 2; continue; }
                        sb.Append(s[i] == '\n' ? '\n' : ' '); i++;
                    }
                    if (i < s.Length) { sb.Append(' '); i++; }
                }
                else { sb.Append(c); i++; }
            }
            return sb.ToString();
        }

        // ──────────────────────────────── Tiện ích ────────────────────────────────

        private static IEnumerable<string> SortUsings(IEnumerable<string> usings) =>
            usings.Distinct()
                  .OrderByDescending(u => u.StartsWith("using System"))   // System lên trước.
                  .ThenBy(u => u, StringComparer.Ordinal);

        private static string Indent(string text, string pad)
        {
            var sb = new StringBuilder();
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length > 0) sb.Append(pad).Append(lines[i]);
                if (i < lines.Length - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        private static string SafeShortName(string ns)
        {
            int dot = ns.LastIndexOf('.');
            return dot >= 0 ? ns.Substring(dot + 1) : ns;
        }

        private static string Sanitize(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');
            return fileName.Replace("(global)", "_global_");
        }

        private static string ToAbsolute(string projectRelative)
        {
            if (string.IsNullOrEmpty(projectRelative)) return Application.dataPath;
            if (Path.IsPathRooted(projectRelative)) return projectRelative;
            string root = Directory.GetParent(Application.dataPath)!.FullName; // .../<Project>
            return Path.GetFullPath(Path.Combine(root, projectRelative));
        }

        private static string ToProjectRelative(string absolute)
        {
            string root = Directory.GetParent(Application.dataPath)!.FullName;
            absolute = Path.GetFullPath(absolute);
            return absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? absolute.Substring(root.Length + 1).Replace('\\', '/')
                : absolute.Replace('\\', '/');
        }

        // ──────────────────────────────── Mô hình dữ liệu ────────────────────────────────

        private readonly struct ParsedNamespace
        {
            public readonly string Name;
            public readonly List<string> Usings;
            public readonly string Body;
            public ParsedNamespace(string name, List<string> usings, string body)
            { Name = name; Usings = usings; Body = body; }
        }

        private sealed class NamespaceBucket
        {
            public readonly string Name;
            public readonly HashSet<string> Usings = new HashSet<string>(StringComparer.Ordinal);
            public readonly List<MemberBlock> Members = new List<MemberBlock>();
            public NamespaceBucket(string name) { Name = name; }
        }

        private readonly struct MemberBlock
        {
            public readonly string SourcePath;
            public readonly string Body;
            public MemberBlock(string sourcePath, string body)
            { SourcePath = sourcePath; Body = body; }
        }
    }
}

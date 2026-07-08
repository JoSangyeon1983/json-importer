using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OTAON.Editor.Data
{
	/// <summary>
	/// Manages data table import from Google Sheets TSV exports.
	/// Converts TSV to JSON and CSV with key-based structure for easy runtime lookup.
	/// </summary>
	public static class DataImporter
	{
		public enum DataTable
		{
			None = -1,
			UIPanelOrder,
			Text,
			_Max_
		}

		private const string MenuPath = "Tools/Data Import";
		private const string EditorPrefsKeyPrefix = "OTAON.DataImporter.Url.";
		public static readonly string OutputRoot = "Assets/StreamingAssets/DataTables";

		[MenuItem(MenuPath)]
		public static void OpenWindow()
		{
			DataImportWindow.ShowWindow();
		}

		public static bool ImportTable(DataTable table, Dictionary<DataTable, string> urls)
		{
			if (!urls.TryGetValue(table, out var url) || string.IsNullOrWhiteSpace(url))
			{
				Debug.LogWarning($"[DataImporter] URL이 비어있습니다: {table}");
				return false;
			}

			try
			{
				Debug.Log($"[DataImporter] Downloading: {table} from {url}");
				var tsv = WebDownloader.DownloadText(url);

				if (string.IsNullOrWhiteSpace(tsv))
				{
					Debug.LogWarning($"[DataImporter] Downloaded TSV is empty: {table}");
					return false;
				}

				Debug.Log($"[DataImporter] Downloaded {tsv.Length} characters for {table}");
				Debug.Log($"[DataImporter] First 200 chars:\n{tsv.Substring(0, Math.Min(200, tsv.Length))}");

				var (json, csv) = TsvConverter.Convert(tsv, msg => Debug.Log($"[DataImporter] {msg}"));

				if (string.IsNullOrWhiteSpace(json) || json == "{}")
				{
					Debug.LogWarning($"[DataImporter] Converted JSON is empty: {table}");
					return false;
				}

				FileSaver.SaveJsonFile(OutputRoot, table, json);
				FileSaver.SaveCsvFile(OutputRoot, table, csv);

				return true;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[DataImporter] Import failed: {table}\n{ex}");
				return false;
			}
		}

		public static IEnumerable<DataTable> EnumerateTables()
		{
			for (var i = 0; i < (int)DataTable._Max_; i++)
			{
				var table = (DataTable)i;
				if (table == DataTable.None)
					continue;
				yield return table;
			}
		}

		public static string GetPrefsKey(DataTable table) => EditorPrefsKeyPrefix + table;
	}

	// ==================== Internal Utilities ====================

	internal static class WebDownloader
	{
		public static string DownloadText(string url)
		{
			using var wc = new System.Net.WebClient();
			wc.Headers.Add("User-Agent", "UnityDataImporter");
			wc.Encoding = System.Text.Encoding.UTF8;
			return wc.DownloadString(url);
		}
	}

	internal static class FileSaver
	{
		public static void SaveJsonFile(string outputRoot, DataImporter.DataTable table, string json)
		{
			Directory.CreateDirectory(outputRoot);
			var outputPath = Path.Combine(outputRoot, $"{table.ToString().ToLowerInvariant()}.json");
			File.WriteAllText(outputPath, json, Encoding.UTF8);
			Debug.Log($"[DataImporter] Wrote JSON: {outputPath} ({json.Length} chars)");
			AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
		}

		public static void SaveCsvFile(string outputRoot, DataImporter.DataTable table, string csv)
		{
			Directory.CreateDirectory(outputRoot);
			var outputPath = Path.Combine(outputRoot, $"{table.ToString().ToLowerInvariant()}.csv");
			// Excel 등에서 한글이 깨지지 않도록 UTF-8 BOM 인코딩을 명시적으로 사용합니다.
			var utf8WithBom = new UTF8Encoding(true);
			File.WriteAllText(outputPath, csv, utf8WithBom);
			Debug.Log($"[DataImporter] Wrote CSV: {outputPath} ({csv.Length} chars)");
			AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
		}
	}

	/// <summary>
	/// TSV to JSON and CSV converter with comment filtering and key-based structure.
	/// </summary>
	internal static class TsvConverter
	{
		private const string CommentPrefix = "//";

		public static (string json, string csv) Convert(string tsvText, Action<string> log = null)
		{
			var lines = SplitLines(tsvText);
			if (lines.Count == 0)
			{
				log?.Invoke("No lines in TSV");
				return ("{}", string.Empty);
			}

			var headerCells = SplitTsvLine(lines[0]);
			if (headerCells.Count == 0)
			{
				log?.Invoke("No header cells in TSV");
				return ("{}", string.Empty);
			}

			log?.Invoke($"Header: {string.Join(" | ", headerCells)}");

			var (includeIndices, includeKeys) = FilterCommentColumns(headerCells, log);

			if (includeKeys.Count < 1)
			{
				log?.Invoke("No valid columns after filtering comments");
				return ("{}", string.Empty);
			}

			log?.Invoke($"Using '{includeKeys[0]}' as primary key column");

			var (table, csv) = ParseDataRows(lines, includeIndices, includeKeys, log);

			log?.Invoke($"Parsed {table.Count} entries");

			return (JsonSerializer.Serialize(table), csv);
		}

		private static (List<int> indices, List<string> keys) FilterCommentColumns(
			List<string> headerCells,
			Action<string> log)
		{
			var indices = new List<int>();
			var keys = new List<string>();

			for (int i = 0; i < headerCells.Count; i++)
			{
				var key = headerCells[i]?.Trim() ?? string.Empty;

				if (key.StartsWith(CommentPrefix, StringComparison.Ordinal))
				{
					log?.Invoke($"Skipping comment column: {key}");
					continue;
				}

				if (string.IsNullOrEmpty(key))
				{
					log?.Invoke($"Skipping empty column at index {i}");
					continue;
				}

				indices.Add(i);
				keys.Add(key);
			}

			log?.Invoke($"Active columns: {string.Join(", ", keys)}");
			return (indices, keys);
		}

		private static (Dictionary<string, Dictionary<string, object>> table, string csv) ParseDataRows(
			List<string> lines,
			List<int> includeIndices,
			List<string> includeKeys,
			Action<string> log)
		{
			var table = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
			var csvBuilder = new StringBuilder();

			// CSV 헤더 작성
			csvBuilder.AppendLine(string.Join(",", includeKeys.Select(EscapeCsv)));

			for (int lineIndex = 1; lineIndex < lines.Count; lineIndex++)
			{
				var rowCells = SplitTsvLine(lines[lineIndex]);
				if (rowCells.Count == 0)
					continue;

				var firstCell = rowCells[0]?.Trim() ?? string.Empty;
				if (firstCell.StartsWith(CommentPrefix, StringComparison.Ordinal))
				{
					log?.Invoke($"Skipping comment row at line {lineIndex + 1}");
					continue;
				}

				var keyIndex = includeIndices[0];
				var keyValue = keyIndex < rowCells.Count
					? (rowCells[keyIndex]?.Trim() ?? string.Empty)
					: string.Empty;

				if (string.IsNullOrEmpty(keyValue))
				{
					log?.Invoke($"Row {lineIndex + 1} has empty key, skipping");
					continue;
				}

				var valueObj = new Dictionary<string, object>(StringComparer.Ordinal);
				var csvRow = new List<string>();

				// k = 0부터 시작하여 CSV 행 데이터를 구성합니다. JSON의 valueObj 에는 첫 번째 키 컬럼을 제외합니다.
				for (int k = 0; k < includeIndices.Count; k++)
				{
					var cellIndex = includeIndices[k];
					var columnName = includeKeys[k];
					var value = cellIndex < rowCells.Count ? rowCells[cellIndex] : string.Empty;

					if (k > 0)
					{
						valueObj[columnName] = TypeInference.Infer(value);
					}

					csvRow.Add(EscapeCsv(value));
				}

				if (table.ContainsKey(keyValue))
				{
					log?.Invoke($"Duplicate key '{keyValue}' at row {lineIndex + 1}, overwriting");
				}

				table[keyValue] = valueObj;
				csvBuilder.AppendLine(string.Join(",", csvRow));
			}

			return (table, csvBuilder.ToString());
		}

		private static string EscapeCsv(string s)
		{
			if (string.IsNullOrEmpty(s))
				return string.Empty;

			// CSV 특수문자(쉼표, 쌍따옴표, 줄바꿈)가 포함된 경우 쌍따옴표로 감싸고, 내부 쌍따옴표는 두 개로 늘려줍니다.
			if (s.Contains(",") || s.Contains("\"") || s.Contains("\n") || s.Contains("\r"))
			{
				return $"\"{s.Replace("\"", "\"\"")}\"";
			}

			return s;
		}

		private static List<string> SplitLines(string text)
		{
			var result = new List<string>();
			using var sr = new StringReader(text ?? string.Empty);
			string line;
			while ((line = sr.ReadLine()) != null)
			{
				result.Add(line);
			}
			return result;
		}

		private static List<string> SplitTsvLine(string line)
		{
			return new List<string>((line ?? string.Empty).Split('\t'));
		}
	}

	internal static class TypeInference
	{
		public static object Infer(string raw)
		{
			var s = (raw ?? string.Empty).Trim();
			if (string.IsNullOrEmpty(s))
				return string.Empty;

			if (bool.TryParse(s, out var b))
				return b;

			if (int.TryParse(s, System.Globalization.NumberStyles.Integer,
				System.Globalization.CultureInfo.InvariantCulture, out var i))
				return i;

			if (float.TryParse(s, System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture, out var f))
				return f;

			return s;
		}
	}

	internal static class JsonSerializer
	{
		public static string Serialize(Dictionary<string, Dictionary<string, object>> table)
		{
			var sb = new StringBuilder();
			sb.AppendLine("{");

			var entries = new List<KeyValuePair<string, Dictionary<string, object>>>(table);
			for (int i = 0; i < entries.Count; i++)
			{
				var entry = entries[i];
				sb.AppendLine($"  \"{EscapeJson(entry.Key)}\": {{");

				var valueEntries = new List<KeyValuePair<string, object>>(entry.Value);
				for (int j = 0; j < valueEntries.Count; j++)
				{
					var kv = valueEntries[j];
					sb.Append($"    \"{EscapeJson(kv.Key)}\": ");
					sb.Append(ValueToJson(kv.Value));
					sb.AppendLine(j < valueEntries.Count - 1 ? "," : string.Empty);
				}

				sb.Append("  }");
				sb.AppendLine(i < entries.Count - 1 ? "," : string.Empty);
			}

			sb.AppendLine("}");
			return sb.ToString();
		}

		private static string ValueToJson(object value)
		{
			if (value == null)
				return "null";

			if (value is string s)
				return $"\"{EscapeJson(s)}\"";

			if (value is bool b)
				return b ? "true" : "false";

			if (value is int || value is long || value is float || value is double)
				return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);

			return $"\"{EscapeJson(value.ToString())}\"";
		}

		private static string EscapeJson(string s)
		{
			if (string.IsNullOrEmpty(s))
				return string.Empty;

			return s.Replace("\\", "\\\\")
					.Replace("\"", "\\\"")
					.Replace("\n", "\\n")
					.Replace("\r", "\\r")
					.Replace("\t", "\\t");
		}
	}

	// ==================== Editor Window ====================

	public sealed class DataImportWindow : EditorWindow
	{
		private const int MinWindowWidth = 640;
		private const int MinWindowHeight = 320;
		private const int ButtonHeight = 28;

		private Vector2 _scroll;
		private readonly Dictionary<DataImporter.DataTable, string> _urls = new();
		private GUIStyle _wrapLabel;

		public static void ShowWindow()
		{
			var window = GetWindow<DataImportWindow>(utility: false, title: "Data Import", focus: true);
			window.minSize = new Vector2(MinWindowWidth, MinWindowHeight);
			window.Show();
		}

		private void OnEnable()
		{
			_wrapLabel = new GUIStyle(EditorStyles.wordWrappedLabel);
			LoadUrlsFromPrefs();
		}

		private void OnGUI()
		{
			DrawHeader();
			DrawUrlInputFields();
			DrawOutputPath();
			DrawImportButtons();
		}

		private void DrawHeader()
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Google Sheets TSV Export URLs", EditorStyles.boldLabel);
			EditorGUILayout.LabelField(
				"각 테이블별로 Google Sheets의 TSV export URL을 입력하세요.\n" +
				"예: https://docs.google.com/spreadsheets/d/<id>/export?format=tsv&gid=0&range=A5:C7",
				_wrapLabel);
			EditorGUILayout.Space();
		}

		private void DrawUrlInputFields()
		{
			using var scroll = new EditorGUILayout.ScrollViewScope(_scroll);
			_scroll = scroll.scrollPosition;

			foreach (var table in DataImporter.EnumerateTables())
			{
				EditorGUILayout.BeginVertical("box");
				EditorGUILayout.LabelField(table.ToString(), EditorStyles.boldLabel);

				var currentUrl = _urls.TryGetValue(table, out var u) ? u : string.Empty;
				var newUrl = EditorGUILayout.TextField("URL", currentUrl);

				if (!string.Equals(newUrl, currentUrl, StringComparison.Ordinal))
				{
					_urls[table] = newUrl;
					SaveUrlToPrefs(table, newUrl);
				}

				EditorGUILayout.EndVertical();
			}
		}

		private void DrawOutputPath()
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
			EditorGUILayout.LabelField($"JSON / CSV 저장 경로: {DataImporter.OutputRoot}", _wrapLabel);
			EditorGUILayout.Space();
		}

		private void DrawImportButtons()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Import All", GUILayout.Height(ButtonHeight)))
				{
					ImportAll();
				}

				if (GUILayout.Button("Import Selected...", GUILayout.Height(ButtonHeight)))
				{
					ShowImportMenu();
				}
			}
		}

		private void ImportAll()
		{
			var successCount = 0;
			var tables = DataImporter.EnumerateTables().ToList();

			foreach (var table in tables)
			{
				if (DataImporter.ImportTable(table, _urls))
					successCount++;
			}

			EditorUtility.DisplayDialog("Data Import",
				$"완료: {successCount}/{tables.Count} tables", "OK");
		}

		private void ShowImportMenu()
		{
			var menu = new GenericMenu();
			foreach (var table in DataImporter.EnumerateTables())
			{
				menu.AddItem(new GUIContent(table.ToString()), false, () =>
				{
					DataImporter.ImportTable(table, _urls);
				});
			}
			menu.ShowAsContext();
		}

		private void LoadUrlsFromPrefs()
		{
			_urls.Clear();
			foreach (var table in DataImporter.EnumerateTables())
			{
				_urls[table] = EditorPrefs.GetString(DataImporter.GetPrefsKey(table), string.Empty);
			}
		}

		private void SaveUrlToPrefs(DataImporter.DataTable table, string url)
		{
			EditorPrefs.SetString(DataImporter.GetPrefsKey(table), url ?? string.Empty);
		}
	}
}

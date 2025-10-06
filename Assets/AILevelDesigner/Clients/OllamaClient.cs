using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading.Tasks;
using AILevelDesigner.Configs;
using UnityEngine;
using UnityEngine.Networking;

namespace AILevelDesigner
{
    public class OllamaClient : IAIClient
    {
        private readonly AIConfig _config;

        public OllamaClient(AIConfig config)
        {
            _config = config;
        }

        public async Task<LayoutData> GenerateLayoutAsync(string prompt, string capabilitiesJson, string schemaJson)
        {
            var systemMsg = PromptBuilder.BuildSystemMessage(schemaJson);
            var userMsg = PromptBuilder.BuildUserMessage(prompt ?? string.Empty, capabilitiesJson ?? "{}");

            var body = new StringBuilder();
            body.Append("{");
            body.AppendFormat("\"model\":\"{0}\",", Escape(_config.openAIModel));
            body.Append("\"stream\":false,");
            body.Append("\"format\":\"json\",");
            body.Append("\"options\":{\"temperature\":0},");
            body.Append("\"messages\":[");
            body.AppendFormat("{{\"role\":\"system\",\"content\":{0}}},", ToJson(systemMsg + "\nReturn only a single compact JSON object."));
            body.AppendFormat("{{\"role\":\"user\",\"content\":{0}}}", ToJson(userMsg));
            body.Append("]}");

            var url = string.IsNullOrWhiteSpace(_config.endpoint)
                ? "http://localhost:11434/api/chat"
                : _config.endpoint.TrimEnd('/');
            if (!url.Contains("/api/"))
                url += "/api/chat";

            using var req = new UnityWebRequest(url, "POST");
            var payload = Encoding.UTF8.GetBytes(body.ToString());
            req.uploadHandler = new UploadHandlerRaw(payload);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone) 
                await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[AI LD] Ollama HTTP error {req.responseCode}: {req.error}\n{req.downloadHandler.text}");
                return null;
            }

            var raw = req.downloadHandler.text;
            var content = ExtractJsonFromOllamaResponse(raw);
            content = StripCodeFences(content)?.Trim();
            content = UnwrapIfQuotedJson(content);
            content = FixJsonCommas(content);
            var candidate = ExtractFirstLayoutJson(content);
            if (candidate != null) content = candidate;
            if (!LooksLikeLayout(content)) content = TryRepairTruncatedJson(content);

            try
            {
                var layout = JsonUtility.FromJson<LayoutData>(content);
                if (layout != null && layout.objects != null && layout.objects.Count > 0) return layout;
            }
            catch {}

            try
            {
                var tolerant = TolerantParseLayoutData(content);
                if (tolerant != null && tolerant.objects != null && tolerant.objects.Count > 0) return tolerant;
            }
            catch (Exception ex)
            {
                Debug.LogError("[AI LD] Tolerant parse failed: " + ex);
            }

            Debug.LogError("[AI LD] Error parsing LayoutData (both strict and tolerant). Content:\n" + content);
            return null;
        }

        static string ExtractJsonFromOllamaResponse(string raw)
        {
            if (string.IsNullOrEmpty(raw)) 
                return null;
            try
            {
                var root = JsonUtility.FromJson<OllamaChatResponse>(raw);
                if (!string.IsNullOrEmpty(root?.message?.content))
                    return root.message.content;
            }
            catch {}

            var start = raw.IndexOf('{');
            var end = raw.LastIndexOf('}');
            if (start >= 0 && end > start)
                return raw.Substring(start, end - start + 1);
            return raw;
        }

        static string StripCodeFences(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.StartsWith("```"))
            {
                var i = s.IndexOf('\n');
                var j = s.LastIndexOf("```");
                if (i >= 0 && j > i) return s.Substring(i + 1, j - i - 1);
            }
            return s;
        }

        static string UnwrapIfQuotedJson(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            s = s.Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
            {
                var inner = s.Substring(1, s.Length - 2);
                inner = inner.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");
                var pruned = PruneAfterBalancedJson(inner);
                return string.IsNullOrEmpty(pruned) ? inner : pruned;
            }
            return s;
        }

        static string FixJsonCommas(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return s;
            s = Regex.Replace(s, @"}\s*{", "},{");
            s = Regex.Replace(s, @",\s*([}\]])", "$1");
            return s;
        }

        static string ExtractFirstLayoutJson(string s)
        {
            if (string.IsNullOrEmpty(s)) 
                return null;
            var start = s.IndexOf('{');
            if (start < 0) return null;
            var inStr = false;
            var esc = false;
            var depth = 0;
            for (var i = start; i < s.Length; i++)
            {
                var ch = s[i];
                if (inStr)
                {
                    if (esc) esc = false;
                    else if (ch == '\\') esc = true;
                    else if (ch == '"') inStr = false;
                    continue;
                }

                if (ch == '"')
                {
                    inStr = true; 
                    continue;
                }

                if (ch == '{')
                {
                    if (depth == 0) 
                        start = i; 
                    depth++; 
                    continue;
                }
                if (ch == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        var candidate = s.Substring(start, i - start + 1);
                        if (LooksLikeLayout(candidate)) return candidate;
                    }
                }
            }
            return null;
        }

        static string PruneAfterBalancedJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var start = s.IndexOf('{');
            if (start < 0) return s;
            var depth = 0;
            var inStr = false;
            var esc = false;
            for (var i = start; i < s.Length; i++)
            {
                var ch = s[i];
                if (inStr)
                {
                    if (esc) esc = false;
                    else if (ch == '\\') esc = true;
                    else if (ch == '"') inStr = false;
                }
                else
                {
                    if (ch == '"') inStr = true;
                    else if (ch == '{') depth++;
                    else if (ch == '}')
                    {
                        depth--;
                        if (depth == 0) return s.Substring(start, i - start + 1);
                    }
                }
            }
            return null;
        }

        static string TryRepairTruncatedJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var start = s.IndexOf('{');
            if (start < 0) return s;
            var sb = new StringBuilder();
            var depthObj = 0;
            var depthArr = 0;
            var inStr = false;
            var esc = false;
            for (var i = start; i < s.Length; i++)
            {
                var ch = s[i];
                sb.Append(ch);
                if (inStr)
                {
                    if (esc) esc = false;
                    else if (ch == '\\') esc = true;
                    else if (ch == '"') inStr = false;
                    continue;
                }
                if (ch == '"') inStr = true;
                else if (ch == '{') depthObj++;
                else if (ch == '}') depthObj = Mathf.Max(0, depthObj - 1);
                else if (ch == '[') depthArr++;
                else if (ch == ']') depthArr = Mathf.Max(0, depthArr - 1);
            }
            while (depthArr-- > 0)
                sb.Append(']');
            while (depthObj-- > 0)
                sb.Append('}');
            return sb.ToString();
        }

        static bool LooksLikeLayout(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return false;
            var t = s.TrimStart();
            return t.StartsWith("{") && (t.Contains("\"objects\"") || t.Contains("\"gameType\""));
        }

        static LayoutData TolerantParseLayoutData(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;
            
            var layout = new LayoutData();
            layout.gameType = ExtractGameType(s) ?? "";
            var rxObj = new Regex("\\{\\s*\"id\"\\s*:\\s*\"(?<id>[^\"]+)\"[\\s\\S]*?\"position\"\\s*:\\s*\\{\\s*\"x\"\\s*:\\s*(?<x>-?\\d+(?:\\.\\d+)?)[^\\d-]+\"y\"\\s*:\\s*(?<y>-?\\d+(?:\\.\\d+)?)[^\\d-]+\"z\"\\s*:\\s*(?<z>-?\\d+(?:\\.\\d+)?)\\s*\\}", RegexOptions.Singleline);
            
            var matches = rxObj.Matches(s);
            foreach (Match m in matches)
            {
                var id = m.Groups["id"].Value;
                if (string.IsNullOrWhiteSpace(id)) continue;
                var x = ParseF(m.Groups["x"].Value);
                var y = ParseF(m.Groups["y"].Value);
                var z = ParseF(m.Groups["z"].Value);
                var o = new LayoutObject { id = id, position = new Vector3(x, y, z) };
                var slice = m.Value;

                var rot = ExtractVec3(slice, "rotationEuler") ?? ExtractVec3(slice, "rotation");
                if (rot.HasValue) 
                    o.rotation = rot.Value;

                var scObj = ExtractVec3(slice, "scale");
                if (scObj.HasValue)
                    o.scale = scObj.Value;

                layout.objects.Add(o);
            }
            return layout;
        }

        static string ExtractGameType(string s)
        {
            var m = Regex.Match(s ?? "", "\"gameType\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        static Vector3? ExtractVec3(string src, string key)
        {
            var m = Regex.Match(src ?? "", $"\"{Regex.Escape(key)}\"\\s*:\\s*\\{{\\s*\"x\"\\s*:\\s*(?<x>-?\\d+(?:\\.\\d+)?)[^\\d-]+\"y\"\\s*:\\s*(?<y>-?\\d+(?:\\.\\d+)?)[^\\d-]+\"z\"\\s*:\\s*(?<z>-?\\d+(?:\\.\\d+)?)\\s*\\}}", RegexOptions.Singleline);
            if (!m.Success) 
                return null;
            
            var x = ParseF(m.Groups["x"].Value);
            var y = ParseF(m.Groups["y"].Value);
            var z = ParseF(m.Groups["z"].Value);
            return new Vector3(x, y, z);
        }

        static float ParseF(string s)
        {
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
            if (float.TryParse(s, out v))
                return v;
            return 0f;
        }

        static string Escape(string s)
        {
            if (s == null)
                return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        static string ToJson(string s) => $"\"{Escape(s)}\"";

        [Serializable]
        class OllamaChatResponse
        {
            public OllamaMessage message;
        }

        [Serializable]
        class OllamaMessage
        {
            public string role; 
            public string content;
        }
    }
}

using System.Text;
using System.Threading.Tasks;
using AILevelDesigner.Configs;
using UnityEngine;
using UnityEngine.Networking;

namespace AILevelDesigner
{
    public class OpenAIClient : IAIClient
    {
        private readonly AIConfig _config;

        public OpenAIClient(AIConfig config)
        {
            _config = config;
        }

        public async Task<LayoutData> GenerateLayoutAsync(string prompt, string capabilitiesJson, string schemaJson)
        {
            var systemMsg = PromptBuilder.BuildSystemMessage(string.IsNullOrWhiteSpace(_config.systemPromptHint)
                ? schemaJson : (schemaJson + "\n\n-- HINTS --\n" + _config.systemPromptHint));
            var userMsg = PromptBuilder.BuildUserMessage(prompt ?? string.Empty, capabilitiesJson ?? "{}");

            var body = BuildResponsesBody(_config.openAIModel, systemMsg, userMsg, schemaJson);

            var url = string.IsNullOrWhiteSpace(_config.endpoint)
                ? "https://api.openai.com/v1/responses"
                : _config.endpoint.Trim();

            using var req = new UnityWebRequest(url, "POST");
            var payload = Encoding.UTF8.GetBytes(body);
            req.uploadHandler = new UploadHandlerRaw(payload);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + _config.apiKey);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"[AI LD] OpenAI HTTP error {req.responseCode}: {req.error}\n{req.downloadHandler.text}");
                return null;
            }

            var raw = req.downloadHandler.text;

            var content = ExtractOutputText(raw);

            if (string.IsNullOrEmpty(content))
                content = ExtractFirstJson(raw);

            if (string.IsNullOrEmpty(content))
            {
                Debug.LogError("[AI LD] Could not extract JSON from result. Raw:\n" + raw);
                return null;
            }

            content = StripFences(content).Trim();

            try
            {
                var layout = JsonUtility.FromJson<LayoutData>(content);
                return layout;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[AI LD] Error parsing LayoutData: " + ex + "\nContent:\n" + content);
                return null;
            }
        }

        private static string BuildResponsesBody(string model, string systemMsg, string userMsg, string schemaJson)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.AppendFormat("\"model\":\"{0}\",", Escape(model));
            sb.Append("\"temperature\":0,");
            sb.Append("\"input\":[");
            sb.AppendFormat("{{\"role\":\"system\",\"content\":{0}}},", ToJson(systemMsg));
            sb.AppendFormat("{{\"role\":\"user\",\"content\":{0}}}", ToJson(userMsg));
            sb.Append("],");

            if (!string.IsNullOrWhiteSpace(schemaJson))
            {
                sb.Append("\"response_format\":{");
                sb.Append("\"type\":\"json_schema\",");
                sb.Append("\"json_schema\":{");
                sb.Append("\"name\":\"layout_v1\",");
                sb.Append("\"schema\":");
                sb.Append(schemaJson);
                sb.Append("}}");
            }
            else
            {
                sb.Append("\"response_format\":{\"type\":\"json_object\"}");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static string ExtractOutputText(string raw)
        {
            const string key = "\"output_text\":";
            var i = raw.IndexOf(key);
            if (i < 0)
                return null;

            i += key.Length;

            while (i < raw.Length && char.IsWhiteSpace(raw[i]))
                i++;
            if (i >= raw.Length || raw[i] != '\"')
                return null;

            i++;

            var sb = new StringBuilder();
            bool esc = false;
            for (; i < raw.Length; i++)
            {
                var c = raw[i];
                if (esc)
                {
                    sb.Append(c);
                    esc = false;
                    continue;
                }

                if (c == '\\')
                {
                    esc = true;
                    continue;
                }

                if (c == '\"')
                    break;
                sb.Append(c);
            }

            return sb.ToString();
        }

        private static string ExtractFirstJson(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;
            int start = s.IndexOf('{');
            int end = s.LastIndexOf('}');
            if (start >= 0 && end > start)
                return s.Substring(start, end - start + 1);

            return null;
        }

        private static string StripFences(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            if (s.StartsWith("```"))
            {
                var i = s.IndexOf('\n');
                var j = s.LastIndexOf("```");
                if (i >= 0 && j > i) return s.Substring(i + 1, j - (i + 1));
            }

            return s;
        }

        private static string Escape(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
        private static string ToJson(string s) => $"\"{Escape(s)}\"";
    }
}

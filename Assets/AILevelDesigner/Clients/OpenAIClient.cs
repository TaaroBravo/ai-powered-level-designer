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
            var systemMsg = PromptBuilder.BuildSystemMessage(schemaJson);
            var userMsg = PromptBuilder.BuildUserMessage(prompt, capabilitiesJson);

            var body = new StringBuilder();
            body.Append("{");
            body.AppendFormat("\"model\":\"{0}\",", Escape(_config.openAIModel));
            body.Append("\"response_format\":{\"type\":\"json_object\"},");
            body.Append("\"messages\":[");
            body.AppendFormat("{{\"role\":\"system\",\"content\":{0}}},", ToJsonString(systemMsg));
            body.AppendFormat("{{\"role\":\"user\",\"content\":{0}}}", ToJsonString(userMsg));
            body.Append("]}");

            using var req = new UnityWebRequest(
                string.IsNullOrWhiteSpace(_config.endpoint) ? "https://api.openai.com/v1/chat/completions" : _config.endpoint,
                "POST");
            
            byte[] payload = Encoding.UTF8.GetBytes(body.ToString());
            req.uploadHandler = new UploadHandlerRaw(payload);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + _config.apiKey);

            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"[AI LD] OpenAI HTTP error {req.responseCode}: {req.error}\n{req.downloadHandler.text}");
                return null;
            }

            var json = req.downloadHandler.text;
            string content = ExtractContent(json);
            
            if (string.IsNullOrEmpty(content))
            {
                Debug.LogError("[AI LD] No response. Raw: " + json);
                return null;
            }
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

        private static string Escape(string s) =>
            s?.Replace("\\", "\\\\")
                .Replace("\"", "\\\"") ?? "";

        private static string ToJsonString(string s) => $"\"{Escape(s)}\"";

        private static string ExtractContent(string raw)
        {
            const string anchor = "\"content\":";
            int i = raw.IndexOf(anchor);
            if (i < 0)
                return null;

            i += anchor.Length;

            while (i < raw.Length && char.IsWhiteSpace(raw[i]))
                i++;

            if (i >= raw.Length || raw[i] != '\"')
                return null;
            i++;

            var sb = new StringBuilder();
            bool esc = false;
            for (; i < raw.Length; i++)
            {
                char c = raw[i];
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
    }
}

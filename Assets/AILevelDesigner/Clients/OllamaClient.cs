using System;
using System.Text;
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
            body.AppendFormat("{{\"role\":\"system\",\"content\":{0}}},", ToJsonString(systemMsg));
            body.AppendFormat("{{\"role\":\"user\",\"content\":{0}}}", ToJsonString(userMsg));
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
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[AI LD] Ollama HTTP error {req.responseCode}: {req.error}\n{req.downloadHandler.text}");
                return null;
            }

            var raw = req.downloadHandler.text;

            var resp = JsonUtility.FromJson<OllamaChatResponse>(raw);
            var content = resp?.message?.content;

            if (string.IsNullOrEmpty(content))
            {
                content = ExtractJsonSubstring(raw);
                if (string.IsNullOrEmpty(content))
                {
                    Debug.LogError("[AI LD] No response from Ollama. Raw:\n" + raw);
                    return null;
                }
            }

            content = StripCodeFences(content).Trim();

            try
            {
                var layout = JsonUtility.FromJson<LayoutData>(content);
                return layout;
            }
            catch (Exception ex)
            {
                Debug.LogError("[AI LD] Error parsing LayoutData from Ollama: " + ex + "\nContent:\n" + content);
                return null;
            }
        }

        private static string Escape(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
        private static string ToJsonString(string s) => $"\"{Escape(s)}\"";

        private static string StripCodeFences(string s)
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

        private static string ExtractJsonSubstring(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            int start = s.IndexOf('{');
            int end = s.LastIndexOf('}');

            if (start >= 0 && end > start)
                return s.Substring(start, end - start + 1);

            return null;
        }
        
        [Serializable]
        private class OllamaChatResponse
        {
            public OllamaMessage message;
        }

        [Serializable]
        private class OllamaMessage
        {
            public string role;
            public string content;
        }
    }
}
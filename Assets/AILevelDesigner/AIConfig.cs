using UnityEngine;

namespace AILevelDesigner.Configs
{
    [CreateAssetMenu(fileName = "AIConfig", menuName = "AILevelDesigner/AI Config")]
    public class AIConfig : ScriptableObject
    {
        [Header("Provider")]
        public AIProvider aiProvider = AIProvider.OpenAI;
        public string openAIModel = "gpt-4o-mini";
        public string endpoint = "https://api.openai.com/v1/responses";
        [TextArea] public string systemPromptHint;

        [Header("Auth")]
        public string apiKey;

        public enum AIProvider
        {
            Fake, 
            OpenAI, 
            Ollama
        }
    }
}
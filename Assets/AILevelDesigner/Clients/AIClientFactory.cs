using AILevelDesigner.Configs;

namespace AILevelDesigner
{
    public class AIClientFactory
    {
        public static IAIClient Create(AIConfig config)
        {
            return config.aiProvider switch
            {
                AIConfig.AIProvider.Fake => new FakeClient(),
                AIConfig.AIProvider.OpenAI => new OpenAIClient(config),
                AIConfig.AIProvider.Ollama => new OllamaClient(config),
                _ => new FakeClient()
            };
        }
    }
}
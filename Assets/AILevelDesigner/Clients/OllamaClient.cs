using System.Threading.Tasks;
using AILevelDesigner.Configs;

namespace AILevelDesigner
{
    public class OllamaClient : IAIClient
    {
        private readonly AIConfig _config;

        public OllamaClient(AIConfig config)
        {
            _config = config;
        }

        public Task<LayoutData> GenerateLayoutAsync(string prompt, string capabilitiesJson, string schemaHint)
        {
            throw new System.NotImplementedException();
        }
    }
}
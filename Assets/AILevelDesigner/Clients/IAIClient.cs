using System.Threading.Tasks;

namespace AILevelDesigner
{
    public interface IAIClient
    {
        Task<LayoutData> GenerateLayoutAsync(string prompt, string capabilitiesJson, string schemaHint);
    }
}
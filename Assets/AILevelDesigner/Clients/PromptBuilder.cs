using System.Text;

namespace AILevelDesigner
{
    public class PromptBuilder
    {
        public static string BuildSystemMessage(string schemaJson)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You generate level layouts for a Unity editor tool.");
            sb.AppendLine("Output requirements:");
            sb.AppendLine("- Return ONLY a single JSON object. No prose, no markdown, no code fences.");
            sb.AppendLine("- The JSON MUST validate against the JSON Schema provided below.");
            sb.AppendLine("- Use ONLY object IDs present in the provided catalog.");
            sb.AppendLine("- Do NOT invent new fields or properties.");
            sb.AppendLine("- Units: meters. Axis: Y is up.");
            sb.AppendLine();
            sb.AppendLine("JSON Schema:");
            sb.AppendLine(string.IsNullOrWhiteSpace(schemaJson) ? "{}" : schemaJson);
            return sb.ToString();
        }

        public static string BuildUserMessage(string userPrompt, string capabilitiesJson)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Catalog and constraints (capabilities):");
            sb.AppendLine(string.IsNullOrWhiteSpace(capabilitiesJson) ? "{}" : capabilitiesJson);
            sb.AppendLine();
            sb.AppendLine("User request:");
            sb.AppendLine(userPrompt ?? string.Empty);
            sb.AppendLine();
            sb.AppendLine("Important:");
            sb.AppendLine("- gameType must equal capabilities.gameType.");
            sb.AppendLine("- theme must be one of capabilities.allowedThemes; if none fits, use \"default\".");
            sb.AppendLine("- Every object must include:");
            sb.AppendLine("  • id  (must exist in catalog)");
            sb.AppendLine("  • position { x, y, z }");
            sb.AppendLine("- Respect maxPerLevel for each id.");
            sb.AppendLine();
            sb.AppendLine("Return only the JSON object; do not wrap it in any extra characters.");
            return sb.ToString();
        }
    }
}
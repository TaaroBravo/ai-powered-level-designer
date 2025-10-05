using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using AILevelDesigner;
using AILevelDesigner.Building;
using AILevelDesigner.Configs;
using AILevelDesigner.Profiles;
using AILevelDesigner.Validation;

public class AILevelDesignerWindow : EditorWindow
{
    [SerializeField] private AIConfig config;
    [SerializeField] private GameTypeProfile profile;

    [SerializeField] private string prompt =
        "Create a small desert arena with 2 enemy spawners, a health pickup, and some crates as cover.";

    [SerializeField, TextArea(6, 20)] private string jsonPreview;

    private LayoutData _lastLayout;

    [MenuItem("Tools/AI Level Designer")]
    public static void ShowWindow() => GetWindow<AILevelDesignerWindow>("AI Level Designer");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("AI Level Designer", EditorStyles.boldLabel);
        config = (AIConfig) EditorGUILayout.ObjectField("AI Config", config, typeof(AIConfig), false);
        profile = (GameTypeProfile) EditorGUILayout.ObjectField("Game Type Profile", profile, typeof(GameTypeProfile),
            false);

        EditorGUILayout.LabelField("Prompt");
        prompt = EditorGUILayout.TextArea(prompt, GUILayout.MinHeight(60));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate (AI/Fake)"))
            _ = GenerateAsync();

        EditorGUI.BeginDisabledGroup(_lastLayout == null);

        if (GUILayout.Button("Build Scene"))
            BuildScene();

        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("JSON (preview)");
        EditorGUI.BeginDisabledGroup(true);
        jsonPreview = EditorGUILayout.TextArea(jsonPreview, GUILayout.MinHeight(120));
        EditorGUI.EndDisabledGroup();

        if (_lastLayout != null && profile != null)
        {
            var res = LayoutValidator.Validate(_lastLayout, profile);
            EditorGUILayout.HelpBox(res.message, res.ok ? MessageType.Info : MessageType.Error);
        }
    }

    private async Task GenerateAsync()
    {
        if (config == null || profile == null)
        {
            ShowNotification(new GUIContent("Set AI Config & Profile first."));
            return;
        }

        try
        {
            string capabilitiesJson;
            
            try
            {
                capabilitiesJson = BuildCapabilitiesJsonSafe(profile);
            }
            catch (System.Exception buildEx)
            {
                Debug.LogWarning($"[AI LD] Capabilities build failed, fallback to empty JSON. Details: {buildEx}");
                capabilitiesJson = "{}";
            }
            
            var schemaAsset = Resources.Load<TextAsset>("AILevelDesigner/Schemas/layout_v1");
            var schemaJson  = schemaAsset != null ? schemaAsset.text : "";
            var client = AIClientFactory.Create(config);

            _lastLayout = await client.GenerateLayoutAsync(prompt, capabilitiesJson,schemaJson) ?? new LayoutData
            {
                gameType = profile.gameTypeId ?? "unknown",
                theme = "default",
                objects = new System.Collections.Generic.List<LayoutObject>()
            };

            jsonPreview = JsonUtility.ToJson(_lastLayout, true);
            Repaint();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[AI Level Designer] Generate failed: {ex}");
            jsonPreview = $"ERROR: {ex.Message}";
            _lastLayout = null;
            Repaint();
        }
    }

    private static string BuildCapabilitiesJsonSafe(GameTypeProfile profile)
    {
        var caps = new CapabilitiesDescriptor
        {
            gameType = profile.gameTypeId ?? string.Empty,
            allowedThemes = profile.allowedThemes ?? System.Array.Empty<string>()
        };

        var entries = profile.catalog?.entries ?? new System.Collections.Generic.List<Entry>();

        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.id)) continue;

            caps.objects.Add(new CatalogItemDescriptor
            {
                id = e.id,
                maxPerLevel = e.maxPerLevel,
                tags = e.tags ?? System.Array.Empty<string>()
            });
        }

        return JsonUtility.ToJson(caps, true);
    }

    private void BuildScene()
    {
        if (_lastLayout == null || profile == null)
            return;

        var res = LayoutValidator.Validate(_lastLayout, profile);
        if (!res.ok)
        {
            ShowNotification(new GUIContent("Layout invalid: " + res.message));
            return;
        }

        var parent = new GameObject("AILevel").transform;
        SceneBuilder.Build(_lastLayout, profile, parent);
        Selection.activeTransform = parent;
    }
}
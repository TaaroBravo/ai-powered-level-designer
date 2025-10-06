using System;
using System.Collections.Generic;
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
    private bool _isGenerating;
    private float _spinnerAngle;
    private double _lastTime;

    [MenuItem("Tools/AI Level Designer")]
    public static void ShowWindow() => GetWindow<AILevelDesignerWindow>("AI Level Designer");

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("AI Level Designer", EditorStyles.boldLabel);
        EditorGUILayout.Space(3);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            config = (AIConfig)EditorGUILayout.ObjectField("AI Config", config, typeof(AIConfig), false);
            profile = (GameTypeProfile)EditorGUILayout.ObjectField("Game Type Profile", profile, typeof(GameTypeProfile), false);
        }

        EditorGUILayout.Space(5);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Prompt", EditorStyles.boldLabel);
            prompt = EditorGUILayout.TextArea(prompt, GUILayout.MinHeight(60));

            EditorGUILayout.Space(5);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(_isGenerating);
                if (GUILayout.Button("Generate Layout", GUILayout.Height(30)))
                    _ = GenerateAsync();
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(_lastLayout == null || _isGenerating);
                if (GUILayout.Button("Build Scene", GUILayout.Height(30)))
                    BuildScene();
                EditorGUI.EndDisabledGroup();
            }

            if (_isGenerating)
            {
                DrawLoadingBar("Generating layout...");
                Repaint();
            }
        }

        EditorGUILayout.Space(8);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("JSON Preview", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            jsonPreview = EditorGUILayout.TextArea(jsonPreview, GUILayout.MinHeight(120));
            EditorGUI.EndDisabledGroup();
        }

        EditorGUILayout.Space(8);

        if (_lastLayout != null && profile != null)
        {
            var res = LayoutValidator.Validate(_lastLayout, profile);
            var type = res.ok ? MessageType.Info : MessageType.Error;
            EditorGUILayout.HelpBox(res.message, type);
        }
    }

    private async Task GenerateAsync()
    {
        if (config == null || profile == null)
        {
            ShowNotification(new GUIContent("Set AI Config & Profile first."));
            return;
        }

        _isGenerating = true;
        jsonPreview = "Generating...";
        _lastLayout = null;
        Repaint();

        try
        {
            string capabilitiesJson;
            try
            {
                capabilitiesJson = BuildCapabilitiesJsonSafe(profile);
            }
            catch (Exception buildEx)
            {
                Debug.LogWarning($"[AI LD] Capabilities build failed, fallback to empty JSON. Details: {buildEx}");
                capabilitiesJson = "{}";
            }

            var schemaAsset = Resources.Load<TextAsset>("AILevelDesigner/Schemas/layout_v1");
            var schemaJson = schemaAsset != null ? schemaAsset.text : "";
            var client = AIClientFactory.Create(config);

            _lastLayout = await client.GenerateLayoutAsync(prompt, capabilitiesJson, schemaJson) ?? new LayoutData
            {
                gameType = profile.gameTypeId ?? "unknown",
                theme = "default",
                objects = new List<LayoutObject>()
            };

            _lastLayout = LayoutSanitizer.PruneToCatalogCaps(_lastLayout, profile);
            jsonPreview = JsonUtility.ToJson(_lastLayout, true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AI Level Designer] Generate failed: {ex}");
            jsonPreview = $"ERROR: {ex.Message}";
            _lastLayout = null;
        }
        finally
        {
            _isGenerating = false;
            Repaint();
        }
    }

    private static string BuildCapabilitiesJsonSafe(GameTypeProfile profile)
    {
        var caps = new CapabilitiesDescriptor
        {
            gameType = profile.gameTypeId ?? string.Empty,
            worldDescription = profile.worldDescription ?? "",
            coordinateSpace = profile.coordinateSpace.ToString(),
            worldScale = profile.worldScale,
            cellSize = profile.cellSize,
            gridWidth = profile.gridWidth,
            gridHeight = profile.gridHeight
        };

        var entries = profile.catalog?.entries ?? new List<Entry>();
        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.id))
                continue;
            caps.objects.Add(new CatalogItemDescriptor
            {
                id = e.id,
                maxPerLevel = e.maxPerLevel,
                tags = e.tags ?? Array.Empty<string>()
            });
        }

        return JsonUtility.ToJson(caps, true);
    }

    private void BuildScene()
    {
        if (_lastLayout == null || profile == null)
            return;

        _lastLayout = LayoutSanitizer.PruneToCatalogCaps(_lastLayout, profile);
        var res = LayoutValidator.Validate(_lastLayout, profile);

        if (!res.ok)
        {
            ShowNotification(new GUIContent("Layout invalid: " + res.message));
            return;
        }

        var prev = GameObject.Find("AILevel");
        if (prev)
            DestroyImmediate(prev);

        var parent = new GameObject("AILevel").transform;
        SceneBuilder.Build(_lastLayout, profile, parent);
        Selection.activeTransform = parent;

        ShowNotification(new GUIContent("Scene built successfully!"));
    }

    private void DrawLoadingBar(string message)
    {
        _spinnerAngle += (float)(EditorApplication.timeSinceStartup - _lastTime) * 360f;
        _lastTime = EditorApplication.timeSinceStartup;
        var spinnerRect = GUILayoutUtility.GetRect(18, 18, GUILayout.ExpandWidth(false));
        var r = spinnerRect;
        r.x += (position.width / 2f) - 10;
        r.y += 4;

        Handles.BeginGUI();
        Handles.color = Color.gray;
        Handles.DrawSolidArc(new Vector3(r.x, r.y, 0), Vector3.forward, Vector3.right, _spinnerAngle % 360f, 10f);
        Handles.EndGUI();

        GUILayout.Space(4);
        EditorGUILayout.LabelField(message, EditorStyles.centeredGreyMiniLabel);
    }
}

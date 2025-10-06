using NUnit.Framework;
using UnityEngine;
using AILevelDesigner;
using AILevelDesigner.Building;
using AILevelDesigner.Configs;
using AILevelDesigner.Profiles;
using AILevelDesigner.Validation;
using System.Collections.Generic;

public class AILevelDesignerTests
{
    [Test]
    public void LayoutData_ShouldParse_ValidJson()
    {
        var json = @"{
            ""schemaVersion"": ""1.0.0"",
            ""gameType"": ""arena-3d"",
            ""objects"": [
                { ""id"": ""Crate"", ""position"": { ""x"": 1, ""y"": 0, ""z"": 2 } },
                { ""id"": ""EnemySpawner.Basic"", ""position"": { ""x"": 0, ""y"": 0, ""z"": 0 } }
            ]
        }";

        var layout = JsonUtility.FromJson<LayoutData>(json);
        Assert.IsNotNull(layout);
        Assert.AreEqual("arena-3d", layout.gameType);
        Assert.AreEqual(2, layout.objects.Count);
    }

    [Test]
    public void LayoutValidator_ShouldDetectMissingObjects()
    {
        var profile = ScriptableObject.CreateInstance<GameTypeProfile>();
        profile.gameTypeId = "arena-3d";
        profile.catalog = ScriptableObject.CreateInstance<PrefabCatalog>();
        profile.catalog.entries = new List<Entry>
        {
            new Entry { id = "Crate" }
        };

        var layout = new LayoutData
        {
            gameType = "arena-3d",
            objects = new List<LayoutObject>
            {
                new LayoutObject { id = "Crate", position = Vector3.zero },
                new LayoutObject { id = "MissingThing", position = Vector3.one }
            }
        };

        var result = LayoutValidator.Validate(layout, profile);
        Assert.IsFalse(result.ok);
        Assert.IsTrue(result.message.Contains("MissingThing"));
    }

    [Test]
    public void SceneBuilder_ShouldInstantiate_Objects()
    {
        var profile = ScriptableObject.CreateInstance<GameTypeProfile>();
        profile.coordinateSpace = CoordinateSpace.World;
        profile.catalog = ScriptableObject.CreateInstance<PrefabCatalog>();
        profile.catalog.entries = new List<Entry>
        {
            new Entry { id = "Crate", prefab = GameObject.CreatePrimitive(PrimitiveType.Cube) }
        };

        var layout = new LayoutData
        {
            objects = new List<LayoutObject>
            {
                new LayoutObject { id = "Crate", position = new Vector3(1, 0, 1) }
            }
        };

        SceneBuilder.Build(layout, profile);
        var obj = GameObject.Find("Crate");
        Assert.IsNotNull(obj);
        Object.DestroyImmediate(obj);
    }

    [Test]
    public void OpenAIClient_FixJsonCommas_ShouldCleanBrokenJson()
    {
        var dirtyJson = "{\"a\":1,}{\"b\":2}";
        var clean = typeof(OpenAIClient)
            .GetMethod("FixJsonCommas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Invoke(null, new object[] { dirtyJson }) as string;

        Assert.IsTrue(clean.Contains("},{"));
        Assert.IsFalse(clean.Contains(",}"));
    }
}

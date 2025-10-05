using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace AILevelDesigner
{
    public class FakeClient : IAIClient
    {
        public Task<LayoutData> GenerateLayoutAsync(string prompt, string capabilitiesJson, string schemaHint)
        {
            var data = new LayoutData
            {
                gameType = "arena-3d",
                theme = "desert",
                objects = new List<LayoutObject>()
                {
                    new() { id="EnemySpawner.Basic", position = new Vector3(2,0,5)},
                    new() { id="Pickup.HealthSmall", position = new Vector3(0,0,-3)},
                    new() { id="Cover.CrateSmall", position = new Vector3(-4,0,2)}
                }
            };
            return Task.FromResult(data);
        }
    }
}
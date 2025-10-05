using UnityEngine;

namespace AILevelDesigner.Profiles
{
    [CreateAssetMenu(fileName = "GameTypeProfile", menuName = "AILevelDesigner/Game Type Profile")]
    public class GameTypeProfile : ScriptableObject
    {
        public string gameTypeId = "arena-3d";
        public CoordinateSpace coordinateSpace = CoordinateSpace.World;
        public PrefabCatalog catalog;
        public bool navmeshRequired = false;
        public string[] allowedThemes = new[] {"default","desert","forest","city"};
        
        [Header("World Settings")]
        public float worldScale = 1f;
        public Vector2 arenaSize = new Vector2(40,40);
        public bool generateGroundPlane = true;
        public Material groundMaterial;
        
        [Header("Grid Settings")]
        public float cellSize = 2f;
        public int gridWidth = 12;
        public int gridHeight = 8;
        public Vector3 gridOrigin = Vector3.zero;
    }

    public enum CoordinateSpace
    {
        World,
        Grid
    }
}
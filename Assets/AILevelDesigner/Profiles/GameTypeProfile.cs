using UnityEngine;

namespace AILevelDesigner.Profiles
{
    [CreateAssetMenu(fileName = "GameTypeProfile", menuName = "AILevelDesigner/Game Type Profile")]
    public class GameTypeProfile : ScriptableObject
    {
        public string gameTypeId = "arena-3d";
        public CoordinateSpace coordinateSpace = CoordinateSpace.World;
        public PrefabCatalog catalog;
        [Header("LLM Context")]
        [TextArea(3,8)] public string worldDescription;
        
        [Header("World Settings")]
        public float worldScale = 1f;
        public Vector2 arenaSize = new Vector2(40, 40);
        public bool generateGroundPlane = true;
        public Material groundMaterial;

        [Header("World Fit")]
        public bool autoFitToArena = true;
        [Range(0.1f, 1f)] public float fitMargin = 0.9f;
        public bool clampToArena = true;
        public float clampPadding = 0.5f; 
        public bool snapToStep = true;
        public float step = 2f;
        
        [Header("Grid Settings")]
        public float cellSize = 2f;
        public int gridWidth = 12;
        public int gridHeight = 8;
        public Vector3 gridOrigin = Vector3.zero;
        public GridOriginMode gridOriginMode = GridOriginMode.BottomLeft;
    }

    public enum CoordinateSpace
    {
        World,
        Grid
    }

    public enum GridOriginMode
    {
        BottomLeft,
        Center
    }

}
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
    }

    public enum CoordinateSpace
    {
        World,
        Grid
    }
}
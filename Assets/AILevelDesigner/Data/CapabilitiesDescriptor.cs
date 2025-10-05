using System;
using System.Collections.Generic;

[Serializable]
public class CapabilitiesDescriptor
{
    public string gameType;
    public string[] allowedThemes;
    public List<CatalogItemDescriptor> objects = new();
    public string coordinateSpace;
    public float cellSize;
    public int gridWidth, gridHeight;
    public float worldScale;
}

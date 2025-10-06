using System;
using System.Collections.Generic;

[Serializable]
public class CapabilitiesDescriptor
{
    public string gameType;
    public List<CatalogItemDescriptor> objects = new();
    public string worldDescription;
    public string coordinateSpace;
    public float worldScale;
    public float cellSize;
    public int gridWidth, gridHeight;
}
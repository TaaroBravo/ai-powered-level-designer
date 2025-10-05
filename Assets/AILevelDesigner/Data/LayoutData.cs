using System;
using System.Collections.Generic;

[Serializable]
public class LayoutData
{
    public string schemaVersion = "1.0.0";
    public string gameType;
    public string theme;
    public List<LayoutObject> objects = new List<LayoutObject>();
}
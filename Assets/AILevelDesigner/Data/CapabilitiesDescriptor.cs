using System;
using System.Collections.Generic;

[Serializable]
public class CapabilitiesDescriptor
{
    public string gameType;
    public string[] allowedThemes;
    public List<CatalogItemDescriptor> objects = new List<CatalogItemDescriptor>();
}
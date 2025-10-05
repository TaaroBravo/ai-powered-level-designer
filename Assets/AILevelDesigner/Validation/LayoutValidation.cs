using System.Collections.Generic;
using System.Linq;
using AILevelDesigner.Profiles;

namespace AILevelDesigner.Validation
{
    public static class LayoutValidator
    {
        public static Result Validate(LayoutData data, GameTypeProfile profile)
        {
            if (data == null)
                return Result.NullLayout();
            if (profile == null)
                return Result.NullProfile();
            if (!string.Equals(data.gameType, profile.gameTypeId, System.StringComparison.OrdinalIgnoreCase))
                return Result.GameTypeMismatch(data.gameType, profile.gameTypeId);
            
            var catalog = profile.catalog;
            if (catalog == null)
                return Result.NullCatalog();

            var counts = new Dictionary<string,int>();
            foreach (var o in data.objects)
            {
                if (string.IsNullOrWhiteSpace(o.id))
                    return Result.IdEmpty();
                if (!catalog.IsAllowed(o.id))
                    return Result.ObjectIdNotInCatalog(o.id);
                    
                counts.TryGetValue(o.id, out var counter);
                counter++; 
                counts[o.id] = counter;
                
                var entry = catalog.entries.First(e => e.id == o.id);
                if (counter > entry.maxPerLevel)
                    return Result.ObjectExceedsMaxPerLevel(o.id, entry.maxPerLevel);
            }

            return Result.OK;
        }
        
        public struct Result
        {
            public bool ok; 
            public string message;

            private Result(bool okValue, string msg)
            {
                ok = okValue;
                message = msg;
            }

            public static Result OK = new Result(true, "OK");
            
            public static Result NullLayout() => new Result(false, "NullLayout");
            public static Result NullProfile() => new Result(false, "Null Profile");
            public static Result NullCatalog() => new Result(false, "Null catalog");
            public static Result GameTypeMismatch(string dataGameType, string profileGameType) => new(false, $"gameType mismatch: {dataGameType} vs profile {profileGameType}");
            public static Result IdEmpty() => new Result(false, "Found object with empty id");
            public static Result ObjectIdNotInCatalog(string id) => new Result(false, $"Object id '{id}' not in catalog");
            public static Result ObjectExceedsMaxPerLevel(string id, int max) => new Result(false, $"Object '{id}' exceeds maxPerLevel {max}");
        }
    }
}
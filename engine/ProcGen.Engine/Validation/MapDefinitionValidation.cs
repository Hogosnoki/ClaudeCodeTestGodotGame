using System.Collections.Generic;

namespace ProcGen.Engine.Validation
{
    /// <summary>
    /// Shared duplicate-id check for anything that only needs to be unique within a single map
    /// (entrance ids, exit ids) rather than across the whole game. Living here -- not duplicated
    /// in the tool and the game -- guarantees both consumers enforce the same rule, same as
    /// <see cref="TransformationAxis"/>.
    /// </summary>
    public static class MapDefinitionValidation
    {
        public static bool TryFindDuplicateId(IEnumerable<string> ids, out string duplicate)
        {
            var seen = new HashSet<string>();
            foreach (var id in ids)
            {
                if (!seen.Add(id))
                {
                    duplicate = id;
                    return true;
                }
            }
            duplicate = "";
            return false;
        }
    }
}

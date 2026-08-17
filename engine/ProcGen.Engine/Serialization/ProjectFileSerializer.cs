#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ProcGen.Engine.Model;
using ProcGen.Engine.Overrides;
using ProcGen.Engine.Validation;

namespace ProcGen.Engine.Serialization
{
    /// <summary>
    /// Reads and writes the on-disk project file (JSON) -- every map in one game, together. This
    /// is deliberately the only reader/writer: the editor tool and the actual game must both call
    /// through here rather than each growing their own, since "the only two variables to
    /// reproducing the engine should be the configuration and the algorithm" only holds if both
    /// consumers agree on exactly what the configuration means. Tile display colors and other
    /// editor-only presentation state never belong in this format.
    /// </summary>
    public static class ProjectFileSerializer
    {
        public const int CurrentFormatVersion = 3;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        public static string Serialize(IEnumerable<(MapDefinition Map, OverrideStore Overrides)> maps)
        {
            if (maps == null) throw new ArgumentNullException(nameof(maps));

            var file = new ProjectFile
            {
                FormatVersion = CurrentFormatVersion,
                Maps = maps.Select(m => new MapEntry
                {
                    Map = m.Map,
                    Overrides = new List<TileOverride>(m.Overrides.Enumerate()),
                }).ToList(),
            };
            return JsonSerializer.Serialize(file, Options);
        }

        /// <summary>
        /// Parses a saved project file, validating map ids are present and unique across the
        /// project, and that each map's own entrance/exit ids are unique within that map.
        /// </summary>
        public static List<(MapDefinition Map, OverrideStore Overrides)> Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Project file is empty.");

            ProjectFile? file;
            try
            {
                file = JsonSerializer.Deserialize<ProjectFile>(json, Options);
            }
            catch (JsonException ex)
            {
                throw new FormatException($"Project file is not valid JSON: {ex.Message}", ex);
            }

            if (file == null || file.Maps == null || file.Maps.Count == 0)
            {
                throw new FormatException("Project file has no maps.");
            }

            var result = new List<(MapDefinition, OverrideStore)>(file.Maps.Count);
            var seenMapIds = new HashSet<string>();
            foreach (var entry in file.Maps)
            {
                if (entry.Map == null)
                {
                    throw new FormatException("Project file has a map entry with no map.");
                }
                if (string.IsNullOrEmpty(entry.Map.MapId))
                {
                    throw new FormatException("Every map in a project must have a map id.");
                }
                if (!seenMapIds.Add(entry.Map.MapId))
                {
                    throw new FormatException($"Duplicate map id '{entry.Map.MapId}' -- map ids must be unique within a project.");
                }

                ValidateMap(entry.Map);
                var overrides = OverrideStore.FromRecords(entry.Overrides ?? new List<TileOverride>());
                result.Add((entry.Map, overrides));
            }
            return result;
        }

        private static void ValidateMap(MapDefinition map)
        {
            if (MapDefinitionValidation.TryFindDuplicateId(map.Entrances.ConvertAll(e => e.Id), out var dupEntrance))
            {
                throw new FormatException($"Map '{map.MapId}': duplicate entrance id '{dupEntrance}' -- entrance ids must be unique within a map.");
            }
            if (MapDefinitionValidation.TryFindDuplicateId(map.Exits.ConvertAll(e => e.Id), out var dupExit))
            {
                throw new FormatException($"Map '{map.MapId}': duplicate exit id '{dupExit}' -- exit ids must be unique within a map.");
            }
            if (MapDefinitionValidation.TryFindDuplicateId(map.Variations.ConvertAll(v => v.Id), out var dupVariation))
            {
                throw new FormatException($"Map '{map.MapId}': duplicate variation id '{dupVariation}' -- variation ids must be unique within a map.");
            }
        }
    }
}

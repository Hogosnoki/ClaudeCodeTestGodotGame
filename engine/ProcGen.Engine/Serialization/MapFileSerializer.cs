#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using ProcGen.Engine.Model;
using ProcGen.Engine.Overrides;
using ProcGen.Engine.Validation;

namespace ProcGen.Engine.Serialization
{
    /// <summary>
    /// Reads and writes the on-disk map file (JSON). This is deliberately the only reader/writer --
    /// the editor tool and the actual game must both call through here rather than each growing
    /// their own, since "the only two variables to reproducing the engine should be the
    /// configuration and the algorithm" only holds if both consumers agree on exactly what the
    /// configuration means. Tile display colors and other editor-only presentation state never
    /// belong in this format: they're not part of the algorithm's input, so saving them here would
    /// blur the "these two things alone reproduce the map" contract.
    /// </summary>
    public static class MapFileSerializer
    {
        public const int CurrentFormatVersion = 1;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        public static string Serialize(MapDefinition map, OverrideStore overrides)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            overrides ??= new OverrideStore();

            var file = new MapFile
            {
                FormatVersion = CurrentFormatVersion,
                Map = map,
                Overrides = new List<TileOverride>(overrides.Enumerate()),
            };
            return JsonSerializer.Serialize(file, Options);
        }

        /// <summary>Parses a saved map file, validating the parts that would otherwise silently produce an ambiguous or broken map (missing map section, duplicate entrance/exit ids).</summary>
        public static (MapDefinition Map, OverrideStore Overrides) Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Map file is empty.");

            MapFile? file;
            try
            {
                file = JsonSerializer.Deserialize<MapFile>(json, Options);
            }
            catch (JsonException ex)
            {
                throw new FormatException($"Map file is not valid JSON: {ex.Message}", ex);
            }

            if (file == null || file.Map == null)
            {
                throw new FormatException("Map file is missing its 'map' section.");
            }

            ValidateMap(file.Map);

            var overrides = OverrideStore.FromRecords(file.Overrides ?? new List<TileOverride>());
            return (file.Map, overrides);
        }

        private static void ValidateMap(MapDefinition map)
        {
            if (MapDefinitionValidation.TryFindDuplicateId(map.Entrances.ConvertAll(e => e.Id), out var dupEntrance))
            {
                throw new FormatException($"Duplicate entrance id '{dupEntrance}' -- entrance ids must be unique within a map.");
            }
            if (MapDefinitionValidation.TryFindDuplicateId(map.Exits.ConvertAll(e => e.Id), out var dupExit))
            {
                throw new FormatException($"Duplicate exit id '{dupExit}' -- exit ids must be unique within a map.");
            }
        }
    }
}

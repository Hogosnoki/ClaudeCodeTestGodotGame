#nullable enable
using System.Collections.Generic;

namespace ProcGen.Engine.Model
{
    /// <summary>
    /// One layer's overrides within a <see cref="MapVariation"/>. Every field is optional --
    /// null means "inherit the base layer's value unchanged" -- so a variation only ever stores
    /// what actually diverges, not a full copy of the layer. <see cref="Tiles"/> is an
    /// all-or-nothing replacement of the base layer's tile list rather than a per-tile patch,
    /// since a variation is as likely to swap the whole palette (a season) as to nudge one tile's
    /// range, and a partial patch would need its own merge semantics for reordering/removal that
    /// aren't worth the complexity for what's meant to be a lightweight diff.
    /// </summary>
    public sealed class LayerVariation
    {
        /// <summary>Which base layer this overrides -- matches a <see cref="LayerDef.Id"/> on the owning map.</summary>
        public string LayerId { get; set; } = "";

        public SeedPosition? Seed { get; set; }
        public NoiseParams? Noise { get; set; }
        public List<TileDef>? Tiles { get; set; }
    }
}

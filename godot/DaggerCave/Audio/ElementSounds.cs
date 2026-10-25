using System;
using static DaggerCave.Synth;

namespace DaggerCave;

/// <summary>
/// The elements' sounds, engineered to sound like the real thing rather than played as notes: no held pitch anywhere, only noise shaped
/// the way fire, ice, air, earth, leaves, water and shadow actually sound (crackle, turbulence, crystal tinks, grit, gurgle, breath).
/// Every element has five events: <see cref="Ev.Charge"/> (readying it: fire flares, wind whooshes, frost crystallizes...), <see cref="Ev.Cast"/>
/// (an attack loosed), <see cref="Ev.Hit"/> (it meets a creature), <see cref="Ev.Wall"/> (it meets rock) and <see cref="Ev.Fizzle"/> (it runs
/// out and dissipates). Each sound is made from a seed: an ability's own name seeds its own variant (brighter or darker, longer or shorter,
/// denser or sparser), so every attack sounds like itself and like its element. A pair layers its second element under the first.
/// </summary>
public static class ElementSounds
{
    public enum Ev { Charge, Cast, Hit, Wall, Fizzle }

    /// <summary>A variant: how bright (scales every band), how long, how weighty (a pair or an overcharge is bigger), its own random.</summary>
    private sealed class V
    {
        public Random R;
        public float Bright, Len, Size;
        public int Seed;
        public float U() => (float)R.NextDouble();
        public int S(int k) => Seed * 7 + k * 131;
    }

    private static V Make(int seed, float weight)
    {
        var r = new Random(seed);
        return new V { R = r, Seed = seed, Bright = 0.78f + 0.5f * (float)r.NextDouble(), Len = (0.85f + 0.35f * (float)r.NextDouble()) * (0.8f + 0.3f * weight), Size = weight };
    }

    /// <summary>A stable hash (the same in every game, so a sound sent online is made alike there).</summary>
    public static int StableHash(string s)
    {
        unchecked
        {
            int h = (int)2166136261;
            foreach (char c in s) h = (h ^ c) * 16777619;
            return h & 0x7fffffff;
        }
    }

    private static float BaseLength(Ev ev, HeroElement el) => ev switch
    {
        Ev.Charge => 0.45f,
        Ev.Cast => el is HeroElement.Earth or HeroElement.Fire ? 0.65f : 0.55f,
        Ev.Hit => el is HeroElement.Earth or HeroElement.Frost ? 0.42f : 0.36f,
        Ev.Wall => 0.5f,
        _ => 0.55f,
    };

    /// <summary>How hard each element bites (the crunch it's driven through): stone most, then fire, ice, the rest.</summary>
    private static float Bite(HeroElement el, Ev ev)
    {
        float b = el switch { HeroElement.Earth => 0.6f, HeroElement.Fire => 0.4f, HeroElement.Frost => 0.3f, HeroElement.Nature => 0.3f, _ => 0.18f };
        return Math.Min(0.9f, b + (ev is Ev.Hit or Ev.Wall ? 0.2f : 0f));
    }

    /// <summary>The sound of <paramref name="ev"/> for an element (and a second under it), its weight, made from <paramref name="seed"/>.</summary>
    public static float[] Build(Ev ev, HeroElement a, HeroElement b, float weight, int seed)
    {
        var v = Make(seed, weight);
        float len = BaseLength(ev, a) * v.Len;
        var buf = Buf(len + 0.04f);
        Layer(buf, len, ev, a, v, 1f);
        if (b != HeroElement.None && b != a) Layer(buf, len, ev, b, Make(seed * 31 + 7, weight * 0.8f), 0.55f);
        Crunch(buf, Bite(a, ev));
        return buf;
    }

    private static Func<float, float> Env(Func<float, float> f, float g) => t => f(t) * g;

    private static void Layer(float[] buf, float L, Ev ev, HeroElement el, V v, float g)
    {
        float B = v.Bright, S = v.Size;
        switch (el)
        {
            // ---------------------------------------------------------------- fire: catching, roaring, crackling, sizzling
            case HeroElement.Fire:
                switch (ev)
                {
                    case Ev.Charge:
                        // a flare: the air catching (a swell of low breath), a lick of flame, a few crackles and a sizzle
                        Rumble(buf, 0, L, 150f * B, Env(AD(0.25f, 5f), g * 0.9f));
                        Whoosh(buf, 0, L, t => 450f * B + 1000f * B * t, Env(AD(0.3f, 4f), g * 0.8f), 0.9f, 0.7f, v.S(1));
                        Crackle(buf, 0.03f, L * 0.9f, Const(80f), t => g * (1f - t), 3200f * B, v.S(2), 1100f);
                        NoiseBurst(buf, 0.05f, L * 0.8f, Const(0.7f), t => g * 0.08f * MathF.Sin(t * MathF.PI), true);
                        break;
                    case Ev.Cast:
                        // the whoomp of it going, then the roar of flame with crackle running through it
                        Rumble(buf, 0, L * 0.5f, 110f * B, Env(AD(0.04f, 9f), g * (0.8f + 0.4f * S)));
                        Whoosh(buf, 0, L, t => 380f * B + 650f * B * MathF.Sin(t * MathF.PI), Env(AD(0.12f, 3.2f), g), 0.8f, 0.85f, v.S(1));
                        Crackle(buf, 0.02f, L, Const(110f * S + 40f * v.U()), t => g * (1f - 0.7f * t), 2700f * B, v.S(2), 900f);
                        NoiseBurst(buf, 0.04f, L * 0.9f, Const(0.75f), t => g * 0.07f * (1f - t), true);
                        break;
                    case Ev.Hit:
                        // a soft heavy thump, the whoomph of flame bursting, a shower of crackle, and the sizzle left on it
                        Rumble(buf, 0, L * 0.45f, 95f * B, Env(Decay(10f), g * 1.1f));
                        BandNoise(buf, 0, L * 0.5f, Const(500f * B), Const(1f), Env(Decay(12f), g * 0.6f));
                        NoiseBurst(buf, 0, 0.015f, Const(1f), Env(Decay(35f), g * 0.5f), true);
                        Whoosh(buf, 0, L * 0.6f, t => 900f * B - 400f * B * t, Env(Decay(6f), g * 0.75f), 0.7f, 0.6f, v.S(1));
                        Crackle(buf, 0, L, t => 260f * (1f - t) + 15f, t => g * (1f - t), 3000f * B, v.S(3), 1000f);
                        NoiseBurst(buf, 0.03f, L, Const(0.8f), Env(Decay(4f), g * 0.1f), true);
                        break;
                    case Ev.Wall:
                        // splashing on stone: a smaller whoomph and a long sizzle where it clings to the rock
                        Rumble(buf, 0, L * 0.3f, 120f * B, Env(Decay(14f), g * 0.7f));
                        Whoosh(buf, 0, L * 0.5f, t => 1200f * B - 600f * B * t, Env(Decay(8f), g * 0.6f), 0.8f, 0.6f, v.S(1));
                        NoiseBurst(buf, 0.01f, L, Const(0.8f), Env(Decay(3.2f), g * 0.22f), true);
                        Crackle(buf, 0.02f, L, t => 90f * (1f - t), t => g * 0.8f * (1f - t), 3400f * B, v.S(4), 1200f);
                        break;
                    default:
                        // guttering out: the roar dying to a breath, the crackle thinning, a last hiss
                        Whoosh(buf, 0, L, t => 320f * B, t => g * 0.5f * (1f - t) * (1f - t), 0.7f, 1f, v.S(1));
                        Crackle(buf, 0, L, t => 45f * (1f - t) + 2f, t => g * 0.7f * (1f - t), 2600f * B, v.S(5), 900f);
                        NoiseBurst(buf, 0, L, Const(0.75f), t => g * 0.05f * MathF.Sin(t * MathF.PI), true);
                        break;
                }
                break;

            // ---------------------------------------------------------------- frost: crystallizing, cracking, shattering
            case HeroElement.Frost:
                switch (ev)
                {
                    case Ev.Charge:
                        // the twinkling of crystallization: ice tinks scattered over a breath of frost forming, the creak of it creeping
                        Tinkles(buf, 0, L, 16 + (int)(6 * v.U()), 6200f * B, g * 0.5f, v.S(1), 0.4f, 0.8f);
                        NoiseBurst(buf, 0, L, Const(0.88f), t => g * 0.12f * MathF.Sin(t * MathF.PI), true);
                        Crackle(buf, 0, L, Const(90f), t => g * 0.35f * t, 5200f * B, v.S(2), 1600f);
                        break;
                    case Ev.Cast:
                        // a cold rush of air with ice forming in it as it goes, and a glitter
                        Whoosh(buf, 0, L, t => 2100f * B + 600f * B * t, Env(AD(0.2f, 4f), g * 0.6f), 1.3f, 0.5f, v.S(1));
                        Crackle(buf, 0, L * 0.9f, Const(150f * S), t => g * 0.55f * (1f - 0.5f * t), 4800f * B, v.S(2), 1500f);
                        Tinkles(buf, 0.05f, L * 0.9f, 7 + (int)(4 * S), 5600f * B, g * 0.35f, v.S(3));
                        break;
                    case Ev.Hit:
                        // a brittle crack, the shatter (a dense burst of tiny breaks), shards tinkling down, a small knock under it
                        NoiseBurst(buf, 0, 0.012f, Const(1f), Env(Decay(40f), g), true);
                        Crackle(buf, 0, Math.Min(L, 0.12f), Const(650f), t => g * 0.8f * (1f - t), 4200f * B, v.S(2), 1300f);
                        Tinkles(buf, 0.01f, L, 12, 5200f * B, g * 0.45f, v.S(3), 0.45f, 2f);
                        Rumble(buf, 0, L * 0.4f, 150f * B, Env(Decay(12f), g * 0.9f));
                        BandNoise(buf, 0, L * 0.4f, Const(450f * B), Const(1.1f), Env(Decay(14f), g * 0.5f));
                        break;
                    case Ev.Wall:
                        // ice smashing on rock: a harder crack, more shatter, shards skittering a long while, grit
                        NoiseBurst(buf, 0, 0.018f, Const(1f), Env(Decay(30f), g), true);
                        Crackle(buf, 0, Math.Min(L, 0.2f), Const(800f), t => g * 0.85f * (1f - t), 3800f * B, v.S(2), 1200f);
                        Tinkles(buf, 0.02f, L, 22, 4800f * B, g * 0.4f, v.S(3), 0.5f, 1.4f);
                        Crackle(buf, 0.02f, L * 0.6f, t => 120f * (1f - t), t => g * 0.4f * (1f - t), 1800f * B, v.S(4), 700f);
                        break;
                    default:
                        // subliming away: a soft hiss and the last few tinks
                        NoiseBurst(buf, 0, L, Const(0.9f), t => g * 0.1f * (1f - t), true);
                        Tinkles(buf, 0, L * 0.8f, 5, 6000f * B, g * 0.3f, v.S(5));
                        Crackle(buf, 0, L, t => 30f * (1f - t), t => g * 0.3f, 5000f * B, v.S(6), 1500f);
                        break;
                }
                break;

            // ---------------------------------------------------------------- wind: turbulent air, never a whistle
            case HeroElement.Wind:
                switch (ev)
                {
                    case Ev.Charge:
                        // the whoosh: air drawn in, swelling and rising
                        Whoosh(buf, 0, L, t => 350f * B + 1500f * B * t, Env(AD(0.55f, 3.5f), g), 0.9f, 0.9f, v.S(1));
                        Whoosh(buf, 0, L, t => 170f * B, Env(AD(0.4f, 3f), g * 0.4f), 0.6f, 0.9f, v.S(2));
                        break;
                    case Ev.Cast:
                        // a gust thrown: the rush past (rising and falling as it goes), a flutter in it, the push of the air behind
                        Whoosh(buf, 0, L, t => 300f * B + 1900f * B * MathF.Sin(MathF.PI * MathF.Min(1f, t * 1.1f)), t => g * MathF.Pow(MathF.Sin(t * MathF.PI), 1.3f), 0.8f, 0.7f, v.S(1));
                        Whoosh(buf, 0, L, t => 900f * B, t => g * 0.22f * MathF.Sin(t * MathF.PI) * (0.5f + 0.5f * MathF.Sin(t * L * MathF.Tau * (14f + 6f * B))), 1.1f, 0.6f, v.S(2));
                        Rumble(buf, 0, L, 70f * B, t => g * 0.3f * MathF.Sin(t * MathF.PI) * S);
                        break;
                    case Ev.Hit:
                        // a buffet: the air's blow (a dull thump) and a short burst of gust off it
                        Rumble(buf, 0, L * 0.5f, 75f * B, Env(Decay(9f), g * 0.9f));
                        BandNoise(buf, 0, L * 0.5f, Const(350f * B), Const(1f), Env(Decay(10f), g * 0.5f));
                        Whoosh(buf, 0, L, t => 900f * B, Env(Decay(7f), g * 0.7f), 0.7f, 0.8f, v.S(1));
                        NoiseBurst(buf, 0, L * 0.4f, Const(0.25f), Env(Decay(18f), g * 0.4f));
                        break;
                    case Ev.Wall:
                        // a gust slapping into stone and swirling off it
                        Rumble(buf, 0, L * 0.4f, 90f * B, Env(Decay(12f), g * 0.7f));
                        NoiseBurst(buf, 0, L * 0.3f, Const(0.45f), Env(Decay(14f), g * 0.6f));
                        Whoosh(buf, 0.03f, L * 0.9f, t => 700f * B - 300f * B * t, t => g * 0.4f * MathF.Sin(t * MathF.PI), 0.7f, 1f, v.S(2));
                        break;
                    default:
                        // a breeze dying away
                        Whoosh(buf, 0, L, t => 600f * B - 300f * B * t, t => g * 0.5f * MathF.Pow(1f - t, 1.5f), 0.7f, 1f, v.S(1));
                        break;
                }
                break;

            // ---------------------------------------------------------------- earth: grinding, cracking, crunching, gravel
            case HeroElement.Earth:
                switch (ev)
                {
                    case Ev.Charge:
                        // stone grinding on stone: a low churn and the crunch of grit in it
                        Rumble(buf, 0, L, 55f * B, Env(AD(0.3f, 3f), g * 0.8f));
                        Crackle(buf, 0, L, Const(260f), t => g * 0.6f * MathF.Sin(t * MathF.PI), 1500f * B, v.S(1), 600f);
                        Crackle(buf, 0, L, Const(120f), t => g * 0.25f, 3800f * B, v.S(2), 1200f);
                        break;
                    case Ev.Cast:
                        // the rock cracks, the ground heaves (a churn of weight), gravel showering off, the thunk of a slab
                        NoiseBurst(buf, 0, 0.02f, Const(1f), Env(Decay(25f), g * 0.8f), true);
                        Rumble(buf, 0, L, 60f * B, Env(AD(0.02f, 5f), g * (0.8f + 0.4f * S)));
                        Crackle(buf, 0.01f, L * 0.75f, Const(300f * S), t => g * 0.7f * (1f - t), 1700f * B, v.S(1), 500f);
                        Modes(buf, 0, 300f * B, StoneModes, 3f, g * 0.25f);
                        break;
                    case Ev.Hit:
                        // a heavy crunching blow: the weight of it, the body of the thud, the crack, gravel bursting
                        Rumble(buf, 0, L * 0.6f, 50f * B, Env(Decay(8f), g * 1.3f));
                        BandNoise(buf, 0, L * 0.5f, Const(420f * B), Const(1.2f), Env(Decay(14f), g * 0.7f));
                        NoiseBurst(buf, 0, L * 0.5f, Const(0.35f), Env(Decay(18f), g * 0.9f));
                        NoiseBurst(buf, 0, 0.015f, Const(1f), Env(Decay(35f), g * 0.7f), true);
                        Crackle(buf, 0, L * 0.4f, t => 900f * (1f - t), t => g * 0.8f, 1400f * B, v.S(1), 500f);
                        break;
                    case Ev.Wall:
                        // rock on rock: a hard clack, the crack, pebbles trickling down
                        Modes(buf, 0, 900f * B, StoneModes, 4f, g * 0.5f);
                        NoiseBurst(buf, 0, 0.02f, Const(1f), Env(Decay(28f), g * 0.8f), true);
                        Crackle(buf, 0.03f, L, t => 60f * (1f - t) + 4f, t => g * 0.5f * (1f - 0.6f * t), 2500f * B, v.S(2), 700f);
                        break;
                    default:
                        // it crumbles: a last trickle of pebbles and dust
                        Crackle(buf, 0, L, t => 60f * (1f - t) + 3f, t => g * 0.6f, 2200f * B, v.S(3), 700f);
                        NoiseBurst(buf, 0, L, Const(0.3f), t => g * 0.12f * (1f - t));
                        break;
                }
                break;

            // ---------------------------------------------------------------- nature: rustling leaves, lashing vines, snapping twigs
            case HeroElement.Nature:
                switch (ev)
                {
                    case Ev.Charge:
                        // leaves stirring: a dense rustle over a breath of moving foliage
                        Crackle(buf, 0, L, Const(700f), t => g * 0.45f * MathF.Sin(t * MathF.PI), 3200f * B, v.S(1), 2200f, 0.7f);
                        Whoosh(buf, 0, L, t => 2400f * B, t => g * 0.22f * MathF.Sin(t * MathF.PI), 1f, 0.8f, v.S(2));
                        break;
                    case Ev.Cast:
                        // a vine's lash (a fast whip of air), a crack of green wood at its end, leaves thrashing
                        Whoosh(buf, 0, L * 0.7f, t => 800f * B + 2500f * B * t, Env(AD(0.6f, 12f), g * 0.8f), 1.2f, 0.4f, v.S(1));
                        Crackle(buf, L * 0.42f, 0.03f, Const(1500f), Const(g), 2600f * B, v.S(2), 900f);
                        Crackle(buf, 0, L, Const(400f * S), t => g * 0.35f * (1f - t), 3400f * B, v.S(3), 2000f, 0.7f);
                        break;
                    case Ev.Hit:
                        // a wet green thwack and twigs snapping
                        NoiseBurst(buf, 0, L * 0.4f, Const(0.4f), Env(Decay(22f), g * 0.9f));
                        BandNoise(buf, 0, L * 0.6f, Const(700f * B), Const(1.4f), Env(Decay(14f), g * 0.6f));
                        Crackle(buf, 0, L * 0.3f, Const(200f), Const(g * 0.9f), 2200f * B, v.S(1), 900f);
                        Rumble(buf, 0, L * 0.4f, 120f * B, Env(Decay(12f), g * 0.5f));
                        break;
                    case Ev.Wall:
                        // a thunk into the rock and a rustle of leaves falling away
                        Modes(buf, 0, 240f * B, WoodModes, 3.2f, g * 0.5f);
                        NoiseBurst(buf, 0, 0.02f, Const(0.6f), Env(Decay(25f), g * 0.5f), true);
                        Crackle(buf, 0.02f, L, t => 500f * (1f - t), t => g * 0.35f, 3200f * B, v.S(2), 2000f, 0.7f);
                        break;
                    default:
                        Crackle(buf, 0, L, t => 500f * (1f - t) + 10f, t => g * 0.35f * (1f - t), 3000f * B, v.S(3), 2000f, 0.7f);
                        break;
                }
                break;

            // ---------------------------------------------------------------- water: sloshing, rushing, splashing, dripping
            case HeroElement.Water:
                switch (ev)
                {
                    case Ev.Charge:
                        Gurgle(buf, 0, L, 420f * B, t => g * 0.8f * MathF.Sin(t * MathF.PI), v.S(1), 14f);
                        break;
                    case Ev.Cast:
                        // a rush of water, the slosh under it, droplets thrown off
                        Whoosh(buf, 0, L, t => 1100f * B, Env(AD(0.15f, 4f), g * 0.8f), 0.6f, 0.8f, v.S(1));
                        Gurgle(buf, 0, L, 380f * B, t => g * 0.45f * MathF.Sin(t * MathF.PI), v.S(2), 10f);
                        Crackle(buf, 0.05f, L, Const(40f * S), t => g * 0.5f, 1300f * B, v.S(3), 160f);
                        break;
                    case Ev.Hit:
                        // a splash: the slap of it, the burst of spray, droplets falling
                        Whoosh(buf, 0, L * 0.7f, t => 1600f * B - 900f * B * t, Env(Decay(7f), g), 0.6f, 0.7f, v.S(1));
                        Rumble(buf, 0, L * 0.4f, 130f * B, Env(Decay(12f), g * 0.9f));
                        BandNoise(buf, 0, L * 0.4f, Const(500f * B), Const(1.2f), Env(Decay(14f), g * 0.5f));
                        NoiseBurst(buf, 0, L * 0.4f, Const(0.3f), Env(Decay(16f), g * 0.6f));
                        Crackle(buf, 0.02f, L, t => 80f * (1f - t), t => g * 0.5f, 1500f * B, v.S(2), 200f);
                        break;
                    case Ev.Wall:
                        // splattering on stone
                        NoiseBurst(buf, 0, L * 0.5f, Const(0.6f), Env(Decay(10f), g * 0.6f), true);
                        Rumble(buf, 0, L * 0.3f, 140f * B, Env(Decay(14f), g * 0.5f));
                        Crackle(buf, 0.01f, L, t => 140f * (1f - t), t => g * 0.55f, 1600f * B, v.S(2), 220f);
                        break;
                    default:
                        // drips, slowing
                        Crackle(buf, 0, L, t => 14f * (1f - t) + 2f, t => g * 0.6f * (1f - 0.6f * t), 1400f * B, v.S(3), 140f);
                        break;
                }
                break;

            // ---------------------------------------------------------------- shadow: breath, whisper, smoke
            case HeroElement.Dark:
                switch (ev)
                {
                    case Ev.Charge:
                        Whisper(buf, 0, L, new[] { 600f * B, 1100f * B, 2600f * B }, t => g * 0.9f * MathF.Pow(MathF.Sin(t * MathF.PI), 1.5f), v.S(1));
                        Rumble(buf, 0, L, 45f, t => g * 0.5f * MathF.Sin(t * MathF.PI));
                        break;
                    case Ev.Cast:
                        // a rush drawn in (rising to its peak and cut), a whisper riding it, a low weight under it
                        Whoosh(buf, 0, L, t => 400f * B + 1600f * B * t * t, t => g * t * t * (t < 0.9f ? 1f : (1f - t) / 0.1f), 0.9f, 0.6f, v.S(1));
                        Whisper(buf, 0, L, new[] { 500f * B, 1000f * B, 2400f * B }, t => g * 0.5f * MathF.Sin(t * MathF.PI), v.S(2));
                        Rumble(buf, 0, L, 50f, t => g * 0.4f * t);
                        break;
                    case Ev.Hit:
                        // a muffled blow and the hiss of shadow off it
                        Rumble(buf, 0, L * 0.5f, 70f * B, Env(Decay(9f), g));
                        BandNoise(buf, 0, L * 0.5f, Const(300f * B), Const(1f), Env(Decay(10f), g * 0.55f));
                        NoiseBurst(buf, 0, L, Const(0.7f), Env(Decay(6f), g * 0.08f), true);
                        Whisper(buf, 0, L * 0.6f, new[] { 700f, 1200f, 2700f }, Env(Decay(5f), g * 0.35f), v.S(1));
                        break;
                    case Ev.Wall:
                        // a puff of smoke against the rock
                        NoiseBurst(buf, 0, L, Const(0.12f), Env(AD(0.05f, 5f), g * 0.8f));
                        NoiseBurst(buf, 0, L, Const(0.75f), Env(Decay(6f), g * 0.15f), true);
                        break;
                    default:
                        Whisper(buf, 0, L, new[] { 650f * B, 1150f * B, 2500f * B }, t => g * 0.5f * (1f - t), v.S(3));
                        break;
                }
                break;

            // ---------------------------------------------------------------- nothing elemental: weight and air
            default:
                switch (ev)
                {
                    case Ev.Charge: Whoosh(buf, 0, L, t => 800f * B, t => g * 0.4f * MathF.Sin(t * MathF.PI), 0.9f, 0.6f, v.S(1)); break;
                    case Ev.Cast: Whoosh(buf, 0, L, t => 600f * B + 1500f * B * MathF.Sin(t * MathF.PI), t => g * 0.6f * MathF.Sin(t * MathF.PI), 0.9f, 0.6f, v.S(1)); break;
                    case Ev.Hit:
                        Rumble(buf, 0, L * 0.5f, 90f * B, Env(Decay(10f), g));
                        NoiseBurst(buf, 0, L * 0.4f, Const(0.3f), Env(Decay(18f), g * 0.6f));
                        NoiseBurst(buf, 0, 0.01f, Const(1f), Env(Decay(40f), g * 0.4f), true);
                        break;
                    case Ev.Wall:
                        Modes(buf, 0, 700f * B, StoneModes, 4f, g * 0.4f);
                        NoiseBurst(buf, 0, 0.02f, Const(0.8f), Env(Decay(25f), g * 0.6f), true);
                        break;
                    default: NoiseBurst(buf, 0, L, Const(0.5f), t => g * 0.15f * (1f - t), true); break;
                }
                break;
        }
    }
}

using System;
using static DaggerCave.Synth;

namespace DaggerCave;

/// <summary>What a blow is: the thing that strikes (a blade's edge, a dagger's point, a club, a bolt of power, fire, frost, water, thorns).</summary>
public enum Strike : byte { Blade, Pierce, Blunt, Bolt, Fire, Frost, Water, Nature }

/// <summary>What is struck: a creature's body, a hero's, or the cave itself.</summary>
public enum Body : byte { Flesh, Chitin, Bone, Stone, Metal, Wood, Ice, Gel, Flame, Armor }

/// <summary>
/// The sound of one thing meeting another (<see cref="Strike"/> x <see cref="Body"/>), made on demand from the pair, so every meeting has its
/// own: a sword on rock rings a bright steel "chink" with grit falling; on a beetle's or a scorpion's shell it's a hollow, plastic-y thock;
/// on bone a dry clack; on flesh a wet thwack; on iron a clang that rings on; on wood a knock and splinters. A club is the same meeting with
/// weight and no edge; a fireball on rock is a whoomph and a sizzling crack, on a shell a crackle of popping, on flesh a sear, on ice a hiss
/// of steam; frost cracks and crunches; water slaps and splashes; thorns lash. Each pair is made from its own seed (the same in every game).
/// Played as "%strike|body" (<see cref="SoundBank"/>).
/// </summary>
public static class MaterialSounds
{
    // a beetle's or a scorpion's shell: a few hollow, inharmonic modes that die quickly (the "plastic" thock)
    private static readonly float[][] ShellModes = { new[] { 1f, 1f, 70f }, new[] { 1.71f, 0.65f, 95f }, new[] { 2.83f, 0.4f, 130f }, new[] { 4.17f, 0.22f, 180f } };
    // bone: dry and short, high
    private static readonly float[][] BoneModes = { new[] { 1f, 1f, 110f }, new[] { 2.27f, 0.55f, 150f }, new[] { 3.86f, 0.3f, 220f } };

    public static string Name(Strike s, Body b) => $"%{(int)s}|{(int)b}";

    /// <summary>How hard the strike is (how much of the body's own ring it draws out).</summary>
    private static float Hardness(Strike s) => s switch
    {
        Strike.Blade => 1f, Strike.Pierce => 0.85f, Strike.Blunt => 0.9f, Strike.Bolt => 0.5f, Strike.Frost => 0.6f, Strike.Nature => 0.5f, Strike.Fire => 0.25f, _ => 0.15f,
    };

    public static float[] Build(Strike s, Body m, int seed)
    {
        var r = new Random(seed);
        float U() => (float)r.NextDouble();
        float hard = Hardness(s);
        bool edge = s is Strike.Blade or Strike.Pierce;
        var b = Buf(0.5f);

        // ---- the strike's own contact
        switch (s)
        {
            case Strike.Blade:
                NoiseBurst(b, 0, 0.006f, Const(1f), Decay(45), true);
                BandNoise(b, 0, 0.06f, t => 4200f - 2000f * t, Const(2.2f), t => 0.55f * MathF.Exp(-t * 14f));
                break;
            case Strike.Pierce:
                NoiseBurst(b, 0, 0.004f, Const(1f), Decay(60), true);
                BandNoise(b, 0, 0.04f, t => 5200f - 1500f * t, Const(3f), t => 0.4f * MathF.Exp(-t * 20f));
                break;
            case Strike.Blunt:
                NoiseBurst(b, 0, 0.012f, Const(0.3f), Decay(30));
                Rumble(b, 0, 0.16f, 85f + 20f * U(), t => 1.1f * MathF.Exp(-t * 12f));
                break;
            case Strike.Bolt:
                Rumble(b, 0, 0.12f, 140f, Decay(14));
                BandNoise(b, 0, 0.1f, t => 1800f - 900f * t, Const(1.4f), t => 0.6f * MathF.Exp(-t * 16f));
                break;
            case Strike.Fire:
                Rumble(b, 0, 0.2f, 95f, Decay(10));
                Whoosh(b, 0, 0.3f, t => 2600f - 1400f * t, t => 0.6f * MathF.Exp(-t * 6f), 0.9f, 0.5f, seed);
                Crackle(b, 0.02f, 0.32f, t => 60f * (1f - t), t => 0.7f * (1f - t), 3000f, seed + 1, 1000f);
                break;
            case Strike.Frost:
                NoiseBurst(b, 0, 0.015f, Const(1f), Decay(25), true);
                Crackle(b, 0, 0.1f, Const(500f), t => 0.7f * (1f - t), 4200f, seed + 2, 1300f);
                Tinkles(b, 0.01f, 0.25f, 7, 5200f, 0.35f, seed + 3);
                break;
            case Strike.Water:
                BandNoise(b, 0, 0.14f, t => 950f - 500f * t, Const(1.3f), Decay(12));
                Crackle(b, 0.02f, 0.25f, t => 60f * (1f - t), Const(0.6f), 1500f, seed + 4, 180f);
                break;
            case Strike.Nature:
                BandNoise(b, 0, 0.05f, t => 3000f - 2000f * t, Const(2f), Decay(30));
                Crackle(b, 0.01f, 0.18f, Const(120f), t => 0.5f * (1f - t), 2500f, seed + 5, 700f);
                break;
        }

        // ---- the body's answer, coloured by how hard the strike was and what it was
        switch (m)
        {
            case Body.Flesh:
                BandNoise(b, 0.002f, 0.1f, t => 1100f - 500f * t, Const(1.2f), t => 0.8f * MathF.Exp(-t * 14f) * (0.6f + 0.4f * hard));
                Rumble(b, 0, 0.16f, 110f, t => MathF.Exp(-t * 10f) * (0.5f + 0.6f * hard));
                if (edge) BandNoise(b, 0.005f, 0.06f, t => 2400f - 900f * t, Const(1.6f), t => 0.5f * MathF.Exp(-t * 18f));
                if (s == Strike.Fire)
                {
                    // (a sear: a long hiss and fat spitting)
                    BandNoise(b, 0.04f, 0.38f, Const(5200f), Const(0.8f), t => 0.3f * (1f - t));
                    Crackle(b, 0.05f, 0.32f, Const(90f), t => 0.5f * (1f - t), 3500f, seed + 6, 900f);
                }
                if (s == Strike.Frost) BandNoise(b, 0, 0.05f, Const(3200f), Const(1.5f), Decay(30));
                break;
            case Body.Chitin:
                // (the hollow, plastic thock of a shell; a click on top)
                Modes(b, 0.001f, 820f + 260f * hard + 120f * U(), ShellModes, 1.3f, 0.55f * (0.35f + 0.65f * hard));
                NoiseBurst(b, 0, 0.003f, Const(1f), Decay(70), true);
                Rumble(b, 0, 0.07f, 180f, t => 0.5f * MathF.Exp(-t * 22f));
                if (s == Strike.Fire) Crackle(b, 0.03f, 0.32f, Const(40f), t => 0.9f * (1f - t), 2800f, seed + 7, 350f, 0.9f); // (a crackle of popping)
                if (s == Strike.Frost) Crackle(b, 0, 0.08f, Const(700f), t => 0.6f * (1f - t), 3600f, seed + 8, 1500f);
                if (s == Strike.Pierce) Modes(b, 0.004f, 1900f, ShellModes, 2f, 0.15f);
                break;
            case Body.Bone:
                Modes(b, 0, 1450f + 200f * U(), BoneModes, 1.2f, 0.5f * hard + 0.15f);
                Crackle(b, 0.01f, 0.16f, Const(200f), t => 0.45f * (1f - t), 2600f, seed + 9, 600f);
                if (s == Strike.Fire) BandNoise(b, 0.03f, 0.3f, Const(4200f), Const(0.9f), t => 0.25f * (1f - t));
                break;
            case Body.Stone:
                if (edge)
                {
                    // (steel on rock: a bright chink, the blade ringing a moment, grit scattering)
                    Modes(b, 0, 1900f + 300f * U(), StoneModes, 2.6f, 0.55f);
                    Modes(b, 0.003f, 2600f + 300f * U(), MetalModes, 6f, 0.14f);
                    Crackle(b, 0.02f, 0.2f, Const(150f), t => 0.7f * (1f - t), 2600f, seed + 10, 800f);
                }
                else if (s == Strike.Blunt)
                {
                    Modes(b, 0, 700f + 120f * U(), StoneModes, 2f, 0.5f);
                    Crackle(b, 0.01f, 0.25f, Const(180f), t => 0.8f * (1f - t), 1800f, seed + 11, 600f);
                    Rumble(b, 0, 0.2f, 70f, Decay(10));
                }
                else if (s == Strike.Fire)
                {
                    // (fire on rock: a hiss and a sparse sizzling crack, the thud under it)
                    BandNoise(b, 0.02f, 0.35f, Const(4600f), Const(0.9f), t => 0.3f * (1f - t));
                    Crackle(b, 0.02f, 0.3f, Const(35f), t => 0.8f * (1f - t), 2200f, seed + 12, 500f);
                    Modes(b, 0, 900f, StoneModes, 1.5f, 0.12f);
                }
                else
                {
                    Modes(b, 0, 1200f + 200f * U(), StoneModes, 1.5f, 0.25f * hard + 0.05f);
                    Crackle(b, 0.02f, 0.2f, Const(100f), t => 0.5f * (1f - t), 2400f, seed + 13, 800f);
                }
                break;
            case Body.Metal:
            case Body.Armor:
            {
                // (iron: a clang that rings on after an edge, a dull clank after a club, a hiss and a ping after fire)
                bool plate = m == Body.Armor;
                if (s == Strike.Pierce) Modes(b, 0, (plate ? 2600f : 2200f) + 300f * U(), MetalModes, 2.2f, 0.4f); // (a point's high, short tink)
                else if (edge) Modes(b, 0, (plate ? 1400f : 1100f) + 300f * U(), MetalModes, plate ? 4f : 7f, 0.5f);
                else if (s == Strike.Blunt) Modes(b, 0, (plate ? 600f : 480f) + 80f * U(), MetalModes, 3f, 0.5f);
                else
                {
                    Modes(b, 0.01f, 2300f + 300f * U(), MetalModes, 3f, 0.12f + 0.15f * hard);
                    if (s == Strike.Fire) BandNoise(b, 0.02f, 0.3f, Const(5000f), Const(0.9f), t => 0.3f * (1f - t));
                }
                NoiseBurst(b, 0, 0.004f, Const(1f), Decay(60), true);
                break;
            }
            case Body.Wood:
                Modes(b, 0, 170f + 90f * hard + 40f * U(), WoodModes, 1.6f, 0.55f);
                if (edge) Crackle(b, 0.005f, 0.14f, Const(160f), Const(0.8f), 2200f, seed + 14, 900f); // (splinters)
                if (s == Strike.Fire) Crackle(b, 0.02f, 0.35f, Const(70f), t => 0.9f * (1f - t), 2600f, seed + 15, 700f);
                break;
            case Body.Ice:
                if (s == Strike.Fire)
                {
                    // (fire on ice: steam)
                    BandNoise(b, 0, 0.4f, Const(4200f), Const(0.7f), t => 0.55f * (1f - t));
                    Crackle(b, 0.03f, 0.3f, Const(60f), t => 0.4f * (1f - t), 3000f, seed + 16, 900f);
                }
                else
                {
                    Crackle(b, 0, 0.1f, Const(600f * hard + 150f), t => 0.8f * (1f - t), 4300f, seed + 17, 1300f);
                    Tinkles(b, 0.01f, 0.28f, (int)(4 + 8 * hard), 5000f, 0.4f, seed + 18, 0.45f, 2f);
                }
                break;
            case Body.Gel:
                // (a squelch: something soft giving way, and a gurgle)
                Gurgle(b, 0, 0.25f, 220f + 60f * U(), t => 0.8f * MathF.Exp(-t * 8f), seed + 19, 6f);
                BandNoise(b, 0, 0.12f, t => 600f - 300f * t, Const(1.1f), Decay(12));
                break;
            case Body.Flame:
                // (striking living fire: a fwump, and what it was made of answering)
                Rumble(b, 0, 0.2f, 80f, Decay(9));
                Whoosh(b, 0, 0.25f, t => 1600f + 800f * t, t => 0.5f * MathF.Exp(-t * 7f), 0.8f, 0.6f, seed + 20);
                if (s is Strike.Frost or Strike.Water) BandNoise(b, 0, 0.4f, Const(4400f), Const(0.7f), t => 0.55f * (1f - t)); // (steam)
                break;
        }
        Crunch(b, 0.2f + 0.35f * hard);
        return b;
    }
}

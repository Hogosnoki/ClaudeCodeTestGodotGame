namespace DaggerCave;

/// <summary>Every sprite set that has a 3D design (sets without one keep a placeholder).</summary>
public static class CreatureRegistry
{
    public static void RegisterAll()
    {
        CreatureLibrary.Register("swordsman", () => new HeroDesign(false));
        CreatureLibrary.Register("warden", () => new HeroDesign(true));
    }
}

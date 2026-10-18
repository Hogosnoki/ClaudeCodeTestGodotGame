using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// Save files: each its own progress (the meta save: embers, the Ember Grid, heroes freed, loadouts, the journal) and its own in-game
/// achievements, in <c>user://saves/&lt;id&gt;/</c>; the list and the one in use are in <c>user://saves/index.json</c>. The player can make
/// new ones, rename, reset (start again from nothing) and delete them on the Save Files page. (Steam's achievements, once the game is on
/// Steam, are the account's, whichever save earned them.) The settings and controls are the machine's, shared by every save.
/// A save from before save files becomes the first one.
/// </summary>
public static class Profiles
{
    public sealed class Info
    {
        public string Id, Name;
        public long Created;
    }

    public static readonly List<Info> All = new();
    public static string Active { get; private set; } = "1";
    public static Info Current => All.FirstOrDefault(p => p.Id == Active);

    private static string Root = "user://saves";
    private static string IndexPath => Root + "/index.json";
    public static string DirOf(string id) => $"{Root}/{id}";
    public static string MetaPath => DirOf(Active) + "/meta.json";
    public static string AchPath => DirOf(Active) + "/achievements.json";

    static Profiles() { LoadIndex(); }

    /// <summary>The tests: save files kept somewhere else (emptied first), read afresh.</summary>
    public static void UseRoot(string root)
    {
        Root = root;
        if (DirAccess.DirExistsAbsolute(root))
        {
            foreach (var d in DirAccess.GetDirectoriesAt(root))
            {
                foreach (var f in DirAccess.GetFilesAt($"{root}/{d}")) DirAccess.RemoveAbsolute($"{root}/{d}/{f}");
                DirAccess.RemoveAbsolute($"{root}/{d}");
            }
            foreach (var f in DirAccess.GetFilesAt(root)) DirAccess.RemoveAbsolute($"{root}/{f}");
        }
        LoadIndex();
        Meta.Reload();
        Achievements.Load();
    }

    private static void LoadIndex()
    {
        All.Clear();
        if (FileAccess.FileExists(IndexPath))
        {
            try
            {
                var d = Json.ParseString(FileAccess.GetFileAsString(IndexPath)).AsGodotDictionary();
                if (d.ContainsKey("active")) Active = (string)d["active"];
                if (d.ContainsKey("saves"))
                    foreach (var v in d["saves"].AsGodotArray())
                    {
                        var e = v.AsGodotDictionary();
                        All.Add(new Info { Id = (string)e["id"], Name = (string)e["name"], Created = e.ContainsKey("created") ? (long)e["created"] : 0 });
                    }
            }
            catch (Exception ex) { GD.PrintErr($"[saves] index unreadable: {ex.Message}"); }
        }
        if (All.Count > 0)
        {
            if (Current == null) Active = All[0].Id;
            return;
        }
        // (the first time: one save, holding whatever was kept before there were save files)
        var first = new Info { Id = "1", Name = "Save 1", Created = (long)Time.GetUnixTimeFromSystem() };
        All.Add(first);
        Active = first.Id;
        if (G.NoSave) return;
        DirAccess.MakeDirRecursiveAbsolute(DirOf(first.Id));
        foreach (var (old, now) in new[] { ("user://meta.json", "/meta.json"), ("user://achievements.json", "/achievements.json") })
            if (FileAccess.FileExists(old)) DirAccess.CopyAbsolute(old, DirOf(first.Id) + now);
        SaveIndex();
    }

    private static void SaveIndex()
    {
        if (G.NoSave) return;
        DirAccess.MakeDirRecursiveAbsolute(Root);
        var saves = new Godot.Collections.Array();
        foreach (var p in All) saves.Add(new Godot.Collections.Dictionary { ["id"] = p.Id, ["name"] = p.Name, ["created"] = p.Created });
        using var f = FileAccess.Open(IndexPath, FileAccess.ModeFlags.Write);
        f?.StoreString(Json.Stringify(new Godot.Collections.Dictionary { ["active"] = Active, ["saves"] = saves }, "  "));
    }

    /// <summary>A new, empty save (not switched to).</summary>
    public static Info Create(string name = null)
    {
        int n = All.Select(p => int.TryParse(p.Id, out var k) ? k : 0).DefaultIfEmpty(0).Max() + 1;
        var info = new Info { Id = n.ToString(), Name = string.IsNullOrWhiteSpace(name) ? $"Save {n}" : name.Trim(), Created = (long)Time.GetUnixTimeFromSystem() };
        All.Add(info);
        if (!G.NoSave) DirAccess.MakeDirRecursiveAbsolute(DirOf(info.Id));
        SaveIndex();
        return info;
    }

    /// <summary>Plays on with another save: everything it keeps is read afresh.</summary>
    public static void Switch(string id)
    {
        if (All.All(p => p.Id != id)) return;
        Active = id;
        SaveIndex();
        Meta.Reload();
        Achievements.Load();
    }

    public static void Rename(string id, string name)
    {
        var p = All.FirstOrDefault(x => x.Id == id);
        if (p == null || string.IsNullOrWhiteSpace(name)) return;
        p.Name = name.Trim().Length > 24 ? name.Trim()[..24] : name.Trim();
        SaveIndex();
    }

    /// <summary>Starts a save again from nothing (its progress and its achievements are gone).</summary>
    public static void Reset(string id)
    {
        if (!G.NoSave)
            foreach (var file in new[] { "/meta.json", "/achievements.json" })
                if (FileAccess.FileExists(DirOf(id) + file)) DirAccess.RemoveAbsolute(DirOf(id) + file);
        if (id == Active) { Meta.Reload(); Achievements.Load(); }
    }

    /// <summary>Deletes a save (another is used if it was this one; a fresh one is made if it was the last).</summary>
    public static void Delete(string id)
    {
        var p = All.FirstOrDefault(x => x.Id == id);
        if (p == null) return;
        Reset(id);
        if (!G.NoSave && DirAccess.DirExistsAbsolute(DirOf(id)))
        {
            foreach (var f in DirAccess.GetFilesAt(DirOf(id))) DirAccess.RemoveAbsolute($"{DirOf(id)}/{f}");
            DirAccess.RemoveAbsolute(DirOf(id));
        }
        All.Remove(p);
        if (All.Count == 0) Create();
        if (Active == id) Switch(All[0].Id);
        else SaveIndex();
    }

    /// <summary>What a save holds, for its row on the page (read from its file; the one in use from memory).</summary>
    public static (int embers, int runs, int best, int heroes, int earned) Peek(string id)
    {
        if (id == Active) return (Meta.Embers, Meta.Runs, Meta.BestDepth, Meta.Unlocked.Count, Achievements.Completion(AchKind.Progress).got + Achievements.Completion(AchKind.Feat).got);
        int I(Godot.Collections.Dictionary d, string k) => d != null && d.ContainsKey(k) ? (int)d[k] : 0;
        Godot.Collections.Dictionary m = null, a = null;
        try { if (FileAccess.FileExists(DirOf(id) + "/meta.json")) m = Json.ParseString(FileAccess.GetFileAsString(DirOf(id) + "/meta.json")).AsGodotDictionary(); } catch { }
        try { if (FileAccess.FileExists(DirOf(id) + "/achievements.json")) a = Json.ParseString(FileAccess.GetFileAsString(DirOf(id) + "/achievements.json")).AsGodotDictionary(); } catch { }
        int heroes = m != null && m.ContainsKey("unlocked") ? m["unlocked"].AsGodotArray().Count : 3;
        int earned = a != null && a.ContainsKey("earned") ? a["earned"].AsGodotDictionary().Count : 0;
        return (I(m, "embers"), I(m, "runs"), I(m, "best_depth"), heroes, earned);
    }
}

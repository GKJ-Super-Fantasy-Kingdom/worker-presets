using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using SuperFantasyKingdom;
using WorkerPresets;

internal static class Program
{
    private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static void Check(bool ok, string scenario)
    {
        if (!ok) throw new InvalidOperationException(scenario);
        Console.WriteLine("PASS " + scenario);
    }

    private static Workplace Site(string prefab, int x, int current, bool carrier = false) =>
        new Workplace { Key = Workplace.Plot(prefab, x, -13), Title = prefab + " " + x,
            Current = current, Max = 4, JobCapacity = 4, Carrier = carrier };

    private static LayoutBook Scenarios()
    {
        var east = Site("Quarry", 4, 3);
        var west = Site("Quarry", -4, 1);
        var haul = Site("Port", 8, 2, true);
        var lost = Site("Farm", -8, 2);
        var sites = new[] { east, west, haul, lost }.ToDictionary(b => b.Key);
        var book = new LayoutBook();
        var layout = book.Slots[0];
        layout.Name = "Stone";
        layout.Entries = sites.Values.Select(b => b.Capture()).ToList();
        book.Slots[1].Entries = new List<Entry> { west.Capture(), lost.Capture() };
        east.Current = 0;
        west.Current = 4;
        haul.Current = 0;
        book.Remove(lost.Key);
        sites[lost.Key] = Site("Farm", -8, 1);
        var plan = new Preview(layout, sites, 2, 1);
        Check(plan.WorkersShort == 3 && plan.CarriersShort == 1, "worker and carrier shortages include unchanged replacement demand");
        var ordered = plan.InApplyOrder().ToArray();
        Check(ordered.Length == 3 && ordered[0].Building == west && ordered[0].Target == 1 &&
            ordered[1].Building == east && ordered[1].Target == 3, "duplicate quarries keep distinct targets and reductions happen first");
        Check(east.Current == 0 && west.Current == 4 && haul.Current == 0, "preview and leaving it perform no writes");
        Check(plan.Rows.Single(r => r.Saved.Key == lost.Key).Building == null &&
            book.Slots[1].Entries.Last().Removed, "demolition retires both saved entries even after rebuilding on the same plot");
        var oldPlot = new Change { Saved = new Entry { Key = "BuildingWood@-5,-17", Title = "Lumberyard (-5, -17)" } };
        Check(oldPlot.BuildingName == "Lumberyard" && oldPlot.Plot == "-5, -17",
            "older and demolished plots retain their readable names and saved coordinates");
        var changedPlan = new Preview(layout, sites, 20, 20);
        Check(!plan.Matches(changedPlan), "a changed workforce warning requires fresh confirmation");
        west.Min = 2;
        east.Max = 2;
        var limited = new Preview(layout, sites, 20, 20);
        Check(limited.Rows[0].Target == 2 && limited.Rows[1].Target == 2, "capacity changes and active individual jobs limit targets");
        var unlimited = Site("Workshop", 2, 9);
        unlimited.Max = 9;
        unlimited.Individual = true;
        unlimited.JobCapacity = 2;
        var auto = new Preview(new Layout { Entries = new List<Entry> { unlimited.Capture() } },
            new Dictionary<string, Workplace> { [unlimited.Key] = unlimited }, 2, 0);
        Check(auto.WorkersShort == 0 && unlimited.AmountText(9) == "∞", "unlimited setting respects simultaneous building capacity");
        var hunter = Site("UndeadHunter", 3, 1);
        hunter.CrewSize = 3;
        var hunt = new Preview(new Layout { Entries = new List<Entry> { hunter.Capture() } },
            new Dictionary<string, Workplace> { [hunter.Key] = hunter }, 2, 0);
        Check(hunt.WorkersShort == 1, "a partial undead hunter assignment needs its native three-worker crew");
        sites.Remove(east.Key);
        var missing = new Preview(layout, sites, 20, 20);
        Check(missing.Rows[0].Building == null, "missing building is skipped without falling back to a matching type");
        return book;
    }

    private static object InvokeHook(string type, string method, params object[] args) =>
        typeof(SaveHooks).GetNestedType(type, Flags).GetMethod(method, Flags).Invoke(null, args);

    private static void SaveContinue(string copy, LayoutBook book)
    {
        var hooks = typeof(SaveHooks);
        var original = File.ReadAllText(copy);
        var game = JObject.Parse(original);
        Check(game["buildings"] is JArray buildings && buildings.Count > 1, "copied kingdom checkpoint contains buildings");
        var savedPlots = game["buildings"].Select(b => Workplace.Plot(b.Value<string>("name"),
            b["position"].Value<float>("x"), b["position"].Value<float>("y"))).ToArray();
        Check(savedPlots.Length == savedPlots.Distinct().Count(), "prefab and saved position distinguish all plots in the copied kingdom");
        hooks.GetField("writing", Flags).SetValue(null, book);
        hooks.GetField("writePath", Flags).SetValue(null, "human_game");
        object[] write = { "other_game", original };
        InvokeHook("WriteJson", "Prefix", write);
        Check((string)write[1] == original, "save writer ignores unrelated paths");
        write[0] = "human_game";
        InvokeHook("WriteJson", "Prefix", write);
        var serialized = (string)write[1];
        var merged = JObject.Parse(serialized);
        Check(merged.Remove("workerPresetsV1") && JToken.DeepEquals(game, merged), "native checkpoint fields survive mod serialization");
        var context = Activator.CreateInstance(hooks.GetNestedType("ReadContext", Flags), true);
        context.GetType().GetField("Path", Flags).SetValue(context, "human_game");
        hooks.GetField("reading", Flags).SetValue(null, context);
        LayoutBook Restore(string raw, string run)
        {
            InvokeHook("ReadJson", "Postfix", "human_game", raw);
            var data = new GameData { runID = run, hash = game.Value<string>("hash") };
            hooks.GetMethod("Attach", Flags).Invoke(null, new object[] { data });
            InvokeHook("Restore", "Prefix", data);
            return Plugin.Book;
        }
        var run = game.Value<string>("runID");
        // The test round-trips a sibling temporary file, never the supplied checkpoint.
        var scratch = Path.GetTempFileName();
        try
        {
            File.WriteAllText(scratch, serialized);
            var loaded = Restore(File.ReadAllText(scratch), run);
            Check(loaded != book && loaded.Valid() && loaded.Slots[0].Name == "Stone" &&
                loaded.Slots[0].Entries.Count == 4 && loaded.Slots[1].Entries.Count == 2 &&
                loaded.Slots[1].Entries.Last().Removed, "continue restores both named layouts and retired buildings");
        }
        finally { File.Delete(scratch); }
        Check(Restore(serialized, Guid.NewGuid().ToString()).Slots.All(s => s.Entries == null), "different run starts empty");
        Check(Restore(original, run).Slots.All(s => s.Entries == null), "unmodded checkpoint starts with two empty slots");
        Check(File.ReadAllText(copy) == original, "input save copy remains byte-for-byte intact");
    }

    public static void Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Pass the path to a copied game checkpoint.");
        Check(PresetPanel.PreviewRows(720) == 3 && PresetPanel.PreviewRows(720 / 1.5f) == 2 &&
            PresetPanel.PanelWidth(1920) == 520 && PresetPanel.PanelWidth(1920 / 1.5f) == 448 && PresetPanel.PanelWidth(400) == 352,
            "720p preview budgets at 1x and 1.5x scale and narrow panel width");
        SaveContinue(args[0], Scenarios());
    }
}

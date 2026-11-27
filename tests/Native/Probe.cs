using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Buildings;
using SuperFantasyKingdom.UI;

// Runs in an isolated Mono process. Only the worker label, warning and sound
// need scene objects for these unregistered building fixtures.
internal class Probe
{
    private static int paints;
    private static bool Skip() { return false; }
    private static bool Paint() { paints++; return false; }
    private static void Verify(bool ok, string label)
    {
        if (!ok) throw new Exception(label);
        Console.WriteLine("PASS " + label);
    }

    public static void Main(string[] args)
    {
        var plugin = Assembly.LoadFrom(args[0]);
        var harmony = new Harmony("gkj.sfk.workerpresets.offline");
        harmony.PatchAll(plugin);
        var patched = harmony.GetPatchedMethods().ToArray();
        Verify(patched.Length == 10 && patched.Any(m => m.DeclaringType == typeof(UIBuildingOverlayElementWorker)),
            "all ten production Harmony targets installed in the game's Mono runtime");
        var writer = patched.Single(m => m.DeclaringType == typeof(SaveManager) && m.Name == "Save");
        var backupWriter = AccessTools.Method(typeof(SaveManager), "Save", new[] { typeof(string), typeof(string), typeof(bool) });
        var expectedWriter = backupWriter ?? AccessTools.Method(typeof(SaveManager), "Save", new[] { typeof(string), typeof(string) });
        Verify(writer == expectedWriter && Harmony.GetPatchInfo(writer).Prefixes.Count(p => p.owner == harmony.Id) == 1,
            "exactly one mod prefix registered on " + writer);
        var pause = new HarmonyMethod(typeof(Probe), "Skip");
        harmony.Patch(AccessTools.Method(typeof(UIBuildingOverlayElementWorker), "SetWorkerAmount"),
            prefix: new HarmonyMethod(typeof(Probe), "Paint"));
        var sound = AccessTools.TypeByName("FMODUnity.RuntimeManager");
        foreach (var method in sound.GetMethods().Where(m => m.Name == "PlayOneShot" && m.GetParameters()[0].ParameterType == typeof(string)))
            harmony.Patch(method, prefix: pause);
        harmony.Patch(AccessTools.Method(typeof(BuildingCity), "SignShouldBeRed"), prefix: pause);

        var row = new UIBuildingOverlayElementWorker();
        var b = (BuildingCity)FormatterServices.GetUninitializedObject(typeof(BuildingCity));
        b.SetIdentifier(-1);
        AccessTools.Field(typeof(BuildingCity), "m_MaxWorkers").SetValue(b, 3);
        var apply = AccessTools.Method(plugin.GetType("WorkerPresets.CityStaffing"), "ApplyRow");
        Action<int> set = target => apply.Invoke(null, new object[] { row, b, target });
        set(3);
        Verify(b.GetPossibleWorkers() == 3 && paints == 3, "native worker row performs three individual additions");
        set(4);
        Verify(b.GetPossibleWorkers() == 3 && paints == 3, "native capacity rejects excess jobs");
        set(1);
        Verify(b.GetPossibleWorkers() == 1 && paints == 5, "native worker row removes jobs");
        AccessTools.Field(typeof(BuildingCity), "assignJobsIndividually").SetValue(b, true);
        b.SetCurrentlyWorking(1);
        set(0);
        Verify(b.GetPossibleWorkers() == 1 && paints == 6, "native occupied-job floor stops an unachievable reduction");
    }
}

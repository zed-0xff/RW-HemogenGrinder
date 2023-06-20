using HarmonyLib;
using RimWorld;
using Verse;

namespace zed_0xff.HemogenGrinder;

[StaticConstructorOnStartup]
public class Init
{
    static Init()
    {
        Harmony harmony = new Harmony("zed_0xff.HemogenGrinder");
        harmony.PatchAll();
    }
}

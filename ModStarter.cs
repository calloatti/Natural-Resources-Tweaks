using Calloatti.Config;
using HarmonyLib;
using Timberborn.Modding;
using Timberborn.ModManagerScene;
using UnityEngine;

namespace Calloatti.NaturalResourcesTweaks
{
  public class ModStarter : IModStarter
  {
    // Declare the globally accessible static instance
    public static SimpleConfig Config { get; private set; }

    private const string HarmonyId = "calloatti.naturalresourcestweaks";

    // We store the config value here so the patches can read it instantly
    public static bool MarkForDemolition { get; private set; } = true;

    public void StartMod(IModEnvironment modEnvironment)
    {
      // Instantiate the config. This instantly runs the TXT synchronization.
      Config = new SimpleConfig(modEnvironment.ModPath);

      // Apply Harmony patches globally at game startup
      Harmony harmony = new Harmony(HarmonyId);
      harmony.PatchAll(typeof(ModStarter).Assembly);
      Debug.Log($"[{HarmonyId}] All Harmony patches applied successfully!");
    }
  }
}
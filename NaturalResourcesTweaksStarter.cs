using Calloatti.Config;
using HarmonyLib;
using Timberborn.Modding;
using Timberborn.ModManagerScene;
using UnityEngine;

namespace Calloatti.NaturalResourcesTweaks
{
  public class NaturalResourcesTweaksStarter : IModStarter
  {
    private const string HarmonyId = "calloatti.naturalresourcestweaks";

    // We store the config value here so the patches can read it instantly
    public static bool MarkForDemolition { get; private set; } = true;

    public void StartMod(IModEnvironment modEnvironment)
    {
      // 1. Load the config, get/set the default, and save the file immediately
      SimpleIniConfig config = new SimpleIniConfig("NaturalResourcesTweaks.txt");
      MarkForDemolition = config.GetBool("MarkForDemolition", true);
      config.Save();

      // 2. Apply Harmony patches globally at game startup
      Harmony harmony = new Harmony(HarmonyId);
      harmony.PatchAll(typeof(NaturalResourcesTweaksStarter).Assembly);
      Debug.Log($"[{HarmonyId}] All Harmony patches applied successfully!");
    }
  }
}
using Bindito.Core;
using HarmonyLib;
using UnityEngine;

namespace Calloatti.NaturalResourcesTweaks
{
  [Context("Game")]
  internal class PatchConfigurator : IConfigurator
  {
    private const string HarmonyId = "calloatti.naturalresourcestweaks";
    private static Harmony _harmony;

    public void Configure(IContainerDefinition containerDefinition)
    {
      // Register our new custom tool and the UI adder so the game knows they exist
      containerDefinition.Bind<MixedForestTool>().AsSingleton();
      containerDefinition.Bind<MixedForestButtonAdder>().AsSingleton();

      // We only want to patch once. Because this configurator loads in both 
      // the Game and MapEditor contexts, the null check prevents duplicate patching.
      if (_harmony == null)
      {
        _harmony = new Harmony(HarmonyId);
        _harmony.PatchAll(typeof(PatchConfigurator).Assembly);
        Debug.Log($"[{HarmonyId}] All Harmony patches applied successfully!");
      }
    }
  }
}
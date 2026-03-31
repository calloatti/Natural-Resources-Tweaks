using Bindito.Core;
using HarmonyLib;
using Timberborn.BehaviorSystem;
using Timberborn.BlockSystem;
using Timberborn.NaturalResources;
using Timberborn.Planting;
using Timberborn.SingletonSystem;
using Timberborn.TimeSystem;
using Timberborn.WorkSystem;
using UnityEngine;

namespace Calloatti.NaturalResourcesTweaks
{
  // ==========================================
  // 1. STATE HOLDER FOR INJECTED SERVICES
  // ==========================================
  public static class PlantingBugFixState
  {
    public static SpawnValidationService SpawnValidationService;
    public static IBlockService BlockService;
  }

  // ==========================================
  // 2. DEPENDENCY INJECTION BINDING
  // ==========================================
  [Context("Game")]
  public class PlantingBugFixConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<PlantingBugFixLoader>().AsSingleton();
    }
  }

  public class PlantingBugFixLoader : IPostLoadableSingleton
  {
    private readonly SpawnValidationService _spawnValidationService;
    private readonly IBlockService _blockService;

    public PlantingBugFixLoader(SpawnValidationService spawnValidationService, IBlockService blockService)
    {
      _spawnValidationService = spawnValidationService;
      _blockService = blockService;
    }

    public void PostLoad()
    {
      // Cache the services so our Harmony patches can access them safely during the Tick phase
      PlantingBugFixState.SpawnValidationService = _spawnValidationService;
      PlantingBugFixState.BlockService = _blockService;
    }
  }

  // ==========================================
  // 3. HARMONY PATCHES
  // ==========================================

  // Prevent the beaver from even starting the planting process if the spot is fully obstructed
  [HarmonyPatch(typeof(PlantExecutor), nameof(PlantExecutor.Launch))]
  public static class Patch_PlantExecutor_Launch
  {
    public static bool Prefix(Vector3Int coordinates, string resource, ref bool __result)
    {
      if (PlantingBugFixState.SpawnValidationService != null && PlantingBugFixState.BlockService != null)
      {
        if (!PlantingBugFixState.SpawnValidationService.IsUnobstructed(coordinates, resource))
        {
          // Allow the job to launch if the obstruction is just a path, so the beaver can demolish it natively.
          // Otherwise, it's a solid block (like an uncleared Demolishable), so abort early.
          if (PlantingBugFixState.BlockService.GetPathObjectAt(coordinates) == null)
          {
            __result = false;
            return false; // Skip original Launch logic
          }
        }
      }
      return true;
    }
  }

  // Catch the race condition where a space becomes obstructed WHILE the beaver is actively planting
  [HarmonyPatch(typeof(PlantExecutor), nameof(PlantExecutor.Tick))]
  public static class Patch_PlantExecutor_Tick
  {
    public static bool Prefix(PlantExecutor __instance, ref ExecutorStatus __result, Worker ____worker, string ____naturalResource, Vector3Int ____coordinates, float ____finishTimestamp, IDayNightCycle ____dayNightCycle)
    {
      if (!____worker.Workplace || ____naturalResource == null) return true;

      // If the timer is up and the beaver is about to call SpawnResource()
      if (____dayNightCycle.PartialDayNumber > ____finishTimestamp)
      {
        if (PlantingBugFixState.SpawnValidationService != null && PlantingBugFixState.BlockService != null)
        {
          // Verify one last time that the space is completely clear
          if (!PlantingBugFixState.SpawnValidationService.IsUnobstructed(____coordinates, ____naturalResource))
          {
            if (PlantingBugFixState.BlockService.GetPathObjectAt(____coordinates) == null)
            {
              // The spot is obstructed (e.g., the Builder hasn't removed the Demolishable object yet). 
              // Force a safe failure instead of crashing the BlockObject array. The beaver will safely release the job.
              AccessTools.Method(typeof(PlantExecutor), "FinishPlanting").Invoke(__instance, null);
              __result = ExecutorStatus.Failure;
              return false; // Skip original Tick logic
            }
          }
        }
      }
      return true;
    }
  }
}
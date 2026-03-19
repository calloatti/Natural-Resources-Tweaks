using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Timberborn.BlockSystem;
using Timberborn.Demolishing;
using Timberborn.NaturalResources;
using Timberborn.Planting;
using Timberborn.PlantingUI;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Calloatti.NaturalResourcesTweaks
{
  [HarmonyPatch]
  public static class Patch_PlantingAreaSelection
  {
    // We target the private Preview and Action callbacks for BOTH the Planting and Cancel tools
    public static IEnumerable<MethodBase> TargetMethods()
    {
      yield return AccessTools.Method(typeof(PlantingTool), "PreviewCallback");
      yield return AccessTools.Method(typeof(PlantingTool), "ActionCallback");
      yield return AccessTools.Method(typeof(CancelPlantingTool), "PreviewCallback");
      yield return AccessTools.Method(typeof(CancelPlantingTool), "ActionCallback");
    }

    // By passing inputBlocks by 'ref', we can replace the enumerable with our filtered LINQ query
    public static void Prefix(ref IEnumerable<Vector3Int> inputBlocks)
    {
      bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
      bool ctrl = Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;
      bool alt = Keyboard.current != null && Keyboard.current.altKey.isPressed;

      // If no modifiers are pressed, let the game process the blocks normally
      if (!shift && !ctrl && !alt) return;

      // Replace the incoming blocks with our filtered pattern
      inputBlocks = inputBlocks.Where(coords =>
      {
        if (shift) return Math.Abs(coords.x) % 2 == 0;          // Vertical lines (along X axis)
        if (ctrl) return Math.Abs(coords.y) % 2 == 0;           // Horizontal lines (along Y axis)
        if (alt) return Math.Abs(coords.x + coords.y) % 2 == 0; // Checkered pattern

        return true;
      });
    }
  }

  // 1. The UI Patch (Letting you paint)
  [HarmonyPatch(typeof(PlantingAreaValidator), nameof(PlantingAreaValidator.CanPlant))]
  public static class Patch_PlantingAreaValidator_CanPlant
  {
    public static void Postfix(Vector3Int coordinates, string name, ref bool __result, IBlockService ____blockService)
    {
      // If the game says we can't plant here, check if it's because of a Demolishable object
      if (!__result)
      {
        Demolishable demolishable = ____blockService.GetBottomObjectComponentAt<Demolishable>(coordinates);
        if (demolishable != null)
        {
          // Turn the brush green and allow painting right over it
          __result = true;
        }
      }
    }
  }

  // 2. The Backend Trick (Making the beavers do the work)
  [HarmonyPatch(typeof(PlantingService), "CreatePlantingSpot")]
  public static class Patch_PlantingService_CreatePlantingSpot
  {
    public static void Postfix(Vector3Int coordinates, string resourceToPlant, ref PlantingSpot __result, SpawnValidationService ____spawnValidationService, IBlockService ____blockService)
    {
      // If the tile is obstructed, we hijack the PlantingBlocker logic
      if (!____spawnValidationService.IsUnobstructed(coordinates, resourceToPlant))
      {
        Demolishable demolishable = ____blockService.GetBottomObjectComponentAt<Demolishable>(coordinates);
        if (demolishable != null)
        {
          BlockObject blockObject = demolishable.GetComponent<BlockObject>();
          if (blockObject != null)
          {
            // Set the tree/crop as the PlantingBlocker so PlanterWorkplaceBehavior force-demolishes it
            __result = new PlantingSpot(coordinates, resourceToPlant, blockObject);
          }
        }
      }
    }
  }

  // 3. Just mark the existing crop/tree for demolition when painting
  [HarmonyPatch(typeof(PlantingService), nameof(PlantingService.SetPlantingCoordinates))]
  public static class Patch_PlantingService_SetPlantingCoordinates
  {
    public static void Postfix(Vector3Int coordinates, IBlockService ____blockService)
    {
      Demolishable demolishable = ____blockService.GetBottomObjectComponentAt<Demolishable>(coordinates);
      if (demolishable != null && !demolishable.IsMarked)
      {
        demolishable.Mark();
      }
    }
  }
}
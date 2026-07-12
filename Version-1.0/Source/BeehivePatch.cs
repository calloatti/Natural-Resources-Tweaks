using Bindito.Core;
using HarmonyLib;
using System;
using System.Collections.Generic;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.CoreUI;
using Timberborn.EntitySystem;
using Timberborn.Forestry;
using Timberborn.Planting;
using Timberborn.SingletonSystem;
using Timberborn.TemplateSystem;
using Timberborn.UILayoutSystem;
using UnityEngine;

namespace Calloatti.NaturalResourcesTweaks
{
  [Context("Game")]
  public class BeehivePatchConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<BeehivePatchUiListener>().AsSingleton();
    }
  }

  public class BeehivePatchUiListener : ILoadableSingleton, IDisposable
  {
    private readonly EventBus _eventBus;
    public BeehivePatchUiListener(EventBus eventBus) => _eventBus = eventBus;
    public void Load() => _eventBus.Register(this);

    public void Dispose()
    {
      if (_eventBus != null) _eventBus.Unregister(this);
    }

    [OnEvent]
    public void OnShowPrimaryUI(ShowPrimaryUIEvent @event)
    {
      PlacementContext.IsFunctional = true;
      Debug.Log("[BeehivePatch] UI loaded, beehive crop clearing enabled.");
    }
  }

  public static class PlacementContext
  {
    public static BlockObject CurrentValidatingObject;
    public static bool IsFunctional = false;
  }

  public static class CropDetector
  {
    // Helper to identify map editor objects, ruins, and natural resources
    public static bool IsExcludedObject(BlockObject blockObject)
    {
      if (blockObject == null) return false;
      return !blockObject.HasComponent<BuildingSpec>();
    }

    // NEW: Specifically check if the building being placed is a Beehive
    public static bool IsBeehive(BlockObject blockObject)
    {
      if (blockObject == null) return false;
      TemplateSpec templateSpec = blockObject.GetComponent<TemplateSpec>();
      // This will safely catch "Beehive.Folktails" or any other faction's beehive variant
      return templateSpec != null && templateSpec.TemplateName.Contains("Beehive");
    }

    // A strict validation method to determine if an object is a crop.
    public static bool IsRemovableCrop(BlockObject blockObject)
    {
      if (blockObject == null) return false;

      // Must be a planted resource
      if (!blockObject.HasComponent<PlantableSpec>()) return false;

      // Must NOT be a tree (we only want to clear crops like carrots/potatoes)
      if (blockObject.HasComponent<TreeComponent>()) return false;

      return true;
    }
  }

  [HarmonyPatch(typeof(BlockObject), nameof(BlockObject.IsValid))]
  static class BlockObject_IsValid_Patch
  {
    static void Prefix(BlockObject __instance)
    {
      if (PlacementContext.IsFunctional) PlacementContext.CurrentValidatingObject = __instance;
    }

    [HarmonyFinalizer]
    static void Finalizer() => PlacementContext.CurrentValidatingObject = null;
  }

  [HarmonyPatch(typeof(BlockObject), nameof(BlockObject.IsAlmostValid))]
  static class BlockObject_IsAlmostValid_Patch
  {
    static void Prefix(BlockObject __instance)
    {
      if (PlacementContext.IsFunctional) PlacementContext.CurrentValidatingObject = __instance;
    }

    [HarmonyFinalizer]
    static void Finalizer() => PlacementContext.CurrentValidatingObject = null;
  }

  // VALIDATION PHASE: The game asks if a specific tile is blocked. We intercept to say "No" if it's just a crop.
  [HarmonyPatch(typeof(BlockService), nameof(BlockService.AnyNonOverridableObjectsAt))]
  static class BlockService_AnyNonOverridableObjectsAt_Patch
  {
    static void Postfix(Vector3Int coordinates, BlockOccupations occupations, ref bool __result, BlockService __instance)
    {
      if (!PlacementContext.IsFunctional || !__result) return;

      // CRITICAL FIX: Only spoof if the PLAYER is actively validating a building preview. 
      if (PlacementContext.CurrentValidatingObject == null) return;
      if (CropDetector.IsExcludedObject(PlacementContext.CurrentValidatingObject)) return;

      // NEW RESTRICTION: Only spoof the grid if the player is specifically holding a Beehive
      if (!CropDetector.IsBeehive(PlacementContext.CurrentValidatingObject)) return;

      var objectsAtTile = __instance.GetObjectsAt(coordinates);
      bool onlyBlockedByRemovableCrops = true;
      bool foundRemovableCrop = false;

      foreach (var obj in objectsAtTile)
      {
        if (obj.Overridable) continue;

        if (CropDetector.IsRemovableCrop(obj))
        {
          foundRemovableCrop = true;
          continue;
        }

        // If it's a solid object that isn't a crop, check if their occupations actually intersect
        if (obj.PositionedBlocks != null)
        {
          var block = obj.PositionedBlocks.GetBlock(coordinates);
          if (block.Occupation.Intersects(occupations))
          {
            onlyBlockedByRemovableCrops = false;
            break;
          }
        }
        else
        {
          onlyBlockedByRemovableCrops = false;
          break;
        }
      }

      // If the ONLY thing in our way is a crop, spoof the result so the preview turns green
      if (foundRemovableCrop && onlyBlockedByRemovableCrops)
      {
        __result = false;
      }
    }
  }

  // EXECUTION PHASE: The player clicked "Build". The game is spawning the real building.
  [HarmonyPatch(typeof(BlockObject), "AddToService")]
  static class BlockObject_AddToService_Patch
  {
    static void Prefix(BlockObject __instance)
    {
      // Ignore previews, and only act if the building has a physical footprint and is a player-built structure
      if (!PlacementContext.IsFunctional || __instance.IsPreview || __instance.AddedToService) return;
      if (__instance.PositionedBlocks == null) return;
      if (CropDetector.IsExcludedObject(__instance)) return;

      // NEW RESTRICTION: Only execute the aggressive crop deletion if the building spawning is actually a Beehive
      if (!CropDetector.IsBeehive(__instance)) return;

      // Iterate through the exact footprint of the placed building
      foreach (var block in __instance.PositionedBlocks.GetAllBlocks())
      {
        var objectsAtTile = __instance._blockService.GetObjectsAt(block.Coordinates);
        List<BlockObject> toDelete = new List<BlockObject>();

        foreach (var objAtTile in objectsAtTile)
        {
          if (objAtTile != null && objAtTile != __instance && CropDetector.IsRemovableCrop(objAtTile))
          {
            // Direct Spatial Check: Only delete the crop if our new building physically shares the same slot
            if (objAtTile.PositionedBlocks != null)
            {
              var cropBlock = objAtTile.PositionedBlocks.GetBlock(block.Coordinates);
              if (cropBlock.Occupation.Intersects(block.Occupation))
              {
                toDelete.Add(objAtTile);
              }
            }
          }
        }

        // Execute the deletions instantly
        foreach (var crop in toDelete)
        {
          if (crop != null)
          {
            // Instantly yank the crop out of the Bottom occupation slot synchronously
            ((IDeletableEntity)crop).DeleteEntity();

            // Safely flag the GameObject for native Unity destruction
            __instance._entityService.Delete(crop);
          }
        }
      }
    }
  }
}
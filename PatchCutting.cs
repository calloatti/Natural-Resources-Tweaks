using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.AreaSelectionSystemUI;
using Timberborn.BlockSystem;
using Timberborn.Forestry;
using Timberborn.ForestryUI;
using Timberborn.Rendering;
using Timberborn.SelectionSystem;
using Timberborn.TerrainQueryingSystem;
using Timberborn.TerrainSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Calloatti.NaturalResourcesTweaks
{
  public static class TreeCuttingAreaHelper
  {
    // Evaluates modifiers, and snaps X,Y coordinates to the Z-level in direct vertical line of sight
    public static IEnumerable<Vector3Int> ProcessBlocks(IEnumerable<Vector3Int> inputBlocks, TerrainAreaService terrainAreaService, IBlockService blockService)
    {
      // Extract the private ITerrainService so we can check for underground blocks
      ITerrainService terrainService = (ITerrainService)AccessTools.Field(typeof(TerrainAreaService), "_terrainService").GetValue(terrainAreaService);

      bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
      bool ctrl = Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;
      bool alt = Keyboard.current != null && Keyboard.current.altKey.isPressed;

      // The game feeds us blocks perfectly flat at the exact Z-level of the initial click
      Vector3Int firstBlock = inputBlocks.FirstOrDefault();
      int initialZ = firstBlock != default ? firstBlock.z : 0;

      // If Alt is held, select every tile on the map
      if (alt)
      {
        Vector3Int mapSize = blockService.Size;
        List<Vector3Int> allMapCoords = new List<Vector3Int>(mapSize.x * mapSize.y);
        for (int x = 0; x < mapSize.x; x++)
        {
          for (int y = 0; y < mapSize.y; y++)
          {
            allMapCoords.Add(SnapToSurface(new Vector3Int(x, y, initialZ), terrainService, blockService));
          }
        }
        return allMapCoords;
      }

      // Otherwise, apply modifier filters if any are pressed
      IEnumerable<Vector3Int> filteredBlocks = inputBlocks;
      if (shift || ctrl)
      {
        filteredBlocks = inputBlocks.Where(coords =>
        {
          if (shift && ctrl) return Math.Abs(coords.x + coords.y) % 2 == 0; // Checkered pattern
          if (shift) return Math.Abs(coords.x) % 2 == 0;                   // Vertical lines
          if (ctrl) return Math.Abs(coords.y) % 2 == 0;                    // Horizontal lines
          return true;
        });
      }

      // Snap each flat coordinate to the actual surface in vertical line of sight
      return filteredBlocks.Select(b => SnapToSurface(b, terrainService, blockService));
    }

    private static Vector3Int SnapToSurface(Vector3Int startPos, ITerrainService terrainService, IBlockService blockService)
    {
      int x = startPos.x;
      int y = startPos.y;
      int initialZ = startPos.z;

      if (terrainService.Underground(startPos))
      {
        // We dragged into a hill. Go UP until we find air.
        for (int z = initialZ; z <= terrainService.Size.z; z++)
        {
          Vector3Int pos = new Vector3Int(x, y, z);
          if (!terrainService.Underground(pos))
          {
            return pos; // The surface block (air resting on terrain)
          }
        }
        return new Vector3Int(x, y, terrainService.Size.z);
      }
      else
      {
        // We dragged over a valley. Go DOWN until we hit terrain or an object.
        for (int z = initialZ; z >= 0; z--)
        {
          Vector3Int pos = new Vector3Int(x, y, z);

          if (terrainService.Underground(pos))
          {
            return new Vector3Int(x, y, z + 1); // Surface is the block right above the terrain
          }

          if (blockService.GetBottomObjectAt(pos) != null)
          {
            return pos; // Hit a platform, tree, or building
          }
        }
        return new Vector3Int(x, y, 0); // Reached the bottom of the map
      }
    }
  }

  [HarmonyPatch(typeof(TreeCuttingAreaSelectionTool))]
  public static class Patch_TreeCuttingAreaSelectionTool
  {
    [HarmonyPrefix]
    [HarmonyPatch("PreviewCallback")]
    public static bool PreviewCallback_Prefix(
        IEnumerable<Vector3Int> inputBlocks, Ray ray,
        TreeCuttingArea ____treeCuttingArea,
        TerrainAreaService ____terrainAreaService,
        AreaHighlightingService ____areaHighlightingService,
        IBlockService ____blockService,
        MeasurableAreaDrawer ____measurableAreaDrawer,
        Color ____toolActionTileColor)
    {
      var smartCoords = TreeCuttingAreaHelper.ProcessBlocks(inputBlocks, ____terrainAreaService, ____blockService);

      foreach (Vector3Int item in smartCoords)
      {
        if (!____treeCuttingArea.IsInCuttingArea(item))
        {
          ____areaHighlightingService.DrawTile(item, ____toolActionTileColor);
          ____measurableAreaDrawer.AddMeasurableCoordinates(item);

          TreeComponent tree = ____blockService.GetBottomObjectComponentAt<TreeComponent>(item);
          if (tree != null)
          {
            ____areaHighlightingService.AddForHighlight(tree);
          }
        }
      }
      ____areaHighlightingService.Highlight();

      return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("ActionCallback")]
    public static bool ActionCallback_Prefix(
        IEnumerable<Vector3Int> inputBlocks, Ray ray,
        TreeCuttingArea ____treeCuttingArea,
        TerrainAreaService ____terrainAreaService,
        AreaHighlightingService ____areaHighlightingService,
        IBlockService ____blockService)
    {
      ____areaHighlightingService.UnhighlightAll();
      var smartCoords = TreeCuttingAreaHelper.ProcessBlocks(inputBlocks, ____terrainAreaService, ____blockService);
      ____treeCuttingArea.AddCoordinates(smartCoords);
      return false;
    }
  }

  [HarmonyPatch(typeof(TreeCuttingAreaUnselectionTool))]
  public static class Patch_TreeCuttingAreaUnselectionTool
  {
    [HarmonyPrefix]
    [HarmonyPatch("PreviewCallback")]
    public static bool PreviewCallback_Prefix(
        IEnumerable<Vector3Int> inputBlocks, Ray ray,
        TreeCuttingArea ____treeCuttingArea,
        TerrainAreaService ____terrainAreaService,
        AreaHighlightingService ____areaHighlightingService,
        IBlockService ____blockService,
        MeasurableAreaDrawer ____measurableAreaDrawer,
        MeshDrawer ____actionMeshDrawer,
        MeshDrawer ____noActionMeshDrawer)
    {
      var smartCoords = TreeCuttingAreaHelper.ProcessBlocks(inputBlocks, ____terrainAreaService, ____blockService);

      foreach (Vector3Int item in smartCoords)
      {
        ____measurableAreaDrawer.AddMeasurableCoordinates(item);
        if (____treeCuttingArea.IsInCuttingArea(item))
        {
          ____actionMeshDrawer.DrawAtCoordinates(item, 0.03f);
          TreeComponent tree = ____blockService.GetBottomObjectComponentAt<TreeComponent>(item);
          if (tree != null)
          {
            ____areaHighlightingService.AddForHighlight(tree);
          }
        }
        else
        {
          ____noActionMeshDrawer.DrawAtCoordinates(item, 0.02f);
        }
      }
      ____areaHighlightingService.Highlight();
      return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("ActionCallback")]
    public static bool ActionCallback_Prefix(
        IEnumerable<Vector3Int> inputBlocks, Ray ray,
        TreeCuttingArea ____treeCuttingArea,
        TerrainAreaService ____terrainAreaService,
        AreaHighlightingService ____areaHighlightingService,
        IBlockService ____blockService)
    {
      ____areaHighlightingService.UnhighlightAll();
      var smartCoords = TreeCuttingAreaHelper.ProcessBlocks(inputBlocks, ____terrainAreaService, ____blockService);
      ____treeCuttingArea.RemoveCoordinates(smartCoords);
      return false;
    }
  }
}
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
using UnityEngine;
using UnityEngine.InputSystem;

namespace Calloatti.NaturalResourcesTweaks
{
  public static class TreeCuttingAreaHelper
  {
    // Evaluates modifiers, generates columns, and returns all valid Z-levels for those columns
    public static IEnumerable<Vector3Int> ProcessBlocks(IEnumerable<Vector3Int> inputBlocks, TerrainAreaService terrainAreaService, IBlockService blockService)
    {
      bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
      bool ctrl = Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;
      bool alt = Keyboard.current != null && Keyboard.current.altKey.isPressed;

      // If BOTH Shift and Ctrl are held, select every tile on the map
      if (shift && ctrl)
      {
        Vector3Int mapSize = blockService.Size;
        List<Vector2Int> allMapCoords = new List<Vector2Int>(mapSize.x * mapSize.y);
        for (int x = 0; x < mapSize.x; x++)
        {
          for (int y = 0; y < mapSize.y; y++)
          {
            allMapCoords.Add(new Vector2Int(x, y));
          }
        }
        return terrainAreaService.InMapCoordinates(allMapCoords);
      }

      // Otherwise, apply modifier filters if any are pressed
      IEnumerable<Vector3Int> filteredBlocks = inputBlocks;
      if (shift || ctrl || alt)
      {
        filteredBlocks = inputBlocks.Where(coords =>
        {
          if (shift) return Math.Abs(coords.x) % 2 == 0;          // Vertical lines
          if (ctrl) return Math.Abs(coords.y) % 2 == 0;           // Horizontal lines
          if (alt) return Math.Abs(coords.x + coords.y) % 2 == 0; // Checkered pattern
          return true;
        });
      }

      // Extract unique X,Y columns and return all valid Z-levels for them
      IEnumerable<Vector2Int> coords2D = filteredBlocks.Select(b => new Vector2Int(b.x, b.y)).Distinct();
      return terrainAreaService.InMapCoordinates(coords2D);
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
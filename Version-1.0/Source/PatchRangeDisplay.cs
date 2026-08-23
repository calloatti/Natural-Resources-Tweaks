using Bindito.Core;
using HarmonyLib;
using System;
using System.Collections.Generic;
using Timberborn.Buildings;
using Timberborn.BuildingsNavigation;
using Timberborn.BuildingsReachability;
using Timberborn.EntitySystem;
using Timberborn.Forestry;
using Timberborn.Planting;
using Timberborn.PlantingUI;
using Timberborn.Rendering;
using Timberborn.SelectionSystem;
using Timberborn.SelectionToolSystem;
using Timberborn.SingletonSystem;
using Timberborn.TemplateSystem;
using Timberborn.ToolSystem;
using UnityEngine;

namespace Calloatti.NaturalResourcesTweaks
{
  // ====================================================================
  // 1. ISOLATED BINDINGS 
  // ====================================================================
  [Context("Game")]
  internal class RangeDisplayConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<NRTweaksRegistryLocator>().AsSingleton();
      Bind<UnifiedPlantingRangeDrawer>().AsSingleton();
    }
  }

  // ====================================================================
  // 2. LOCATOR & CUSTOM DRAWER LOGIC
  // ====================================================================
  internal class NRTweaksRegistryLocator : ILoadableSingleton, IDisposable
  {
    public static EntityComponentRegistry Registry { get; private set; }

    public NRTweaksRegistryLocator(EntityComponentRegistry registry)
    {
      Registry = registry;
    }

    public void Load() { }

    public void Dispose()
    {
      Registry = null;
    }
  }

  public class UnifiedPlantingRangeDrawer : ILoadableSingleton, IUpdatableSingleton, IDisposable
  {
    private readonly BoundsNavRangeDrawer _boundsDrawer;
    private readonly AreaHighlightingService _areaHighlightingService;
    private readonly EventBus _eventBus;
    private readonly ToolGroupService _toolGroupService;
    private readonly TemplateService _templateService;

    private IReadOnlyCollection<Vector3Int> _currentArea;
    private readonly Color _fillColor = new Color(0f, 0.6f, 0.5f, 0.35f);

    private string _treeTemplate;
    private string _cropTemplate;

    public static UnifiedPlantingRangeDrawer Instance { get; private set; }
    public bool IsActive { get; private set; }

    // Inject the new services for tool group testing
    public UnifiedPlantingRangeDrawer(
      BoundsNavRangeDrawer boundsDrawer,
      AreaHighlightingService areaHighlightingService,
      EventBus eventBus,
      ToolGroupService toolGroupService,
      TemplateService templateService)
    {
      _boundsDrawer = boundsDrawer;
      _areaHighlightingService = areaHighlightingService;
      _eventBus = eventBus;
      _toolGroupService = toolGroupService;
      _templateService = templateService;
      Instance = this;
    }

    public void Load()
    {
      _eventBus.Register(this);
    }

    // Dynamically find one tree and one crop to test building capabilities
    private void EnsureTemplatesLoaded()
    {
      if (_treeTemplate == null || _cropTemplate == null)
      {
        foreach (var spec in _templateService.GetAll<PlantableSpec>())
        {
          if (spec.HasSpec<TreeComponentSpec>()) _treeTemplate = spec.TemplateName;
          else _cropTemplate = spec.TemplateName;

          if (_treeTemplate != null && _cropTemplate != null) break;
        }
      }
    }

    // Use the native EventBus instead of Harmony to detect when a tool is equipped
    [OnEvent]
    public void OnToolEntered(ToolEnteredEvent e)
    {
      if (NRTweaksRegistryLocator.Registry == null) return;

      ToolGroupSpec fieldsGroup = null;
      ToolGroupSpec forestryGroup = null;

      try { fieldsGroup = _toolGroupService.GetGroup("Fields"); } catch { }
      try { forestryGroup = _toolGroupService.GetGroup("Forestry"); } catch { }

      bool isFields = fieldsGroup != null && _toolGroupService.IsAssignedToGroup(e.Tool, fieldsGroup);
      bool isForestry = forestryGroup != null && _toolGroupService.IsAssignedToGroup(e.Tool, forestryGroup);

      if (!isFields && !isForestry)
      {
        HideRanges();
        return;
      }

      EnsureTemplatesLoaded();
      HashSet<Vector3Int> combinedRange = new HashSet<Vector3Int>();

      foreach (Building building in NRTweaksRegistryLocator.Registry.GetEnabled<Building>())
      {
        PlanterBuilding planter = building.GetComponent<PlanterBuilding>();
        if (planter == null) continue;

        bool canAdd = false;
        // If the tool is in Fields, keep only farms (they plant crops)
        if (isFields && _cropTemplate != null && planter.CanPlant(_cropTemplate)) canAdd = true;

        // If the tool is in Forestry, keep only foresters (they plant trees)
        if (isForestry && _treeTemplate != null && planter.CanPlant(_treeTemplate)) canAdd = true;

        if (canAdd)
        {
          BuildingTerrainRange terrainRange = building.GetComponent<BuildingTerrainRange>();
          if (terrainRange != null)
          {
            foreach (Vector3Int coords in terrainRange.GetRange())
            {
              combinedRange.Add(coords);
            }
          }
        }
      }

      ShowRanges(combinedRange);
    }

    [OnEvent]
    public void OnToolExited(ToolExitedEvent e)
    {
      HideRanges();
    }

    public void UpdateSingleton()
    {
      if (IsActive)
      {
        _boundsDrawer.Draw();
      }
    }

    public void DrawInteriorHighlights(IEnumerable<Vector3Int> cursorBlocks)
    {
      if (_currentArea != null)
      {
        HashSet<Vector3Int> activeCursor = new HashSet<Vector3Int>();
        if (cursorBlocks != null)
        {
          activeCursor.UnionWith(cursorBlocks);
        }

        foreach (var coords in _currentArea)
        {
          if (!activeCursor.Contains(coords))
          {
            _areaHighlightingService.DrawTile(coords, _fillColor);
          }
        }
        _areaHighlightingService.Highlight();
      }
    }

    public void ShowRanges(IReadOnlyCollection<Vector3Int> combinedArea)
    {
      _boundsDrawer.UpdateArea(combinedArea);
      _currentArea = combinedArea;
      IsActive = true;
      DrawInteriorHighlights(null);
    }

    public void HideRanges()
    {
      IsActive = false;
      _boundsDrawer.UpdateArea(new Vector3Int[0]);

      if (_currentArea != null)
      {
        _areaHighlightingService.UnhighlightAll();
        _currentArea = null;
      }
    }

    public void Dispose()
    {
      if (_eventBus != null) _eventBus.Unregister(this);
      Instance = null;
    }
  }

  // ====================================================================
  // 3. HARMONY PATCHES
  // ====================================================================

  [HarmonyPatch(typeof(BoundsNavRangeDrawingService), nameof(BoundsNavRangeDrawingService.DrawRange))]
  public static class Patch_BoundsNavRangeDrawingService_DrawRange
  {
    public static bool Prefix()
    {
      if (UnifiedPlantingRangeDrawer.Instance != null && UnifiedPlantingRangeDrawer.Instance.IsActive)
      {
        return false;
      }
      return true;
    }
  }

  // We now intercept both PlantingTool and CancelPlantingTool so the cursor does not paint teal
  internal static class PlantingRangeHighlightPostfix
  {
    public static void Postfix(IEnumerable<Vector3Int> inputBlocks)
    {
      if (UnifiedPlantingRangeDrawer.Instance != null && UnifiedPlantingRangeDrawer.Instance.IsActive)
      {
        UnifiedPlantingRangeDrawer.Instance.DrawInteriorHighlights(inputBlocks);
      }
    }
  }

  [HarmonyPatch(typeof(PlantingTool), nameof(PlantingTool.PreviewCallback))]
  public static class Patch_Range_PlantingTool_PreviewCallback
  {
    public static void Postfix(IEnumerable<Vector3Int> inputBlocks) => PlantingRangeHighlightPostfix.Postfix(inputBlocks);
  }

  [HarmonyPatch(typeof(PlantingTool), nameof(PlantingTool.ActionCallback))]
  public static class Patch_Range_PlantingTool_ActionCallback
  {
    public static void Postfix(IEnumerable<Vector3Int> inputBlocks) => PlantingRangeHighlightPostfix.Postfix(inputBlocks);
  }

  [HarmonyPatch(typeof(CancelPlantingTool), nameof(CancelPlantingTool.PreviewCallback))]
  public static class Patch_Range_CancelPlantingTool_PreviewCallback
  {
    public static void Postfix(IEnumerable<Vector3Int> inputBlocks) => PlantingRangeHighlightPostfix.Postfix(inputBlocks);
  }

  [HarmonyPatch(typeof(CancelPlantingTool), nameof(CancelPlantingTool.ActionCallback))]
  public static class Patch_Range_CancelPlantingTool_ActionCallback
  {
    public static void Postfix(IEnumerable<Vector3Int> inputBlocks) => PlantingRangeHighlightPostfix.Postfix(inputBlocks);
  }
}
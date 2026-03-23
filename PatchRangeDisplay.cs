using Bindito.Core;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
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

    // Inyectamos los nuevos servicios para el test de grupos
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

    // Buscamos dinámicamente un árbol y un cultivo para testear las capacidades de los edificios
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

    // Usamos el EventBus nativo en lugar de Harmony para detectar cuando se equipa una herramienta
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
        // Si el tool está en Fields, nos quedamos solo con las granjas (plantan crops)
        if (isFields && _cropTemplate != null && planter.CanPlant(_cropTemplate)) canAdd = true;

        // Si el tool está en Forestry, nos quedamos solo con los foresters (plantan trees)
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

  // Ahora interceptamos tanto el PlantingTool como el CancelPlantingTool para que el cursor no se pinte de teal
  [HarmonyPatch]
  public static class Patch_PlantingTool_Callbacks
  {
    public static IEnumerable<MethodBase> TargetMethods()
    {
      yield return AccessTools.Method(typeof(PlantingTool), "PreviewCallback");
      yield return AccessTools.Method(typeof(PlantingTool), "ActionCallback");
      yield return AccessTools.Method(typeof(CancelPlantingTool), "PreviewCallback");
      yield return AccessTools.Method(typeof(CancelPlantingTool), "ActionCallback");
    }

    public static void Postfix(IEnumerable<Vector3Int> inputBlocks)
    {
      if (UnifiedPlantingRangeDrawer.Instance != null && UnifiedPlantingRangeDrawer.Instance.IsActive)
      {
        UnifiedPlantingRangeDrawer.Instance.DrawInteriorHighlights(inputBlocks);
      }
    }
  }
}
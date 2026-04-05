using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using Timberborn.AreaSelectionSystemUI;
using Timberborn.BlockSystem;
using Timberborn.CameraSystem;
using Timberborn.Forestry;
using Timberborn.ForestryUI;
using Timberborn.Rendering;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainQueryingSystem;
using Timberborn.TerrainSystem;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using Timberborn.Localization;
using UnityEngine;
using UnityEngine.UIElements;
using Timberborn.SelectionToolSystem;
using Timberborn.NaturalResourcesLifecycle;
using Timberborn.Planting;

namespace Calloatti.NaturalResourcesTweaks
{
  public static class WoodcuttingState
  {
    public static SelectionPattern Pattern = SelectionPattern.Solid;
    public static SelectionScope Scope = SelectionScope.Manual;
    public static SelectionLevel Level = SelectionLevel.Single;
    public static SelectionTreeType TreeType = SelectionTreeType.All;
    public static CameraService CameraService;
  }

  [Context("Game")]
  public class WoodcuttingToolConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<WoodcuttingPatternTool>().AsSingleton();
      Bind<WoodcuttingScopeTool>().AsSingleton();
      Bind<WoodcuttingLevelTool>().AsSingleton();
      Bind<WoodcuttingTreeTypeTool>().AsSingleton();
      Bind<WoodcuttingButtonAdder>().AsSingleton();
    }
  }

  public class WoodcuttingPatternTool : SharedToggleToolBase
  {
    private readonly ILoc _loc;
    public WoodcuttingPatternTool(SelectionToolProcessorFactory f, ILoc loc) : base(f)
    {
      _loc = loc;
    }

    public override void Cycle()
    {
      WoodcuttingState.Pattern = (SelectionPattern)(((int)WoodcuttingState.Pattern + 1) % 4);
      UpdateIcons();
    }

    protected override void UpdateIcons()
    {
      if (Sprites == null) return;
      var bg = new StyleBackground(Sprites[(int)WoodcuttingState.Pattern]);
      foreach (var icon in Icons) if (icon != null) icon.style.backgroundImage = bg;
    }

    public override ToolDescription DescribeTool() => new ToolDescription.Builder(_loc.T("Calloatti.NaturalResourcesTweaks.WoodcuttingPatternTool.Title")).AddSection(_loc.T("Calloatti.NaturalResourcesTweaks.WoodcuttingPatternTool.Description")).Build();
  }

  public class WoodcuttingScopeTool : SharedToggleToolBase
  {
    private readonly ILoc _loc;
    public WoodcuttingScopeTool(SelectionToolProcessorFactory f, ILoc loc) : base(f)
    {
      _loc = loc;
    }

    public override void Cycle()
    {
      WoodcuttingState.Scope = (SelectionScope)(((int)WoodcuttingState.Scope + 1) % 2);
      UpdateIcons();
    }

    protected override void UpdateIcons()
    {
      if (Sprites == null) return;
      var bg = new StyleBackground(Sprites[(int)WoodcuttingState.Scope]);
      foreach (var icon in Icons) if (icon != null) icon.style.backgroundImage = bg;
    }

    public override ToolDescription DescribeTool() => new ToolDescription.Builder(_loc.T("Calloatti.NaturalResourcesTweaks.WoodcuttingScopeTool.Title")).AddSection(_loc.T("Calloatti.NaturalResourcesTweaks.WoodcuttingScopeTool.Description")).Build();
  }

  public class WoodcuttingLevelTool : SharedToggleToolBase
  {
    private readonly ILoc _loc;
    public WoodcuttingLevelTool(SelectionToolProcessorFactory f, ILoc loc) : base(f)
    {
      _loc = loc;
    }

    public override void Cycle()
    {
      WoodcuttingState.Level = (SelectionLevel)(((int)WoodcuttingState.Level + 1) % 2);
      UpdateIcons();
    }

    protected override void UpdateIcons()
    {
      if (Sprites == null) return;
      var bg = new StyleBackground(Sprites[(int)WoodcuttingState.Level]);
      foreach (var icon in Icons) if (icon != null) icon.style.backgroundImage = bg;
    }

    public override ToolDescription DescribeTool() => new ToolDescription.Builder(_loc.T("Calloatti.NaturalResourcesTweaks.WoodcuttingLevelTool.Title")).AddSection(_loc.T("Calloatti.NaturalResourcesTweaks.WoodcuttingLevelTool.Description")).Build();
  }

  public class WoodcuttingTreeTypeTool : SharedToggleToolBase
  {
    private readonly ILoc _loc;
    public WoodcuttingTreeTypeTool(SelectionToolProcessorFactory f, ILoc loc) : base(f)
    {
      _loc = loc;
    }

    public override void Cycle()
    {
      WoodcuttingState.TreeType = (SelectionTreeType)(((int)WoodcuttingState.TreeType + 1) % 2);
      UpdateIcons();
    }

    protected override void UpdateIcons()
    {
      if (Sprites == null) return;
      var bg = new StyleBackground(Sprites[(int)WoodcuttingState.TreeType]);
      foreach (var icon in Icons) if (icon != null) icon.style.backgroundImage = bg;
    }

    public override ToolDescription DescribeTool() => new ToolDescription.Builder(_loc.T("Calloatti.NaturalResourcesTweaks.WoodcuttingTreeTypeTool.Title")).AddSection(_loc.T("Calloatti.NaturalResourcesTweaks.WoodcuttingTreeTypeTool.Description")).Build();
  }

  public class WoodcuttingButtonAdder : IPostLoadableSingleton, IDisposable
  {
    private readonly ToolButtonFactory _f;
    private readonly ToolGroupService _g;
    private readonly ToolButtonService _s;
    private readonly WoodcuttingPatternTool _tp;
    private readonly WoodcuttingScopeTool _ts;
    private readonly WoodcuttingLevelTool _tl;
    private readonly WoodcuttingTreeTypeTool _tt;
    private readonly ToolService _toolService;
    private readonly EventBus _eventBus;

    private ITool _lastRealTool;

    public WoodcuttingButtonAdder(ToolButtonFactory f, ToolGroupService g, ToolButtonService s, WoodcuttingPatternTool tp, WoodcuttingScopeTool ts, WoodcuttingLevelTool tl, WoodcuttingTreeTypeTool tt, CameraService cam, ToolService toolService, EventBus eventBus)
    {
      _f = f; _g = g; _s = s; _tp = tp; _ts = ts; _tl = tl; _tt = tt;
      WoodcuttingState.CameraService = cam;
      _toolService = toolService;
      _eventBus = eventBus;
      _eventBus.Register(this);
    }

    public void Dispose()
    {
      WoodcuttingState.CameraService = null;
      if (_eventBus != null) _eventBus.Unregister(this);
    }

    [OnEvent]
    public void OnToolEntered(ToolEnteredEvent e)
    {
      if (e.Tool != _tp && e.Tool != _ts && e.Tool != _tl && e.Tool != _tt)
      {
        _lastRealTool = e.Tool;
      }
    }

    public void PostLoad()
    {
      ToolGroupSpec group = _g.GetGroup("TreeCutting");
      ToolButton exist = _s.ToolButtons.FirstOrDefault(b => _g.IsAssignedToGroup(b.Tool, group));
      if (exist == null) return;

      var gBtn = _s.GetToolGroupButton(exist);

      Add(_tp, i => SharedSpriteGenerator.GenPattern((SelectionPattern)i), typeof(SelectionPattern), gBtn, group, (int)WoodcuttingState.Pattern);
      Add(_ts, i => SharedSpriteGenerator.GenScope((SelectionScope)i), typeof(SelectionScope), gBtn, group, (int)WoodcuttingState.Scope);
      Add(_tl, i => SharedSpriteGenerator.GenLevel((SelectionLevel)i), typeof(SelectionLevel), gBtn, group, (int)WoodcuttingState.Level);
      Add(_tt, i => SharedSpriteGenerator.GenTreeType((SelectionTreeType)i), typeof(SelectionTreeType), gBtn, group, (int)WoodcuttingState.TreeType);
    }

    private void Add(SharedToggleToolBase tool, Func<int, Sprite> gen, Type enumType, ToolGroupButton gBtn, ToolGroupSpec group, int currentState)
    {
      var sprites = Enum.GetValues(enumType).Cast<int>().Select(gen).ToArray();
      tool.SetSprites(sprites);

      var btn = _f.Create(tool, sprites[currentState], gBtn.ToolButtonsElement);
      btn.PostLoad();

      tool.RegisterIcon(btn.Root.Q<VisualElement>("ToolImage"));

      btn.Root.Q<Button>().RegisterCallback<ClickEvent>(e => {
        tool.Cycle();

        if (_lastRealTool != null && _g.ActiveToolGroup != null && _g.IsAssignedToGroup(_lastRealTool, _g.ActiveToolGroup))
        {
          _toolService.SwitchTool(_lastRealTool);
        }
      });

      gBtn.AddTool(btn);
      _g.AssignToGroup(group, tool);
    }
  }

  public static class TreeCuttingAreaHelper
  {
    // Pass TreeCuttingArea so we can optimize the "Whole Map Unselect" operation
    public static IEnumerable<Vector3Int> ProcessBlocks(IEnumerable<Vector3Int> inputBlocks, TerrainAreaService terrainAreaService, IBlockService blockService, TreeCuttingArea treeCuttingArea, bool isUnselecting)
    {
      var blocksList = inputBlocks.ToList();
      if (blocksList.Count == 0) return blocksList;

      ITerrainService terrainService = (ITerrainService)AccessTools.Field(typeof(TerrainAreaService), "_terrainService").GetValue(terrainAreaService);

      Vector3Int anchor = blocksList.First();
      int initialZ = anchor.z;

      if (WoodcuttingState.Scope == SelectionScope.WholeMap)
      {
        // Massively optimized Whole Map Unselection
        if (isUnselecting && treeCuttingArea != null)
        {
          return treeCuttingArea.CuttingArea.ToList();
        }

        // Standard Whole Map Selection
        Vector3Int mapSize = blockService.Size;
        List<Vector3Int> allMapCoords = new List<Vector3Int>(mapSize.x * mapSize.y);
        for (int x = 0; x < mapSize.x; x++)
        {
          for (int y = 0; y < mapSize.y; y++)
          {
            if (WoodcuttingState.Level == SelectionLevel.Multi)
            {
              allMapCoords.Add(SnapToSurface(new Vector3Int(x, y, initialZ), terrainService));
            }
            else
            {
              allMapCoords.Add(new Vector3Int(x, y, initialZ));
            }
          }
        }
        return allMapCoords;
      }

      IEnumerable<Vector3Int> workingBlocks = blocksList;

      if (WoodcuttingState.Pattern != SelectionPattern.Solid)
      {
        int rotIndex = Mathf.RoundToInt(((WoodcuttingState.CameraService.HorizontalAngle % 360 + 360) % 360) / 90f) % 4;

        workingBlocks = workingBlocks.Where(coords =>
        {
          int localX = Math.Abs(coords.x - anchor.x);
          int localY = Math.Abs(coords.y - anchor.y);

          return WoodcuttingState.Pattern switch
          {
            SelectionPattern.Checkered => (localX + localY) % 2 == 0,
            SelectionPattern.Vertical => (rotIndex % 2 == 0) ? localX % 2 == 0 : localY % 2 == 0,
            SelectionPattern.Horizontal => (rotIndex % 2 == 0) ? localY % 2 == 0 : localX % 2 == 0,
            _ => true
          };
        });
      }

      if (WoodcuttingState.Level == SelectionLevel.Multi)
      {
        workingBlocks = workingBlocks.Select(b => SnapToSurface(b, terrainService));
      }

      return workingBlocks.ToList();
    }

    private static Vector3Int SnapToSurface(Vector3Int startPos, ITerrainService terrainService)
    {
      int targetZ = terrainService.GetTerrainHeight(startPos);
      return new Vector3Int(startPos.x, startPos.y, targetZ);
    }
  }

  [HarmonyPatch(typeof(TreeCuttingAreaSelectionTool))]
  public static class Patch_TreeCuttingAreaSelectionTool
  {
    [HarmonyPrefix]
    [HarmonyPatch("PreviewCallback")]
    public static bool PreviewCallback_Prefix(ref IEnumerable<Vector3Int> inputBlocks, Ray ray, TreeCuttingArea ____treeCuttingArea, TerrainAreaService ____terrainAreaService, AreaHighlightingService ____areaHighlightingService, IBlockService ____blockService, MeasurableAreaDrawer ____measurableAreaDrawer, Color ____toolActionTileColor)
    {
      inputBlocks = TreeCuttingAreaHelper.ProcessBlocks(inputBlocks, ____terrainAreaService, ____blockService, ____treeCuttingArea, false);

      if (WoodcuttingState.Level == SelectionLevel.Single) return true;

      foreach (Vector3Int item in inputBlocks)
      {
        if (!____treeCuttingArea.IsInCuttingArea(item))
        {
          ____areaHighlightingService.DrawTile(item, ____toolActionTileColor);
          ____measurableAreaDrawer.AddMeasurableCoordinates(item);
          TreeComponent tree = ____blockService.GetBottomObjectComponentAt<TreeComponent>(item);
          if (tree != null) ____areaHighlightingService.AddForHighlight(tree);
        }
      }
      ____areaHighlightingService.Highlight();
      return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("ActionCallback")]
    public static bool ActionCallback_Prefix(ref IEnumerable<Vector3Int> inputBlocks, Ray ray, TreeCuttingArea ____treeCuttingArea, TerrainAreaService ____terrainAreaService, AreaHighlightingService ____areaHighlightingService, IBlockService ____blockService)
    {
      inputBlocks = TreeCuttingAreaHelper.ProcessBlocks(inputBlocks, ____terrainAreaService, ____blockService, ____treeCuttingArea, false);

      if (WoodcuttingState.TreeType == SelectionTreeType.OnlyDead)
      {
        inputBlocks = inputBlocks.Where(pos => {
          Vector3Int posAbove = new Vector3Int(pos.x, pos.y, pos.z + 1);
          BlockObject objAtZPlus1 = ____blockService.GetBottomObjectComponentAt<BlockObject>(posAbove);

          LivingNaturalResource resource = null;

          if (objAtZPlus1 != null)
          {
            resource = objAtZPlus1.GetComponent<LivingNaturalResource>();
          }

          // Fallback to exact Z if nothing was found at Z+1
          if (resource == null)
          {
            BlockObject objAtZ = ____blockService.GetBottomObjectComponentAt<BlockObject>(pos);
            if (objAtZ != null)
            {
              resource = objAtZ.GetComponent<LivingNaturalResource>();
            }
          }

          return resource != null && resource.IsDead;
        }).ToList();
      }

      if (WoodcuttingState.Level == SelectionLevel.Single) return true;

      ____areaHighlightingService.UnhighlightAll();
      ____treeCuttingArea.AddCoordinates(inputBlocks);
      return false;
    }
  }

  [HarmonyPatch(typeof(TreeCuttingAreaUnselectionTool))]
  public static class Patch_TreeCuttingAreaUnselectionTool
  {
    [HarmonyPrefix]
    [HarmonyPatch("PreviewCallback")]
    public static bool PreviewCallback_Prefix(ref IEnumerable<Vector3Int> inputBlocks, Ray ray, TreeCuttingArea ____treeCuttingArea, TerrainAreaService ____terrainAreaService, AreaHighlightingService ____areaHighlightingService, IBlockService ____blockService, MeasurableAreaDrawer ____measurableAreaDrawer, MeshDrawer ____actionMeshDrawer, MeshDrawer ____noActionMeshDrawer)
    {
      // Pass isUnselecting = true
      inputBlocks = TreeCuttingAreaHelper.ProcessBlocks(inputBlocks, ____terrainAreaService, ____blockService, ____treeCuttingArea, true);

      if (WoodcuttingState.Level == SelectionLevel.Single && WoodcuttingState.Scope != SelectionScope.WholeMap) return true;

      foreach (Vector3Int item in inputBlocks)
      {
        ____measurableAreaDrawer.AddMeasurableCoordinates(item);
        if (____treeCuttingArea.IsInCuttingArea(item))
        {
          ____actionMeshDrawer.DrawAtCoordinates(item, 0.03f);
          TreeComponent tree = ____blockService.GetBottomObjectComponentAt<TreeComponent>(item);
          if (tree != null) ____areaHighlightingService.AddForHighlight(tree);
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
    public static bool ActionCallback_Prefix(ref IEnumerable<Vector3Int> inputBlocks, Ray ray, TreeCuttingArea ____treeCuttingArea, TerrainAreaService ____terrainAreaService, AreaHighlightingService ____areaHighlightingService, IBlockService ____blockService)
    {
      // Pass isUnselecting = true
      inputBlocks = TreeCuttingAreaHelper.ProcessBlocks(inputBlocks, ____terrainAreaService, ____blockService, ____treeCuttingArea, true);

      if (WoodcuttingState.Level == SelectionLevel.Single && WoodcuttingState.Scope != SelectionScope.WholeMap) return true;

      ____areaHighlightingService.UnhighlightAll();
      ____treeCuttingArea.RemoveCoordinates(inputBlocks);
      return false;
    }
  }
}
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
using UnityEngine;
using UnityEngine.UIElements;
using Timberborn.SelectionToolSystem;

namespace Calloatti.NaturalResourcesTweaks
{
  public static class WoodcuttingState
  {
    public static SelectionPattern Pattern = SelectionPattern.Solid;
    public static SelectionScope Scope = SelectionScope.Manual;
    public static CameraService CameraService;
  }

  [Context("Game")]
  public class WoodcuttingToolConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<WoodcuttingPatternTool>().AsSingleton();
      Bind<WoodcuttingScopeTool>().AsSingleton();
      Bind<WoodcuttingButtonAdder>().AsSingleton();
    }
  }

  public class WoodcuttingPatternTool : SharedToggleToolBase
  {
    public WoodcuttingPatternTool(SelectionToolProcessorFactory f) : base(f) { }

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

    public override ToolDescription DescribeTool() => new ToolDescription.Builder("Cutting Pattern").AddSection("Cycle selection patterns.").Build();
  }

  public class WoodcuttingScopeTool : SharedToggleToolBase
  {
    public WoodcuttingScopeTool(SelectionToolProcessorFactory f) : base(f) { }

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

    public override ToolDescription DescribeTool() => new ToolDescription.Builder("Selection Scope").AddSection("Toggle Manual vs. Whole Map.").Build();
  }

  public class WoodcuttingButtonAdder : IPostLoadableSingleton, IDisposable
  {
    private readonly ToolButtonFactory _f;
    private readonly ToolGroupService _g;
    private readonly ToolButtonService _s;
    private readonly WoodcuttingPatternTool _tp;
    private readonly WoodcuttingScopeTool _ts;
    private readonly ToolService _toolService;

    private ITool _lastRealTool;

    public WoodcuttingButtonAdder(ToolButtonFactory f, ToolGroupService g, ToolButtonService s, WoodcuttingPatternTool tp, WoodcuttingScopeTool ts, CameraService cam, ToolService toolService, EventBus eventBus)
    {
      _f = f; _g = g; _s = s; _tp = tp; _ts = ts;
      WoodcuttingState.CameraService = cam;
      _toolService = toolService;
      eventBus.Register(this);
    }

    public void Dispose()
    {
      WoodcuttingState.CameraService = null;
    }

    [OnEvent]
    public void OnToolEntered(ToolEnteredEvent e)
    {
      if (e.Tool != _tp && e.Tool != _ts)
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

        // Safety Check: Only switch if the tool actually belongs to the open panel
        if (_lastRealTool != null && _g.ActiveToolGroup != null && _g.IsAssignedToGroup(_lastRealTool, _g.ActiveToolGroup))
        {
          _toolService.SwitchTool(_lastRealTool);
        }
        // Left the fake tool equipped if there isn't a valid fallback
      });

      gBtn.AddTool(btn);
      _g.AssignToGroup(group, tool);
    }
  }

  public static class TreeCuttingAreaHelper
  {
    public static IEnumerable<Vector3Int> ProcessBlocks(IEnumerable<Vector3Int> inputBlocks, TerrainAreaService terrainAreaService, IBlockService blockService)
    {
      ITerrainService terrainService = (ITerrainService)AccessTools.Field(typeof(TerrainAreaService), "_terrainService").GetValue(terrainAreaService);
      bool alt = WoodcuttingState.Scope == SelectionScope.WholeMap;

      Vector3Int firstBlock = inputBlocks.FirstOrDefault();
      int initialZ = firstBlock != default ? firstBlock.z : 0;

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

      IEnumerable<Vector3Int> filteredBlocks = inputBlocks;
      if (WoodcuttingState.Pattern != SelectionPattern.Solid && inputBlocks.Any())
      {
        Vector3Int anchor = inputBlocks.First();
        int rotIndex = Mathf.RoundToInt(((WoodcuttingState.CameraService.HorizontalAngle % 360 + 360) % 360) / 90f) % 4;

        filteredBlocks = inputBlocks.Where(coords =>
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

      return filteredBlocks.Select(b => SnapToSurface(b, terrainService, blockService));
    }

    private static Vector3Int SnapToSurface(Vector3Int startPos, ITerrainService terrainService, IBlockService blockService)
    {
      int x = startPos.x;
      int y = startPos.y;
      int initialZ = startPos.z;

      if (terrainService.Underground(startPos))
      {
        for (int z = initialZ; z <= terrainService.Size.z; z++)
        {
          Vector3Int pos = new Vector3Int(x, y, z);
          if (!terrainService.Underground(pos)) return pos;
        }
        return new Vector3Int(x, y, terrainService.Size.z);
      }
      else
      {
        for (int z = initialZ; z >= 0; z--)
        {
          Vector3Int pos = new Vector3Int(x, y, z);
          if (terrainService.Underground(pos)) return new Vector3Int(x, y, z + 1);
          if (blockService.GetBottomObjectAt(pos) != null) return pos;
        }
        return new Vector3Int(x, y, 0);
      }
    }
  }

  [HarmonyPatch(typeof(TreeCuttingAreaSelectionTool))]
  public static class Patch_TreeCuttingAreaSelectionTool
  {
    [HarmonyPrefix]
    [HarmonyPatch("PreviewCallback")]
    public static bool PreviewCallback_Prefix(IEnumerable<Vector3Int> inputBlocks, Ray ray, TreeCuttingArea ____treeCuttingArea, TerrainAreaService ____terrainAreaService, AreaHighlightingService ____areaHighlightingService, IBlockService ____blockService, MeasurableAreaDrawer ____measurableAreaDrawer, Color ____toolActionTileColor)
    {
      var smartCoords = TreeCuttingAreaHelper.ProcessBlocks(inputBlocks, ____terrainAreaService, ____blockService);
      foreach (Vector3Int item in smartCoords)
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
    public static bool ActionCallback_Prefix(IEnumerable<Vector3Int> inputBlocks, Ray ray, TreeCuttingArea ____treeCuttingArea, TerrainAreaService ____terrainAreaService, AreaHighlightingService ____areaHighlightingService, IBlockService ____blockService)
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
    public static bool PreviewCallback_Prefix(IEnumerable<Vector3Int> inputBlocks, Ray ray, TreeCuttingArea ____treeCuttingArea, TerrainAreaService ____terrainAreaService, AreaHighlightingService ____areaHighlightingService, IBlockService ____blockService, MeasurableAreaDrawer ____measurableAreaDrawer, MeshDrawer ____actionMeshDrawer, MeshDrawer ____noActionMeshDrawer)
    {
      var smartCoords = TreeCuttingAreaHelper.ProcessBlocks(inputBlocks, ____terrainAreaService, ____blockService);
      foreach (Vector3Int item in smartCoords)
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
    public static bool ActionCallback_Prefix(IEnumerable<Vector3Int> inputBlocks, Ray ray, TreeCuttingArea ____treeCuttingArea, TerrainAreaService ____terrainAreaService, AreaHighlightingService ____areaHighlightingService, IBlockService ____blockService)
    {
      ____areaHighlightingService.UnhighlightAll();
      var smartCoords = TreeCuttingAreaHelper.ProcessBlocks(inputBlocks, ____terrainAreaService, ____blockService);
      ____treeCuttingArea.RemoveCoordinates(smartCoords);
      return false;
    }
  }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bindito.Core;
using HarmonyLib;
using Timberborn.BlockSystem;
using Timberborn.CameraSystem;
using Timberborn.Demolishing;
using Timberborn.NaturalResources;
using Timberborn.Planting;
using Timberborn.PlantingUI;
using Timberborn.SelectionToolSystem;
using Timberborn.SingletonSystem;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using Timberborn.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Calloatti.NaturalResourcesTweaks
{
  public static class PlantingState
  {
    public static SelectionPattern Pattern = SelectionPattern.Solid;
    public static SelectionReplantMode ReplantMode = SelectionReplantMode.Replant;
    public static CameraService CameraService;
  }

  [Context("Game")]
  public class PatternToolConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<PlantingPatternTool>().AsSingleton();
      Bind<PlantingReplantTool>().AsSingleton();
      Bind<PlantingButtonAdder>().AsSingleton();
    }
  }

  public class PlantingPatternTool : SharedToggleToolBase
  {
    private readonly ILoc _loc;
    public PlantingPatternTool(SelectionToolProcessorFactory f, ILoc loc) : base(f)
    {
      _loc = loc;
    }

    public override void Cycle()
    {
      PlantingState.Pattern = (SelectionPattern)(((int)PlantingState.Pattern + 1) % 4);
      UpdateIcons();
    }

    protected override void UpdateIcons()
    {
      if (Sprites == null) return;
      var currentSprite = new StyleBackground(Sprites[(int)PlantingState.Pattern]);
      foreach (var icon in Icons)
      {
        if (icon != null) icon.style.backgroundImage = currentSprite;
      }
    }

    public override ToolDescription DescribeTool() => new ToolDescription.Builder(_loc.T("Calloatti.NaturalResourcesTweaks.PlantingPatternTool.Title")).AddSection(_loc.T("Calloatti.NaturalResourcesTweaks.PlantingPatternTool.Description")).Build();
  }

  public class PlantingReplantTool : SharedToggleToolBase
  {
    private readonly ILoc _loc;
    public PlantingReplantTool(SelectionToolProcessorFactory f, ILoc loc) : base(f)
    {
      _loc = loc;
    }

    public override void Cycle()
    {
      PlantingState.ReplantMode = (SelectionReplantMode)(((int)PlantingState.ReplantMode + 1) % 2);
      UpdateIcons();
    }

    protected override void UpdateIcons()
    {
      if (Sprites == null) return;
      var currentSprite = new StyleBackground(Sprites[(int)PlantingState.ReplantMode]);
      foreach (var icon in Icons)
      {
        if (icon != null) icon.style.backgroundImage = currentSprite;
      }
    }

    public override ToolDescription DescribeTool() => new ToolDescription.Builder(_loc.T("Calloatti.NaturalResourcesTweaks.PlantingReplantTool.Title")).AddSection(_loc.T("Calloatti.NaturalResourcesTweaks.PlantingReplantTool.Description")).Build();
  }

  public class PlantingButtonAdder : IPostLoadableSingleton, IDisposable
  {
    private readonly ToolButtonFactory _f;
    private readonly ToolGroupService _g;
    private readonly ToolButtonService _s;
    private readonly PlantingPatternTool _tp;
    private readonly PlantingReplantTool _tr;
    private readonly ToolService _toolService;
    private readonly EventBus _eventBus;

    private ITool _lastRealTool;

    public PlantingButtonAdder(ToolButtonFactory f, ToolGroupService g, ToolButtonService s, PlantingPatternTool tp, PlantingReplantTool tr, CameraService cam, ToolService toolService, EventBus eventBus)
    {
      _f = f; _g = g; _s = s; _tp = tp; _tr = tr;
      PlantingState.CameraService = cam;
      _toolService = toolService;
      _eventBus = eventBus;
      _eventBus.Register(this);
    }

    public void Dispose()
    {
      PlantingState.CameraService = null;
      SharedSpriteGenerator.ClearCache();
      if (_eventBus != null) _eventBus.Unregister(this);
    }

    [OnEvent]
    public void OnToolEntered(ToolEnteredEvent e)
    {
      if (e.Tool != _tp && e.Tool != _tr)
      {
        _lastRealTool = e.Tool;
      }
    }

    public void PostLoad()
    {
      var patternSprites = Enum.GetValues(typeof(SelectionPattern)).Cast<SelectionPattern>().Select(SharedSpriteGenerator.GenPattern).ToArray();
      _tp.SetSprites(patternSprites);

      var replantSprites = Enum.GetValues(typeof(SelectionReplantMode)).Cast<SelectionReplantMode>().Select(SharedSpriteGenerator.GenReplantMode).ToArray();
      _tr.SetSprites(replantSprites);

      string[] groups = { "Fields", "Forestry" };
      foreach (string groupName in groups)
      {
        AddButtonToGroup(groupName, _tp, patternSprites, (int)PlantingState.Pattern);
        AddButtonToGroup(groupName, _tr, replantSprites, (int)PlantingState.ReplantMode);
      }
    }

    private void AddButtonToGroup(string groupName, SharedToggleToolBase tool, Sprite[] sprites, int currentState)
    {
      ToolGroupSpec group = _g.GetGroup(groupName);
      ToolButton existing = _s.ToolButtons.FirstOrDefault(b => _g.IsAssignedToGroup(b.Tool, group));
      if (existing == null) return;

      var gBtn = _s.GetToolGroupButton(existing);
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
      });

      var cancel = _s.ToolButtons.FirstOrDefault(b => (b.Tool is CancelPlantingTool || b.Tool.GetType().Name.Contains("Cancel")) && _g.IsAssignedToGroup(b.Tool, group));
      if (cancel != null) btn.Root.PlaceBehind(cancel.Root);

      gBtn.AddTool(btn);
      _g.AssignToGroup(group, tool);
    }
  }

  [HarmonyPatch]
  public static class Patch_PlantingAreaSelection
  {
    public static IEnumerable<MethodBase> TargetMethods()
    {
      yield return AccessTools.Method(typeof(PlantingTool), "PreviewCallback");
      yield return AccessTools.Method(typeof(PlantingTool), "ActionCallback");
      yield return AccessTools.Method(typeof(CancelPlantingTool), "PreviewCallback");
      yield return AccessTools.Method(typeof(CancelPlantingTool), "ActionCallback");
    }

    public static void Prefix(ref IEnumerable<Vector3Int> inputBlocks)
    {
      if (PlantingState.Pattern == SelectionPattern.Solid || PlantingState.CameraService == null) return;

      var blocksList = inputBlocks.ToList();
      if (blocksList.Count == 0) return;

      Vector3Int anchor = blocksList.First();
      int rotIndex = Mathf.RoundToInt(((PlantingState.CameraService.HorizontalAngle % 360 + 360) % 360) / 90f) % 4;

      inputBlocks = blocksList.Where(c =>
      {
        int localX = Math.Abs(c.x - anchor.x);
        int localY = Math.Abs(c.y - anchor.y);

        return PlantingState.Pattern switch
        {
          SelectionPattern.Checkered => (localX + localY) % 2 == 0,
          SelectionPattern.Vertical => (rotIndex % 2 == 0) ? localX % 2 == 0 : localY % 2 == 0,
          SelectionPattern.Horizontal => (rotIndex % 2 == 0) ? localY % 2 == 0 : localX % 2 == 0,
          _ => true
        };
      });
    }
  }

  [HarmonyPatch(typeof(PlantingAreaValidator), nameof(PlantingAreaValidator.CanPlant))]
  public static class Patch_PlantingAreaValidator_CanPlant
  {
    public static void Postfix(Vector3Int coordinates, string name, ref bool __result, IBlockService ____blockService)
    {
      if (PlantingState.ReplantMode == SelectionReplantMode.Vanilla) return;

      if (!__result)
      {
        Demolishable demolishable = ____blockService.GetBottomObjectComponentAt<Demolishable>(coordinates);
        if (demolishable != null)
        {
          __result = true;
        }
      }
    }
  }

  [HarmonyPatch(typeof(PlantingService), "CreatePlantingSpot")]
  public static class Patch_PlantingService_CreatePlantingSpot
  {
    public static void Postfix(Vector3Int coordinates, string resourceToPlant, ref PlantingSpot __result, SpawnValidationService ____spawnValidationService, IBlockService ____blockService)
    {
      if (PlantingState.ReplantMode == SelectionReplantMode.Vanilla) return;

      if (!____spawnValidationService.IsUnobstructed(coordinates, resourceToPlant))
      {
        PlantableSpec plantableSpec = ____blockService.GetBottomObjectComponentAt<PlantableSpec>(coordinates);
        if (plantableSpec != null && plantableSpec.TemplateName == resourceToPlant) return;

        Demolishable demolishable = ____blockService.GetBottomObjectComponentAt<Demolishable>(coordinates);
        if (demolishable != null)
        {
          BlockObject blockObject = demolishable.GetComponent<BlockObject>();
          if (blockObject != null)
          {
            __result = new PlantingSpot(coordinates, resourceToPlant, blockObject);
          }
        }
      }
    }
  }

  [HarmonyPatch(typeof(PlantingService), nameof(PlantingService.SetPlantingCoordinates))]
  public static class Patch_PlantingService_SetPlantingCoordinates
  {
    public static void Postfix(Vector3Int coordinates, string resource, IBlockService ____blockService)
    {
      if (PlantingState.ReplantMode == SelectionReplantMode.Vanilla) return;

      if (!ModStarter.Config.GetBool("MarkForDemolition")) return;

      bool alt = Keyboard.current != null && Keyboard.current.altKey.isPressed;
      if (alt) return;

      PlantableSpec plantableSpec = ____blockService.GetBottomObjectComponentAt<PlantableSpec>(coordinates);
      if (plantableSpec != null && plantableSpec.TemplateName == resource) return;

      Demolishable demolishable = ____blockService.GetBottomObjectComponentAt<Demolishable>(coordinates);
      if (demolishable != null && !demolishable.IsMarked)
      {
        demolishable.Mark();
      }
    }
  }
}
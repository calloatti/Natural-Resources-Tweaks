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
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Calloatti.NaturalResourcesTweaks
{
  public static class PlantingState
  {
    public static SelectionPattern Pattern = SelectionPattern.Solid;
    public static CameraService CameraService;
  }

  [Context("Game")]
  public class PatternToolConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<PlantingPatternTool>().AsSingleton();
      Bind<PlantingButtonAdder>().AsSingleton();
    }
  }

  public class PlantingPatternTool : SharedToggleToolBase
  {
    public PlantingPatternTool(SelectionToolProcessorFactory f) : base(f) { }

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

    public override ToolDescription DescribeTool() => new ToolDescription.Builder("Planting Pattern").AddSection("Click to cycle to the next pattern.").Build();
  }

  public class PlantingButtonAdder : IPostLoadableSingleton, IDisposable
  {
    private readonly ToolButtonFactory _f;
    private readonly ToolGroupService _g;
    private readonly ToolButtonService _s;
    private readonly PlantingPatternTool _t;
    private readonly ToolService _toolService;
    private readonly EventBus _eventBus;

    private ITool _lastRealTool;

    public PlantingButtonAdder(ToolButtonFactory f, ToolGroupService g, ToolButtonService s, PlantingPatternTool t, CameraService cam, ToolService toolService, EventBus eventBus)
    {
      _f = f; _g = g; _s = s; _t = t;
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
      if (e.Tool != _t)
      {
        _lastRealTool = e.Tool;
      }
    }

    public void PostLoad()
    {
      var sprites = Enum.GetValues(typeof(SelectionPattern)).Cast<SelectionPattern>().Select(SharedSpriteGenerator.GenPattern).ToArray();
      _t.SetSprites(sprites);

      string[] groups = { "Fields", "Forestry" };
      foreach (string groupName in groups) AddButtonToGroup(groupName, sprites);
    }

    private void AddButtonToGroup(string groupName, Sprite[] sprites)
    {
      ToolGroupSpec group = _g.GetGroup(groupName);
      ToolButton existing = _s.ToolButtons.FirstOrDefault(b => _g.IsAssignedToGroup(b.Tool, group));
      if (existing == null) return;

      var gBtn = _s.GetToolGroupButton(existing);
      var btn = _f.Create(_t, sprites[(int)PlantingState.Pattern], gBtn.ToolButtonsElement);
      btn.PostLoad();

      _t.RegisterIcon(btn.Root.Q<VisualElement>("ToolImage"));

      btn.Root.Q<Button>().RegisterCallback<ClickEvent>(e => {
        _t.Cycle();

        // Safety Check: Only switch if the tool actually belongs to the open panel
        if (_lastRealTool != null && _g.ActiveToolGroup != null && _g.IsAssignedToGroup(_lastRealTool, _g.ActiveToolGroup))
        {
          _toolService.SwitchTool(_lastRealTool);
        }
        // If there is no previous tool, we just leave our pattern tool equipped!
      });

      var cancel = _s.ToolButtons.FirstOrDefault(b => (b.Tool is CancelPlantingTool || b.Tool.GetType().Name.Contains("Cancel")) && _g.IsAssignedToGroup(b.Tool, group));
      if (cancel != null) btn.Root.PlaceBehind(cancel.Root);

      gBtn.AddTool(btn);
      _g.AssignToGroup(group, _t);
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
      if (!ModStarter.MarkForDemolition) return;

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
using System;
using System.Collections.Generic;
using Timberborn.SelectionToolSystem;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.NaturalResourcesTweaks
{
  // ==========================================
  // 1. SHARED ENUMS
  // ==========================================
  public enum SelectionPattern { Solid, Checkered, Vertical, Horizontal }
  public enum SelectionScope { Manual, WholeMap }
  public enum SelectionLevel { Single, Multi }
  public enum SelectionTreeType { All, OnlyDead }
  public enum SelectionReplantMode { Replant, Vanilla }

  // ==========================================
  // 2. SHARED GENERIC TOOL BASE
  // ==========================================
  public abstract class SharedToggleToolBase : ITool, IToolDescriptor
  {
    protected readonly SelectionToolProcessor Processor;
    protected readonly List<VisualElement> Icons = new List<VisualElement>();
    protected Sprite[] Sprites;

    protected SharedToggleToolBase(SelectionToolProcessorFactory factory)
    {
      Processor = factory.Create((i, r) => { }, (i, r) => { }, () => { }, "PlantingCursor");
    }

    public void SetSprites(Sprite[] sprites)
    {
      Sprites = sprites;
    }

    public void RegisterIcon(VisualElement icon)
    {
      if (!Icons.Contains(icon))
      {
        Icons.Add(icon);
      }
      UpdateIcons();
    }

    public abstract void Cycle();
    protected abstract void UpdateIcons();

    public void Enter() => Processor.Enter();
    public void Exit() => Processor.Exit();
    public bool ProcessInput() => Processor.ProcessInput();
    public abstract ToolDescription DescribeTool();
  }

  // ==========================================
  // 3. SHARED SPRITE GENERATOR 
  // ==========================================
  public static class SharedSpriteGenerator
  {
    private static readonly Color Gold = new Color(0.89f, 0.79f, 0.56f, 1f);
    private static readonly Color Ghost = new Color(0.89f, 0.79f, 0.56f, 0.15f);

    private static readonly Dictionary<SelectionPattern, Sprite> PatternCache = new Dictionary<SelectionPattern, Sprite>();
    private static readonly Dictionary<SelectionScope, Sprite> ScopeCache = new Dictionary<SelectionScope, Sprite>();
    private static readonly Dictionary<SelectionLevel, Sprite> LevelCache = new Dictionary<SelectionLevel, Sprite>();
    private static readonly Dictionary<SelectionTreeType, Sprite> TreeTypeCache = new Dictionary<SelectionTreeType, Sprite>();
    private static readonly Dictionary<SelectionReplantMode, Sprite> ReplantModeCache = new Dictionary<SelectionReplantMode, Sprite>();

    public static Sprite GenPattern(SelectionPattern p)
    {
      if (PatternCache.TryGetValue(p, out Sprite cachedSprite)) return cachedSprite;

      Sprite newSprite = Draw(tex => {
        int grid = 5, sq = 12, gap = 7, off = 20;
        for (int c = 0; c < grid; c++)
        {
          for (int r = 0; r < grid; r++)
          {
            bool act = p switch
            {
              SelectionPattern.Solid => true,
              SelectionPattern.Checkered => (c + r) % 2 == 0,
              SelectionPattern.Vertical => c % 2 == 0,
              SelectionPattern.Horizontal => r % 2 == 0,
              _ => true
            };
            Fill(tex, off + c * (sq + gap), off + r * (sq + gap), sq, sq, act ? Gold : Ghost);
          }
        }
      });

      PatternCache[p] = newSprite;
      return newSprite;
    }

    public static Sprite GenScope(SelectionScope s)
    {
      if (ScopeCache.TryGetValue(s, out Sprite cachedSprite)) return cachedSprite;

      Sprite newSprite = Draw(tex => {
        if (s == SelectionScope.Manual)
        {
          Fill(tex, 48, 48, 32, 32, Gold);
        }
        else
        {
          int off = 20, size = 88, border = 4;
          Fill(tex, off, off, size, size, Ghost);
          Fill(tex, off, off, size, border, Gold);
          Fill(tex, off, off + size - border, size, border, Gold);
          Fill(tex, off, off, border, size, Gold);
          Fill(tex, off + size - border, off, border, size, Gold);
        }
      });

      ScopeCache[s] = newSprite;
      return newSprite;
    }

    public static Sprite GenLevel(SelectionLevel l)
    {
      if (LevelCache.TryGetValue(l, out Sprite cachedSprite)) return cachedSprite;

      Sprite newSprite = Draw(tex => {
        if (l == SelectionLevel.Single)
        {
          Fill(tex, 20, 60, 88, 8, Gold);
        }
        else
        {
          Fill(tex, 20, 40, 30, 8, Gold);
          Fill(tex, 42, 40, 8, 48, Gold);
          Fill(tex, 42, 80, 44, 8, Gold);
          Fill(tex, 78, 40, 8, 48, Gold);
          Fill(tex, 78, 40, 30, 8, Gold);
        }
      });

      LevelCache[l] = newSprite;
      return newSprite;
    }

    public static Sprite GenTreeType(SelectionTreeType t)
    {
      if (TreeTypeCache.TryGetValue(t, out Sprite cachedSprite)) return cachedSprite;

      Sprite newSprite = Draw(tex => {
        int w = 20;
        int h = 72;
        int y = 28;
        int border = 4;

        Action<int> drawOutline = (xPos) => {
          Fill(tex, xPos, y, w, border, Gold);
          Fill(tex, xPos, y + h - border, w, border, Gold);
          Fill(tex, xPos, y, border, h, Gold);
          Fill(tex, xPos + w - border, y, border, h, Gold);
        };

        if (t == SelectionTreeType.All)
        {
          drawOutline(20);
          Fill(tex, 54, y, w, h, Gold); // Middle filled
          drawOutline(88);
        }
        else
        {
          drawOutline(20);
          drawOutline(54); // Middle outline only
          drawOutline(88);
        }
      });

      TreeTypeCache[t] = newSprite;
      return newSprite;
    }

    public static Sprite GenReplantMode(SelectionReplantMode m)
    {
      if (ReplantModeCache.TryGetValue(m, out Sprite cachedSprite)) return cachedSprite;

      Sprite newSprite = Draw(tex => {
        if (m == SelectionReplantMode.Replant)
        {
          // Two overlapping squares (indicating replace/stack over)
          Fill(tex, 32, 48, 48, 48, Ghost);
          Fill(tex, 48, 32, 48, 48, Gold);
        }
        else
        {
          // Single square (indicating standard vanilla behavior)
          Fill(tex, 40, 40, 48, 48, Gold);
        }
      });

      ReplantModeCache[m] = newSprite;
      return newSprite;
    }

    // Explicitly destroy the unmanaged textures to free RAM/VRAM on map unload
    public static void ClearCache()
    {
      foreach (var sprite in PatternCache.Values)
      {
        if (sprite != null)
        {
          if (sprite.texture != null) UnityEngine.Object.Destroy(sprite.texture);
          UnityEngine.Object.Destroy(sprite);
        }
      }
      PatternCache.Clear();

      foreach (var sprite in ScopeCache.Values)
      {
        if (sprite != null)
        {
          if (sprite.texture != null) UnityEngine.Object.Destroy(sprite.texture);
          UnityEngine.Object.Destroy(sprite);
        }
      }
      ScopeCache.Clear();

      foreach (var sprite in LevelCache.Values)
      {
        if (sprite != null)
        {
          if (sprite.texture != null) UnityEngine.Object.Destroy(sprite.texture);
          UnityEngine.Object.Destroy(sprite);
        }
      }
      LevelCache.Clear();

      foreach (var sprite in TreeTypeCache.Values)
      {
        if (sprite != null)
        {
          if (sprite.texture != null) UnityEngine.Object.Destroy(sprite.texture);
          UnityEngine.Object.Destroy(sprite);
        }
      }
      TreeTypeCache.Clear();

      foreach (var sprite in ReplantModeCache.Values)
      {
        if (sprite != null)
        {
          if (sprite.texture != null) UnityEngine.Object.Destroy(sprite.texture);
          UnityEngine.Object.Destroy(sprite);
        }
      }
      ReplantModeCache.Clear();
    }

    private static Sprite Draw(Action<Texture2D> a)
    {
      var t = new Texture2D(128, 128, TextureFormat.RGBA32, true);
      t.filterMode = FilterMode.Bilinear;
      t.wrapMode = TextureWrapMode.Clamp;

      for (int i = 0; i < 128 * 128; i++) t.SetPixel(i % 128, i / 128, Color.clear);

      a(t);
      t.Apply(true);
      return Sprite.Create(t, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
    }

    private static void Fill(Texture2D t, int x, int y, int w, int h, Color c)
    {
      for (int i = 0; i < w; i++)
      {
        for (int j = 0; j < h; j++)
        {
          t.SetPixel(x + i, y + j, c);
        }
      }
    }
  }
}
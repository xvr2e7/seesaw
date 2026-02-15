# Player Tool Controller Refactoring - Integration Guide

## Overview

The PlayerToolController has been refactored into a modular component system that reveals the "operational layer" of machine vision. The new system uses a **bounding box detection** aesthetic (like CV object recognition) instead of circular rings, and displays turbulence pattern keywords to show what the "machine sees."

**Note:** The old controller has been backed up as `PlayerToolController_Old.cs`

## New Components Created

### Core Components

1. **`TurbulenceClassifier.cs`** - Detects turbulence events at cursor position
2. **`PerformanceTracker.cs`** - Tracks flow coherence and adjusts grid density
3. **`SamplingGrid.cs`** - Renders the visual sampling grid and event labels
4. **`ToolEnergySystem.cs`** - Manages energy drain/regen (extracted from old controller)
5. **`PlayerToolController_New.cs`** - Orchestrates all components
6. **`GridParticleEffect.cs`** - Spawns particles from grid sample points

### Key Features

- **Dynamic Grid Density**: 3x3 (struggling) → 5x5 (baseline) → 7x7 (excelling)
- **Minimal Text**: Only shows single-word event name when over turbulence
- **Visual Feedback**: Grid pulses, color shifts, and brightness communicate status
- **Performance-Based Rewards**: Better flow coherence = denser grid = better perception

## Integration Steps

### Integration Steps

1. **Backup your scene** first!

2. **On your existing PlayerToolController GameObject**:
   - The old controller is now backed up as `PlayerToolController_Old.cs`
   - The refactored controller is now the main `PlayerToolController.cs`
   - Unity may need to reimport - let it finish
   - Component references should auto-update

3. **Clean up old visuals**:
   - Find child objects "ToolCursorRing" and "ToolEnergyRing" if they exist
   - Delete them (replaced by bounding box)

4. **Add GridParticleEffect** (optional but recommended):
   - Add `GridParticleEffect` component to your tool GameObject
   - It will auto-find SamplingGrid and PlayerToolController

5. **Test in Play Mode**:
   - Bounding box should appear at cursor
   - Click and drag to smooth flow
   - Pattern keywords (VORTEX, SCATTER, etc.) appear when over turbulence
   - Box quality changes based on performance

### Settings to Adjust

#### In Inspector - PlayerToolController

- **Base Radius**: 8 (default) - size of sampling area
- **Scroll Sensitivity**: 0.5 - how fast radius changes
- **Dampening Strength**: 0.3 to 0.85 - smoothing intensity
- **Ramp Up Time**: 1.5s - time to reach max strength

#### In Inspector - SamplingGrid (auto-created)

- **Point Size**: 0.2 - size of grid dots
- **Idle Pulse Speed**: 2 - calm breathing effect
- **Active Pulse Speed**: 10 - intense pulsing when active
- **Idle Color**: Light blue (0.6, 0.8, 1, 0.7)
- **Active Color**: Bright cyan (0.3, 0.9, 1, 1)
- **Event Name Font Size**: 18

#### In Inspector - PerformanceTracker (auto-created)

- **Min Grid Size**: 3 (degraded performance)
- **Default Grid Size**: 5 (baseline)
- **Max Grid Size**: 7 (excellent performance)
- **Upgrade Threshold**: 0.75 coherence
- **Downgrade Threshold**: 0.35 coherence
- **Change Delay**: 4s - time at threshold before changing

#### In Inspector - ToolEnergySystem (auto-created)

- **Max Energy**: 100
- **Drain Rate**: 20/s
- **Regen Rate**: 8/s
- **Regen Delay**: 0.5s after use

## Visual Comparison

### Old System
```
    ○━━━━━━━━━○        ← Circular ring
   /          \
  ○            ○
  |            |       ← Energy arc
  ○            ○
   \          /
    ○━━━━━━━━━○
```

### New System
```
┌─────────────┐       ← Bounding box
│             │          (object detection style)
│             │
└─────────────┘
      ↑
   VORTEX              ← Pattern keyword
```

## Behavior Differences

| Aspect | Old | New |
|--------|-----|-----|
| Cursor | Circular ring | Bounding box with corners |
| Energy Display | Colored arc | Line opacity + color shift |
| Status | Ring color | Pulse speed + line intensity |
| Event Detection | N/A | Displays pattern keyword |
| Performance Feedback | N/A | Box line quality + density |
| Particle Spawn | From agents | From sample grid points |

## Testing Checklist

- [ ] Grid appears at mouse cursor
- [ ] Grid follows mouse smoothly
- [ ] Click and drag smooths flow
- [ ] Event name appears when over turbulence (e.g., "Spiral_Formation")
- [ ] Event name disappears when leaving turbulent region
- [ ] Grid pulses faster when tool is active
- [ ] Grid dims when energy is low
- [ ] Grid size changes based on performance (may take 4+ seconds)
- [ ] Particles spawn from grid points
- [ ] Scroll wheel adjusts tool radius
- [ ] Energy depletes when used, regenerates when idle

## Troubleshooting

### Bounding box doesn't appear
- Check that SamplingGrid component was created
- Check camera reference is assigned
- Enable "Show Debug Info" on PlayerToolController to see state

### Pattern keywords don't show
- Check TurbulenceClassifier is assigned
- Verify TurbulentEventScheduler exists in scene
- Make sure turbulence events are active (check scheduler debug)

### Grid size never changes
- Check PerformanceTracker is assigned
- Verify FlowSimulation reference is set
- Lower "Change Delay" for faster response (default is 4s)
- Check coherence value in debug display

### Performance issues
- Reduce max particles per frame
- Decrease grid point count (lock to 3x3 in PerformanceTracker)
- Reduce particle lifetime

## Reverting to Old System

If you need to go back:

1. In Assets/Scripts, rename:
   - `PlayerToolController.cs` → `PlayerToolController_Refactored.cs`
   - `PlayerToolController_Old.cs` → `PlayerToolController.cs`
2. Wait for Unity to reimport
3. Re-enable old `DampeningParticleEffect` component if needed
4. Delete new component child objects (SamplingGrid, etc.)

## Next Steps

Once integrated and tested:

1. **Polish Timing**: Adjust pulse speeds, transition speeds for feel
2. **Color Tuning**: Match grid colors to your visual style
3. **Performance Balance**: Tune coherence thresholds for desired difficulty
4. **Particle Effects**: Adjust spawn rates and behaviors
5. **Font**: Consider loading a custom monospace font for event labels

## Design Philosophy

The refactoring shifts from "teaching CV concepts" to **revealing machine perception**:

- Minimal text (single word)
- Visual behavior over numbers
- Documentary/surveillance aesthetic
- Shows where the machine "samples"
- Shows what the machine "detects"
- Performance affects perception quality (grid density)
- Smooth and dramatic for immersion

The goal is philosophical reflection through gameplay, not education.

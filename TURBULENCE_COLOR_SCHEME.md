# Turbulence Event Color Scheme

This document defines the color scheme used to visualize different turbulence events in the flow field.

---

## Design Philosophy

The color system provides immediate visual feedback to help players:
- **Identify** which agents are affected by turbulence
- **Distinguish** between different turbulence event types
- **Monitor** the goal of restoring organized gray flow

---

## Color States

### Normal Flow (No Turbulence)
**Color**: Gray (varies by speed)
- Slow: `RGB(0.25, 0.25, 0.25)` - Dark gray
- Fast: `RGB(0.65, 0.65, 0.65)` - Medium gray

**Meaning**: Organized, uniform diagonal flow - the desired state

---

## Turbulence Event Colors

When agents enter a turbulence event, they adopt that event's signature color:

### 1. Circular Event
**Color**: Green `RGB(0.3, 0.9, 0.4)`
**Pattern**: Agents orbit around a center point
**Metaphor**: Peaceful assembly, gathering
**Visual Effect**: Bright green swirl

### 2. Scatter Event
**Color**: Red `RGB(1.0, 0.3, 0.3)`
**Pattern**: Explosive outward push with noise
**Metaphor**: Panic, chaos, dispersal
**Visual Effect**: Intense red explosion pattern

### 3. Vortex Event
**Color**: Purple `RGB(0.9, 0.5, 0.9)`
**Pattern**: Spiral motion with inward pull
**Metaphor**: Whirlpool, spiral formation
**Visual Effect**: Purple spiral galaxy

### 4. Wave Event
**Color**: Cyan `RGB(0.3, 0.9, 0.9)`
**Pattern**: Sinusoidal wave in a direction
**Metaphor**: March, organized wave
**Visual Effect**: Cyan ripple/wave pattern

### 5. Oscillation Event
**Color**: Yellow `RGB(1.0, 0.9, 0.3)`
**Pattern**: Violent random shaking
**Metaphor**: Earthquake, tremor
**Visual Effect**: Bright yellow vibration

### 6. Cluster Event
**Color**: Light Gray `RGB(0.7, 0.7, 0.7)`
**Pattern**: Agents cluster and slow down
**Metaphor**: Blockade, sit-in
**Visual Effect**: Light gray congested area

---

## Color Modulation

### Speed-Based Intensity
All colors are modulated by agent speed:
```
finalColor = lerp(color * 0.5, color, speedRatio)
```
- Slower agents → Darker version of pattern color
- Faster agents → Brighter version of pattern color

### Turbulence Blending
Colors blend smoothly based on turbulence influence:
```
turbulenceFactor = pow(saturate((turbulence - 0.05) / 0.45), 0.5)
finalColor = lerp(grayColor, patternColor, turbulenceFactor)
```

**Thresholds**:
- `turbulence < 0.05`: Pure gray (organized)
- `turbulence = 0.5`: Full pattern color
- `turbulence > 0.5`: Saturated pattern color

---

## Dampening State

When the player applies the dampening tool:

**Color**: Lighter Gray `RGB(0.7, 0.7, 0.75)` (slightly blue-tinted)

**Meaning**: Agents are being smoothed/corrected
- Shows tool is actively working
- Transitions back to normal gray as dampening completes
- Provides feedback that player action is effective

---

## Pattern ID Encoding

Pattern types are encoded as integer IDs:

| Pattern Type | ID | RGB Values |
|--------------|----|-----------|
| None/Normal  | 0  | Gray (varies) |
| Circular     | 1  | (0.3, 0.9, 0.4) |
| Scatter      | 2  | (1.0, 0.3, 0.3) |
| Vortex       | 3  | (0.9, 0.5, 0.9) |
| Wave         | 4  | (0.3, 0.9, 0.9) |
| Oscillation  | 5  | (1.0, 0.9, 0.3) |
| Cluster      | 6  | (0.7, 0.7, 0.7) |

---

## Technical Implementation

### Data Flow
1. **TurbulentEventScheduler** marks agents with pattern IDs when forces are applied
2. **FlowSimulation** stores pattern ID per agent in `turbulencePattern[]` array
3. **FlowVisualizer** samples pattern IDs to grid and encodes in texture alpha channel
4. **Shader** decodes pattern and selects appropriate color
5. **AgentRenderer** matches particle colors to flow field

### Texture Encoding
Alpha channel stores both turbulence and pattern:
```
alpha = turbulence * 0.9 + (pattern / 10.0)
```

Decoding in shader:
```hlsl
float pattern = frac(alpha * 10.0) * 10.0;
float turbulence = saturate((alpha - pattern / 10.0) / 0.9);
```

---

## Design Rationale

### Why These Colors?

**Green (Circular)**: Natural, calm, organized movement
**Red (Scatter)**: Alarm, danger, chaos
**Purple (Vortex)**: Mystical, swirling energy
**Cyan (Wave)**: Water, flowing, rhythmic
**Yellow (Oscillation)**: Attention, vibration, energy
**Light Gray (Cluster)**: Neutral, static, blocked

### Accessibility Considerations

Colors chosen to be distinguishable for common color vision deficiencies:
- Red vs. Cyan: High contrast even for protanopia/deuteranopia
- Yellow vs. Purple: Distinguishable by brightness difference
- Green uses high saturation for visibility

---

## Future Enhancements

Potential improvements to the color system:

1. **Player Customization**: Allow players to remap colors per pattern type
2. **Colorblind Modes**: Alternative palettes optimized for specific CVD types
3. **Pattern Mixing**: Blend colors when multiple events overlap
4. **Time-Based Effects**: Pulse or animate colors for emphasis
5. **Intensity Gradients**: Show event strength through color saturation

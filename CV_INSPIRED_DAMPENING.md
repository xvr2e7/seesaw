# Computer Vision-Inspired Dampening System

## Concept Overview

This document outlines a computer vision-inspired approach to the dampening tool that models it as an **optical flow regularization algorithm**, similar to what real CV systems use to correct flow field anomalies.

---

## The Problem

Current dampening simply reduces agent velocity magnitude, which:
- Lacks physical/mathematical motivation
- Doesn't teach CV concepts
- Feels arbitrary rather than systematic
- May not be effective against strong turbulence

---

## The Solution: Flow Field Regularization

Model the dampening tool as a **local smoothness constraint enforcer**, inspired by classical optical flow algorithms.

### Core Principle

In computer vision, optical flow algorithms enforce **spatial coherence** - the assumption that neighboring pixels should have similar motion vectors. When turbulence creates high-divergence regions, CV algorithms apply regularization to restore smoothness.

Your tool becomes a **user-guided regularizer**: you tell the system "apply smoothness constraints here."

---

## Real CV Algorithm Inspiration

### Horn-Schunck Method
- **Global smoothness term**: Penalizes spatial derivatives of flow
- **Energy minimization**: Balance between data fidelity and smoothness
- **Iterative refinement**: Flow converges to smooth solution

### Lucas-Kanade Method
- **Local consistency assumption**: All pixels in a neighborhood move similarly
- **Weighted least squares**: Nearby pixels influence each other

### TV-L1 Method
- **Total variation regularization**: Preserve discontinuities while smoothing
- **Edge-aware**: Don't blur across flow boundaries

---

## Proposed Mechanics

### 1. Local Flow Analysis (Detection Phase)

When the dampening tool is active over a region:

```
For each frame:
1. Sample all agents within tool radius
2. Calculate dominant flow direction (weighted mean velocity)
3. Measure flow coherence (alignment with dominant direction)
4. Identify outlier agents (high angular deviation)
```

**Key Metric**: **Flow Coherence**
```
coherence = 1 - (variance of velocity directions) / π
coherence ∈ [0, 1]
  - 0 = completely random/turbulent
  - 1 = perfectly aligned/laminar
```

---

### 2. Directional Correction (Regularization Phase)

Instead of dampening velocity magnitude, **rotate agent velocities toward local dominant flow**:

```
For each agent in tool radius:
1. Calculate angular deviation from dominant flow
   θ_deviation = angle(agent.velocity, dominant_direction)

2. Apply rotational force proportional to deviation
   rotation_force = k * sin(θ_deviation) * tangent_vector

   where:
   - k = correction strength (tunable parameter)
   - tangent points toward dominant direction

3. Reduce turbulence influence flag
   turbulence_influence *= (1 - correction_rate * dt)

4. Optional: slight velocity dampening for stability
   velocity *= (1 - 0.1 * dt)
```

**Result**: Agents naturally "fall back in line" with their neighbors through angular correction, not just slowdown.

---

### 3. Edge-Preserving Smoothing

Mimic TV-L1's edge preservation:

```
Correction strength varies based on local context:

if (local_coherence < threshold):
    # Inside turbulent region - strong correction
    correction_strength = max_strength

elif (on_turbulence_boundary):
    # Edge of turbulent region - preserve boundary
    correction_strength = 0.3 * max_strength

else:
    # Already organized - gentle nudging only
    correction_strength = 0.1 * max_strength
```

**Boundary Detection**:
- Agent is on boundary if turbulence_influence changes rapidly among neighbors
- Use spatial gradient of turbulence field

---

### 4. Sustained Regularization Bonus

Inspired by iterative refinement in Horn-Schunck:

```
Smoothness energy accumulates in regions under sustained correction:

For each grid cell:
    if (tool active in cell):
        smoothness_energy += dt * coherence_improvement
    else:
        smoothness_energy *= decay_rate

When smoothness_energy > threshold:
    - Correction becomes 2x stronger
    - Turbulence decay rate increases
    - Visual feedback: area glows with "smoothness aura"
```

**Tipping Point**: When local coherence exceeds critical threshold (e.g., 0.7), turbulence "collapses" rapidly:

```
if (coherence > critical_threshold):
    turbulence_influence *= collapse_rate  # Very fast decay
    correction_strength *= 1.5             # Positive feedback loop
```

This creates satisfying moments where chaos suddenly "snaps" into order.

---

## Implementation Details

### Data Structures

Add to FlowSimulation:
```csharp
private float[] flowCoherence;         // Per-agent local coherence
private Vector2[] dominantFlowDir;     // Per-agent local dominant direction
private float[] smoothnessEnergy;      // Per-grid-cell accumulated energy
```

### New Methods

```csharp
// Calculate local dominant flow direction
Vector2 GetDominantFlowDirection(Vector2 center, float radius)
{
    // Weighted average of velocities in radius
    // Weight by inverse distance
}

// Calculate local flow coherence
float GetFlowCoherence(Vector2 center, float radius)
{
    // Measure angular variance of velocities
    // Return normalized coherence score [0, 1]
}

// Apply directional correction to agent
void ApplyFlowRegularization(
    int agentIndex,
    Vector2 dominantDirection,
    float correctionStrength,
    float dt
)
{
    // Rotate velocity toward dominant direction
    // Reduce turbulence influence
    // Update desiredDirection for persistence
}

// Detect turbulence boundaries
bool IsOnTurbulenceBoundary(int agentIndex, float radius)
{
    // Sample turbulence of nearby agents
    // Return true if high spatial gradient
}
```

### Modified Dampening Tool

Replace `DampenInRadius()` with:

```csharp
public void RegularizeFlowInRadius(
    Vector2 center,
    float radius,
    float strength,
    float dt
)
{
    // 1. Calculate local dominant direction
    Vector2 dominant = GetDominantFlowDirection(center, radius);

    // 2. Calculate local coherence
    float coherence = GetFlowCoherence(center, radius);

    // 3. Update smoothness energy grid
    UpdateSmoothnessEnergy(center, radius, coherence, dt);

    // 4. Check for sustained bonus
    float energyBonus = GetSmoothnessEnergyBonus(center);
    float effectiveStrength = strength * (1.0f + energyBonus);

    // 5. Apply to each agent in radius
    for (int i = 0; i < agentCount; i++)
    {
        if (InRadius(positions[i], center, radius))
        {
            // Edge-preserving: reduce strength at boundaries
            bool onBoundary = IsOnTurbulenceBoundary(i, 2.0f);
            float localStrength = onBoundary ?
                effectiveStrength * 0.3f : effectiveStrength;

            ApplyFlowRegularization(
                i,
                dominant,
                localStrength,
                dt
            );
        }
    }

    // 6. Check for coherence tipping point
    if (coherence > criticalCoherenceThreshold)
    {
        TriggerCoherenceCollapse(center, radius);
    }
}
```

---

## Tunable Parameters

```csharp
[Header("Flow Regularization")]
[Tooltip("Base correction strength")]
[Range(0f, 10f)]
public float regularizationStrength = 3.0f;

[Tooltip("Coherence threshold for turbulence collapse")]
[Range(0.5f, 0.95f)]
public float criticalCoherence = 0.7f;

[Tooltip("Smoothness energy accumulation rate")]
[Range(0f, 2f)]
public float smoothnessGainRate = 1.0f;

[Tooltip("Smoothness energy decay rate")]
[Range(0f, 5f)]
public float smoothnessDecayRate = 2.0f;

[Tooltip("Sustained correction bonus multiplier")]
[Range(1f, 3f)]
public float sustainedBonus = 2.0f;

[Tooltip("Turbulence collapse speed multiplier")]
[Range(2f, 10f)]
public float collapseMultiplier = 5.0f;
```

---

## Visual Feedback

### Coherence Visualization

Show local coherence in the flow field shader:

```hlsl
// In OpticalFlowHSV.shader
float coherence = SampleCoherence(input.uv);

// Overlay coherence as brightness modulation
float3 coherenceOverlay = float3(0.3, 0.5, 0.7) * coherence;
finalColor = lerp(finalColor, coherenceOverlay, 0.3);
```

### Smoothness Energy Glow

Show accumulated smoothness energy as a bright glow:

```hlsl
float smoothnessEnergy = SampleSmoothnessEnergy(input.uv);
float3 energyGlow = float3(0.7, 0.9, 1.0) * smoothnessEnergy;
finalColor += energyGlow * 0.5;
```

### Tool Cursor Enhancement

Modify ToolCursor shader to show current coherence:

```hlsl
// Inner ring color changes based on local coherence
float coherence = GetLocalCoherence(toolPosition);
float3 ringColor = lerp(
    float3(1.0, 0.3, 0.2),  // Red when chaotic
    float3(0.3, 0.9, 0.5),  // Green when coherent
    coherence
);
```

---

## Gameplay Benefits

### Educational Value

Players learn fundamental CV concepts:
- **Optical flow**: Motion as a vector field
- **Regularization**: Enforcing smoothness constraints
- **Energy minimization**: Iterative refinement to stable states
- **Edge preservation**: Don't blur across discontinuities

### Strategic Depth

- **Targeting matters**: Hitting turbulence boundaries vs. centers has different effects
- **Timing matters**: Sustained correction builds energy for bigger payoffs
- **Risk/reward**: Wait for coherence collapse vs. constant correction
- **Spatial reasoning**: Where to apply regularization for maximum effect

### Satisfying Moments

- **Coherence collapse**: Visible moment when chaos "snaps" into order
- **Growing smoothness**: Watch energy accumulate under sustained correction
- **Clear causality**: See exactly how your actions affect the flow field

---

## Performance Considerations

### Optimization Strategies

1. **Spatial hashing**: Only check nearby agents for coherence calculations
2. **Grid-based**: Calculate dominant flow per grid cell, not per agent
3. **Update frequency**: Coherence calculations every N frames, not every frame
4. **LOD**: Lower resolution coherence calculation far from tool

### Approximate Coherence

Fast approximation using grid:

```csharp
// Instead of per-agent calculation
float coherence = 1.0f - (velocityVariance / maxVariance);

// Use grid-based variance (already calculated for flow visualization)
float gridCoherence = GetGridCoherence(cellIndex);
```

---

## Testing Metrics

Measure effectiveness with:

```csharp
// Success metrics
float averageCoherence = CalculateGlobalCoherence();
float turbulentArea = CalculateTurbulentAreaPercentage();
float toolEfficiency = coherenceGained / toolTimeUsed;

// Balance metrics
float timeToRestoreOrder = MeasureRecoveryTime();
float sustainedCorrectionRatio = sustainedTime / totalToolTime;
```

---

## Future Extensions

### Multiple Regularization Modes

- **Mode 1: Local smoothing** (current approach)
- **Mode 2: Global alignment** (push toward global mean direction)
- **Mode 3: Gradient descent** (minimize local divergence directly)

### Adaptive Parameters

```csharp
// Auto-tune based on player skill
if (playerSuccessRate > 0.8f)
{
    turbulenceStrength *= 1.1f;  // Make game harder
}
else if (playerSuccessRate < 0.3f)
{
    regularizationStrength *= 1.2f;  // Make tool stronger
}
```

### Coherence-Based Scoring

```csharp
score += Integrate(coherence * dt);  // Reward maintaining high coherence
bonus += coherenceCollapses * 100;   // Bonus for tipping point moments
```

---

## Summary

This CV-inspired approach transforms the dampening tool from a simple velocity reducer into a **mathematically-motivated flow field regularizer** that:

1. **Teaches optical flow concepts** through gameplay
2. **Rewards skillful targeting** of turbulence boundaries
3. **Creates satisfying tipping point moments** via coherence collapse
4. **Provides clear visual feedback** of regularization effect
5. **Scales naturally** with turbulence intensity

The result is a game that feels both **physically plausible** and **pedagogically valuable**, while being more fun through strategic depth.

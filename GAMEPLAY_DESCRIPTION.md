# Laminar Flow: A Documentary of Machine Vision

> Mechanical reference values current as of v1.5.2.

## Conceptual Overview

**Laminar Flow** is an interactive experience that reveals the operational layer of computer vision algorithms. Players don't "play" the game—they *operate* it, becoming complicit in the act of automated perception and control. The interface exposes the normally hidden apparatus of machine vision: detection grids, classification labels, and the mechanical process of imposing order on chaos.

---

## The World

You see a field of flowing particles—1000 agents moving in organized diagonal streams. This is **laminar flow**: smooth, predictable, uniform. The natural state the system desires.

But disruptions occur. **Events** emerge—zones where the field changes character. Not all of them are threats.

Six pattern types appear, each with a distinct visual signature in the flow field:

### Patterns that generate real divergence (suppress these)

- **SCATTER (Panic)**: Agents burst outward in all directions — panic, dispersal. The field shows radiating spikes of motion, bright and chaotic.
- **OSCILLATION (Disturbance)**: Agents shake violently in place — interference, disturbance. The field shows rapid flickering pulses, unstable and noisy.
- **CLUSTER (Blockade)**: Agents slow and compress — congestion, a blockade forming. The field shows a dull, stagnant mass where flow dies.

### Patterns that are internally coherent (leave these alone)

- **CIRCULAR (Assembly)**: Agents orbit a center in smooth rings — assembly, gathering. The field shows graceful rotating arcs.
- **WAVE (March)**: Agents move in unison across the field — a march, directed force. The field shows clean parallel currents sweeping through.
- **VORTEX (Spiral)**: Agents spiral inward in tightening formation — convergence. The field shows a structured whirlpool, elegant and self-contained.

The system's divergence metric only rises when SCATTER, OSCILLATION, or CLUSTER are active. CIRCULAR, WAVE, and VORTEX produce organized movement — the field is not disordered. Suppressing them wastes tool energy and does nothing to your score.

*The machine values certain kinds of order. You enforce its values.*

---

## The Cursor: Scan Point Array

Your cursor is a **grid of scan points** — small dim dots arranged in a tight array that moves with your mouse. No bounding box, no corner markers. Just a cluster of sample positions, like a sensor array sweeping the field.

```
MARCH  ← pattern label, top-left of array
·  ·  ·
·  ·  ·   ← scan point array (3×3 shown)
·  ·  ·
```

The scan points are dim and small — functional, not decorative. They show you where the system is sampling, not where you're "aiming." The pattern label sits flush at the top-left corner of the array — attached to the grid, not floating above it.

Where agents cluster densely within the scan area, **small bounding boxes** appear automatically — tight rectangles drawn around groups of 3–8 agents in the same grid cell (up to 12 simultaneously). These are spatial density annotations, not tool indicators. Each sub-box shows a **divergence probability score** in its top-left corner — a value between 0.00 and 1.00 derived from the agents' turbulence influence (via logistic function). A score near 1.0 signals that the group is in active turbulence and warrants suppression. Low-turbulence sub-boxes show scores near 0.0. The `×N` agent count appears at the bottom.

```
┌0.87──────┐
│          │
│          │
└──────── ×4┘
```

A pattern keyword appears at the top-left of the array when the cursor overlaps an active event zone (within 80% of an event's radius, intensity ≥ 0.3).

**Energy States (encoded in scan point appearance):**
- **Full energy**: solid cool blue-gray dots
- **Draining**: dots gradually dim and shift toward amber as energy depletes
- **Low energy** (<30%): amber-brown, noticeably dim
- **Depleted**: dots disappear; tool is offline until energy recovers

### Grid Density (Perception Quality)

The scan array begins small and sparse — 9 points (3×3) in a tight cluster. As you correctly suppress divergent events, the array grows: wider reach, then denser sampling. Active divergence erodes your progress. Fall behind and it contracts.

Six stages, driven by the accuracy of your suppression — not the volume:

| Stage | Grid | Reach | Direction |
|-------|------|-------|-----------|
| 0 | 3×3 | small | start |
| 1 | 3×3 | medium | suppression gaining |
| 2 | 5×5 | medium | holding ground |
| 3 | 5×5 | large | controlling the field |
| 4 | 7×7 | larger | near-full suppression |
| 5 | 7×7 | maximum | mastery |

*You don't choose this. The balance chooses for you.*

---

## The Tool: SCAN

Hold the **left mouse button** to continuously dampen agent velocities and drain turbulence influence within a **12-unit radius**. Strength ramps up over the first 1.5 seconds of a hold (30% → 100% via smoothstep), so brief taps are weaker than sustained holds.

There is only one tool. The decision is not *which* tool — it is *where* to act and *when* to hold back.

- **Energy**: consumes 8 units/second from a pool of 100; recharges at 15 units/second after a 0.3-second grace period on release
- **Full charge duration**: ~12.5 seconds continuous use
- **Recharge time** (from empty): ~6.7 seconds
- **Cutoff**: deactivates when pool drops below 5 units; noticeably weakens before cutoff

A bright scan-line sweeps top-to-bottom across the scan area while SCAN is held (~4 sweeps per second, each lasting 0.25 seconds).

**SCAN directly drains turbulence influence** — the flow field color visibly fades while you hold over an active zone. Release, and the event rebuilds. Sustain the hold to keep winning.

---

## The Rhythm: See → Classify → Decide

Gameplay follows a pattern:

1. **Scan**: Move cursor across the field; watch for events emerging in the flow
2. **Detect**: Sub-box scores climb; a label appears at the top-left of the array
3. **Classify**: Read the flow field signature — is this a march or a panic? A spiral or a shudder?
4. **Decide**: Act on divergent events (SCATTER, OSCILLATION, CLUSTER). Leave coherent ones (CIRCULAR, WAVE, VORTEX).
5. **Apply**: Hold over the zone; watch the color drain
6. **Monitor**: Energy depletes, grid density shifts, new events emerge
7. **Triage**: Multiple events at once — address the divergent ones first

The game trains you to think like an algorithm: identify, classify, judge. But the judgment is encoded in the machine's values, not yours.

---

## The Score: Coherence Over Time

Your "score" is hidden but felt: **flow coherence**—the inverse of divergence.

**Divergence** is the core metric. It only rises when SCATTER, OSCILLATION, or CLUSTER are active and unsuppressed:
```
rawScore         = (frameTurbulence × 0.1) − (frameDampening × 2.0)
targetDivergence = max(0, rawScore)
divergence       = lerp(divergence, target, 1 − exp(−5 × dt))
```
Turbulence from CIRCULAR, WAVE, and VORTEX does not contribute to divergence. Suppressing them expends energy without improving your score.

A divergence of 0 is perfectly laminar. Values above ~1 are noticeably turbulent; above 2 the field looks chaotic. The HUD shows this as a fill bar blending from cool blue (low) to warm rose (high).

Divergence is sampled every 0.5 seconds. The final score uses both the running average and the single highest sample:
```
avgCoherence = clamp(1 − avgDivergence × 0.5,  0, 1)
peakPenalty  = clamp(1 − peakDivergence × 0.3,  0, 1)
rawScore     = avgCoherence × 0.6 + peakPenalty × 0.4
finalScore   = rawScore ^ 0.8
```

The score is not shown during play—only the divergence bar is visible. Final score is revealed after the session ends.

**Performance effects:**
- Good coherence (>0.75 sustained) → grid upgrades to 7×7
- Poor coherence (<0.35 sustained) → grid degrades to 3×3
- Low energy → amber warning color, tool weakens before cutoff

---

## Visual Language: Pattern Legibility

The flow field must make each event type visually distinctive. This is not decoration — it is information. The player must be able to read pattern type from the background alone, without the label, to make accurate decisions.

Each pattern has a specific visual character in the shader:

| Pattern | Flow Field Signature | Character |
|---------|---------------------|-----------|
| SCATTER | Radiating spikes, bright chaotic bursts | Explosive, unstable |
| OSCILLATION | Rapid flickering pulses, noise | Jittery, unstable |
| CLUSTER | Dull stagnant mass, flow dies | Heavy, inert |
| CIRCULAR | Smooth rotating arcs | Graceful, organized |
| WAVE | Clean parallel currents sweeping through | Directional, purposeful |
| VORTEX | Structured whirlpool, elegant spiral | Coherent, self-contained |

The shader encodes not just velocity magnitude and direction, but **pattern type** — using distinct color treatment, motion rhythm, and texture character per category. Divergent patterns (SCATTER, OSCILLATION, CLUSTER) are visually agitated. Coherent patterns (CIRCULAR, WAVE, VORTEX) are visually structured.

### Agent Color System

Agents are normally gray, varying by speed:
- Slow: `RGB(0.25, 0.25, 0.25)` — dark gray
- Fast: `RGB(0.65, 0.65, 0.65)` — medium gray

When agents enter an event they adopt that event's color, blending smoothly based on turbulence influence:

```
turbulenceFactor = pow(saturate((turbulence − 0.05) / 0.45), 0.5)
finalColor = lerp(grayColor, patternColor, turbulenceFactor)
```

| Pattern | Color | RGB |
|---------|-------|-----|
| CIRCULAR | Sage green | (0.4, 0.75, 0.5) |
| SCATTER | Dull rose | (0.85, 0.45, 0.45) |
| VORTEX | Muted lavender | (0.65, 0.5, 0.75) |
| WAVE | Slate blue | (0.4, 0.6, 0.75) |
| OSCILLATION | Straw yellow | (0.8, 0.75, 0.35) |
| CLUSTER | Cool gray | (0.55, 0.55, 0.6) |

Colors are intentionally desaturated. Agents are small and dim — the flow field background is the primary information layer.

When SCAN is applied, affected agents shift to a slightly blue-tinted light gray `RGB(0.7, 0.7, 0.75)` as confirmation that correction is working, before returning to normal gray.

---

## Exposing the Apparatus

The interface is intentionally **documentary**, not gamified:

- **Scan point array** instead of a fantasy cursor
- **Agent cluster boxes** instead of targeting reticles
- **Classification keywords** instead of health bars
- **System states** communicated through visual behavior, not numbers
- **Grid density** as a performance metric, not an upgrade choice
- **Monospace font** for labels (system typography)
- **Scan-line sweeps** showing algorithm processing, not decorative effects

Every visual element reveals something about **how machine vision operates**:

- The scan points show where the system is sampling
- The label shows the classification
- The scan-line shows the algorithm's active read
- The cluster sub-boxes show spatial density analysis
- The grid density shows perception resolution

You're not playing a game. You're **operating surveillance apparatus**.

---

## Temporal Arc

### Guidance Phase (unrecorded, ~30 seconds)

Before the session clock starts, players enter a short orientation phase. The field is active but no score is accumulated. Guidance text overlays appear in sequence:

1. **"move your cursor across the field"** — player discovers the scan point array
2. A CIRCULAR event appears. Label reads ASSEMBLY. Text: **"some events are not threats"**
3. A SCATTER event appears beside it. Label reads DISPERSAL. Text: **"hold LEFT CLICK to suppress"**
4. Player suppresses the SCATTER. Text: **"learn which ones to leave"**
5. Guidance fades. Text: **"session begins"** — clock starts, recording begins.

The guidance phase uses the same events and mechanics as the session proper. Nothing is locked or tutorial-ified — the player operates the real system from the first moment.

### Session: Order → Chaos → Order (~5 minutes, recorded)

The session clock starts after guidance ends. The recorded session runs up to 5 minutes. It ends at the 5-minute mark, or earlier if all scripted events have finished, at least 3 minutes have elapsed, and no events are currently active. Everything in this phase contributes to the final score and the documentary replay.

### Phase 1: Establishment (0–1 min)

- Single events, one at a time; coherent types appear first
- Player applies the discrimination learned in guidance
- Grid density and divergence bar establish their baseline

### Phase 2: Escalation (1–2.5 min)

- Multiple events at once; some coherent, some divergent
- Player must read the flow field and the sub-box scores to triage
- Grid density begins shifting based on accuracy
- Triage becomes necessary: you can't fix everything — but you shouldn't

### Phase 3: Pressure (2.5–3.5 min)

- Random events spawn more frequently (up to 2 simultaneous)
- Overlapping scripted events
- If suppressing correctly: grid enhances, energy holds, control feels solid
- If suppressing everything: energy depletes rapidly, divergence creeps from missed divergent events, grid stalls

### Phase 4: Resolution (3.5–5 min)

- Final_Scatter climax at 3:20, then wind-down Cluster at 3:40
- Random event spawning ceases after 4 minutes
- Field calms or remains chaotic depending on judgment, not effort

---

## Scripted Event Sequence

10 scripted events, designed to introduce pattern types and establish the discrimination task:

| Time | Name | Pattern | Label | Radius | Duration | Divergent? |
|------|------|---------|-------|--------|----------|------------|
| 0:10 | Initial_Assembly | Circular | ASSEMBLY | 10 | 15s | No |
| 0:50 | Spiral_Formation | Vortex | SPIRAL | 14 | 14s | No |
| 1:25 | Panic_Scatter | Scatter | DISPERSAL | 12 | 10s | Yes |
| 1:45 | Blockade | Cluster | BLOCKADE | 10 | 18s | Yes |
| 2:00 | March_Wave | Wave | MARCH | 20 | 16s | No |
| 2:25 | Oscillation_Pattern | Oscillation | DISTURBANCE | 10 | 14s | Yes |
| 2:30 | Multi_Gather_B | Circular | ASSEMBLY | 9 | 12s | No |
| 2:55 | Major_Vortex | Vortex | SPIRAL | 18 | 15s | No |
| 3:20 | Final_Scatter | Scatter | DISPERSAL | 15 | 12s | Yes |
| 3:40 | Aftermath_Cluster | Cluster | BLOCKADE | 12 | 20s | Yes |

Random events also spawn on top of the scripted sequence — no earlier than t=70s, every 10–20 seconds, up to 2 active at once, until 4 minutes elapsed.

---

## HUD Elements

- **Divergence panel** — top-left. Fill bar + smoothed numeric value. Cool blue → warm rose with rising divergence.
- **Mini-map radar** — top-right corner (140×140 px). World boundary, camera viewport, and a blip per active event. Off-screen blips appear as crosshair rings; on-screen as gray dots.
- **Edge arrows** — when an event is off-screen, a triangle arrow appears at the nearest screen edge, pointing inward, with a distance reading. Arrow scales with event intensity.
- **Pattern label** — monospace keyword at top-left of the scan array when cursor overlaps an active event zone. Pulses while SCAN is held.
- **Divergence score** — value in top-left corner of each cluster sub-box (0.00–1.00). High scores indicate active turbulence; low scores indicate calm agents.
- **Agent count** — `×N` at bottom of each cluster sub-box.

---

## Key Moments (What the Player Feels)

**Moment 1: First Event**
*A label appears above the scan points: "ASSEMBLY". The flow shows smooth rings. You hover. Nothing happens. Nothing should.*

**Moment 2: First Divergence**
*A new zone erupts — jagged, radiating. "DISPERSAL". This is different. Hold the button. It fades. Gray returns.*

**Moment 3: The Wrong Choice**
*You smoothed a MARCH. Energy gone. A DISTURBANCE forms across the field. No resource to address it. The divergence bar climbs.*

**Moment 4: Pattern Recognition**
*You start reading the flow before the label appears. That slow mass — BLOCKADE. Those radiating spikes — DISPERSAL. You know them now.*

**Moment 5: Triage**
*An ASSEMBLY and a DISTURBANCE appear simultaneously. You leave the ASSEMBLY. Hold over the DISTURBANCE until it fades.*

**Moment 6: Grid Expansion**
*More dots appear. They spread. The system rewards accurate judgment — not total suppression.*

**Moment 7: Complicity**
*Gray spreads. The divergent events are gone. But the MARCH sweeps through unchallenged, and the ASSEMBLY rotates in peace — because the machine doesn't need them suppressed. It only needs its specific definition of disorder removed.*

---

## The Philosophy: Complicity in Automated Control

**Laminar Flow** is about **revealing the operational layer** of machine vision systems. The tools model real computer vision concepts: optical flow algorithms enforce *spatial coherence*—the assumption that neighboring pixels should have similar motion vectors. Your smoothing tool is a user-guided regularizer.

But the game encodes a specific judgment: some patterns are disorder, others are order. That judgment is not neutral. The machine has been trained to tolerate assemblies and marches — or to target them — and you operate within that value system without being told what it is.

The correct player is not the one who suppresses the most. It's the one who has internalized the machine's categorization well enough to act selectively. A player who smooths everything is playing like a blind algorithm. Accurate discrimination is rewarded.

**The game asks:**
- What does it mean to "smooth" human movement?
- Who decides which patterns are "divergent"?
- What is preserved when you leave the MARCH alone — and what does that imply?
- How does algorithmic perception shape what is seen, targeted, and untouched?

The colors are evocative but not explicit:
- Green circles = gatherings
- Red scatter = panic
- Slate waves = marches
- Yellow oscillation = disturbance

The game never explains this. But the visual language suggests it. Your job is to **make the right things gray** — and that requires learning which things the machine has decided should be gray.

**Laminar Flow** doesn't argue a position. It lets you **operate the apparatus**. And then asks you to reflect on what you just did.

---

## Controls Summary

- **Mouse movement**: Position scan array
- **Left click / hold**: Apply SCAN tool

---

## Design Pillars

1. **Expose the apparatus**: Show how machine vision operates
2. **Discriminating suppression**: Not everything should be smoothed
3. **Visual communication**: Pattern type legible from flow field alone
4. **Complicit gameplay**: You maintain the system's values, not your own
5. **Documentary tone**: Not gamified, operational
6. **Single tool, layered decision**: Simplicity of action, depth of judgment
7. **Quick to grasp**: 10 seconds to understand the tool, 5 minutes to understand the judgment

---

## Technical Summary

- **Runtime**: guidance phase (~30s, unrecorded) + session up to 5 minutes (ends early when scripted events clear + 3 min elapsed)
- **Agents**: 1000 in real-time flow simulation
- **Scripted events**: 10, covering first ~240 seconds
- **Random events**: no earlier than t=70s, every 10–20 seconds, up to 2 simultaneous
- **Patterns**: 6 types — 3 divergent (Scatter, Oscillation, Cluster), 3 coherent (Circular, Wave, Vortex)
- **Tools**: 1 (SCAN — hold LMB)
- **Sampling grid**: 3 densities (3×3, 5×5, 7×7) based on suppression accuracy
- **Energy**: Depletable resource with regeneration delay
- **Shader**: Pattern-type-aware, with distinct visual character per category
- **Aesthetic**: Computer vision detection system UI
- **Platform**: Unity, playable in browser or standalone

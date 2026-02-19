# Laminar Flow: A Documentary of Machine Vision

> Mechanical reference values current as of v1.4.6.

## Conceptual Overview

**Laminar Flow** is an interactive experience that reveals the operational layer of computer vision algorithms. Players don't "play" the game—they *operate* it, becoming complicit in the act of automated perception and control. The interface exposes the normally hidden apparatus of machine vision: detection grids, classification labels, and the mechanical process of imposing order on chaos.

---

## The World

You see a field of flowing particles—800 agents moving in organized diagonal streams. This is **laminar flow**: smooth, predictable, uniform. The natural state the system desires.

But disruptions occur. **Turbulent events** emerge—zones where the flow becomes chaotic:

- **CIRCULAR**: Sage green swirls where agents orbit a center (peaceful assemblies)
- **SCATTER**: Rose explosions of panic and dispersal (chaos events)
- **VORTEX**: Lavender spirals pulling inward (gathering formations)
- **WAVE**: Slate blue forces moving directionally (marching patterns)
- **OSCILLATION**: Straw yellow zones of violent shaking (disturbances)
- **CLUSTER**: Cool gray congestion where movement slows (blockades)

Each disruption is color-coded, making the chaos visible and distinct. The system wants gray. The system needs order.

---

## The Tool: Detection Grid Overlay

Your interface is a **computer vision detection system**. When you move your cursor, a bounding box appears—a rectangular outline with corner markers, exactly like object recognition algorithms draw on security footage or autonomous vehicle feeds. The box is always visible, not just when a tool is active.

```
┌─────────────────┐
│                 │
│                 │  ← Detection bounding box
│                 │
└─────────────────┘
       ↑
    VORTEX  ← Pattern classification label
```

**The Grid Shows:**
- A rectangular bounding box with corner markers
- Line color and opacity indicating tool state and energy
- Pattern keyword above the box when turbulence is detected (within 80% of an event's radius, intensity ≥ 0.3)
- Pulsing intensity showing system activity (2 Hz idle, 10 Hz when firing)

**Visual States:**
- **Idle**: 2 Hz pulse, tool color, steady
- **Active Correction**: 10 Hz pulse, bright tool color, intense
- **Low Energy**: Amber-brown warning tint when SCAN energy drops below 30%
- **Depleted**: Tool cuts off when energy drops below 5 units

Inside the main box, a spatial bucketing pass renders small **cluster sub-boxes** around groups of 3–8 agents within 3-unit grid cells (up to 12 simultaneously). Sub-boxes overlapping an active event zone shift to a warmer tint and show a `×N` agent count.

### Grid Density (Perception Quality)

The system's perception quality changes based on your performance (coherence):

- **3×3 sampling grid**: Struggling—the system can barely see
- **5×5 sampling grid**: Baseline—normal perception
- **7×7 sampling grid**: Enhanced—high-resolution detection

When coherence stays above **0.75 for 4 consecutive seconds**, the grid upgrades one step. When coherence stays below **0.35 for 4 seconds**, it downgrades. High turbulence pressure (above 0.7) accelerates the downgrade timer. Grid density interpolates smoothly at 1.5× per second rather than stepping instantly.

*You don't choose this. The algorithm adjusts based on how well you maintain order.*

Better performance → denser grid → more effective detection → easier to maintain order.
Poor performance → degraded grid → weaker detection → harder to recover.
The machine rewards competence, punishes failure.

---

## The Three Tools

Press **1**, **2**, or **3** to switch between tools. All tools are aimed with the mouse cursor and fired with **left click**. The **scroll wheel** resizes the active radius (2–25 units, except LOCK which is capped at 4 regardless). The bounding box color reflects the active tool: cool blue-gray for SCAN, warm amber for PULSE, muted red-orange for LOCK.

### SCAN (key 1) — The Workhorse

Hold the left mouse button to continuously dampen agent velocities within a **12-unit radius**. Strength ramps up over the first 1.5 seconds of a hold (30% → 100% via smoothstep), so brief taps are weaker than sustained holds.

- **Energy**: consumes 8 units/second from a pool of 100; recharges at 15 units/second after a 0.3-second grace period on release
- **Full charge duration**: ~12.5 seconds continuous use
- **Recharge time** (from empty): ~6.7 seconds
- **Cutoff**: deactivates when pool drops below 5 units; noticeably weakens before cutoff (floors at 0.3×)

A bright scan-line sweeps top-to-bottom across the bounding box while SCAN is held (~4 sweeps per second, each lasting 0.25 seconds).

### PULSE (key 2) — The Interrupt

A single instant burst of dampening across a **12-unit radius** on a tap.

- **Burst strength**: 1.8 (significantly higher than a single SCAN frame)
- **Cooldown**: 8 seconds
- **Energy cost**: none

Best used to interrupt an event the moment it appears, or as a fallback when SCAN energy is depleted. A single 0.15-second scan-line sweep fires on activation.

### LOCK (key 3) — The Surgical Strike

Pins all agents within a **4-unit radius** to a full dampening factor of 1.0, effectively freezing them.

- **Freeze duration**: ~2 seconds (natural 0.5/s decay rate brings agents back)
- **Cooldown**: 14 seconds
- **Energy cost**: none

Most effective aimed at an event's core. LOCK is not useful against wide events (e.g., WAVE with radius 20) but can anchor a tight vortex or cluster center while SCAN handles the perimeter.

---

## The Rhythm: Scan → Classify → Correct

Gameplay follows a pattern:

1. **Scan**: Move cursor across the field; watch the mini-map for off-screen blips
2. **Detect**: Bounding box identifies turbulent regions
3. **Classify**: Label appears—**SCATTER**, **VORTEX**, etc.
4. **Choose**: Select the right tool for the event type and scale
5. **Apply**: Hold (SCAN), tap (PULSE/LOCK), watch pattern dissolve
6. **Monitor**: Energy depletes, cooldowns tick, grid density shifts
7. **Adapt**: Triage which turbulences to address; you can't fix everything

Multiple turbulent events occur simultaneously. The game trains you to think like an algorithm: identify, classify, suppress.

---

## The Score: Coherence Over Time

Your "score" is hidden but felt: **flow coherence**—the inverse of divergence.

**Divergence** is the core metric:
```
rawScore         = (frameTurbulence × 0.1) − (frameDampening × 2.0)
targetDivergence = max(0, rawScore)
divergence       = lerp(divergence, target, 1 − exp(−5 × dt))
```
A divergence of 0 is perfectly laminar. Values above ~1 are noticeably turbulent; above 2 the field looks very chaotic. The HUD shows this as a fill bar blending from cool blue (low) to warm rose (high).

Divergence is sampled every 0.5 seconds. The final score uses both the running average and the single highest sample:
```
avgCoherence = clamp(1 − avgDivergence × 0.5,  0, 1)
peakPenalty  = clamp(1 − peakDivergence × 0.3,  0, 1)
rawScore     = avgCoherence × 0.6 + peakPenalty × 0.4
finalScore   = rawScore ^ 0.8
```

Letting a single event go unaddressed even briefly drives `peakPenalty` down sharply. The 0.8 power curve spreads scores across the middle range so most players land between 0.4 and 0.8. The score is not shown during play—only the divergence bar is visible. Final score is revealed in the documentary phase after the session ends.

**Performance effects:**
- Good coherence (>0.75 sustained) → grid upgrades to 7×7
- Poor coherence (<0.35 sustained) → grid degrades to 3×3
- Low SCAN energy → amber warning color, tool weakens before cutoff

---

## Visual Language: Exposing the Apparatus

The interface is intentionally **documentary**, not gamified:

- **Bounding boxes** instead of fantasy cursors
- **Classification keywords** instead of health bars
- **System states** communicated through visual behavior, not numbers
- **Grid density** as a performance metric, not an upgrade choice
- **Monospace font** for labels (system typography)
- **Scan-line sweeps** showing algorithm processing, not decorative effects

Every visual element reveals something about **how machine vision operates**:

- The box shows the detection area
- The label shows the classification
- The pulse shows processing intensity
- The scan-line shows the algorithm's active read
- The cluster sub-boxes show spatial density analysis
- The grid density shows perception resolution
- The corner markers mimic real CV annotation tools

You're not playing a game. You're **operating surveillance apparatus**.

### Agent Color System

Agents are normally gray, varying by speed:
- Slow: `RGB(0.25, 0.25, 0.25)` — dark gray
- Fast: `RGB(0.65, 0.65, 0.65)` — medium gray

When agents enter a turbulence event they adopt that event's color. Colors blend smoothly based on turbulence influence:

```
turbulenceFactor = pow(saturate((turbulence − 0.05) / 0.45), 0.5)
finalColor = lerp(grayColor, patternColor, turbulenceFactor)
```

| Pattern | Color | RGB |
|---------|-------|-----|
| CIRCULAR | Bright green | (0.3, 0.9, 0.4) |
| SCATTER | Red | (1.0, 0.3, 0.3) |
| VORTEX | Purple | (0.9, 0.5, 0.9) |
| WAVE | Cyan | (0.3, 0.9, 0.9) |
| OSCILLATION | Yellow | (1.0, 0.9, 0.3) |
| CLUSTER | Light gray | (0.7, 0.7, 0.7) |

Color is further modulated by agent speed: `finalColor = lerp(color × 0.5, color, speedRatio)` — slower agents appear darker.

Pattern type is encoded into the flow field texture's alpha channel for the shader:
```
alpha = turbulence × 0.9 + (patternID / 10.0)
```
Decoded in the shader as:
```hlsl
float pattern    = frac(alpha × 10.0) × 10.0;
float turbulence = saturate((alpha − pattern / 10.0) / 0.9);
```

When the player applies a smoothing tool, affected agents shift to a slightly blue-tinted light gray `RGB(0.7, 0.7, 0.75)` as visual confirmation that correction is working, before returning to normal gray.

---

## Temporal Arc: Order → Chaos → Order (~5 minutes)

A session runs up to 5 minutes. After a 3-second intro fade-in, gameplay begins. The session ends at the 5-minute mark, or earlier if all scripted events have finished, at least 3 minutes have elapsed, and no events are currently active.

### Phase 1: Tutorial Turbulence (0–1 min)

- Single, simple turbulent event appears (Circular assembly at 0:10)
- Learn to detect (box appears), classify (label shows), correct (hold to smooth)
- Energy system and grid interaction become clear
- First taste of control

### Phase 2: Escalation (1–2.5 min)

- Multiple turbulences at once
- Stronger, larger events; Scatter punishes slow reaction
- Grid density begins shifting based on performance
- Triage becomes necessary: you can't fix everything
- Energy management matters; PULSE and LOCK become valuable

### Phase 3: Pressure (2.5–3.5 min)

- Random events spawn more frequently (up to 3 simultaneous)
- Overlapping scripted turbulences (two events overlap at 2:25)
- If struggling: grid degrades, tools feel weak, frustration builds
- If succeeding: grid enhances, tools are crisp, you feel in control
- The machine's judgment becomes clear

### Phase 4: Resolution (3.5–5 min)

- Final_Scatter climax at 3:20, then wind-down Cluster at 3:40
- Random event spawning ceases after 4 minutes
- Final attempts to restore order
- The field calms or remains chaotic depending on your performance

You're left with a feeling: complicity or resistance? Did you maintain order? Should you have?

---

## Scripted Event Sequence

10 scripted events cover the first ~240 seconds, designed to introduce patterns gradually and escalate toward a difficult climax:

| Time | Name | Pattern | Radius | Strength | Duration |
|------|------|---------|--------|----------|----------|
| 0:10 | Initial_Assembly | Circular | 10 | 4 | 15s |
| 0:50 | Spiral_Formation | Vortex | 14 | 5 | 14s |
| 1:15 | Panic_Scatter | Scatter | 12 | 7 | 10s |
| 1:35 | Blockade | Cluster | 10 | 4 | 18s |
| 2:00 | March_Wave | Wave | 20 | 5.5 | 16s |
| 2:25 | Oscillation_Pattern | Oscillation | 10 | 5 | 14s |
| 2:30 | Multi_Gather_B | Circular | 9 | 4.5 | 12s |
| 2:55 | Major_Vortex | Vortex | 18 | 6 | 15s |
| 3:20 | Final_Scatter | Scatter | 15 | 8 | 12s |
| 3:40 | Aftermath_Cluster | Cluster | 12 | 3 | 20s |

Random events also spawn on top of the scripted sequence — every 6–15 seconds (shortening as difficulty scales), up to 3 active at once, until 4 minutes elapsed. Difficulty starts at 1× and ramps by 0.02/second, capping at 2×.

---

## HUD Elements

- **Tool bar** — bottom center. Shows `1 SCAN`, `2 PULSE`, `3 LOCK`. Active tool is brighter. PULSE and LOCK show live cooldown countdown while unavailable.
- **Divergence panel** — top-right. Fill bar + smoothed numeric value. Cool blue → warm rose with rising divergence.
- **Mini-map radar** — top-right corner (140×140 px). World boundary, camera viewport, and a blip per active event. Off-screen blips appear as crosshair rings; on-screen as gray dots.
- **Edge arrows** — when an event is off-screen, a triangle arrow appears at the nearest screen edge, pointing inward, with a distance reading. Arrow scales with event intensity.
- **Pattern label** — monospace keyword above the bounding box when cursor overlaps an active event zone. Pulses while a tool is active.
- **Cluster count labels** — `×N` floating above each cluster sub-box in turbulent zones.

---

## Key Moments (What the Player Feels)

**Moment 1: First Detection**
*The box appears. A label: "VORTEX". Oh. The machine sees.*

**Moment 2: First Correction**
*Hold the button. The lavender spiral fades. Agents align. Gray returns. Satisfaction.*

**Moment 3: Energy Depletion**
*The box shifts amber. The SCAN cuts off. Helpless. Turbulence spreads while you wait.*

**Moment 4: Grid Degradation**
*The lines thin. The box feels fragile. The system can barely see. You're losing.*

**Moment 5: Grid Enhancement**
*The box sharpens. Lines thicken. Perception is crisp. The system trusts you. Power.*

**Moment 6: Triage**
*Three turbulences at once. You can't fix them all. Choose. Prioritize. Let one go.*

**Moment 7: Tool Selection**
*A Scatter forms at the edge. PULSE is ready. One tap—it breaks before it builds.*

**Moment 8: Pattern Recognition**
*You know these now. SCATTER spreads fast. VORTEX pulls inward. CLUSTER is sticky. You think like the algorithm.*

**Moment 9: Complicity**
*Gray spreads. Order returns. The field is calm. But what did you just participate in?*

---

## The Philosophy: Complicity in Automated Control

**Laminar Flow** is about **revealing the operational layer** of machine vision systems. The tools are modeled after real computer vision concepts: optical flow algorithms enforce *spatial coherence*—the assumption that neighboring pixels should have similar motion vectors. Your smoothing tools are user-guided regularizers: you tell the system "apply smoothness constraints here," and it aligns agents with their neighbors, dissolving deviation.

The four operations the system performs—and that you perform as its operator:

1. **Detection**: The algorithm scans constantly
2. **Classification**: It labels what it sees (VORTEX, SCATTER, CLUSTER)
3. **Suppression**: It applies force to regularize deviation
4. **Optimization**: It adjusts its own parameters based on "performance"

You experience this from the inside. You become the operator. The tool rewards competence—sharper grid, more power, responsive controls. Poor performance is punished—degraded perception, weaker tools, harder to recover.

**The game asks:**
- What does it mean to "smooth" human movement?
- Who decides what patterns are "turbulent"?
- What is lost when chaos becomes uniformity?
- How does algorithmic perception shape what is seen and unseen?

The colors are metaphorical but evocative:
- Green circles = gatherings
- Red scatter = panic
- Purple vortex = spiral formations
- Cyan waves = marches

The game never tells you this explicitly. But the visual language suggests it. And your job is to **make them all gray**.

**Laminar Flow** doesn't argue a position. It doesn't preach. It lets you **operate the apparatus**. And then asks you to reflect on what you just did.

---

## Controls Summary

- **Mouse movement**: Position detection grid
- **Left click / hold**: Fire active tool
- **1 / 2 / 3**: Switch between SCAN, PULSE, LOCK
- **Mouse wheel**: Adjust detection area size (2–25 units; LOCK capped at 4)

---

## Design Pillars

1. **Expose the apparatus**: Show how machine vision operates
2. **Minimal text**: One keyword per detection
3. **Visual communication**: Pulse, color, opacity over numbers
4. **Complicit gameplay**: You maintain the system
5. **Documentary tone**: Not gamified, operational
6. **Smooth and dramatic**: Feel the machine working
7. **Quick to grasp**: 10 seconds to understand, 5 minutes to experience

---

## Technical Summary

- **Runtime**: up to 5 minutes (ends early when scripted events clear + 3 min elapsed)
- **Agents**: 800 in real-time flow simulation
- **Scripted events**: 10, covering first ~240 seconds
- **Random events**: every 6–15 seconds until 4 min elapsed, up to 3 simultaneous
- **Patterns**: 6 types (Circular, Scatter, Vortex, Wave, Oscillation, Cluster)
- **Tools**: 3 (SCAN, PULSE, LOCK)
- **Sampling grid**: 3 densities (3×3, 5×5, 7×7) based on coherence
- **Energy**: Depletable resource (SCAN only) with regeneration delay
- **Aesthetic**: Computer vision detection system UI
- **Platform**: Unity, playable in browser or standalone

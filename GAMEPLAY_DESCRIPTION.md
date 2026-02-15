# Laminar Flow: A Documentary of Machine Vision

## Conceptual Overview

**Laminar Flow** is an interactive experience that reveals the operational layer of computer vision algorithms. Players don't "play" the game—they *operate* it, becoming complicit in the act of automated perception and control. The interface exposes the normally hidden apparatus of machine vision: detection grids, classification labels, and the mechanical process of imposing order on chaos.

---

## The World

You see a field of flowing particles—thousands of small gray dots moving in organized diagonal streams. This is **laminar flow**: smooth, predictable, uniform. The natural state the system desires.

But disruptions occur. **Turbulent events** emerge—zones where the flow becomes chaotic:

- **CIRCULAR**: Green swirls where particles orbit a center (peaceful assemblies)
- **SCATTER**: Red explosions of panic and dispersal (chaos events)
- **VORTEX**: Purple spirals pulling inward (gathering formations)
- **WAVE**: Cyan ripples moving directionally (marching patterns)
- **OSCILLATION**: Yellow zones of violent shaking (disturbances)
- **CLUSTER**: Light gray congestion where movement slows (blockades)

Each disruption is color-coded, making the chaos visible and distinct. The system wants gray. The system needs order.

---

## The Tool: Detection Grid Overlay

Your interface is a **computer vision detection system**. When you move your cursor, a bounding box appears—a rectangular outline with corner markers, exactly like object recognition algorithms draw on security footage or autonomous vehicle feeds.

### What You See

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
- Line thickness and opacity indicating system confidence/energy
- Pattern keyword above the box when turbulence is detected
- Pulsing intensity showing system activity

**Visual States:**
- **Idle**: Subtle pulse, light blue, steady
- **Active Correction**: Rapid pulse, bright cyan, intense
- **Low Energy**: Orange tint, dim, weakening
- **Degraded Performance**: Thinner lines, smaller detection area

### Grid Density (Performance Metric)

The system's perception quality changes based on your performance:

- **3x3 sampling grid**: Struggling—the system can barely see
- **5x5 sampling grid**: Baseline—normal perception
- **7x7 sampling grid**: Enhanced—high-resolution detection

*You don't choose this. The algorithm adjusts based on how well you maintain order.*

Better performance → denser grid → more effective smoothing → easier to maintain order.
Poor performance → degraded grid → weaker smoothing → harder to recover.

It's a feedback loop. The machine rewards competence, punishes failure.

---

## The Action: Smoothing Turbulence

**Click and hold** over turbulent regions. The detection box appears. If turbulence is present, a classification label appears: **VORTEX**, **SCATTER**, **WAVE**.

The machine is telling you: *"I see this. I know what this is."*

Hold the button. The box pulses rapidly. Particles within the detection area begin to *regularize*—their chaotic motion dampens, colors fade from vivid patterns back toward organized gray.

You're applying a **smoothing kernel**, like Gaussian blur on an image. The algorithm enforces spatial coherence: agents align with their neighbors, turbulence dissolves, order returns.

### Mechanics

- **Ramp-up**: The longer you hold, the stronger the smoothing (0-1.5 seconds to max strength)
- **Energy drain**: The tool consumes energy while active
- **Falloff**: Smoothing is strongest at the center, weakens at edges
- **Visual feedback**: Particles spawn from the grid, showing the algorithm's touch points

### Limitations

- **Energy system**: The tool has limited energy (visible as line opacity/brightness)
- **Regeneration**: Energy slowly recharges when not in use (with delay)
- **Depletion**: When energy is exhausted, the grid dims to orange, weakens, then vanishes
- **Performance pressure**: Multiple strong turbulences degrade grid quality

---

## The Rhythm: Detection → Classification → Correction

Gameplay follows a pattern:

1. **Scan**: Move cursor across the field
2. **Detect**: Bounding box identifies turbulent regions
3. **Classify**: Label appears—**SCATTER**, **VORTEX**, etc.
4. **Apply**: Hold to smooth, watch pattern dissolve
5. **Monitor**: Energy depletes, grid density shifts
6. **Adapt**: Prioritize which turbulences to address

Multiple turbulent events occur simultaneously. You can't fix everything. You must **triage**: which chaos matters most? What can you let slide? When do you intervene?

The game trains you to think like an algorithm: identify, classify, suppress.

---

## The Score: Coherence Over Time

Your "score" is hidden but felt: **flow coherence**.

- High coherence = organized gray flow = system pleased
- Low coherence = widespread chaos = system stressed

**Performance effects:**
- Good coherence → Grid upgrades to 7x7 (better perception)
- Poor coherence → Grid degrades to 3x3 (system strain)
- Sustained high chaos → Tool feels weaker, energy drains faster
- Restored order → Tool feels responsive, energy recharges faster

You feel the system's judgment through its responsiveness. When you're doing well, the tool is sharp, confident. When you're failing, it struggles.

---

## Visual Language: Exposing the Apparatus

The interface is intentionally **documentary**, not gamified:

- **Bounding boxes** instead of fantasy cursors
- **Classification keywords** instead of health bars
- **System states** communicated through visual behavior, not numbers
- **Grid density** as a performance metric, not an upgrade choice
- **Monospace font** for labels (system typography)
- **Pulsing lines** showing algorithm activity, not decorative effects

Every visual element reveals something about **how machine vision operates**:

- The box shows the detection area
- The label shows the classification
- The pulse shows processing intensity
- The grid density shows perception resolution
- The corner markers mimic real CV annotation tools

You're not playing a game. You're **operating surveillance apparatus**.

---

## Temporal Arc: Order → Chaos → Order (4 minutes)

The experience unfolds over ~4 minutes:

### Phase 1: Tutorial Turbulence (0-1 min)
- Single, simple turbulent event appears
- Learn to detect (box appears), classify (label shows), correct (hold to smooth)
- Energy system and grid interaction become clear
- First taste of control

### Phase 2: Escalation (1-2.5 min)
- Multiple turbulences at once
- Stronger, larger events
- Grid density begins shifting based on performance
- Triage becomes necessary: you can't fix everything
- Energy management matters
- The system's demands increase

### Phase 3: Pressure (2.5-3.5 min)
- Random events spawn more frequently
- Overlapping turbulences
- If you're struggling: grid degrades, tool weakens, frustration builds
- If you're succeeding: grid enhances, tool is powerful, you feel in control
- The machine's judgment becomes clear

### Phase 4: Resolution (3.5-4 min)
- Turbulence slows, scripted events complete
- Final attempts to restore order
- The field calms or remains chaotic depending on your performance
- The experience ends

You're left with a feeling: complicity or resistance? Did you maintain order? Should you have?

---

## The Philosophy: Complicity in Automated Control

**Laminar Flow** is about **revealing the operational layer** of machine vision systems:

1. **Detection**: The algorithm scans constantly
2. **Classification**: It labels what it sees (VORTEX, SCATTER, CLUSTER)
3. **Suppression**: It applies force to regularize deviation
4. **Optimization**: It adjusts its own parameters based on "performance"

You experience this from the inside. You become the operator. The tool rewards competence—smoother action, better grid, more power. Poor performance is punished—degraded perception, weaker tool, harder to recover.

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

---

## Controls

- **Mouse movement**: Position detection grid
- **Left click + hold**: Apply smoothing to detected turbulence
- **Mouse wheel**: Adjust detection area size (2-25 units)

That's it. No complex combos, no menu systems. Just: detect, classify, suppress.

---

## Key Moments (What the Player Feels)

### Moment 1: First Detection
*The box appears. A label: "VORTEX". Oh. The machine sees.*

### Moment 2: First Correction
*Hold the button. The purple spiral fades. Particles align. Gray returns. Satisfaction.*

### Moment 3: Energy Depletion
*The box dims. Orange. Weak. It blinks out. Helpless. Turbulence spreads while you wait.*

### Moment 4: Grid Degradation
*The lines thin. The box feels fragile. The system can barely see. You're losing.*

### Moment 5: Grid Enhancement
*The box sharpens. Lines thicken. Perception is crisp. The system trusts you. Power.*

### Moment 6: Triage
*Three turbulences at once. You can't fix them all. Choose. Prioritize. Let one go.*

### Moment 7: Pattern Recognition
*You know these now. SCATTER is fast chaos. VORTEX is slow spirals. You think like the algorithm.*

### Moment 8: Complicity
*Gray spreads. Order returns. The field is calm. But what did you just participate in?*

---

## After the Experience

The game ends. The field settles (or doesn't).

The player is left with a memory of:
- Bounding boxes scanning for deviation
- Keywords labeling human patterns
- Smoothing kernels dissolving collective behavior
- A system that rewarded compliance with power

**Laminar Flow** doesn't argue a position. It doesn't preach. It lets you **operate the apparatus**. And then asks you to reflect on what you just did.

---

## Technical Summary

- **Runtime**: ~4 minutes
- **Turbulent events**: 9 scripted + dynamic random events
- **Patterns**: 6 types (Circular, Scatter, Vortex, Wave, Oscillation, Cluster)
- **Tool states**: 3 grid densities (3x3, 5x5, 7x7) based on coherence
- **Energy**: Depletable resource with regeneration delay
- **Agents**: ~10,000 particles in real-time flow simulation
- **Aesthetic**: Computer vision detection system UI
- **Platform**: Unity, playable in browser or standalone

---

## Design Pillars

1. **Expose the apparatus**: Show how machine vision operates
2. **Minimal text**: One keyword per detection
3. **Visual communication**: Pulse, color, opacity over numbers
4. **Complicit gameplay**: You maintain the system
5. **Documentary tone**: Not gamified, operational
6. **Smooth and dramatic**: Feel the machine working
7. **Quick to grasp**: 10 seconds to understand, 4 minutes to experience

The point is not to teach CV. The point is to **make visible** what is usually hidden: the mechanical gaze of automated systems, and the human role in operating them.

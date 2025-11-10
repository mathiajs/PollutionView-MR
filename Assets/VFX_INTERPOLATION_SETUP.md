# VFX Graph Setup for Timestep Interpolation

## Overview
This guide shows how to modify your VFX Graph to support smooth cross-fade between timesteps.

## What You Need to Add

### 1. Add Properties (Blackboard)

Open your VFX Graph and add these properties:

- **CurrentTimestep** (Int) - Already exists
- **NextTimestep** (Int) - NEW
- **InterpolationBlend** (Float, 0-1) - NEW
- **EnableInterpolation** (Bool) - NEW

### 2. Modify the Spawn Context

Currently you probably have something like:
```
Set Spawn Count = (particles where t == CurrentTimestep)
```

Change to:
```
If EnableInterpolation:
    Show particles where (t == CurrentTimestep) OR (t == NextTimestep)
Else:
    Show particles where (t == CurrentTimestep)
```

### 3. Add Alpha Blending in Initialize or Update

Add a new attribute: **AlphaBlend** (Float)

In Initialize Particle or Update Particle:
```
If (particle.t == CurrentTimestep):
    AlphaBlend = 1.0 - InterpolationBlend  // Fade OUT
Else if (particle.t == NextTimestep):
    AlphaBlend = InterpolationBlend        // Fade IN
```

### 4. Apply Alpha to Color

In Output Particle (or wherever you set color):
```
Color.a = Color.a * AlphaBlend
```

## Visual Flow

```
Timestep 0 particles: Alpha = 1.0 → 0.0 (fade out)
Timestep 1 particles: Alpha = 0.0 → 1.0 (fade in)
InterpolationBlend:   0.0 → 1.0 (over time)
```

## Example VFX Node Setup

```
[Initialize Particle]
├─ Set Position (from buffer)
├─ Set Color
└─ Set Custom Attribute (AlphaBlend)
    └─ If particle.t == CurrentTimestep
        └─ Value = 1.0 - InterpolationBlend
    └─ Else If particle.t == NextTimestep
        └─ Value = InterpolationBlend
    └─ Else
        └─ Value = 0.0

[Output Particle Quad]
└─ Multiply Color Alpha
    └─ Color.a = Color.a * AlphaBlend
```

## Testing

1. Set EnableInterpolation = True in ParticleAnimationController
2. Set autoPlay = True
3. Watch particles smoothly cross-fade between timesteps!

## Notes

- Doubling visible particles (showing 2 timesteps) may impact performance
- Adjust InterpolationSpeed to control fade duration
- You can also blend scale/size instead of just alpha for different effects

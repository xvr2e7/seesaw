using UnityEngine;

/// <summary>
/// Manages tool energy: drain during use, regeneration when idle.
/// Extracted from PlayerToolController for modularity.
/// </summary>
public class ToolEnergySystem : MonoBehaviour
{
    [Header("Energy Settings")]
    [Tooltip("Maximum energy pool")]
    public float maxEnergy = 100f;

    [Tooltip("Energy consumed per second while tool is active")]
    public float drainRate = 20f;

    [Tooltip("Energy regenerated per second while tool is inactive")]
    public float regenRate = 8f;

    [Tooltip("Delay before energy starts regenerating after use")]
    public float regenDelay = 0.5f;

    [Tooltip("Minimum energy required to activate tool")]
    public float minActivationEnergy = 5f;

    [Tooltip("Strength multiplier when energy is low")]
    public AnimationCurve energyStrengthCurve = AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f);

    // Runtime state
    private float currentEnergy;
    private float timeSinceLastUse;
    private bool isDepleted;

    // Public accessors
    public float CurrentEnergy => currentEnergy;
    public float EnergyRatio => currentEnergy / maxEnergy;
    public bool IsDepleted => isDepleted;
    public bool CanActivate => currentEnergy >= minActivationEnergy && !isDepleted;

    void Start()
    {
        currentEnergy = maxEnergy;
        timeSinceLastUse = regenDelay; // Allow immediate use
    }

    void Update()
    {
        // Auto-regenerate when not in use
        if (timeSinceLastUse >= regenDelay)
        {
            RegenerateEnergy(Time.deltaTime);
        }
        else
        {
            timeSinceLastUse += Time.deltaTime;
        }
    }

    /// <summary>
    /// Consume energy for tool use. Returns false if insufficient energy.
    /// </summary>
    public bool ConsumeEnergy(float deltaTime)
    {
        if (isDepleted) return false;

        currentEnergy -= drainRate * deltaTime;
        currentEnergy = Mathf.Max(0f, currentEnergy);
        timeSinceLastUse = 0f;

        if (currentEnergy <= 0f)
        {
            isDepleted = true;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Regenerate energy when idle
    /// </summary>
    void RegenerateEnergy(float deltaTime)
    {
        currentEnergy += regenRate * deltaTime;
        currentEnergy = Mathf.Min(currentEnergy, maxEnergy);

        // Reset depleted flag when sufficient energy
        if (currentEnergy >= minActivationEnergy)
        {
            isDepleted = false;
        }
    }

    /// <summary>
    /// Get strength multiplier based on current energy
    /// </summary>
    public float GetEnergyStrengthModifier()
    {
        return energyStrengthCurve.Evaluate(EnergyRatio);
    }

    /// <summary>
    /// Restore energy (e.g., from power-up)
    /// </summary>
    public void RestoreEnergy(float amount)
    {
        currentEnergy = Mathf.Min(currentEnergy + amount, maxEnergy);

        if (currentEnergy >= minActivationEnergy)
        {
            isDepleted = false;
        }
    }

    /// <summary>
    /// Reset energy to full
    /// </summary>
    public void ResetEnergy()
    {
        currentEnergy = maxEnergy;
        isDepleted = false;
        timeSinceLastUse = regenDelay;
    }
}

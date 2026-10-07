namespace SboxTwoBrains.Core;

/// <summary>Threat-aware hesitation/flank/withdraw behaviour.</summary>
public sealed class ThreatSection
{
	/// <summary>"Close target" route distance (metres). Range: [0.1, 1000].</summary>
	public double? CloseDistance { get; set; }
	/// <summary>"Very close target" route distance (metres). Range: [0.1, 1000].</summary>
	public double? VeryCloseDistance { get; set; }
	/// <summary>Pause when a dangerous target aims at the monster. Range: [0, 60].</summary>
	public double? AimedWeaponHesitationSeconds { get; set; }
	/// <summary>Keep acting on sight this long after losing it. Range: [0, 60].</summary>
	public double? VisualRetentionSeconds { get; set; }
	/// <summary>Probability of choosing an ingress flank over direct approach. Range: [0, 1].</summary>
	public double? FlankChance { get; set; }
	/// <summary>Threat-aware episode timeout. Range: [1, 3600].</summary>
	public double? ThreatTimeoutSeconds { get; set; }
	/// <summary>Retreat when deterrent exposure persists this long. Range: [0, 600].</summary>
	public double? DeterrentRetreatSeconds { get; set; }
	/// <summary>Threat rating at/above which a target counts as dangerous. Range: [0, 1].</summary>
	public double? DangerousThreatRating { get; set; }
}

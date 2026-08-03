namespace SboxTwoBrains;

/// <summary>Systematic + stimulus-driven search behaviour.</summary>
public sealed class SearchSection
{
	/// <summary>Window in which a searched/sensed position still qualifies systematic search. Range: [1, 600].</summary>
	public double? SystematicWindowSeconds { get; set; }
	/// <summary>A node searched within this window is skipped. Range: [0, 600].</summary>
	public double? NodeRevisitPenaltySeconds { get; set; }
	/// <summary>Give up an unproductive search after this long. Range: [1, 3600].</summary>
	public double? GiveUpSeconds { get; set; }
	/// <summary>Nodes visited per search episode before resting. Range: [1, 64].</summary>
	public int? MaxNodesPerSearch { get; set; }
	/// <summary>Distance at which a searched point counts as reached (metres). Range: [0.1, 100].</summary>
	public double? NodeReachDistance { get; set; }
}

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

/// <summary>Chase/attack execution guards.</summary>
public sealed class CombatSection
{
	/// <summary>Route distance within which an attack may commit (metres). Range: [0.1, 100].</summary>
	public double? AttackRange { get; set; }
	/// <summary>Abandon chase beyond this route distance (metres). Range: [1, 1000].</summary>
	public double? ChaseGiveUpDistance { get; set; }
	/// <summary>Abandon chase after losing the target this long (seconds). Range: [0.1, 600].</summary>
	public double? ChaseGiveUpSeconds { get; set; }
	/// <summary>Minimum time between attack commits. Range: [0, 600].</summary>
	public double? AttackCooldownSeconds { get; set; }
	/// <summary>Time budget for an ingress-flank manoeuvre. Range: [0, 600].</summary>
	public double? FlankIngressSeconds { get; set; }
	/// <summary>After a failed/rejected attack, wait this long before retrying. Range: [0, 600].</summary>
	public double? AttackBanSeconds { get; set; }
}

/// <summary>Offstage staging, sweep and ingress policy for the micro layer.</summary>
public sealed class OffstageSection
{
	/// <summary>After using an ingress, ignore it for this long (seconds). Range: [0, 600].</summary>
	public double? IngressBanSeconds { get; set; }
	/// <summary>Dwell at an offstage node, lower bound (seconds). Range: [0, 600].</summary>
	public double? NodeDwellMinSeconds { get; set; }
	/// <summary>Dwell at an offstage node, upper bound (seconds). Range: [0, 600].</summary>
	public double? NodeDwellMaxSeconds { get; set; }
	/// <summary>Prefer ingress points nearer the pressured region over nearer the monster.</summary>
	public bool? PreferIngressNearPressure { get; set; }
	/// <summary>Allow killtrap-style staged waiting at egress points.</summary>
	public bool? KilltrapEnabled { get; set; }
	/// <summary>Seconds before an unanswered ingress request counts as failed. Range: [1, 120].</summary>
	public double? IngressTimeoutSeconds { get; set; }
}

/// <summary>Ordered module arbitration and enablement.</summary>
public sealed class ModulesSection
{
	/// <summary>Module names in arbitration order (earlier wins). Empty = built-in default order.</summary>
	public string[] Order { get; set; }
	/// <summary>Module names force-disabled for this profile.</summary>
	public string[] Disabled { get; set; }
}

/// <summary>Locomotion scales and small movement behaviours.</summary>
public sealed class MovementSection
{
	/// <summary>Investigate/approach speed scale. Range: [0, 1].</summary>
	public double? SpeedSlow { get; set; }
	/// <summary>Search/stalk speed scale. Range: [0, 1].</summary>
	public double? SpeedFast { get; set; }
	/// <summary>Chase speed scale. Range: [0, 1].</summary>
	public double? SpeedFastest { get; set; }
	/// <summary>Seconds spent facing a point of interest on arrival. Range: [0, 60].</summary>
	public double? InvestigateFacingSeconds { get; set; }
}

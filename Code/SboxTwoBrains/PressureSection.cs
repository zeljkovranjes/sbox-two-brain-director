namespace SboxTwoBrains;

/// <summary>
/// Macro pressure pacing. Units are seconds (timers), metres (distances), unitless [0,1]
/// fractions, and integer counts. All ranges validated in <see cref="EffectiveConfig"/>.
/// Research-derived values appear in the AlienIsolationInspired preset, never here.
/// </summary>
public sealed class PressureSection
{
	/// <summary>Seconds for an eligible candidate gauge to fill. Range: [0.5, 600].</summary>
	public double? FillSeconds { get; set; }
	/// <summary>Cooldown after an opportunity completes before pressure may rise. Range: [0, 600].</summary>
	public double? CooldownSeconds { get; set; }
	/// <summary>Seconds over which a full gauge decays to zero while ineligible. Range: [0.1, 600].</summary>
	public double? DecreaseSeconds { get; set; }
	/// <summary>Grace period after losing eligibility before decrease starts. Range: [0, 600].</summary>
	public double? DecreaseDelaySeconds { get; set; }
	/// <summary>Maximum completed opportunities before quota blocks new cycles. Range: [1, 100].</summary>
	public int? MaxOpportunities { get; set; }
	/// <summary>Event-quota random target lower bound; 0 disables the quota. Range: [0, 100].</summary>
	public int? EventQuotaMin { get; set; }
	/// <summary>Event-quota random target upper bound (inclusive). Range: [0, 100].</summary>
	public int? EventQuotaMax { get; set; }
	/// <summary>Progression at which Normal may switch to Aggressive. Range: (0, 1].</summary>
	public double? AggressiveThresholdProgression { get; set; }
	/// <summary>Progression a fresh/reset cycle starts at. Range: [0, 1].</summary>
	public double? StartProgression { get; set; }
	/// <summary>Seconds an offstage sweep opportunity stays active. Range: [1, 600].</summary>
	public double? SweepDurationSeconds { get; set; }
	/// <summary>Minimum route distance for sweep staging. Range: [0, 1000].</summary>
	public double? SweepMinDistance { get; set; }
	/// <summary>Maximum route distance for sweep staging. Range: [0, 1000].</summary>
	public double? SweepMaxDistance { get; set; }
	/// <summary>Idle dwell at a sweep node, lower bound. Range: [0, 600].</summary>
	public double? SweepIdleMinSeconds { get; set; }
	/// <summary>Idle dwell at a sweep node, upper bound. Range: [0, 600].</summary>
	public double? SweepIdleMaxSeconds { get; set; }
	/// <summary>Ambush role timeout. Range: [1, 600].</summary>
	public double? AmbushTimeoutSeconds { get; set; }
	/// <summary>Dwell time for a killtrap-style staged point. Range: [0, 600].</summary>
	public double? KilltrapSeconds { get; set; }
	/// <summary>Role assignment timeout, lower bound. Range: [0, 600].</summary>
	public double? RoleTimeoutMinSeconds { get; set; }
	/// <summary>Role assignment timeout, upper bound. Range: [0, 600].</summary>
	public double? RoleTimeoutMaxSeconds { get; set; }
	/// <summary>Exclusion radius around target/objective for the first stalk, min. Range: [0, 1000].</summary>
	public double? ExclusionFirstMin { get; set; }
	/// <summary>Exclusion radius around target/objective for the first stalk, max. Range: [0, 1000].</summary>
	public double? ExclusionFirstMax { get; set; }
	/// <summary>Exclusion radius for subsequent stalks, min. Range: [0, 1000].</summary>
	public double? ExclusionSubsequentMin { get; set; }
	/// <summary>Exclusion radius for subsequent stalks, max. Range: [0, 1000].</summary>
	public double? ExclusionSubsequentMax { get; set; }
	/// <summary>Ingress attraction window, lower bound. Range: [0, 600].</summary>
	public double? IngressAttractMinSeconds { get; set; }
	/// <summary>Ingress attraction window, upper bound. Range: [0, 600].</summary>
	public double? IngressAttractMaxSeconds { get; set; }
	/// <summary>Sweep box half width (metres). Range: [0, 100].</summary>
	public double? SweepBoxHalfWidth { get; set; }
	/// <summary>Sweep box minimum half length (metres). Range: [0, 1000].</summary>
	public double? SweepBoxMinHalfLength { get; set; }
	/// <summary>Seconds a macro opportunity remains valid. Range: [1, 600].</summary>
	public double? OpportunityExpirySeconds { get; set; }
}

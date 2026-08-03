namespace SboxTwoBrains;

/// <summary>Pressure controller mode. Normal accumulates; Aggressive discharges an opportunity.</summary>
public enum PressureMode
{
	Normal = 0,
	Aggressive = 1,
}

/// <summary>
/// Declarative macro output. Carries bias and constraints — never target coordinates and
/// never movement instructions. The host may reject or defer the opportunity and must
/// acknowledge that result on a later tick.
/// </summary>
public sealed class PressureDecision
{
	/// <summary>Stable id for this opportunity instance (deterministic).</summary>
	public string OpportunityId { get; set; } = "";

	public PressureMode Mode { get; set; }

	/// <summary>Current gauge progression in [0,1].</summary>
	public double Progression { get; set; }

	/// <summary>Computed urgency in [0,1] (derived from progression, mode, cooldowns).</summary>
	public double Urgency { get; set; }

	/// <summary>Candidate region id for staging, or empty when none is nominated.</summary>
	public string CandidateRegionId { get; set; } = "";

	/// <summary>Roles the micro agent may assume (e.g. "stalker", "ambusher", "sweeper").</summary>
	public string[] AllowedRoles { get; set; } = System.Array.Empty<string>();

	/// <summary>Ingress ids the macro suggests for staging (hints, not orders).</summary>
	public string[] IngressConstraints { get; set; } = System.Array.Empty<string>();

	/// <summary>Exclusion zone ids that shaped this decision (diagnostics).</summary>
	public string[] ExclusionConstraints { get; set; } = System.Array.Empty<string>();

	/// <summary>Tick after which this opportunity lapses if not acted on.</summary>
	public long ExpiryTick { get; set; }

	/// <summary>Machine-readable reason for the decision/transition (telemetry key).</summary>
	public string ReasonCode { get; set; } = "";

	/// <summary>Human-readable evidence lines (config values, checks) for diagnostics.</summary>
	public string[] Evidence { get; set; } = System.Array.Empty<string>();
}

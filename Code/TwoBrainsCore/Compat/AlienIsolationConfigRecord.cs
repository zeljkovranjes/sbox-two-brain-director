using TwoBrains.Core.Config;

namespace TwoBrains.Core.Compat;

/// <summary>
/// Verbatim research record for one shipped intensity configuration (compatibility preset
/// data). Values come from the machine-readable inheritance-resolved decode
/// (research/generated/alienconfigs_decoded.csv in the research repository). They are
/// recorded exactly — including numerically reversed min/max pairs, which the research
/// flags as possibly directional bounds or engine-specific semantics. Field names preserve
/// the shipped spelling (including "MeanceDeemedTime").
///
/// CONFIDENCE: the values themselves are PROVEN decoded data. Their runtime semantics are
/// STRONG RECONSTRUCTION at best; several fields' exact meaning is unresolved. This preset
/// is an Alien: Isolation-inspired compatibility option — never claim exact parity.
/// </summary>
public sealed class AlienIsolationConfigRecord
{
	public string Name { get; set; } = "";

	/// <summary>Shipped template-inheritance parent name, empty when none.</summary>
	public string TemplateName { get; set; } = "";

	public double? VentAttractTimeMax { get; set; }
	public double? VentAttractTimeMin { get; set; }
	public double? AmbushTimeout { get; set; }
	public double? DecreaseSweepDuration { get; set; }
	public double? IncreaseSweepDuration { get; set; }
	public double? KilltrapTime { get; set; }
	public double? MaxDistance { get; set; }
	public double? MaxIdleTime { get; set; }
	public double? MaxMenaces { get; set; }
	public double? MeanceDeemedTime { get; set; }
	public double? MenaceCoolDownTime { get; set; }
	public double? MenaceGaugeDecreaseTime { get; set; }
	public double? MenaceGaugeSecondsToFill { get; set; }
	public double? MinDistance { get; set; }
	public double? MinIdleTime { get; set; }
	public double? NearObjectiveExclusionRadiusFirstStalkMax { get; set; }
	public double? NearObjectiveExclusionRadiusFirstStalkMin { get; set; }
	public double? NearObjectiveExclusionRadiusSubsequentStalkMax { get; set; }
	public double? NearObjectiveExclusionRadiusSubsequentStalkMin { get; set; }
	public double? NearTargetExclusionRadiusFirstStalkMax { get; set; }
	public double? NearTargetExclusionRadiusFirstStalkMin { get; set; }
	public double? NearTargetExclusionRadiusSubsequentStalkMax { get; set; }
	public double? NearTargetExclusionRadiusSubsequentStalkMin { get; set; }
	public double? RoleTimeoutMax { get; set; }
	public double? RoleTimeoutMin { get; set; }
	public double? SweepBoxHalfWidth { get; set; }
	public double? SweepBoxMinHalfLength { get; set; }

	/// <summary>
	/// Maps the verbatim record onto a generic pressure profile section. Unmapped fields
	/// (decrease_sweep_duration, near-objective radii) have no generic equivalent and stay
	/// available on the record itself. Engine "-1" sentinels map to 0 (documented as
	/// "disabled" in the research), never to negative values.
	/// </summary>
	public PressureSection ToPressureSection()
	{
		return new PressureSection
		{
			FillSeconds = MenaceGaugeSecondsToFill,
			CooldownSeconds = MenaceCoolDownTime,
			DecreaseSeconds = MenaceGaugeDecreaseTime,
			DecreaseDelaySeconds = 0.0,
			MaxOpportunities = MaxMenaces.HasValue ? (int)MaxMenaces.Value : null,
			SweepDurationSeconds = IncreaseSweepDuration,
			SweepMinDistance = MinDistance,
			SweepMaxDistance = MaxDistance,
			SweepIdleMinSeconds = MinIdleTime,
			SweepIdleMaxSeconds = MaxIdleTime,
			AmbushTimeoutSeconds = AmbushTimeout,
			KilltrapSeconds = KilltrapTime < 0 ? 0.0 : KilltrapTime,
			RoleTimeoutMinSeconds = RoleTimeoutMin < 0 ? 0.0 : RoleTimeoutMin,
			RoleTimeoutMaxSeconds = RoleTimeoutMax < 0 ? 0.0 : RoleTimeoutMax,
			ExclusionFirstMin = NearTargetExclusionRadiusFirstStalkMin,
			ExclusionFirstMax = NearTargetExclusionRadiusFirstStalkMax,
			ExclusionSubsequentMin = NearTargetExclusionRadiusSubsequentStalkMin,
			ExclusionSubsequentMax = NearTargetExclusionRadiusSubsequentStalkMax,
			IngressAttractMinSeconds = VentAttractTimeMin,
			IngressAttractMaxSeconds = VentAttractTimeMax,
			SweepBoxHalfWidth = SweepBoxHalfWidth,
			SweepBoxMinHalfLength = SweepBoxMinHalfLength,
		};
	}
}

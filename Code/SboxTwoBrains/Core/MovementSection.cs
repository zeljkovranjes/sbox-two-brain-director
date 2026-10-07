namespace SboxTwoBrains.Core;

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

namespace SboxTwoBrains.Core;

/// <summary>Per-sense-channel tuning.</summary>
public sealed class PerceptionChannelSection
{
	/// <summary>Activation threshold: a stimulus counts when confidence &gt;= this. Range: [0, 1].</summary>
	public double? Threshold { get; set; }
	/// <summary>Memory confidence half-life in seconds. Range: [0.1, 3600].</summary>
	public double? DecayHalfLifeSeconds { get; set; }
	/// <summary>Records older than this are forgotten. Range: [1, 3600].</summary>
	public double? MaxAgeSeconds { get; set; }
	/// <summary>Channel weight for weighted-sum combination. Range: [0, 4].</summary>
	public double? Weight { get; set; }
}

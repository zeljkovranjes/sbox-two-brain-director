namespace TwoBrains.Core.Config;

/// <summary>How multiple memories for the same target/region combine into one confidence.</summary>
public enum MemoryCombineMode
{
	/// <summary>Highest confidence wins.</summary>
	Max = 0,
	/// <summary>Per-channel weighted sum, clamped to [0,1].</summary>
	WeightedSum = 1,
}

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

/// <summary>Micro perception + memory.</summary>
public sealed class PerceptionSection
{
	/// <summary>Maximum remembered stimuli retained. Range: [1, 256].</summary>
	public int? MemoryCapacity { get; set; }
	/// <summary>Combination rule for same-subject memories.</summary>
	public MemoryCombineMode? CombineMode { get; set; }
	/// <summary>A memory counts as "recently confirmed" within this window. Range: [0, 60].</summary>
	public double? RecentConfirmationSeconds { get; set; }

	public PerceptionChannelSection Visual { get; set; }
	public PerceptionChannelSection Auditory { get; set; }
	public PerceptionChannelSection Touch { get; set; }
	public PerceptionChannelSection Damage { get; set; }
	public PerceptionChannelSection Light { get; set; }
	public PerceptionChannelSection GameDefined { get; set; }
}

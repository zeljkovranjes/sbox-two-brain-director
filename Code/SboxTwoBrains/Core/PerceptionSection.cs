namespace SboxTwoBrains.Core;

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

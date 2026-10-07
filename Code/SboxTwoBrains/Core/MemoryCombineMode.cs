namespace SboxTwoBrains.Core;

/// <summary>How multiple memories for the same target/region combine into one confidence.</summary>
public enum MemoryCombineMode
{
	/// <summary>Highest confidence wins.</summary>
	Max = 0,
	/// <summary>Per-channel weighted sum, clamped to [0,1].</summary>
	WeightedSum = 1,
}

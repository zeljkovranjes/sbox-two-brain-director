namespace TwoBrains.Core.Contract;

/// <summary>
/// A remembered stimulus owned by the micro perception system (remembered evidence).
/// Confidence decays deterministically per channel; the record is dropped at zero or on expiry.
/// </summary>
public sealed class MemoryRecord
{
	/// <summary>Stable id inherited from the originating stimulus.</summary>
	public string StimulusId { get; set; } = "";

	public SenseChannel Channel { get; set; } = SenseChannel.Visual;

	/// <summary>Optional game-defined subtype carried over from the stimulus.</summary>
	public string Subtype { get; set; }

	/// <summary>Remembered position (metres); never silently updated without a fresh stimulus.</summary>
	public Vec3 Position { get; set; }

	/// <summary>Host region id of the remembered position; empty if none.</summary>
	public string RegionId { get; set; } = "";

	/// <summary>Confidence captured at last confirmation, in [0,1].</summary>
	public double BaseConfidence { get; set; } = 1.0;

	/// <summary>Current confidence after deterministic decay, in [0,1].</summary>
	public double DecayedConfidence { get; set; } = 1.0;

	/// <summary>Optional attributed participant identity.</summary>
	public string TargetId { get; set; }

	/// <summary>Tick the memory was created.</summary>
	public long CreatedTick { get; set; }

	/// <summary>Tick the memory was last refreshed by a matching stimulus.</summary>
	public long LastConfirmedTick { get; set; }

	/// <summary>True while the stimulus is also present in the current tick's evidence.</summary>
	public bool ConfirmedThisTick { get; set; }
}

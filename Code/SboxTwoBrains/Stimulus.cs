namespace SboxTwoBrains;

/// <summary>
/// One host-reported sensed event on the current tick (current evidence, not memory).
/// All ids are host-assigned and must be stable across ticks for the same logical stimulus.
/// </summary>
public sealed class Stimulus
{
	/// <summary>Stable host id for this logical stimulus (re-report to refresh it).</summary>
	public string StimulusId { get; set; } = "";

	/// <summary>Sensory channel. Research-derived channels map onto these generically.</summary>
	public SenseChannel Channel { get; set; } = SenseChannel.Visual;

	/// <summary>Optional game-defined subtype (e.g. "footstep", "gunshot", "flashlight").</summary>
	public string Subtype { get; set; }

	/// <summary>Observed position (metres). May be imprecise for non-visual channels.</summary>
	public Vec3 Position { get; set; }

	/// <summary>Host region id containing <see cref="Position"/>; empty if none.</summary>
	public string RegionId { get; set; } = "";

	/// <summary>Host confidence in [0,1] that this stimulus is real and located correctly.</summary>
	public double Confidence { get; set; } = 1.0;

	/// <summary>Optional identity of the sensed participant, if the host can attribute it.</summary>
	public string TargetId { get; set; }

	/// <summary>Tick the stimulus was first created by the host.</summary>
	public long CreatedTick { get; set; }

	/// <summary>Tick the stimulus was last confirmed by the host (this tick when fresh).</summary>
	public long LastConfirmedTick { get; set; }
}

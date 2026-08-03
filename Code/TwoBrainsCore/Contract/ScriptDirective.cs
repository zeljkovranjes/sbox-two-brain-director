namespace TwoBrains.Core.Contract;

/// <summary>What a host script directive asks the policy to do.</summary>
public enum ScriptDirectiveKind
{
	/// <summary>Force pressure mode (see <see cref="PressureMode"/>), optional progression + reset.</summary>
	SetPressureMode = 0,
	/// <summary>Set pressure progression directly [0,1].</summary>
	SetProgression = 1,
	/// <summary>Full/empty reset of pressure state (count, latches, gauge).</summary>
	ResetPressure = 2,
	/// <summary>Switch the active configuration profile by name.</summary>
	SetProfile = 3,
	/// <summary>Ask macro to nominate an opportunity in a region immediately.</summary>
	ForceOpportunity = 4,
	/// <summary>Ask micro to withdraw/retreat regardless of local motivation.</summary>
	ForceWithdrawal = 5,
	/// <summary>Ask micro to run a named scripted sequence (cinematic control).</summary>
	PlayScriptedSequence = 6,
	/// <summary>Ask micro to despawn.</summary>
	Despawn = 7,
}

/// <summary>
/// One host script order. Directives are consumed once, on the tick they arrive, and are
/// recorded in telemetry as explicit overrides. Optional fields are interpreted per kind.
/// </summary>
public sealed class ScriptDirective
{
	/// <summary>Stable id so hosts can correlate with their script graph.</summary>
	public string DirectiveId { get; set; } = "";

	public ScriptDirectiveKind Kind { get; set; }

	/// <summary>SetPressureMode: desired mode.</summary>
	public PressureMode Mode { get; set; }

	/// <summary>SetPressureMode/SetProgression: progression fraction in [0,1].</summary>
	public double Progression { get; set; }

	/// <summary>SetPressureMode/ResetPressure: also reset the pressure gauge/history.</summary>
	public bool ResetGauge { get; set; }

	/// <summary>SetProfile: profile name (must exist in the host's profile catalogue).</summary>
	public string ProfileName { get; set; }

	/// <summary>ForceOpportunity: preferred region id (empty = director's choice).</summary>
	public string RegionId { get; set; } = "";

	/// <summary>PlayScriptedSequence: sequence name understood by the host.</summary>
	public string SequenceName { get; set; }
}

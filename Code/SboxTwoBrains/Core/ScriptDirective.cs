namespace SboxTwoBrains.Core;

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

namespace SboxTwoBrains.Core;

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

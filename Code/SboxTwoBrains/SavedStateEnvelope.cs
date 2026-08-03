namespace SboxTwoBrains;

/// <summary>
/// Versioned, complete save payload. Restoring this at tick n and replaying identical
/// subsequent inputs must produce byte-identical decisions. All subsystems contribute
/// their full state, including the RNG words.
/// </summary>
public sealed class SavedStateEnvelope
{
	/// <summary>Serialization schema version; current = 1.</summary>
	public int SchemaVersion { get; set; } = 1;

	/// <summary>Config schema/profile version string supplied by the host at save time.</summary>
	public string ConfigVersion { get; set; } = "";

	/// <summary>Tick index this state was captured at (next tick to run is TickIndex + 1).</summary>
	public long TickIndex { get; set; }

	/// <summary>Accumulated simulated seconds.</summary>
	public double SimTimeSeconds { get; set; }

	/// <summary>Complete macro RNG state.</summary>
	public ulong MacroRngS0 { get; set; }
	public ulong MacroRngS1 { get; set; }

	/// <summary>Complete micro RNG state.</summary>
	public ulong MicroRngS0 { get; set; }
	public ulong MicroRngS1 { get; set; }

	/// <summary>Macro subsystem state blob (owned by PressureDirector).</summary>
	public string MacroStateJson { get; set; } = "";

	/// <summary>Micro subsystem state blob (owned by MonsterAgent).</summary>
	public string MicroStateJson { get; set; } = "";
}

using System.Collections.Generic;

namespace SboxTwoBrains;

/// <summary>
/// Everything the policy decided on one tick. Identical config, seed, ticks, snapshots and
/// acknowledgements must serialize this to byte-identical canonical JSON (replay contract).
/// </summary>
public sealed class DecisionBatch
{
	public long TickIndex { get; set; }

	/// <summary>Macro output this tick, or null when the director emitted no change.</summary>
	public PressureDecision Macro { get; set; }

	/// <summary>Declarative action requests for the host to execute and later acknowledge.</summary>
	public List<ActionRequest> Actions { get; set; } = new List<ActionRequest>();

	/// <summary>Structured diagnostics in deterministic emission order.</summary>
	public List<TelemetryEvent> Telemetry { get; set; } = new List<TelemetryEvent>();

	/// <summary>
	/// FNV-1a 64-bit hash of the full internal state after this tick. Two runs with
	/// identical inputs must produce identical hashes every tick.
	/// </summary>
	public ulong StateHash { get; set; }
}

namespace TwoBrains.Core.Contract;

/// <summary>
/// One structured diagnostics record. Every macro/micro transition, override, exclusion
/// decision and acknowledgement emits one; the overlay and replay tooling consume these.
/// </summary>
public sealed class TelemetryEvent
{
	public long Tick { get; set; }

	/// <summary>Subsystem: "macro", "micro", "perception", "action", "config", "state".</summary>
	public string Category { get; set; } = "";

	/// <summary>Machine-readable reason code (e.g. "aggressive_started", "nav_failed").</summary>
	public string Code { get; set; } = "";

	/// <summary>Human-readable detail line.</summary>
	public string Message { get; set; } = "";

	public TelemetryEvent() { }

	public TelemetryEvent( long tick, string category, string code, string message )
	{
		Tick = tick;
		Category = category;
		Code = code;
		Message = message;
	}
}

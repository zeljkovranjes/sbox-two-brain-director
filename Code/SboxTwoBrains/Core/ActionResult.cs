namespace SboxTwoBrains.Core;

/// <summary>
/// Host acknowledgement delivered on a later tick. Exactly one terminal status
/// (Succeeded/PartiallySucceeded/Rejected/Interrupted/Failed) may arrive per action id;
/// Deferred may repeat before a terminal status.
/// </summary>
public sealed class ActionResult
{
	public string ActionId { get; set; } = "";
	public ActionStatus Status { get; set; }

	/// <summary>Host explanation (e.g. "no route", "animation busy"); diagnostics only.</summary>
	public string Detail { get; set; }

	/// <summary>Tick the host produced this result.</summary>
	public long ResultTick { get; set; }
}

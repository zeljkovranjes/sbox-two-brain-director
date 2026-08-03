namespace TwoBrains.Core.Contract;

/// <summary>Terminal or interim host acknowledgement of an <see cref="ActionRequest"/>.</summary>
public enum ActionStatus
{
	/// <summary>Completed fully.</summary>
	Succeeded = 0,
	/// <summary>Completed with reduced effect (host explains in Detail).</summary>
	PartiallySucceeded = 1,
	/// <summary>Host refused to start (policy must pick an alternative).</summary>
	Rejected = 2,
	/// <summary>Host postponed; still pending, not a failure.</summary>
	Deferred = 3,
	/// <summary>Started but aborted by the world (damage, target moved, etc.).</summary>
	Interrupted = 4,
	/// <summary>Started and failed (e.g. pathfinding failure).</summary>
	Failed = 5,
}

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

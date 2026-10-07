namespace SboxTwoBrains.Core;

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

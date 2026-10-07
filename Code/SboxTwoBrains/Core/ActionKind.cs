namespace SboxTwoBrains.Core;

/// <summary>Kinds of declarative actions the micro agent can request from the host.</summary>
public enum ActionKind
{
	/// <summary>Move toward <see cref="ActionRequest.Destination"/>.</summary>
	MoveTo = 0,
	/// <summary>Systematically search a region.</summary>
	Search = 1,
	/// <summary>Investigate a remembered/current stimulus.</summary>
	Investigate = 2,
	/// <summary>Shadow a region/target at range without engaging.</summary>
	Stalk = 3,
	/// <summary>Hold position in concealment waiting for opportunity.</summary>
	Ambush = 4,
	/// <summary>Threat-aware display/hesitation facing a dangerous target.</summary>
	Threat = 5,
	/// <summary>Pursue a known target at speed.</summary>
	Chase = 6,
	/// <summary>Commit to an attack on a target.</summary>
	Attack = 7,
	/// <summary>Withdraw away from threat, possibly toward offstage.</summary>
	Retreat = 8,
	/// <summary>Traverse an approved ingress point between stages.</summary>
	UseIngress = 9,
	/// <summary>Idle in place for a bounded time.</summary>
	Wait = 10,
	/// <summary>Run a host scripted sequence.</summary>
	Scripted = 11,
	/// <summary>Game-defined action; see <see cref="ActionRequest.Param"/>.</summary>
	Custom = 12,
}

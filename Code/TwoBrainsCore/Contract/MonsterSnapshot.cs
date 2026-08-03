namespace TwoBrains.Core.Contract;

/// <summary>Lifecycle of the monster itself, as reported by the host.</summary>
public enum MonsterLifecycle
{
	Alive = 0,
	Dead = 1,
	Suspended = 2,
	Despawning = 3,
}

/// <summary>Where the monster currently is, in staging terms.</summary>
public enum StagePresence
{
	Frontstage = 0,
	Offstage = 1,
	/// <summary>Currently traversing a host-approved ingress (e.g. vent) between stages.</summary>
	InIngress = 2,
}

/// <summary>Immutable host view of the monster for one tick.</summary>
public sealed class MonsterSnapshot
{
	public string MonsterId { get; set; } = "";
	public Vec3 Position { get; set; }
	public string RegionId { get; set; } = "";
	public MonsterLifecycle Lifecycle { get; set; } = MonsterLifecycle.Alive;
	public StagePresence Presence { get; set; } = StagePresence.Frontstage;

	/// <summary>Health in [0,1]; 1 = undamaged.</summary>
	public double HealthFraction { get; set; } = 1.0;

	/// <summary>Host-observed current target participant id, if any.</summary>
	public string CurrentTargetId { get; set; }

	/// <summary>Host navigation says a route to the current goal exists right now.</summary>
	public bool RouteAvailable { get; set; } = true;

	/// <summary>Action id the host is still executing (not yet acknowledged), if any.</summary>
	public string ActiveActionId { get; set; }

	/// <summary>Ingress id being traversed when <see cref="Presence"/> is InIngress.</summary>
	public string CurrentIngressId { get; set; }

	/// <summary>Tick the monster last took damage; -1 = never.</summary>
	public long LastDamageTick { get; set; } = -1;

	/// <summary>Tick the monster last stunned its controller; -1 = never.</summary>
	public long LastStunnedTick { get; set; } = -1;

	// Host-reported feasibility facts (animation/combat/movement capability this tick).
	public bool CanMove { get; set; } = true;
	public bool CanAttack { get; set; } = true;
	public bool CanTraverseIngress { get; set; } = true;
	public bool CanPlayScripted { get; set; } = true;

	/// <summary>Free-form host flags (e.g. "flamed", "aimed_at"); policy reads, never invents.</summary>
	public string[] Flags { get; set; } = System.Array.Empty<string>();
}

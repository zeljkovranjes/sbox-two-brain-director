using System.Collections.Generic;
using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Complete serializable micro state: perception memory, motivation flags, timers, gauges,
/// target state, active module, pending actions, pathfinding-failure history and
/// frontstage/offstage traversal state. SortedDictionaries keep serialization canonical.
/// </summary>
public sealed class MicroState
{
	/// <summary>Remembered stimuli (remembered evidence), capacity-bounded.</summary>
	public List<MemoryRecord> Memories { get; set; } = new List<MemoryRecord>();

	/// <summary>Currently active motivation flags (sorted, e.g. "attack", "stalk").</summary>
	public SortedSet<string> Motivations { get; set; } = new SortedSet<string>( System.StringComparer.Ordinal );

	/// <summary>Active module name; empty when idle between modules.</summary>
	public string ActiveModule { get; set; } = "";

	/// <summary>Action id currently awaited from the host; empty when none.</summary>
	public string AwaitingActionId { get; set; } = "";

	/// <summary>Outstanding requests by action id → expiry tick (sorted).</summary>
	public SortedDictionary<string, long> PendingActions { get; set; } = new SortedDictionary<string, long>( System.StringComparer.Ordinal );

	/// <summary>Bookkeeping per outstanding request: kind, original timeout interval and key params.</summary>
	public SortedDictionary<string, PendingActionMeta> PendingMeta { get; set; } = new SortedDictionary<string, PendingActionMeta>( System.StringComparer.Ordinal );

	/// <summary>Named countdown timers in seconds remaining (sorted).</summary>
	public SortedDictionary<string, double> Timers { get; set; } = new SortedDictionary<string, double>( System.StringComparer.Ordinal );

	/// <summary>Named gauges in [0,1] unless documented otherwise (sorted).</summary>
	public SortedDictionary<string, double> Gauges { get; set; } = new SortedDictionary<string, double>( System.StringComparer.Ordinal );

	/// <summary>Named monotonic counters (sorted).</summary>
	public SortedDictionary<string, long> Counters { get; set; } = new SortedDictionary<string, long>( System.StringComparer.Ordinal );

	/// <summary>Current pursuit target id; empty when none.</summary>
	public string CurrentTargetId { get; set; } = "";

	/// <summary>Tick the current target was last directly sensed (-1 = never).</summary>
	public long LastSensedTargetTick { get; set; } = -1;

	/// <summary>Last sensed position of the current target; null when unknown.</summary>
	public Vec3? LastSensedTargetPosition { get; set; }

	/// <summary>Region of the last systematic search; empty when none.</summary>
	public string LastSearchRegionId { get; set; } = "";

	/// <summary>Tick the last search episode ran (-1 = never).</summary>
	public long LastSearchTick { get; set; } = -1;

	/// <summary>Consecutive pathfinding failures (drives recovery escalation).</summary>
	public int ConsecutiveNavFailures { get; set; }

	/// <summary>Tick of the last pathfinding failure (-1 = never).</summary>
	public long LastNavFailureTick { get; set; } = -1;

	/// <summary>Investigation stage machine position (module-owned, documented per module).</summary>
	public int InvestigationStage { get; set; }

	/// <summary>Stimulus id under investigation; empty when none.</summary>
	public string InvestigationStimulusId { get; set; } = "";

	/// <summary>Ingress id currently being traversed; empty when none.</summary>
	public string ActiveIngressId { get; set; } = "";

	/// <summary>Scripted sequence name in progress; empty when none.</summary>
	public string ActiveScriptedSequence { get; set; } = "";

	/// <summary>Module-local scratch flags (sorted; names must be module-prefixed).</summary>
	public SortedSet<string> Flags { get; set; } = new SortedSet<string>( System.StringComparer.Ordinal );

	/// <summary>Last macro bias received, latched until its ExpiryTick; null when none is active.</summary>
	public PressureDecision LastMacro { get; set; }
}

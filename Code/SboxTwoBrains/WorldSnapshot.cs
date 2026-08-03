using System.Collections.Generic;

namespace SboxTwoBrains;

/// <summary>
/// The complete, immutable input for one tick. The host builds it; policy consumes it
/// read-only. Snapshots must contain everything policy needs — engine queries during
/// evaluation are forbidden by design.
/// </summary>
public sealed class WorldSnapshot
{
	/// <summary>Monotonic tick index; must match the tick the host is simulating.</summary>
	public long TickIndex { get; set; }

	/// <summary>Simulated seconds since the previous tick. Range: (0, 60].</summary>
	public double DeltaTimeSeconds { get; set; } = 1.0 / 60.0;

	/// <summary>The monster being controlled. Required.</summary>
	public MonsterSnapshot Monster { get; set; } = new MonsterSnapshot();

	/// <summary>All relevant participants (potential prey/threats).</summary>
	public List<TargetSnapshot> Targets { get; set; } = new List<TargetSnapshot>();

	/// <summary>Stimuli sensed on this tick (current evidence).</summary>
	public List<Stimulus> CurrentStimuli { get; set; } = new List<Stimulus>();

	/// <summary>Navigation options with host reachability/distance/visibility facts.</summary>
	public List<NavCandidate> NavCandidates { get; set; } = new List<NavCandidate>();

	/// <summary>Approved offstage areas and their topology.</summary>
	public List<OffstageRegion> OffstageRegions { get; set; } = new List<OffstageRegion>();

	/// <summary>Approved ingress points between stages.</summary>
	public List<IngressPoint> IngressPoints { get; set; } = new List<IngressPoint>();

	/// <summary>Active spatial exclusion zones.</summary>
	public List<ExclusionZone> ExclusionZones { get; set; } = new List<ExclusionZone>();

	/// <summary>Script orders issued for this tick (consumed once).</summary>
	public List<ScriptDirective> Directives { get; set; } = new List<ScriptDirective>();

	/// <summary>Host acknowledgements of earlier action requests.</summary>
	public List<ActionResult> Acknowledgements { get; set; } = new List<ActionResult>();

	/// <summary>
	/// Explicit omniscience switch. When false (default) the micro layer must decide from
	/// its own perception memory only. When true the host grants perfect target knowledge;
	/// always visible in telemetry. Game policy choice, never silently enabled.
	/// </summary>
	public bool OmniscientTargets { get; set; }
}

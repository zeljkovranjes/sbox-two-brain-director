using System.Collections.Generic;
using Sandbox;
using SboxTwoBrains;

namespace SboxTwoBrains.Host;

/// <summary>
/// A navigation node the two-brain core may path through. Drop these around the level;
/// <see cref="TwoBrainsComponent"/> reports every node within its nav radius as a
/// <see cref="NavCandidate"/> each tick. The node id is the GameObject name.
/// </summary>
[Title( "Two-Brain Nav Node" )]
[Category( "AI" )]
public sealed class TwoBrainsNavNode : Component
{
	/// <summary>Region this node belongs to (free-form, matches PressureDecision regions).</summary>
	[Property] public string RegionId { get; set; } = "";

	/// <summary>Host routing fact: a path from the monster to this node exists right now.</summary>
	[Property] public bool Reachable { get; set; } = true;

	/// <summary>Frontstage node, offstage node, or ingress marker.</summary>
	[Property] public NavCandidateKind Kind { get; set; } = NavCandidateKind.FrontstageNode;
}

/// <summary>
/// An approved stage-transition point (vent, door, tunnel) between frontstage and offstage.
/// The ingress id defaults to the GameObject name when <see cref="IngressId"/> is empty.
/// </summary>
[Title( "Two-Brain Ingress" )]
[Category( "AI" )]
public sealed class TwoBrainsIngress : Component
{
	/// <summary>Stable ingress id; empty = use the GameObject name.</summary>
	[Property] public string IngressId { get; set; } = "";

	/// <summary>Traversal kind reported to the core.</summary>
	[Property] public IngressKind Kind { get; set; } = IngressKind.Vent;

	/// <summary>Frontstage region this point serves.</summary>
	[Property] public string RegionId { get; set; } = "";

	/// <summary>Offstage node id (a <see cref="TwoBrainsNavNode"/> GameObject name) this connects to.</summary>
	[Property] public string OffstageNodeId { get; set; } = "";

	/// <summary>
	/// Frontstage node id (a <see cref="TwoBrainsNavNode"/> GameObject name) on the arena side
	/// of this ingress. Traversal lands the monster on the OPPOSITE side's node: going
	/// backstage→frontstage lands here, frontstage→backstage lands at <see cref="OffstageNodeId"/>.
	/// Nav nodes are guaranteed navmesh-adjacent, which is why traversal lands on them rather
	/// than on the ingress opening itself.
	/// </summary>
	[Property] public string FrontstageNodeId { get; set; } = "";

	/// <summary>Seconds before this ingress can be used again after a traversal. 0 = no cooldown.</summary>
	[Property] public float CooldownSeconds { get; set; } = 0.0f;
}

/// <summary>
/// Declares an offstage (non-visible) region the monster can occupy for staging and sweeps,
/// with the node/ingress ids the host approves for it and the frontstage regions it touches.
/// </summary>
[Title( "Two-Brain Offstage Region" )]
[Category( "AI" )]
public sealed class TwoBrainsOffstageRegion : Component
{
	/// <summary>Region id; empty = use the GameObject name.</summary>
	[Property] public string RegionId { get; set; } = "";

	/// <summary>Nav node ids (GameObject names of <see cref="TwoBrainsNavNode"/>s) inside this region.</summary>
	[Property] public List<string> NodeIds { get; set; } = new List<string>();

	/// <summary>Ingress ids leading into this region.</summary>
	[Property] public List<string> IngressIds { get; set; } = new List<string>();

	/// <summary>Frontstage region ids this offstage region is adjacent to.</summary>
	[Property] public List<string> AdjacentRegionIds { get; set; } = new List<string>();
}

/// <summary>
/// A spherical exclusion zone (centred on this GameObject) the macro director must respect
/// when choosing candidates and staging — e.g. a safe room or an active objective.
/// </summary>
[Title( "Two-Brain Exclusion Zone" )]
[Category( "AI" )]
public sealed class TwoBrainsExclusionZone : Component
{
	/// <summary>Zone id; empty = use the GameObject name.</summary>
	[Property] public string ZoneId { get; set; } = "";

	/// <summary>What this zone suppresses (target-vicinity, objective-vicinity, or custom).</summary>
	[Property] public ExclusionKind Kind { get; set; } = ExclusionKind.Target;

	/// <summary>Radius in metres (the core's distance unit; converted from this GameObject's position).</summary>
	[Property] public float Radius { get; set; } = 10.0f;

	/// <summary>Inactive zones are reported but ignored by the core. (Named ZoneActive so it does not hide Component.Active.)</summary>
	[Property] public bool ZoneActive { get; set; } = true;
}

/// <summary>
/// Marks a participant the monster may hunt/fear (players, NPCs). Every active instance is
/// reported to the core as a <see cref="TargetSnapshot"/> each tick; the target id is the
/// GameObject name.
/// </summary>
[Title( "Two-Brain Target" )]
[Category( "AI" )]
public sealed class TwoBrainsTarget : Component
{
	/// <summary>Host threat rating in [0,1]: 0 = harmless prey, 1 = lethal threat.</summary>
	[Property] public float ThreatRating { get; set; } = 0.0f;

	/// <summary>Carries a weapon that can hurt the monster.</summary>
	[Property] public bool IsArmed { get; set; }

	/// <summary>Concealed from normal senses (e.g. hiding in a locker).</summary>
	[Property] public bool IsHiding { get; set; }

	/// <summary>Whether the pressure director may target this participant.</summary>
	[Property] public bool PressureEligible { get; set; } = true;

	/// <summary>Region containing this target; empty = derive from the nearest nav node.</summary>
	[Property] public string RegionId { get; set; } = "";

	/// <summary>Objective this participant is currently progressing, if any.</summary>
	[Property] public string ObjectiveId { get; set; } = "";

	/// <summary>Objective progress in [0,1] (drives exclusion/pressure eligibility).</summary>
	[Property] public float ObjectiveProgress { get; set; } = 0.0f;
}

using System.Collections.Generic;
using Sandbox;
using SboxTwoBrains.Core;

namespace SboxTwoBrains.Components;

/// <summary>
/// An approved stage-transition point (vent, door, tunnel) between frontstage and offstage.
/// The ingress id defaults to the GameObject name when <see cref="IngressId"/> is empty.
/// </summary>
[Alias( "SboxTwoBrains.Host.TwoBrainsIngress" )]
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

using System.Collections.Generic;
using Sandbox;
using SboxTwoBrains.Core;

namespace SboxTwoBrains.Components;

/// <summary>
/// A navigation node the two-brain core may path through. Drop these around the level;
/// <see cref="TwoBrainsComponent"/> reports every node within its nav radius as a
/// <see cref="NavCandidate"/> each tick. The node id is the GameObject name.
/// </summary>
[Alias( "SboxTwoBrains.Host.TwoBrainsNavNode" )]
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

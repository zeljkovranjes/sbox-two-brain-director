using System.Collections.Generic;
using Sandbox;
using SboxTwoBrains.Core;

namespace SboxTwoBrains.Components;

/// <summary>
/// Declares an offstage (non-visible) region the monster can occupy for staging and sweeps,
/// with the node/ingress ids the host approves for it and the frontstage regions it touches.
/// </summary>
[Alias( "SboxTwoBrains.Host.TwoBrainsOffstageRegion" )]
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

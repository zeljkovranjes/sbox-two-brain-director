using System.Collections.Generic;
using Sandbox;
using SboxTwoBrains.Core;

namespace SboxTwoBrains.Components;

/// <summary>
/// A spherical exclusion zone (centred on this GameObject) the macro director must respect
/// when choosing candidates and staging — e.g. a safe room or an active objective.
/// </summary>
[Alias( "SboxTwoBrains.Host.TwoBrainsExclusionZone" )]
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

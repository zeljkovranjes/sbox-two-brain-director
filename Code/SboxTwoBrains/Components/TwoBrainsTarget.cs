using System.Collections.Generic;
using Sandbox;
using SboxTwoBrains.Core;

namespace SboxTwoBrains.Components;

/// <summary>
/// Marks a participant the monster may hunt/fear (players, NPCs). Every active instance is
/// reported to the core as a <see cref="TargetSnapshot"/> each tick; the target id is the
/// GameObject name.
/// </summary>
[Alias( "SboxTwoBrains.Host.TwoBrainsTarget" )]
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

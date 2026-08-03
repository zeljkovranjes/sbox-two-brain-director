namespace SboxTwoBrains;

/// <summary>Immutable host view of one participant (potential prey/threat) for one tick.</summary>
public sealed class TargetSnapshot
{
	public string TargetId { get; set; } = "";
	public bool IsValid { get; set; } = true;
	public bool IsAlive { get; set; } = true;
	public Vec3 Position { get; set; }
	public string RegionId { get; set; } = "";

	/// <summary>Host line-of-sight fact: the monster can currently see this target.</summary>
	public bool IsVisible { get; set; }

	/// <summary>Health in [0,1]; 1 = undamaged.</summary>
	public double HealthFraction { get; set; } = 1.0;

	/// <summary>Carries a weapon that can hurt the monster.</summary>
	public bool IsArmed { get; set; }

	/// <summary>Currently aiming a weapon at the monster (threat-aware input).</summary>
	public bool IsAimingAtMonster { get; set; }

	/// <summary>Exposed to a damage-over-time deterrent (e.g. flame) this tick.</summary>
	public bool IsUsingDeterrent { get; set; }

	/// <summary>Concealed from normal senses (e.g. hiding in a locker).</summary>
	public bool IsHiding { get; set; }

	/// <summary>Host threat rating in [0,1]; 0 = harmless prey, 1 = lethal threat.</summary>
	public double ThreatRating { get; set; }

	/// <summary>Current objective the participant is progressing, if any.</summary>
	public string ObjectiveId { get; set; }

	/// <summary>Objective progress in [0,1] (drives exclusion/pressure eligibility).</summary>
	public double ObjectiveProgress { get; set; }

	/// <summary>Ambient light level at the target in [0,1] (Light channel input).</summary>
	public double LightLevel { get; set; }

	/// <summary>True when this participant is eligible for pressure (e.g. not in a safe room).</summary>
	public bool PressureEligible { get; set; } = true;
}

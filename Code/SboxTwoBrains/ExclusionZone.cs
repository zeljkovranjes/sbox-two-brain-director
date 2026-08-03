namespace SboxTwoBrains;

/// <summary>What an exclusion zone suppresses.</summary>
public enum ExclusionKind
{
	/// <summary>Pressure/staging may not centre within this zone around a target.</summary>
	Target = 0,
	/// <summary>Pressure/staging may not centre within this zone around an objective.</summary>
	Objective = 1,
	Custom = 2,
}

/// <summary>
/// A spherical host-authored exclusion zone (metres) the macro layer must respect when
/// choosing candidate regions and staging. Mirrors the research's near-target /
/// near-objective exclusion radii as explicit world data instead of hidden rules.
/// </summary>
public sealed class ExclusionZone
{
	public string ZoneId { get; set; } = "";
	public ExclusionKind Kind { get; set; } = ExclusionKind.Target;
	public Vec3 Center { get; set; }

	/// <summary>Radius in metres; &gt; 0.</summary>
	public double Radius { get; set; } = 10.0;

	/// <summary>Inactive zones are ignored (kept in snapshots for save/replay stability).</summary>
	public bool Active { get; set; } = true;
}

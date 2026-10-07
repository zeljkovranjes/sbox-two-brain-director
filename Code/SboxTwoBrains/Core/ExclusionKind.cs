namespace SboxTwoBrains.Core;

/// <summary>What an exclusion zone suppresses.</summary>
public enum ExclusionKind
{
	/// <summary>Pressure/staging may not centre within this zone around a target.</summary>
	Target = 0,
	/// <summary>Pressure/staging may not centre within this zone around an objective.</summary>
	Objective = 1,
	Custom = 2,
}

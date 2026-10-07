using Sandbox;
using SboxTwoBrains.Core;

namespace SboxTwoBrains.Engine;

/// <summary>
/// Unit conversion between s&amp;box world units and the core's metres. s&amp;box uses
/// Source-style units (1 unit = 1 inch); the deterministic core is specified in metres, and
/// every profile distance (attack range, sweep radius, exclusion radius) assumes metres.
/// </summary>
public static class SandboxVec
{
	/// <summary>s&amp;box units per metre (1 unit = 0.0254 m).</summary>
	public const float UnitsPerMetre = 39.3701f;

	/// <summary>Metres per s&amp;box unit.</summary>
	public const float MetresPerUnit = 0.0254f;

	/// <summary>s&amp;box Vector3 (units) → core Vec3 (metres).</summary>
	public static Vec3 ToCore( Vector3 v )
	{
		return new Vec3( v.x * MetresPerUnit, v.y * MetresPerUnit, v.z * MetresPerUnit );
	}

	/// <summary>Core Vec3 (metres) → s&amp;box Vector3 (units).</summary>
	public static Vector3 ToSbox( Vec3 v )
	{
		return new Vector3( (float)(v.X * UnitsPerMetre), (float)(v.Y * UnitsPerMetre), (float)(v.Z * UnitsPerMetre) );
	}

	/// <summary>s&amp;box units → metres.</summary>
	public static double ToCoreDistance( float units ) => units * MetresPerUnit;

	/// <summary>Metres → s&amp;box units.</summary>
	public static float ToSboxDistance( double metres ) => (float)(metres * UnitsPerMetre);
}

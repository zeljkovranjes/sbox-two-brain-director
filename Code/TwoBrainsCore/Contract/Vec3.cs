using System;

namespace TwoBrains.Core.Contract;

/// <summary>
/// Engine-independent 3D vector in double precision (units: metres).
/// The core never references engine vector types; hosts convert at the boundary.
/// Only + − * / and <see cref="Math.Sqrt"/> are used anywhere in the core (determinism policy).
/// </summary>
public readonly struct Vec3 : IEquatable<Vec3>
{
	public double X { get; }
	public double Y { get; }
	public double Z { get; }

	public Vec3( double x, double y, double z )
	{
		X = x;
		Y = y;
		Z = z;
	}

	public static readonly Vec3 Zero = new Vec3( 0.0, 0.0, 0.0 );

	public static Vec3 operator +( Vec3 a, Vec3 b ) => new Vec3( a.X + b.X, a.Y + b.Y, a.Z + b.Z );
	public static Vec3 operator -( Vec3 a, Vec3 b ) => new Vec3( a.X - b.X, a.Y - b.Y, a.Z - b.Z );
	public static Vec3 operator *( Vec3 a, double s ) => new Vec3( a.X * s, a.Y * s, a.Z * s );

	public double LengthSquared() => X * X + Y * Y + Z * Z;
	public double Length() => Math.Sqrt( LengthSquared() );

	public double DistanceTo( Vec3 other ) => (this - other).Length();
	public double DistanceSquaredTo( Vec3 other ) => (this - other).LengthSquared();

	/// <summary>Horizontal (plan-view) distance; vertical axis ignored. Units: metres.</summary>
	public double PlanarDistanceTo( Vec3 other )
	{
		double dx = X - other.X;
		double dz = Z - other.Z;
		return Math.Sqrt( dx * dx + dz * dz );
	}

	public bool Equals( Vec3 other ) => X.Equals( other.X ) && Y.Equals( other.Y ) && Z.Equals( other.Z );
	public override bool Equals( object obj ) => obj is Vec3 other && Equals( other );
	public override int GetHashCode() => HashCode.Combine( X, Y, Z );
	public override string ToString() => string.Format( System.Globalization.CultureInfo.InvariantCulture, "({0:R}, {1:R}, {2:R})", X, Y, Z );
}

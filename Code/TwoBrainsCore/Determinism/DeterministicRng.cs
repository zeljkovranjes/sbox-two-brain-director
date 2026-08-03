namespace TwoBrains.Core.Determinism;

/// <summary>
/// Seedable, fully serializable RNG (xorshift128+). Two 64-bit state words are the complete
/// state: <see cref="GetState"/>/<see cref="SetState"/> save and restore it exactly.
/// Never use <see cref="System.Random"/> inside policy code.
/// </summary>
public sealed class DeterministicRng
{
	private ulong _s0;
	private ulong _s1;

	/// <summary>Seeds from a single 64-bit value via splitmix64 expansion.</summary>
	public DeterministicRng( ulong seed )
	{
		ulong z = seed;
		_s0 = SplitMix64( ref z );
		_s1 = SplitMix64( ref z );
		if ( _s0 == 0UL && _s1 == 0UL )
			_s1 = 0x9E3779B97F4A7C15UL; // xorshift state must not be all zero
	}

	private DeterministicRng( ulong s0, ulong s1, bool _ )
	{
		_s0 = s0;
		_s1 = s1;
	}

	private static ulong SplitMix64( ref ulong z )
	{
		z += 0x9E3779B97F4A7C15UL;
		ulong r = z;
		r = (r ^ (r >> 30)) * 0xBF58476D1CE4E5B9UL;
		r = (r ^ (r >> 27)) * 0x94D049BB133111EBUL;
		return r ^ (r >> 31);
	}

	/// <summary>Next unsigned 64-bit value; advances state.</summary>
	public ulong NextUInt64()
	{
		ulong x = _s0;
		ulong y = _s1;
		_s0 = y;
		x ^= x << 23;
		_s1 = x ^ y ^ (x >> 18) ^ (y >> 5);
		return _s1 + y;
	}

	/// <summary>Uniform double in [0, 1) using the top 53 bits.</summary>
	public double NextDouble()
	{
		return (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);
	}

	/// <summary>Uniform double in [min, max). max &lt;= min returns min.</summary>
	public double NextRange( double min, double max )
	{
		if ( max <= min ) return min;
		return min + (max - min) * NextDouble();
	}

	/// <summary>Uniform int in [0, maxExclusive). maxExclusive &lt;= 0 returns 0.
	/// Modulo reduction; bias is negligible for policy use and fully deterministic.</summary>
	public int NextInt( int maxExclusive )
	{
		if ( maxExclusive <= 0 ) return 0;
		return (int)(NextUInt64() % (ulong)maxExclusive);
	}

	/// <summary>Uniform int in [minInclusive, maxExclusive). Empty range returns minInclusive.</summary>
	public int NextInt( int minInclusive, int maxExclusive )
	{
		if ( maxExclusive <= minInclusive ) return minInclusive;
		return minInclusive + NextInt( maxExclusive - minInclusive );
	}

	/// <summary>True with probability <paramref name="probability"/> (clamped to [0,1]).</summary>
	public bool NextChance( double probability )
	{
		if ( probability <= 0.0 ) return false;
		if ( probability >= 1.0 ) return true;
		return NextDouble() < probability;
	}

	/// <summary>Complete RNG state; two words are sufficient to resume exactly.</summary>
	public (ulong S0, ulong S1) GetState() => (_s0, _s1);

	/// <summary>Restores a state previously returned by <see cref="GetState"/>.</summary>
	public void SetState( ulong s0, ulong s1 )
	{
		if ( s0 == 0UL && s1 == 0UL )
			s1 = 0x9E3779B97F4A7C15UL;
		_s0 = s0;
		_s1 = s1;
	}

	/// <summary>Creates an independent stream (e.g. macro vs micro) derived from a master seed.</summary>
	public static DeterministicRng Fork( ulong masterSeed, ulong streamId )
	{
		return new DeterministicRng( masterSeed ^ (streamId * 0x9E3779B97F4A7C15UL + 0x165667B19E3779F9UL) );
	}
}

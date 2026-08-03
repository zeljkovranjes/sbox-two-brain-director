using System;

namespace SboxTwoBrains;

/// <summary>
/// Explicit, monotonic time for one tick. The host supplies both fields; policy code never
/// reads wall-clock time. <see cref="DeltaTimeSeconds"/> must be finite and &gt; 0.
/// </summary>
public readonly struct TickContext
{
	/// <summary>Monotonic tick index (0-based, strictly increasing by 1 per tick).</summary>
	public long TickIndex { get; }

	/// <summary>Simulated seconds advanced by this tick. Units: seconds. Range: (0, 60].</summary>
	public double DeltaTimeSeconds { get; }

	public TickContext( long tickIndex, double deltaTimeSeconds )
	{
		if ( tickIndex < 0 )
			throw new ArgumentOutOfRangeException( nameof( tickIndex ), "Tick index must be >= 0." );
		if ( double.IsNaN( deltaTimeSeconds ) || double.IsInfinity( deltaTimeSeconds ) || deltaTimeSeconds <= 0.0 || deltaTimeSeconds > 60.0 )
			throw new ArgumentOutOfRangeException( nameof( deltaTimeSeconds ), "Delta time must be finite and in (0, 60] seconds." );
		TickIndex = tickIndex;
		DeltaTimeSeconds = deltaTimeSeconds;
	}
}

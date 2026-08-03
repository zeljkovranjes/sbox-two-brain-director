using System.Collections.Generic;
using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Macro;

/// <summary>Event-quota rolls, resets and determinism (research row 7).</summary>
public sealed class QuotaTests
{
	[Fact]
	public void EventQuota_RollsTarget_AndResetsCounters_WhenReached()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.StartProgression = 1.0;
		cfg.Pressure.MaxOpportunities = 10;
		cfg.Pressure.EventQuotaMin = 2;
		cfg.Pressure.EventQuotaMax = 2; // target is always exactly 2
		var director = new PressureDirector( new DeterministicRng( 31 ) );
		var telemetry = new List<TelemetryEvent>();

		// Cycle: offer at T2, complete T3 (progress 1), offer T4, complete T5 (progress 2 → event).
		var decisions = MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ).Target( "t", "a", 10.0, 0.0 ), 0, 7, telemetry );

		Assert.Equal( "opportunity_completed", decisions[3].ReasonCode );
		Assert.Equal( "opportunity_completed", decisions[5].ReasonCode );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "quota_event" ) );
		Assert.Equal( 5, telemetry.Find( e => e.Code == "quota_event" ).Tick );

		// After the quota event all counters reset and the cooldown is waived.
		var s = director.State;
		Assert.Equal( 0, s.CompletedOpportunities );
		Assert.Equal( 0, s.EventQuotaProgress );
		Assert.Equal( 2, s.EventQuotaTarget ); // re-rolled from [2,2]
		Assert.Equal( 0.0, s.CooldownRemaining );

		// The next cycle starts immediately (count no longer blocked).
		Assert.Equal( "mode_aggressive_start", decisions[6].ReasonCode );
		Assert.Equal( "op6-0", decisions[6].OpportunityId );
	}

	[Fact]
	public void EventQuota_TargetClampedToAtLeastOne()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.StartProgression = 1.0;
		cfg.Pressure.MaxOpportunities = 10;
		cfg.Pressure.EventQuotaMin = 0; // may roll 0 → clamped to 1
		cfg.Pressure.EventQuotaMax = 1;
		var director = new PressureDirector( new DeterministicRng( 32 ) );
		var telemetry = new List<TelemetryEvent>();

		// Every completion reaches a target of 1 → quota event every time.
		MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ).Target( "t", "a", 10.0, 0.0 ), 0, 10, telemetry );
		Assert.Equal( 4, MacroDrive.CountCode( telemetry, "quota_event" ) );
		Assert.Equal( 0, director.State.CompletedOpportunities );
		Assert.Equal( 0, director.State.EventQuotaProgress );
		Assert.Equal( 1, director.State.EventQuotaTarget );
	}

	[Fact]
	public void EventQuota_RollsAreDeterministicPerSeed()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.StartProgression = 1.0;
		cfg.Pressure.MaxOpportunities = 10;
		cfg.Pressure.EventQuotaMin = 1;
		cfg.Pressure.EventQuotaMax = 3;

		var traceA = RunQuotaTrace( cfg, 33 );
		var traceB = RunQuotaTrace( cfg, 33 );
		Assert.Equal( traceA, traceB );
		Assert.Contains( traceA, line => line.StartsWith( "event@" ) );

		// Different seed → same invariants, rolled target always inside [1,3].
		var traceC = RunQuotaTrace( cfg, 34 );
		foreach ( var line in traceC )
		{
			if ( line.StartsWith( "target=" ) )
			{
				int target = int.Parse( line.Substring( "target=".Length ), System.Globalization.CultureInfo.InvariantCulture );
				if ( target != 0 ) // 0 = not yet rolled (quota arms lazily at first completion)
					Assert.InRange( target, 1, 3 );
			}
		}
	}

	private static List<string> RunQuotaTrace( SboxTwoBrains.EffectiveConfig cfg, ulong seed )
	{
		var director = new PressureDirector( new DeterministicRng( seed ) );
		var telemetry = new List<TelemetryEvent>();
		var trace = new List<string>();
		for ( long tick = 0; tick < 40; tick++ )
		{
			MacroDrive.TickOne( director, cfg, WorldBuilder.At( tick, 0.25 ).Target( "t", "a", 10.0, 0.0 ), telemetry );
			foreach ( var e in telemetry )
			{
				if ( e.Tick == tick && e.Code == "quota_event" )
					trace.Add( "event@" + tick );
			}
			trace.Add( "target=" + director.State.EventQuotaTarget );
		}
		return trace;
	}

	[Fact]
	public void EventQuota_Disabled_WhenMaxIsZero()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.StartProgression = 1.0;
		cfg.Pressure.MaxOpportunities = 10;
		cfg.Pressure.EventQuotaMin = 0;
		cfg.Pressure.EventQuotaMax = 0;
		var director = new PressureDirector( new DeterministicRng( 35 ) );
		var telemetry = new List<TelemetryEvent>();

		MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ).Target( "t", "a", 10.0, 0.0 ), 0, 20, telemetry );

		Assert.Equal( 0, MacroDrive.CountCode( telemetry, "quota_event" ) );
		Assert.Equal( 0, director.State.EventQuotaTarget );
		Assert.Equal( 0, director.State.EventQuotaProgress );
		Assert.True( director.State.CompletedOpportunities >= 4 );
	}
}

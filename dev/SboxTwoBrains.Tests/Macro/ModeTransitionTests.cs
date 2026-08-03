using System.Collections.Generic;
using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Macro;

/// <summary>Normal↔Aggressive state machine, cooldown, quota block and decision shape.</summary>
public sealed class ModeTransitionTests
{
	[Fact]
	public void ThresholdCrossing_EmitsAggressiveStartAndOffer_ExactlyOnce()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 21 ) );
		var telemetry = new List<TelemetryEvent>();

		var decisions = MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ).Target( "t", "atrium", 10.0, 0.0 ), 0, 5, telemetry );

		Assert.Null( decisions[0] );
		Assert.Null( decisions[1] );
		Assert.Null( decisions[4] );

		var offer = decisions[2];
		Assert.NotNull( offer );
		Assert.Equal( "mode_aggressive_start", offer.ReasonCode );
		Assert.Equal( "op2-0", offer.OpportunityId );
		Assert.Equal( 22, offer.ExpiryTick ); // 2 + ceil(5 / 0.25)
		Assert.Equal( PressureMode.Aggressive, offer.Mode );
		Assert.Equal( 1.0, offer.Urgency );
		Assert.Equal( 0.578125, offer.Progression );
		Assert.Equal( "atrium", offer.CandidateRegionId );
		Assert.Equal( new string[] { "stalker", "ambusher", "sweeper" }, offer.AllowedRoles );
		Assert.Equal( 2, offer.Evidence.Length );
		Assert.Contains( "fill=", offer.Evidence[0] );
		Assert.Contains( "candidate=atrium", offer.Evidence[0] );

		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "mode_aggressive_start" ) );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "opportunity_offered" ) );

		// Sweep 0.25 ends on the next tick: completion, count++, back to Normal.
		var completion = decisions[3];
		Assert.NotNull( completion );
		Assert.Equal( "opportunity_completed", completion.ReasonCode );
		Assert.Equal( "", completion.OpportunityId );
		Assert.Equal( PressureMode.Normal, completion.Mode );
		Assert.Equal( new string[] { "stalker" }, completion.AllowedRoles );
		Assert.Equal( 0.0, completion.Urgency ); // progression reset to start (0)

		var s = director.State;
		Assert.Equal( 1, s.CompletedOpportunities );
		Assert.Equal( 0.25, s.Progression ); // refilled on tick 4 after the cooldown aged out
		Assert.Equal( 0.0, s.CooldownRemaining );
		Assert.Equal( 0.0, s.SweepSecondsRemaining );
		Assert.Equal( 3, s.LastTransitionTick );
	}

	[Fact]
	public void FullCycle_RepeatsDeterministically()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 22 ) );
		var telemetry = new List<TelemetryEvent>();

		var decisions = MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ).Target( "t", "atrium", 10.0, 0.0 ), 0, 8, telemetry );

		Assert.Equal( "mode_aggressive_start", decisions[2].ReasonCode );
		Assert.Equal( "opportunity_completed", decisions[3].ReasonCode );
		Assert.Equal( "mode_aggressive_start", decisions[6].ReasonCode );
		Assert.Equal( "opportunity_completed", decisions[7].ReasonCode );
		Assert.Equal( "op2-0", decisions[2].OpportunityId );
		Assert.Equal( "op6-1", decisions[6].OpportunityId ); // count before second completion is 1
		Assert.Equal( 26, decisions[6].ExpiryTick );
		Assert.Equal( 2, director.State.CompletedOpportunities );
	}

	[Fact]
	public void Cooldown_BlocksTransition_UntilAged()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.StartProgression = 1.0;
		cfg.Pressure.CooldownSeconds = 0.5;
		var director = new PressureDirector( new DeterministicRng( 23 ) );
		var telemetry = new List<TelemetryEvent>();

		MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ).Target( "t", "a", 10.0, 0.0 ), 0, 4, telemetry );
		Assert.Equal( 1, director.State.CompletedOpportunities );
		Assert.Equal( 1.0, director.State.Progression );

		// Tick 4: cooldown 0.5 → 0.25; progression is at threshold but blocked.
		var blocked = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 4, 0.25 ).Target( "t", "a", 10.0, 0.0 ), telemetry );
		Assert.Null( blocked );
		Assert.Equal( PressureMode.Normal, director.State.Mode );
		Assert.Equal( 0, MacroDrive.CountCode( telemetry, "quota_blocked" ) );

		// Tick 5: cooldown reaches 0; transition fires.
		var offer = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 5, 0.25 ).Target( "t", "a", 10.0, 0.0 ), telemetry );
		Assert.NotNull( offer );
		Assert.Equal( "mode_aggressive_start", offer.ReasonCode );
		Assert.Equal( "op5-1", offer.OpportunityId );
	}

	[Fact]
	public void MaxOpportunities_BlocksNewCycles_WithQuotaTelemetry()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.StartProgression = 1.0;
		cfg.Pressure.MaxOpportunities = 1;
		var director = new PressureDirector( new DeterministicRng( 24 ) );
		var telemetry = new List<TelemetryEvent>();

		// Ticks 0-3: first cycle completes (count becomes 1 = max).
		MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ).Target( "t", "a", 10.0, 0.0 ), 0, 4, telemetry );
		Assert.Equal( 1, director.State.CompletedOpportunities );

		// Ticks 4-6: gauge at threshold but quota reached → one quota_blocked per tick, no offer.
		var decisions = MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ).Target( "t", "a", 10.0, 0.0 ), 4, 3, telemetry );
		Assert.All( decisions, d => Assert.Null( d ) );
		Assert.Equal( 3, MacroDrive.CountCode( telemetry, "quota_blocked" ) );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "opportunity_offered" ) );
		Assert.Equal( PressureMode.Normal, director.State.Mode );
	}

	[Fact]
	public void Latch_RollsIngressAttractWindow_InConfiguredRange()
	{
		var cfg = MacroConfigs.Fast(); // attract 1..2 seconds
		var director = new PressureDirector( new DeterministicRng( 25 ) );
		var telemetry = new List<TelemetryEvent>();

		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 0.25 ).Target( "t", "a", 10.0, 0.0 ), telemetry );

		Assert.True( director.State.CandidateLatched );
		Assert.InRange( director.State.IngressAttractRemaining, 1.0, 2.0 );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "candidate_latched" ) );
	}

	[Fact]
	public void QuietTick_ReturnsNull_AndEmitsNoTelemetry()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 26 ) );
		var telemetry = new List<TelemetryEvent>();

		var decisions = MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ), 0, 4, telemetry );
		Assert.All( decisions, d => Assert.Null( d ) );
		Assert.Empty( telemetry );
		Assert.Equal( PressureMode.Normal, director.State.Mode );
	}
}

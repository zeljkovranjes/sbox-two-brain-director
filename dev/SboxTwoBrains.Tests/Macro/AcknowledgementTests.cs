using System.Collections.Generic;
using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Macro;

/// <summary>Host opportunity acknowledgements: success, rejection, deferral, expiry, unknown ids.</summary>
public sealed class AcknowledgementTests
{
	/// <summary>Long sweep so acks (not the sweep timer) drive outcomes; offer is op2-0 expiring at 22.</summary>
	private static EffectiveConfig AckConfig()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.SweepDurationSeconds = 50.0;
		return cfg;
	}

	private static PressureDirector OfferedDirector( EffectiveConfig cfg, List<TelemetryEvent> telemetry )
	{
		var director = new PressureDirector( new DeterministicRng( 61 ) );
		MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ).Target( "t", "a", 10.0, 0.0 ), 0, 3, telemetry );
		Assert.Equal( "op2-0", director.State.PendingOpportunityId );
		return director;
	}

	[Fact]
	public void Succeeded_CompletesOpportunity_AndIncrementsCount()
	{
		var cfg = AckConfig();
		var telemetry = new List<TelemetryEvent>();
		var director = OfferedDirector( cfg, telemetry );

		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 3, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Ack( "op2-0", ActionStatus.Succeeded ), telemetry );

		var s = director.State;
		Assert.Equal( 1, s.CompletedOpportunities );
		Assert.Equal( PressureMode.Normal, s.Mode );
		// The ack lands before the macro tick: the 0.25s cooldown it sets is aged to 0
		// by that same tick, and the gauge then refills one step.
		Assert.Equal( 0.0, s.CooldownRemaining );
		Assert.Equal( 0.25, s.Progression );
		Assert.Equal( 0.0, s.SweepSecondsRemaining ); // sweep window closed by the ack
		Assert.Equal( "", s.PendingOpportunityId );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "opportunity_completed" ) );
		Assert.NotNull( decision );
		Assert.Equal( "opportunity_completed", decision.ReasonCode );
	}

	[Fact]
	public void PartiallySucceeded_CompletesOpportunity()
	{
		var cfg = AckConfig();
		var telemetry = new List<TelemetryEvent>();
		var director = OfferedDirector( cfg, telemetry );

		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 3, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Ack( "op2-0", ActionStatus.PartiallySucceeded ), telemetry );

		Assert.Equal( 1, director.State.CompletedOpportunities );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "opportunity_completed" ) );
	}

	[Fact]
	public void Rejected_NoCount_LatchCleared_SuppressesRelatchForOneTick()
	{
		var cfg = AckConfig();
		var telemetry = new List<TelemetryEvent>();
		var director = OfferedDirector( cfg, telemetry );

		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 3, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Ack( "op2-0", ActionStatus.Rejected, "host busy" ), telemetry );

		var s = director.State;
		Assert.Equal( 0, s.CompletedOpportunities );
		Assert.Equal( PressureMode.Normal, s.Mode );
		Assert.False( s.CandidateLatched );
		Assert.Equal( "", s.ActiveCandidateId );
		Assert.Equal( 0.0, s.Progression );
		Assert.Equal( 0.0, s.CooldownRemaining ); // set to 0.25 by the ack, aged to 0 by the same tick
		Assert.Equal( "", s.PendingOpportunityId );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "opportunity_rejected" ) );
		Assert.NotNull( decision );
		Assert.Equal( "opportunity_rejected", decision.ReasonCode );
		Assert.Equal( "", decision.CandidateRegionId );

		// Next tick the region may latch again (cooldown still blocks a new offer).
		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 4, 0.25 ).Target( "t", "a", 10.0, 0.0 ), telemetry );
		Assert.True( director.State.CandidateLatched );
		Assert.Equal( "a", director.State.ActiveCandidateId );
		Assert.Equal( 2, MacroDrive.CountCode( telemetry, "candidate_latched" ) ); // initial latch + re-latch
	}

	[Fact]
	public void Interrupted_TreatedAsRejection_WithNotedMessage()
	{
		var cfg = AckConfig();
		var telemetry = new List<TelemetryEvent>();
		var director = OfferedDirector( cfg, telemetry );

		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 3, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Ack( "op2-0", ActionStatus.Interrupted, "target moved" ), telemetry );

		Assert.Equal( 0, director.State.CompletedOpportunities );
		var rejection = telemetry.Find( e => e.Code == "opportunity_rejected" );
		Assert.NotNull( rejection );
		Assert.Contains( "interrupted", rejection.Message );
		Assert.Contains( "target moved", rejection.Message );
	}

	[Fact]
	public void Failed_TreatedAsRejection()
	{
		var cfg = AckConfig();
		var telemetry = new List<TelemetryEvent>();
		var director = OfferedDirector( cfg, telemetry );

		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 3, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Ack( "op2-0", ActionStatus.Failed, "no route" ), telemetry );

		Assert.Equal( 0, director.State.CompletedOpportunities );
		var rejection = telemetry.Find( e => e.Code == "opportunity_rejected" );
		Assert.NotNull( rejection );
		Assert.Contains( "failed", rejection.Message );
	}

	[Fact]
	public void Deferred_ExtendsExpiry_ExactlyOnce()
	{
		var cfg = AckConfig();
		var telemetry = new List<TelemetryEvent>();
		var director = OfferedDirector( cfg, telemetry );

		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 3, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Ack( "op2-0", ActionStatus.Deferred ), telemetry );
		Assert.Equal( 42, director.State.OpportunityExpiryTick ); // 22 + 20
		Assert.True( director.State.PendingDeferExtensionUsed );
		Assert.Equal( "op2-0", director.State.PendingOpportunityId );
		Assert.Equal( PressureMode.Aggressive, director.State.Mode );

		// Second deferral is a no-op.
		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 4, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Ack( "op2-0", ActionStatus.Deferred ), telemetry );
		Assert.Equal( 42, director.State.OpportunityExpiryTick );

		// A terminal ack still works afterwards.
		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 5, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Ack( "op2-0", ActionStatus.Succeeded ), telemetry );
		Assert.Equal( 1, director.State.CompletedOpportunities );
	}

	[Fact]
	public void Expiry_WithoutAck_LapsesOpportunity()
	{
		var cfg = AckConfig();
		var telemetry = new List<TelemetryEvent>();
		var director = OfferedDirector( cfg, telemetry );

		// Through tick 22 the opportunity is still live (lapses only when expiry < tick).
		var decisions = MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ).Target( "t", "a", 10.0, 0.0 ), 3, 20, telemetry );
		Assert.All( decisions, d => Assert.Null( d ) );
		Assert.Equal( "op2-0", director.State.PendingOpportunityId );
		Assert.Equal( PressureMode.Aggressive, director.State.Mode );
		Assert.Equal( 0, MacroDrive.CountCode( telemetry, "opportunity_expired" ) );

		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 23, 0.25 ).Target( "t", "a", 10.0, 0.0 ), telemetry );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "opportunity_expired" ) );
		var s = director.State;
		Assert.Equal( "", s.PendingOpportunityId );
		Assert.Equal( PressureMode.Normal, s.Mode );
		Assert.Equal( 0.0, s.Progression ); // back to start progression
		Assert.Equal( 0.25, s.CooldownRemaining );
		Assert.Equal( 0.0, s.SweepSecondsRemaining );
		Assert.Equal( 0, s.CompletedOpportunities );
		Assert.NotNull( decision );
		Assert.Equal( "opportunity_expired", decision.ReasonCode );
	}

	[Fact]
	public void UnknownAck_EmitsAckUnknown_AndChangesNothing()
	{
		var cfg = AckConfig();
		var telemetry = new List<TelemetryEvent>();
		var director = OfferedDirector( cfg, telemetry );

		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 3, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Ack( "op2-999", ActionStatus.Succeeded ), telemetry );

		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "ack_unknown" ) );
		Assert.Equal( "op2-0", director.State.PendingOpportunityId );
		Assert.Equal( PressureMode.Aggressive, director.State.Mode );
		Assert.Equal( 0, director.State.CompletedOpportunities );
		Assert.Null( decision );
	}

	[Fact]
	public void Ack_AfterTerminalResult_IsUnknown()
	{
		var cfg = AckConfig();
		var telemetry = new List<TelemetryEvent>();
		var director = OfferedDirector( cfg, telemetry );

		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 3, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Ack( "op2-0", ActionStatus.Succeeded ), telemetry );
		Assert.Equal( 1, director.State.CompletedOpportunities );

		// The same id acked again is no longer pending.
		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 4, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Ack( "op2-0", ActionStatus.Succeeded ), telemetry );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "ack_unknown" ) );
		Assert.Equal( 1, director.State.CompletedOpportunities );
	}
}

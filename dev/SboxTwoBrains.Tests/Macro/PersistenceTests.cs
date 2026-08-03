using System.Collections.Generic;
using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Macro;

/// <summary>Save/restore round-trip, double-run determinism and state diagnostics.</summary>
public sealed class PersistenceTests
{
	private static EffectiveConfig PersistenceConfig()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.MaxOpportunities = 3;
		cfg.Pressure.EventQuotaMin = 1;
		cfg.Pressure.EventQuotaMax = 3;
		cfg.Pressure.StartProgression = 0.5;
		return cfg;
	}

	private static WorldBuilder ScriptedWorld( long tick )
	{
		var b = WorldBuilder.At( tick, 0.25 ).Monster( 0, 0 );
		if ( tick % 10 < 7 )
			b.Target( "t" + tick % 3, "region" + tick % 3, 5.0 + tick % 3 * 10.0, 0.0 );
		if ( tick % 11 == 5 )
			b.Zone( "z" + tick % 2, ExclusionKind.Target, 15.0, 0.0, 6.0 );
		if ( tick == 20 )
			b.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.SetProgression, Progression = 0.9 } );
		if ( tick == 30 )
			b.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.ResetPressure, ResetGauge = true } );
		if ( tick == 50 )
			b.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.ForceOpportunity } );
		return b;
	}

	[Fact]
	public void CaptureRestore_Continues_ByteForByte()
	{
		var cfg = PersistenceConfig();
		var telemetryA = new List<TelemetryEvent>();

		var rngA = new DeterministicRng( 424242 );
		var a = new PressureDirector( rngA );
		MacroDrive.Run( a, cfg, ScriptedWorld, 0, 40, telemetryA );

		// A mid-flight ingress ban (host-side use record) must survive the round trip too.
		a.State.IngressBanRemaining["vent1"] = 3.0;
		string capturedState = a.CaptureState();
		var (s0, s1) = rngA.GetState();

		// Uninterrupted continuation for another 40 ticks.
		var postA = MacroDrive.Run( a, cfg, ScriptedWorld, 40, 40, telemetryA );
		string finalStateA = a.CaptureState();
		var (a0, a1) = rngA.GetState();

		// Restored twin: same macro state + same rng words (the facade saves both).
		var rngB = new DeterministicRng( 999 );
		rngB.SetState( s0, s1 );
		var b = new PressureDirector( rngB );
		b.RestoreState( capturedState );
		Assert.Equal( capturedState, b.CaptureState() ); // restore is lossless on arrival

		var telemetryB = new List<TelemetryEvent>();
		var postB = MacroDrive.Run( b, cfg, ScriptedWorld, 40, 40, telemetryB );

		for ( int i = 0; i < 40; i++ )
			Assert.Equal( CanonicalJson.ToJson( postA[i] ), CanonicalJson.ToJson( postB[i] ) );
		Assert.Equal( finalStateA, b.CaptureState() );
		Assert.Equal( a0, rngB.GetState().S0 );
		Assert.Equal( a1, rngB.GetState().S1 );

		// Telemetry for the continuation window matches as well.
		var codesA = new List<string>();
		foreach ( var e in telemetryA )
		{
			if ( e.Tick >= 40 )
				codesA.Add( e.Code );
		}
		Assert.Equal( codesA, MacroDrive.Codes( telemetryB ) );
	}

	[Fact]
	public void DoubleRun_IdenticalInputs_IdenticalOutputEveryTick()
	{
		var cfg = PersistenceConfig();
		var rngA = new DeterministicRng( 77 );
		var rngB = new DeterministicRng( 77 );
		var a = new PressureDirector( rngA );
		var b = new PressureDirector( rngB );
		var telemetryA = new List<TelemetryEvent>();
		var telemetryB = new List<TelemetryEvent>();

		for ( long tick = 0; tick < 150; tick++ )
		{
			string pending = a.State.PendingOpportunityId;
			var decisionA = MacroDrive.TickOne( a, cfg, InteractiveWorld( tick, pending ), telemetryA );
			var decisionB = MacroDrive.TickOne( b, cfg, InteractiveWorld( tick, pending ), telemetryB );
			Assert.Equal( CanonicalJson.ToJson( decisionA ), CanonicalJson.ToJson( decisionB ) );
			Assert.Equal( a.CaptureState(), b.CaptureState() );
		}
		Assert.Equal( MacroDrive.Codes( telemetryA ), MacroDrive.Codes( telemetryB ) );
	}

	private static WorldBuilder InteractiveWorld( long tick, string pendingId )
	{
		var b = WorldBuilder.At( tick, 0.25 ).Monster( 0, 0 );
		if ( tick % 8 < 6 )
			b.Target( "t" + tick % 3, "region" + tick % 3, 5.0 + tick % 3 * 10.0, 0.0 );
		if ( pendingId.Length > 0 && tick % 4 == 1 )
			b.Ack( pendingId, tick % 8 == 1 ? ActionStatus.Succeeded : ActionStatus.Rejected );
		if ( pendingId.Length > 0 && tick % 12 == 5 )
			b.Ack( pendingId, ActionStatus.Deferred );
		if ( tick == 20 )
			b.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.SetProgression, Progression = 0.9 } );
		if ( tick == 40 )
			b.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.ResetPressure, ResetGauge = false } );
		if ( tick == 60 )
			b.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.ForceOpportunity } );
		if ( tick == 80 )
			b.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.SetPressureMode, Mode = PressureMode.Normal, Progression = 0.5 } );
		if ( tick == 100 )
			b.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.ResetPressure, ResetGauge = true } );
		return b;
	}

	[Fact]
	public void Restore_ToleratesNullCollections()
	{
		var director = new PressureDirector( new DeterministicRng( 5 ) );
		const string json = "{\"Mode\":\"Normal\",\"Progression\":0.5,\"CompletedOpportunities\":0,"
			+ "\"EventQuotaProgress\":0,\"EventQuotaTarget\":0,\"Enabled\":true,\"CandidateLatched\":false,"
			+ "\"CooldownRemaining\":0,\"DecreaseDelayRemaining\":0,\"ActiveCandidateId\":null,"
			+ "\"PendingOpportunityId\":null,\"OpportunityExpiryTick\":0,\"PendingDeferExtensionUsed\":false,"
			+ "\"LastTransitionTick\":-1,\"SweepSecondsRemaining\":0,\"IngressAttractRemaining\":0,"
			+ "\"IngressBanRemaining\":null,\"RecentReasons\":null}";
		director.RestoreState( json );

		Assert.NotNull( director.State.IngressBanRemaining );
		Assert.NotNull( director.State.RecentReasons );
		Assert.Equal( "", director.State.ActiveCandidateId );
		Assert.Equal( "", director.State.PendingOpportunityId );
		Assert.Equal( 0.5, director.State.Progression );

		// The restored director keeps ticking normally.
		var telemetry = new List<TelemetryEvent>();
		MacroDrive.TickOne( director, MacroConfigs.Fast(), WorldBuilder.At( 0, 0.25 ).Target( "t", "a", 10.0, 0.0 ), telemetry );
		Assert.True( director.State.CandidateLatched );
	}

	[Fact]
	public void RecentReasons_BoundedRing_KeepsNewest16()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 3 ) );
		var telemetry = new List<TelemetryEvent>();

		for ( long tick = 0; tick < 300; tick++ )
			MacroDrive.TickOne( director, cfg, WorldBuilder.At( tick, 0.25 ).Target( "t", "a", 10.0, 0.0 ), telemetry );

		var codes = MacroDrive.Codes( telemetry );
		Assert.True( codes.Count > PressureState.MaxRecentReasons );
		Assert.Equal( PressureState.MaxRecentReasons, director.State.RecentReasons.Count );
		for ( int i = 0; i < PressureState.MaxRecentReasons; i++ )
			Assert.Equal( codes[codes.Count - PressureState.MaxRecentReasons + i], director.State.RecentReasons[i] );
	}
}

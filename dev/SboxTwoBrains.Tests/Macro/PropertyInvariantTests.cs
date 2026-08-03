using System;
using System.Collections.Generic;
using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Macro;

/// <summary>Property-style invariant checks over long seeded random runs.</summary>
public sealed class PropertyInvariantTests
{
	[Theory]
	[InlineData( 1UL )]
	[InlineData( 2UL )]
	[InlineData( 3UL )]
	public void SeededRandomWorlds_InvariantsHold_2000Ticks( ulong seed )
	{
		var cfg = PropertyConfig();
		var director = new PressureDirector( new DeterministicRng( seed * 7919UL ) );
		var inputRng = new DeterministicRng( seed * 104729UL + 13UL );
		var telemetry = new List<TelemetryEvent>();

		for ( long tick = 0; tick < 2000; tick++ )
		{
			// Host-side ingress bans appear occasionally and must age out cleanly.
			if ( inputRng.NextChance( 0.05 ) )
				director.State.IngressBanRemaining["i" + inputRng.NextInt( 0, 2 )] = inputRng.NextRange( 0.25, 2.0 );

			var snap = BuildRandomSnapshot( inputRng, tick, director.State );
			var ctx = new TickContext( tick, snap.DeltaTimeSeconds );
			director.ApplyOpportunityResults( ctx, cfg, snap.Acknowledgements, telemetry );
			director.ApplyDirectives( ctx, cfg, snap.Directives, telemetry );
			var decision = director.Tick( ctx, snap, cfg, telemetry );
			AssertInvariants( director.State, decision, tick );
		}
	}

	private static EffectiveConfig PropertyConfig()
	{
		var cfg = MacroConfigs.Fast();
		var p = cfg.Pressure;
		p.FillSeconds = 0.75;
		p.DecreaseDelaySeconds = 0.25;
		p.MaxOpportunities = 3;
		p.EventQuotaMin = 1;
		p.EventQuotaMax = 3;
		p.AggressiveThresholdProgression = 0.6;
		p.SweepDurationSeconds = 0.5;
		p.OpportunityExpirySeconds = 2.0;
		return cfg;
	}

	private static WorldSnapshot BuildRandomSnapshot( DeterministicRng rng, long tick, PressureState state )
	{
		var snap = new WorldSnapshot { TickIndex = tick, DeltaTimeSeconds = 0.25 };
		snap.Monster = new MonsterSnapshot { MonsterId = "m", Lifecycle = MonsterLifecycle.Alive, Position = Vec3.Zero };

		int targetCount = rng.NextInt( 0, 4 );
		for ( int i = 0; i < targetCount; i++ )
		{
			snap.Targets.Add( new TargetSnapshot
			{
				TargetId = "t" + i,
				RegionId = "r" + rng.NextInt( 0, 4 ),
				Position = new Vec3( rng.NextRange( -50.0, 50.0 ), 0.0, rng.NextRange( -50.0, 50.0 ) ),
				IsValid = rng.NextChance( 0.95 ),
				IsAlive = rng.NextChance( 0.95 ),
				PressureEligible = rng.NextChance( 0.8 ),
			} );
		}

		if ( rng.NextChance( 0.3 ) )
		{
			snap.ExclusionZones.Add( new ExclusionZone
			{
				ZoneId = "z" + tick,
				Kind = (ExclusionKind)rng.NextInt( 0, 3 ),
				Center = new Vec3( rng.NextRange( -30.0, 30.0 ), 0.0, rng.NextRange( -30.0, 30.0 ) ),
				Radius = rng.NextRange( 3.0, 10.0 ),
				Active = rng.NextChance( 0.8 ),
			} );
		}

		for ( int i = 0; i < 2; i++ )
		{
			snap.IngressPoints.Add( new IngressPoint
			{
				IngressId = "i" + i,
				RegionId = "r" + rng.NextInt( 0, 4 ),
				Usable = rng.NextChance( 0.7 ),
				CooldownUntilTick = rng.NextChance( 0.2 ) ? tick + rng.NextInt( 0, 5 ) : -1,
			} );
		}

		if ( state.PendingOpportunityId.Length > 0 )
		{
			int roll = rng.NextInt( 0, 20 );
			ActionStatus? status = null;
			if ( roll < 3 ) status = ActionStatus.Succeeded;
			else if ( roll < 5 ) status = ActionStatus.Rejected;
			else if ( roll < 7 ) status = ActionStatus.Deferred;
			else if ( roll < 8 ) status = ActionStatus.Failed;
			if ( status.HasValue )
				snap.Acknowledgements.Add( new ActionResult { ActionId = state.PendingOpportunityId, Status = status.Value, ResultTick = tick } );
		}

		if ( rng.NextChance( 0.02 ) )
		{
			switch ( rng.NextInt( 0, 3 ) )
			{
				case 0:
					snap.Directives.Add( new ScriptDirective { Kind = ScriptDirectiveKind.SetProgression, Progression = rng.NextDouble() } );
					break;
				case 1:
					snap.Directives.Add( new ScriptDirective { Kind = ScriptDirectiveKind.ResetPressure, ResetGauge = rng.NextChance( 0.5 ) } );
					break;
				case 2:
					snap.Directives.Add( new ScriptDirective { Kind = ScriptDirectiveKind.ForceOpportunity, RegionId = rng.NextChance( 0.5 ) ? "r" + rng.NextInt( 0, 4 ) : "" } );
					break;
			}
		}
		return snap;
	}

	private static void AssertInvariants( PressureState s, PressureDecision decision, long tick )
	{
		Assert.InRange( s.Progression, 0.0, 1.0 );
		Assert.True( s.CompletedOpportunities >= 0, "completed count negative" );
		Assert.True( s.EventQuotaProgress >= 0, "quota progress negative" );
		Assert.True( s.EventQuotaTarget >= 0, "quota target negative" );
		if ( s.EventQuotaTarget > 0 )
			Assert.True( s.EventQuotaProgress < s.EventQuotaTarget, "quota progress must reset when the target is reached" );
		Assert.True( Enum.IsDefined( typeof( PressureMode ), s.Mode ), "mode outside the two-state machine" );
		Assert.True( s.CooldownRemaining >= 0.0, "cooldown negative" );
		Assert.True( s.DecreaseDelayRemaining >= 0.0, "decrease delay negative" );
		Assert.True( s.SweepSecondsRemaining >= 0.0, "sweep negative" );
		Assert.True( s.IngressAttractRemaining >= 0.0, "ingress attract negative" );
		foreach ( var kv in s.IngressBanRemaining )
			Assert.True( kv.Value > 0.0, "expired ingress ban must be removed" );
		Assert.True( s.RecentReasons.Count <= PressureState.MaxRecentReasons, "reason ring unbounded" );
		if ( s.PendingOpportunityId.Length > 0 )
		{
			// An opportunity lapses only once its expiry tick has passed (< tick), so a live
			// pending opportunity may sit exactly at its expiry tick for that one tick.
			Assert.True( s.OpportunityExpiryTick >= tick, "pending opportunity expiry in the past" );
		}
		if ( decision != null )
		{
			Assert.True( decision.ReasonCode.Length > 0, "decision without a reason code" );
			if ( decision.OpportunityId.Length > 0 )
				Assert.True( decision.ExpiryTick > tick, "offered opportunity must expire in the future" );
			Assert.InRange( decision.Urgency, 0.0, 1.0 );
		}
	}
}

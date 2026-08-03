using System.Collections.Generic;
using SboxTwoBrains;
using Xunit;
using Host = SboxTwoBrains.Tests.FakeHost.FakeHost;

namespace SboxTwoBrains.Tests.Integration;

/// <summary>
/// System-level invariants under seeded chaos: randomized FakeHost worlds (random stimuli,
/// target motion, directives, capability/zone toggles, damage and ack policies — all drawn
/// from a local <see cref="DeterministicRng"/>, never System.Random) run for 2000 ticks.
/// Every batch must carry unique action ids with future expiries, the macro gauge must stay
/// in [0,1], all counters must stay non-negative, nothing may throw, and a mid-run
/// CaptureState → canonical-JSON round trip → RestoreState must leave the state-hash
/// sequence untouched. Three seeds run the same scenario twice (uninterrupted vs restored).
/// </summary>
public sealed class SystemLevelInvariantTests
{
	private const int Ticks = 2000;
	private const long RestoreAt = 1000;
	private static readonly string[] Regions = { "atrium", "hall", "yard" };

	/// <summary>One seeded world + driver. All randomness flows through the core's RNG type.</summary>
	private sealed class RandomWorld
	{
		public readonly Host Host;
		private readonly DeterministicRng _rng;
		private readonly HashSet<string> _deferredOnce = new HashSet<string>( System.StringComparer.Ordinal );

		public RandomWorld( ulong seed )
		{
			_rng = new DeterministicRng( seed );
			var catalogue = AlienIsolationPresets.CreateCatalogue();
			var system = new TwoBrainsSystem( catalogue, "DEFAULT", seed );
			Host = new Host( system ) { DeltaTime = IntegrationSupport.Dt };
			Host.MonsterPosition = Vec3.Zero;
			Host.MonsterRegionId = "atrium";

			for ( int i = 0; i < 3; i++ )
			{
				var t = Host.AddTarget( "rt" + i, new Vec3( Range( -15.0, 15.0 ), 0.0, Range( -15.0, 15.0 ) ), Regions[_rng.NextInt( Regions.Length )], threat: _rng.NextDouble() );
				t.IsArmed = _rng.NextDouble() < 0.4;
				t.IsAimingAtMonster = _rng.NextDouble() < 0.3;
				t.IsVisible = _rng.NextDouble() < 0.5;
			}
			foreach ( var region in Regions )
			{
				IntegrationSupport.AddPlanarNode( Host, "n-" + region + "-a", Range( -10.0, 10.0 ), Range( -10.0, 10.0 ), region );
				IntegrationSupport.AddPlanarNode( Host, "n-" + region + "-b", Range( -10.0, 10.0 ), Range( -10.0, 10.0 ), region );
			}
			IntegrationSupport.AddPlanarNode( Host, "n-hall-dead", 12.0, 12.0, "hall", reachable: false );
			IntegrationSupport.AddPlanarNode( Host, "on1", 0.0, 30.0, "OFF1", kind: NavCandidateKind.OffstageNode );
			IntegrationSupport.AddPlanarNode( Host, "on2", 5.0, 30.0, "OFF1", kind: NavCandidateKind.OffstageNode );
			Host.AddIngress( "vent1", 3.0, 0.0, 0.0, "hall", "on1" );
			Host.OffstageRegions.Add( new OffstageRegion
			{
				RegionId = "OFF1",
				NodeIds = { "on1", "on2" },
				IngressIds = { "vent1" },
				AdjacentRegionIds = { "hall" },
			} );
			Host.ExclusionZones.Add( new ExclusionZone { ZoneId = "z1", Kind = ExclusionKind.Target, Center = Vec3.Zero, Radius = 5.0, Active = false } );

			Host.Policies[ActionKind.MoveTo] = ( request, h ) =>
				_deferredOnce.Add( request.ActionId ) ? ActionStatus.Deferred : ActionStatus.Succeeded;
			Host.Policies[ActionKind.Chase] = ( request, h ) =>
				_rng.NextDouble() < 0.15 ? ActionStatus.Failed : ActionStatus.Succeeded;
		}

		private double Range( double min, double max ) => min + ( max - min ) * _rng.NextDouble();

		public DecisionBatch StepOnce()
		{
			long t = Host.TickIndex;
			if ( _rng.NextDouble() < 0.30 ) EmitStimulus( t, 0 );
			if ( _rng.NextDouble() < 0.30 ) EmitStimulus( t, 1 );
			if ( _rng.NextDouble() < 0.25 ) MoveRandomTarget();
			if ( _rng.NextDouble() < 0.04 ) QueueDirective();
			if ( _rng.NextDouble() < 0.03 ) ToggleCapability();
			if ( _rng.NextDouble() < 0.02 ) Host.ExclusionZones[0].Active = !Host.ExclusionZones[0].Active;
			if ( _rng.NextDouble() < 0.02 ) Host.DamageMonster( 0.05 + 0.2 * _rng.NextDouble() );
			return Host.Step();
		}

		private void EmitStimulus( long tick, int ordinal )
		{
			var stimulus = new Stimulus
			{
				StimulusId = "rs" + tick + "-" + ordinal,
				Channel = (SenseChannel)_rng.NextInt( 6 ),
				Position = new Vec3( Range( -20.0, 20.0 ), 0.0, Range( -20.0, 20.0 ) ),
				RegionId = Regions[_rng.NextInt( Regions.Length )],
				Confidence = _rng.NextDouble(),
			};
			if ( _rng.NextDouble() < 0.5 )
				stimulus.TargetId = "rt" + _rng.NextInt( 3 );
			if ( stimulus.Channel == SenseChannel.Auditory )
				stimulus.Subtype = "footstep";
			Host.EmitStimulus( stimulus );
		}

		private void MoveRandomTarget()
		{
			var t = Host.Targets[_rng.NextInt( Host.Targets.Count )];
			double x = t.Position.X + ( _rng.NextDouble() - 0.5 ) * 4.0;
			double z = t.Position.Z + ( _rng.NextDouble() - 0.5 ) * 4.0;
			if ( x > 30.0 ) x = 30.0; if ( x < -30.0 ) x = -30.0;
			if ( z > 30.0 ) z = 30.0; if ( z < -30.0 ) z = -30.0;
			t.Position = new Vec3( x, 0.0, z );
		}

		private void QueueDirective()
		{
			ScriptDirective directive;
			switch ( _rng.NextInt( 6 ) )
			{
				case 0:
					directive = new ScriptDirective { Kind = ScriptDirectiveKind.SetPressureMode, Mode = PressureMode.Aggressive, Progression = 0.5 + 0.5 * _rng.NextDouble(), ResetGauge = _rng.NextDouble() < 0.5 };
					break;
				case 1:
					directive = new ScriptDirective { Kind = ScriptDirectiveKind.SetPressureMode, Mode = PressureMode.Normal };
					break;
				case 2:
					directive = new ScriptDirective { Kind = ScriptDirectiveKind.SetProgression, Progression = _rng.NextDouble() };
					break;
				case 3:
					directive = new ScriptDirective { Kind = ScriptDirectiveKind.ResetPressure, ResetGauge = _rng.NextDouble() < 0.5 };
					break;
				case 4:
					directive = new ScriptDirective { Kind = ScriptDirectiveKind.ForceOpportunity, RegionId = Regions[_rng.NextInt( Regions.Length )] };
					break;
				default:
					directive = new ScriptDirective { Kind = ScriptDirectiveKind.ForceWithdrawal };
					break;
			}
			Host.PendingDirectives.Add( directive );
		}

		private void ToggleCapability()
		{
			switch ( _rng.NextInt( 3 ) )
			{
				case 0: Host.CanMove = !Host.CanMove; break;
				case 1: Host.CanAttack = !Host.CanAttack; break;
				default: Host.CanTraverseIngress = !Host.CanTraverseIngress; break;
			}
		}
	}

	[Theory]
	[InlineData( 7L )]
	[InlineData( 123L )]
	[InlineData( 9999L )]
	public void RandomizedWorld_MaintainsInvariants_AcrossSaveRestore( long seed )
	{
		var hashes = new List<ulong>[2];
		for ( int run = 0; run < 2; run++ )
		{
			var world = new RandomWorld( (ulong)seed );
			var runHashes = new List<ulong>( Ticks );
			var seenThisTick = new HashSet<string>( System.StringComparer.Ordinal );
			string context = "seed " + seed + " run " + run;

			for ( int i = 0; i < Ticks; i++ )
			{
				if ( run == 1 && world.Host.TickIndex == RestoreAt )
				{
					// Mid-run save: envelope through canonical JSON, then restore in place.
					var envelope = world.Host.System.CaptureState();
					string json = CanonicalJson.ToJson( envelope );
					var parsed = CanonicalJson.FromJson<SavedStateEnvelope>( json );
					Assert.Equal( json, CanonicalJson.ToJson( parsed ) );
					world.Host.System.RestoreState( parsed );
				}

				DecisionBatch batch = world.StepOnce(); // any exception fails the test here

				Assert.Equal( i, batch.TickIndex );
				seenThisTick.Clear();
				foreach ( var action in batch.Actions )
				{
					Assert.False( string.IsNullOrEmpty( action.ActionId ), "empty action id at " + context + " tick " + i );
					Assert.True( seenThisTick.Add( action.ActionId ), "duplicate action id " + action.ActionId + " at " + context + " tick " + i );
					Assert.True( action.ExpiryTick > batch.TickIndex, "non-future expiry for " + action.ActionId + " at " + context + " tick " + i );
				}

				var macro = world.Host.System.MacroState;
				Assert.InRange( macro.Progression, 0.0, 1.0 );
				Assert.True( macro.CompletedOpportunities >= 0, "negative completed count at " + context + " tick " + i );
				Assert.True( macro.EventQuotaProgress >= 0, "negative quota progress at " + context + " tick " + i );
				Assert.True( macro.CooldownRemaining >= 0.0, "negative cooldown at " + context + " tick " + i );
				Assert.True( macro.SweepSecondsRemaining >= 0.0, "negative sweep at " + context + " tick " + i );
				Assert.True( world.Host.System.MicroState.ConsecutiveNavFailures >= 0, "negative nav failures at " + context + " tick " + i );
				runHashes.Add( batch.StateHash );
			}
			hashes[run] = runHashes;
		}

		// The restored run must agree with the uninterrupted run on every tick —
		// before the restore point trivially, after it by the save/restore contract.
		Assert.Equal( hashes[0], hashes[1] );
	}
}

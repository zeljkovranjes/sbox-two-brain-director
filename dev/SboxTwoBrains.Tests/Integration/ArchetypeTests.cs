using System.Collections.Generic;
using SboxTwoBrains;
using Xunit;
using Host = SboxTwoBrains.Tests.FakeHost.FakeHost;

namespace SboxTwoBrains.Tests.Integration;

/// <summary>
/// Three archetypes authored purely as configuration (no core changes): "stalker"
/// (slow fill, long cooldown, high perception), "brute" (fast fill, short attack cooldown,
/// no offstage), "lurker" (ambush-biased via a long ambush timeout, maximum flank chance).
/// All inherit one base profile whose only tuning concession is
/// AggressiveThresholdProgression 0.5 — the asymptotic gauge never reaches 1.0 in floating
/// point, so a threshold of 0.5 makes natural macro transitions observable at dt 1/20.
/// Every archetype pair gets at least one concrete behavioral divergence under the same
/// seed and the same world.
/// </summary>
public sealed class ArchetypeTests
{
	private static ProfileCatalogue ArchetypeCatalogue()
	{
		return new ProfileCatalogue()
			.Add( new MonsterProfileConfig
			{
				Name = "archetype-base",
				ConfigVersion = "archetypes-1",
				Pressure = new PressureSection { AggressiveThresholdProgression = 0.5 },
			} )
			.Add( new MonsterProfileConfig
			{
				Name = "stalker",
				BasedOn = "archetype-base",
				// 30s ambush timeout (vs the 40s baseline) so the timeout lands inside the
				// 700-tick divergence window used below.
				Pressure = new PressureSection { FillSeconds = 12.0, CooldownSeconds = 40.0, AmbushTimeoutSeconds = 30.0 },
				Perception = new PerceptionSection
				{
					MemoryCapacity = 64,
					Visual = new PerceptionChannelSection { DecayHalfLifeSeconds = 60.0, MaxAgeSeconds = 300.0 },
					Auditory = new PerceptionChannelSection { DecayHalfLifeSeconds = 45.0, MaxAgeSeconds = 240.0 },
				},
			} )
			.Add( new MonsterProfileConfig
			{
				Name = "brute",
				BasedOn = "archetype-base",
				Pressure = new PressureSection { FillSeconds = 0.5 },
				Combat = new CombatSection { AttackCooldownSeconds = 0.3 },
				Threat = new ThreatSection { FlankChance = 0.0 },
				Modules = new ModulesSection { Disabled = new[] { "Offstage" } },
			} )
			.Add( new MonsterProfileConfig
			{
				Name = "lurker",
				BasedOn = "archetype-base",
				Pressure = new PressureSection { FillSeconds = 6.0, AmbushTimeoutSeconds = 120.0 },
				Threat = new ThreatSection { FlankChance = 1.0 },
			} );
	}

	private static Host NewArchetypeHost( string profile )
	{
		var system = new TwoBrainsSystem( ArchetypeCatalogue(), profile, IntegrationSupport.Seed );
		var host = new Host( system ) { DeltaTime = IntegrationSupport.Dt };
		host.MonsterPosition = Vec3.Zero;
		host.MonsterRegionId = "atrium";
		return host;
	}

	[Fact]
	public void ConfigsResolveAndDescribeDiffers()
	{
		var catalogue = ArchetypeCatalogue();
		var resolved = new Dictionary<string, EffectiveConfig>();
		foreach ( var name in new[] { "stalker", "brute", "lurker" } )
		{
			var cfg = catalogue.Resolve( name ); // Resolve() already calls Validated()
			Assert.Empty( cfg.Validate() );
			resolved[name] = cfg;
		}

		// The authored deltas landed.
		Assert.Equal( 12.0, resolved["stalker"].Pressure.FillSeconds );
		Assert.Equal( 40.0, resolved["stalker"].Pressure.CooldownSeconds );
		Assert.Equal( 64, resolved["stalker"].Perception.MemoryCapacity );
		Assert.Equal( 0.5, resolved["brute"].Pressure.FillSeconds );
		Assert.Equal( 0.3, resolved["brute"].Combat.AttackCooldownSeconds );
		Assert.Contains( "Offstage", resolved["brute"].Modules.Disabled );
		Assert.Equal( 6.0, resolved["lurker"].Pressure.FillSeconds );
		Assert.Equal( 120.0, resolved["lurker"].Pressure.AmbushTimeoutSeconds );
		Assert.Equal( 1.0, resolved["lurker"].Threat.FlankChance );

		// Describe() and the config hash differ pairwise (stable identity per archetype).
		var descriptions = new HashSet<string>();
		var hashes = new HashSet<ulong>();
		foreach ( var name in new[] { "stalker", "brute", "lurker" } )
		{
			Assert.True( descriptions.Add( resolved[name].Describe() ), name + " shares a Describe() with a sibling" );
			Assert.True( hashes.Add( resolved[name].ComputeHash() ), name + " shares a config hash with a sibling" );
		}
	}

	[Fact]
	public void StalkerVsBrute_MacroTimingDiffers()
	{
		// Same seed, same world: a passive candidate and no stimuli. The gauge crosses the
		// shared 0.5 threshold after ~FillSeconds — observable directly in transition ticks.
		long bruteTick = FirstAggressiveTick( "brute", 30 );
		long stalkerTick = FirstAggressiveTick( "stalker", 200 );

		Assert.InRange( bruteTick, 5, 10 ); // fill 0.5s
		Assert.InRange( stalkerTick, 160, 175 ); // fill 12s
		Assert.True( bruteTick < stalkerTick, "brute pressure must ramp far faster than stalker pressure" );
	}

	private static long FirstAggressiveTick( string profile, int ticks )
	{
		var host = NewArchetypeHost( profile );
		host.AddTarget( "t1", new Vec3( 10.0, 0.0, 0.0 ), "hall" );
		var batches = host.Run( ticks );
		for ( int i = 0; i < batches.Count; i++ )
		{
			if ( batches[i].Macro != null )
			{
				Assert.Equal( "mode_aggressive_start", batches[i].Macro.ReasonCode );
				return batches[i].TickIndex;
			}
		}
		return -1;
	}

	[Fact]
	public void BruteVsStalker_AttackCadenceDiffers()
	{
		// Same visible weak target: brute's 0.3s attack cooldown re-commits far sooner
		// than stalker's inherited 1.5s — a different action mix over the same window.
		var bruteAttacks = AttackTicks( "brute", 100 );
		var stalkerAttacks = AttackTicks( "stalker", 100 );

		Assert.True( bruteAttacks.Count >= 2 && stalkerAttacks.Count >= 2, "both archetypes must re-commit at least once" );
		long bruteGap = bruteAttacks[1] - bruteAttacks[0];
		long stalkerGap = stalkerAttacks[1] - stalkerAttacks[0];
		Assert.InRange( bruteGap, 5, 10 );
		Assert.InRange( stalkerGap, 28, 34 );
		Assert.True( bruteGap < stalkerGap );
		Assert.True( bruteAttacks.Count > stalkerAttacks.Count, "brute attacks more often in the same 100 ticks" );
	}

	private static List<long> AttackTicks( string profile, int ticks )
	{
		var host = NewArchetypeHost( profile );
		var target = host.AddTarget( "t1", new Vec3( 4.0, 0.0, 0.0 ), "hall" );
		var ticksOfAttacks = new List<long>();
		for ( int i = 0; i < ticks; i++ )
		{
			host.EmitVisual( "vis-t1", "t1", target.Position, "hall" );
			var batch = host.Step();
			if ( IntegrationSupport.HasCode( new[] { batch }, "attack_commit" ) )
				ticksOfAttacks.Add( batch.TickIndex );
		}
		return ticksOfAttacks;
	}

	[Fact]
	public void BruteVsLurker_ThreatResponseDiffers()
	{
		// Same aimed-at dangerous target in close range with a usable vent toward it:
		// lurker's flank chance 1.0 always takes the ingress flank; brute's 0.0 never does.
		var lurker = ThreatWorld( "lurker" );
		var lurkerBatches = new List<DecisionBatch>();
		for ( int i = 0; i < 10; i++ )
		{
			lurker.EmitVisual( "vis-t1", "t1", lurker.Targets[0].Position, "hall" );
			lurkerBatches.Add( lurker.Step() );
		}
		var flank = IntegrationSupport.AllActions( lurkerBatches ).Find( a => a.Kind == ActionKind.UseIngress );
		Assert.NotNull( flank );
		Assert.Equal( "flank", flank.ReasonCode );
		Assert.Equal( "vent-hall", flank.IngressId );
		Assert.True( IntegrationSupport.HasCode( lurkerBatches, "flank" ) );

		var brute = ThreatWorld( "brute" );
		var bruteBatches = new List<DecisionBatch>();
		for ( int i = 0; i < 60; i++ )
		{
			brute.EmitVisual( "vis-t1", "t1", brute.Targets[0].Position, "hall" );
			bruteBatches.Add( brute.Step() );
		}
		Assert.Equal( 0, IntegrationSupport.CountActions( bruteBatches, ActionKind.UseIngress ) );
		Assert.Equal( 0, IntegrationSupport.CountActions( bruteBatches, ActionKind.Attack ) );
		Assert.Equal( 0, IntegrationSupport.CountActions( bruteBatches, ActionKind.Chase ) );

		// Both start identically (hesitation), then diverge on the flank roll.
		Assert.True( IntegrationSupport.HasCode( lurkerBatches, "hesitate" ) );
		Assert.True( IntegrationSupport.HasCode( bruteBatches, "hesitate" ) );
	}

	private static Host ThreatWorld( string profile )
	{
		var host = NewArchetypeHost( profile );
		var t = host.AddTarget( "t1", new Vec3( 6.0, 0.0, 0.0 ), "hall", threat: 0.9 );
		t.IsArmed = true;
		t.IsAimingAtMonster = true;
		t.IsVisible = true;
		host.AddIngress( "vent-hall", 1.0, 0.0, 0.0, "hall", "" );
		return host;
	}

	[Fact]
	public void StalkerVsLurker_AmbushDurationDiffers()
	{
		// Same script: brief visual contact, a forced aggressive opportunity, then the target
		// vanishes. Both archetypes ambush the remembered position — stalker times out after
		// its 30s ambush timeout and falls through to search; lurker's 120s timeout still holds.
		var stalker = AmbushWorld( "stalker" );
		var stalkerBatches = RunAmbushScript( stalker );
		long stalkerAmbush = IntegrationSupport.FirstTickOfCode( stalkerBatches, "ambush_start" );
		long stalkerTimeout = IntegrationSupport.FirstTickOfCode( stalkerBatches, "ambush_timeout" );
		long stalkerSearch = IntegrationSupport.FirstTickOfCode( stalkerBatches, "search_start" );
		Assert.True( stalkerAmbush > 0, "stalker must ambush the remembered target" );
		Assert.True( stalkerTimeout > stalkerAmbush, "stalker's 30s ambush must time out inside 700 ticks" );
		// The timeout makes Ambush ineligible mid-arbitration, so Search wins the rest of
		// the same tick's pass — search_start may share the timeout tick.
		Assert.True( stalkerSearch >= stalkerTimeout, "stalker must fall through to systematic search" );
		Assert.True( IntegrationSupport.FirstTickOfCode( stalkerBatches, "search_end" ) > stalkerSearch,
			"the one-node region exhausts the episode right after it starts" );

		var lurker = AmbushWorld( "lurker" );
		var lurkerBatches = RunAmbushScript( lurker );
		Assert.True( IntegrationSupport.FirstTickOfCode( lurkerBatches, "ambush_start" ) > 0, "lurker must ambush too" );
		Assert.Equal( -1, IntegrationSupport.FirstTickOfCode( lurkerBatches, "ambush_timeout" ) );
		Assert.Equal( -1, IntegrationSupport.FirstTickOfCode( lurkerBatches, "search_start" ) );
		// Lurker is still holding the ambush at the horizon: no search, no attack, no chase.
		Assert.Equal( 0, IntegrationSupport.CountActions( lurkerBatches, ActionKind.Attack ) );
		Assert.Equal( 0, IntegrationSupport.CountActions( lurkerBatches, ActionKind.Search ) );
	}

	private static Host AmbushWorld( string profile )
	{
		var host = NewArchetypeHost( profile );
		host.AddTarget( "t1", new Vec3( 6.0, 0.0, 0.0 ), "hall" );
		IntegrationSupport.AddPlanarNode( host, "h1", 6.0, 2.0, "hall" );
		return host;
	}

	private static List<DecisionBatch> RunAmbushScript( Host host )
	{
		var batches = new List<DecisionBatch>();
		for ( int i = 0; i < 700; i++ )
		{
			if ( host.TickIndex < 10 )
				host.EmitVisual( "vis-t1", "t1", new Vec3( 6.0, 0.0, 0.0 ), "hall" );
			if ( host.TickIndex == 10 )
				IntegrationSupport.Direct( host, IntegrationSupport.ForceOpportunity( "hall" ) );
			if ( host.TickIndex == 11 )
				host.Targets.Clear(); // the target vanishes; the memory persists
			batches.Add( host.Step() );
		}
		return batches;
	}
}

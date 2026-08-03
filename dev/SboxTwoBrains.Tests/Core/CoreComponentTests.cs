using TwoBrains.Core.Compat;
using TwoBrains.Core.Config;
using TwoBrains.Core.Contract;
using TwoBrains.Core.Determinism;
using TwoBrains.Core.Serialization;
using Xunit;

namespace SboxTwoBrains.Tests.Core;

public sealed class RngTests
{
	[Fact]
	public void SameSeed_SameSequence()
	{
		var a = new DeterministicRng( 12345 );
		var b = new DeterministicRng( 12345 );
		for ( int i = 0; i < 64; i++ )
			Assert.Equal( a.NextUInt64(), b.NextUInt64() );
	}

	[Fact]
	public void DifferentSeeds_Diverge()
	{
		var a = new DeterministicRng( 1 );
		var b = new DeterministicRng( 2 );
		Assert.NotEqual( a.NextUInt64(), b.NextUInt64() );
	}

	[Fact]
	public void StateSaveRestore_ResumesExactly()
	{
		var a = new DeterministicRng( 999 );
		for ( int i = 0; i < 37; i++ ) a.NextUInt64();
		var (s0, s1) = a.GetState();
		var expected = a.NextUInt64();

		var b = new DeterministicRng( 1 );
		b.SetState( s0, s1 );
		Assert.Equal( expected, b.NextUInt64() );
	}

	[Fact]
	public void ZeroSeed_ProducesNonZeroState()
	{
		var a = new DeterministicRng( 0 );
		var (s0, s1) = a.GetState();
		Assert.False( s0 == 0 && s1 == 0 );
		Assert.NotEqual( 0UL, a.NextUInt64() );
	}

	[Fact]
	public void DoubleInUnitInterval()
	{
		var a = new DeterministicRng( 7 );
		for ( int i = 0; i < 10000; i++ )
		{
			var d = a.NextDouble();
			Assert.InRange( d, 0.0, 1.0 );
			Assert.True( d < 1.0 );
		}
	}

	[Fact]
	public void IntBoundsRespected()
	{
		var a = new DeterministicRng( 77 );
		for ( int i = 0; i < 10000; i++ )
		{
			Assert.InRange( a.NextInt( 5 ), 0, 4 );
			Assert.InRange( a.NextInt( 3, 9 ), 3, 8 );
		}
		Assert.Equal( 0, a.NextInt( 0 ) );
		Assert.Equal( 4, a.NextInt( 4, 4 ) );
	}

	[Fact]
	public void RangeBoundsRespected()
	{
		var a = new DeterministicRng( 88 );
		for ( int i = 0; i < 10000; i++ )
		{
			var v = a.NextRange( 2.5, 7.5 );
			Assert.InRange( v, 2.5, 7.5 );
		}
		Assert.Equal( 3.0, a.NextRange( 3.0, 3.0 ) );
	}

	[Fact]
	public void ForksAreIndependent()
	{
		var m = new DeterministicRng( 42 );
		var a = DeterministicRng.Fork( 42, 1 );
		var b = DeterministicRng.Fork( 42, 2 );
		var c = DeterministicRng.Fork( 42, 1 );
		Assert.NotEqual( a.NextUInt64(), b.NextUInt64() );
		Assert.Equal( new DeterministicRng( 42 ).GetState(), m.GetState() ); // forking never mutates master
		Assert.Equal( DeterministicRng.Fork( 42, 1 ).GetState(), c.GetState() ); // fork is deterministic
	}
}

public sealed class CanonicalJsonTests
{
	[Fact]
	public void SameValue_ByteIdentical()
	{
		var batch = new DecisionBatch
		{
			TickIndex = 12,
			StateHash = 987654321,
			Macro = new PressureDecision { OpportunityId = "op12-0", Mode = PressureMode.Aggressive, Progression = 0.5125, Urgency = 1.0, CandidateRegionId = "atrium", ReasonCode = "opportunity_offered" },
		};
		batch.Actions.Add( new ActionRequest { ActionId = "a12-0", Kind = ActionKind.Chase, TargetId = "p1", ExpiryTick = 99 } );
		batch.Telemetry.Add( new TelemetryEvent( 12, "macro", "mode_aggressive_start", "test" ) );

		var first = CanonicalJson.ToJson( batch );
		var second = CanonicalJson.ToJson( batch );
		Assert.Equal( first, second );
		Assert.Equal( CanonicalJson.Hash( first ), CanonicalJson.Hash( second ) );

		var roundTrip = CanonicalJson.FromJson<DecisionBatch>( first );
		Assert.Equal( 12, roundTrip.TickIndex );
		Assert.Equal( "op12-0", roundTrip.Macro.OpportunityId );
		Assert.Equal( ActionKind.Chase, roundTrip.Actions[0].Kind );
	}

	[Fact]
	public void Hash_DetectsAnyChange()
	{
		var a = CanonicalJson.ToJson( new DecisionBatch { TickIndex = 1 } );
		var b = CanonicalJson.ToJson( new DecisionBatch { TickIndex = 2 } );
		Assert.NotEqual( CanonicalJson.Hash( a ), CanonicalJson.Hash( b ) );
	}
}

public sealed class TickContextTests
{
	[Fact]
	public void RejectsInvalidInputs()
	{
		Assert.Throws<System.ArgumentOutOfRangeException>( () => new TickContext( -1, 0.016 ) );
		Assert.Throws<System.ArgumentOutOfRangeException>( () => new TickContext( 0, 0.0 ) );
		Assert.Throws<System.ArgumentOutOfRangeException>( () => new TickContext( 0, -1.0 ) );
		Assert.Throws<System.ArgumentOutOfRangeException>( () => new TickContext( 0, double.NaN ) );
		Assert.Throws<System.ArgumentOutOfRangeException>( () => new TickContext( 0, double.PositiveInfinity ) );
		Assert.Throws<System.ArgumentOutOfRangeException>( () => new TickContext( 0, 61.0 ) );
	}

	[Fact]
	public void AcceptsNominal()
	{
		var ctx = new TickContext( 0, 1.0 / 60.0 );
		Assert.Equal( 0, ctx.TickIndex );
		Assert.True( ctx.DeltaTimeSeconds > 0 );
	}
}

public sealed class ConfigTests
{
	private static ProfileCatalogue BuildCatalogue()
	{
		return new ProfileCatalogue()
			.Add( new MonsterProfileConfig { Name = "base", Pressure = new PressureSection { FillSeconds = 3.0, MaxOpportunities = 4 } } )
			.Add( new MonsterProfileConfig { Name = "child", BasedOn = "base", Pressure = new PressureSection { FillSeconds = 5.0 } } );
	}

	[Fact]
	public void ChildWinsOnSetFields_ParentFillsRest()
	{
		var cfg = BuildCatalogue().Resolve( "child" );
		Assert.Equal( 5.0, cfg.Pressure.FillSeconds );   // child override
		Assert.Equal( 4, cfg.Pressure.MaxOpportunities ); // inherited
		Assert.Equal( 25.0, cfg.Pressure.CooldownSeconds ); // generic baseline
	}

	[Fact]
	public void CycleDetected()
	{
		var cat = new ProfileCatalogue()
			.Add( new MonsterProfileConfig { Name = "a", BasedOn = "b" } )
			.Add( new MonsterProfileConfig { Name = "b", BasedOn = "a" } );
		var ex = Assert.Throws<ConfigException>( () => cat.Resolve( "a" ) );
		Assert.Contains( "cycle", ex.Message );
	}

	[Fact]
	public void MissingParentFails()
	{
		var cat = new ProfileCatalogue().Add( new MonsterProfileConfig { Name = "a", BasedOn = "ghost" } );
		var ex = Assert.Throws<ConfigException>( () => cat.Resolve( "a" ) );
		Assert.Contains( "ghost", ex.Message );
	}

	[Fact]
	public void ModifierAddsAndClamps()
	{
		var cat = BuildCatalogue()
			.Add( new MonsterProfileConfig { Name = "hard", Pressure = new PressureSection { FillSeconds = -5.0, MaxOpportunities = 1 } } );
		var cfg = cat.ResolveWithModifier( "base", "hard" );
		Assert.Equal( EffectiveConfig.ResolvedPressure.MinFillSeconds, cfg.Pressure.FillSeconds ); // 3 + (-5) clamped to 0.5
		Assert.Equal( 5, cfg.Pressure.MaxOpportunities ); // 4 + 1
	}

	[Fact]
	public void ValidationCatchesOutOfRange()
	{
		var cat = new ProfileCatalogue().Add( new MonsterProfileConfig { Name = "bad", Pressure = new PressureSection { FillSeconds = 0.1 } } );
		Assert.Throws<ConfigException>( () => cat.Resolve( "bad" ) );
	}

	[Fact]
	public void DescribeDeterministicAndHashStable()
	{
		var cfg = BuildCatalogue().Resolve( "child" );
		var d1 = cfg.Describe();
		var d2 = cfg.Describe();
		Assert.Equal( d1, d2 );
		Assert.Equal( cfg.ComputeHash(), cfg.ComputeHash() );
		Assert.Contains( "Pressure.FillSeconds=5", d1 );
	}

	[Fact]
	public void HashChangesWithConfig()
	{
		var cat = BuildCatalogue();
		Assert.NotEqual( cat.Resolve( "base" ).ComputeHash(), cat.Resolve( "child" ).ComputeHash() );
	}
}

public sealed class PresetTests
{
	[Fact]
	public void AllPresetRecords_AllResolve()
	{
		// 12 usable configs from the 13 shipped files (the 13th is an empty master index record).
		Assert.Equal( 12, AlienIsolationPresets.All().Length );
		var cat = AlienIsolationPresets.CreateCatalogue();
		foreach ( var record in AlienIsolationPresets.All() )
		{
			var cfg = cat.Resolve( record.Name );
			Assert.NotNull( cfg );
		}
		Assert.NotNull( cat.Resolve( AlienIsolationPresets.InspiredProfileName ) );
	}

	[Fact]
	public void SpotCheckDecodedValues()
	{
		var cat = AlienIsolationPresets.CreateCatalogue();
		Assert.Equal( 3.0, cat.Resolve( "DEFAULT" ).Pressure.FillSeconds );
		Assert.Equal( 4.0, cat.Resolve( "INTENSE" ).Pressure.FillSeconds );
		Assert.Equal( 30.0, cat.Resolve( "MILD" ).Pressure.CooldownSeconds );
		Assert.Equal( 5, cat.Resolve( "INTENSE" ).Pressure.MaxOpportunities );
		Assert.Equal( 4, cat.Resolve( "DEFAULT" ).Pressure.MaxOpportunities );
		// micro tuning constants from the recovered trees
		Assert.Equal( 0.5, cat.Resolve( "DEFAULT" ).Threat.VisualRetentionSeconds );
		Assert.Equal( 0.2, cat.Resolve( "DEFAULT" ).Threat.FlankChance );
		Assert.Equal( 20.0, cat.Resolve( "DEFAULT" ).Search.SystematicWindowSeconds );
	}

	[Fact]
	public void InheritanceMirrorsTemplateNames()
	{
		// BACKSTAGEHOLD_VCLOSE inherits BACKSTAGEHOLD; its own killtrap is disabled (-1 -> 0)
		var cat = AlienIsolationPresets.CreateCatalogue();
		var vclose = cat.Resolve( "BACKSTAGEHOLD_VCLOSE" );
		Assert.Equal( 0.0, vclose.Pressure.KilltrapSeconds );
		Assert.Equal( 1.0, vclose.Pressure.SweepMinDistance );
	}
}

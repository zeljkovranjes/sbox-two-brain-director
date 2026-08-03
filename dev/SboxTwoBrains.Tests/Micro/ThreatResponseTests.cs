using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Micro;

/// <summary>ThreatResponse module: hesitation, flank roll, retention, fall-through, timeout.</summary>
public sealed class ThreatResponseTests
{
	private static AgentDriver ThreatDriver( ulong seed, double threat = 0.8, double distance = 6.0, bool aiming = false )
	{
		var d = new AgentDriver( seed );
		var target = Snap.Target( "t1", distance, 0.0, threat: threat, visible: true );
		target.IsAimingAtMonster = aiming;
		d.World.Targets.Add( target );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 1.0, targetId: "t1", x: distance ) );
		return d;
	}

	private static ulong SeedWhereFirstDoubleBelow( bool below )
	{
		for ( ulong s = 1; s < 100000; s++ )
		{
			var probe = new DeterministicRng( s );
			double v = probe.NextDouble();
			if ( below ? v < 0.5 : v >= 0.5 ) return s;
		}
		throw new System.InvalidOperationException( "no seed found" );
	}

	[Fact]
	public void AimedWeaponCausesHesitation()
	{
		var d = ThreatDriver( 1, aiming: true );
		var actions = d.StepOnce();
		var action = Assert.Single( actions );
		Assert.Equal( ActionKind.Threat, action.Kind );
		Assert.Equal( "t1", action.TargetId );
		Assert.False( action.Destination.HasValue ); // no movement
		Assert.True( d.Tele( "hesitate" ) );
		Assert.Equal( 0.5, d.State.Timers["hesitate"], 10 );
	}

	[Fact]
	public void FlankRollIsSeededAndDeterministic()
	{
		ulong flankSeed = SeedWhereFirstDoubleBelow( true );
		ulong holdSeed = SeedWhereFirstDoubleBelow( false );

		var flanker = ThreatDriver( flankSeed );
		flanker.World.IngressPoints.Add( Snap.Ingress( "ing1", 5.0, 0.0, region: "R1" ) );
		var flankActions = flanker.StepOnce();
		var flank = Assert.Single( flankActions );
		Assert.Equal( ActionKind.UseIngress, flank.Kind );
		Assert.Equal( "ing1", flank.IngressId );
		Assert.True( flanker.Tele( "flank" ) );

		var holder = ThreatDriver( holdSeed );
		holder.World.IngressPoints.Add( Snap.Ingress( "ing1", 5.0, 0.0, region: "R1" ) );
		Assert.Empty( holder.StepOnce() ); // roll failed: threat-aware hold
		Assert.False( holder.Hist( "flank" ) );
		Assert.Contains( "flank_rolled", holder.State.Flags ); // rolled once per episode

		// identical seed reproduces the identical outcome
		var repeat = ThreatDriver( flankSeed );
		repeat.World.IngressPoints.Add( Snap.Ingress( "ing1", 5.0, 0.0, region: "R1" ) );
		Assert.Equal( ActionKind.UseIngress, Assert.Single( repeat.StepOnce() ).Kind );
	}

	[Fact]
	public void VeryCloseTargetFallsThroughToAttack()
	{
		var cfg = new SboxTwoBrains.EffectiveConfig();
		cfg.Threat.FlankChance = 0.0;
		var d = new AgentDriver( 1, cfg );
		d.World.Targets.Add( Snap.Target( "t1", 4.0, 0.0, threat: 0.8, visible: true ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 1.0, targetId: "t1", x: 4.0 ) );
		var action = Assert.Single( d.StepOnce() );
		Assert.Equal( ActionKind.Chase, action.Kind ); // 4 m < VeryClose (5 m) but > AttackRange
	}

	[Fact]
	public void VisualRetentionHoldsBrieflyAfterLosingSight()
	{
		var cfg = new SboxTwoBrains.EffectiveConfig();
		cfg.Threat.FlankChance = 0.0;
		var d = new AgentDriver( 1, cfg );
		d.World.Targets.Add( Snap.Target( "t1", 6.0, 0.0, threat: 0.8, visible: true ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 1.0, targetId: "t1", x: 6.0 ) );
		Assert.Empty( d.StepOnce() ); // tick 0: threat hold (not aiming, 6 m > very close)
		d.World.CurrentStimuli.Clear();
		d.Step( 5 ); // ticks 1..5: retention window (0.5 s) keeps threat response eligible
		Assert.False( d.Hist( "chase" ) );
		Assert.False( d.Hist( "suspect_response" ) );
		d.Step( 3 ); // ticks 6..8: retention lapses -> suspect response takes over
		Assert.True( d.Hist( "suspect_response" ) );
	}

	[Fact]
	public void ThreatEpisodeTimesOut()
	{
		var cfg = new SboxTwoBrains.EffectiveConfig();
		cfg.Threat.FlankChance = 0.0;
		cfg.Threat.ThreatTimeoutSeconds = 1.0;
		var d = new AgentDriver( 1, cfg );
		d.World.Targets.Add( Snap.Target( "t1", 6.0, 0.0, threat: 0.8, visible: true ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 1.0, targetId: "t1", x: 6.0 ) );
		d.Step( 15 );
		Assert.True( d.Hist( "threat_timeout" ) );
	}
}

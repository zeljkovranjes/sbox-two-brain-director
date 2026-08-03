using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Micro;

/// <summary>Attack module: precondition matrix, chase/attack transition, chase loss.</summary>
public sealed class AttackTests
{
	private static AgentDriver AttackDriver( double distance, SenseChannel channel = SenseChannel.Visual )
	{
		var d = new AgentDriver();
		d.World.Targets.Add( Snap.Target( "t1", distance, 0.0, visible: true ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", channel, 1.0, targetId: "t1", x: distance ) );
		return d;
	}

	[Fact]
	public void AttackCommitsInsideRange()
	{
		var d = AttackDriver( 2.0 );
		var action = Assert.Single( d.StepOnce() );
		Assert.Equal( ActionKind.Attack, action.Kind );
		Assert.Equal( "t1", action.TargetId );
		Assert.True( d.Tele( "attack_commit" ) );
		Assert.Equal( 1.5, d.State.Timers["attack_cd"], 10 );
		Assert.Contains( "attack", d.State.Motivations );
	}

	[Fact]
	public void CooldownBlocksImmediateReattack()
	{
		var d = AttackDriver( 2.0 );
		d.StepOnce(); // tick 0: attack
		d.AckLast( ActionStatus.Succeeded );
		var next = d.StepOnce(); // tick 1: cooldown active
		Assert.Equal( ActionKind.Wait, Assert.Single( next ).Kind );
		d.Step( 15 ); // ticks 2..16: cooldown (1.5 s) expires at tick 15
		Assert.Equal( 2, d.HistCount( "attack_commit" ) );
	}

	[Fact]
	public void BeyondGiveUpDistanceDoesNotEngage()
	{
		var d = AttackDriver( 50.0 ); // ChaseGiveUpDistance = 40
		var action = Assert.Single( d.StepOnce() );
		Assert.Equal( ActionKind.Wait, action.Kind );
		Assert.DoesNotContain( "attack", d.State.Motivations );
	}

	[Fact]
	public void HostAttackCapabilityGatesAttack()
	{
		var d = AttackDriver( 2.0 );
		d.World.Monster.CanAttack = false;
		var action = Assert.Single( d.StepOnce() );
		Assert.Equal( ActionKind.Wait, action.Kind );
	}

	[Fact]
	public void AuditoryOnlyEvidenceDoesNotTriggerAttack()
	{
		var d = AttackDriver( 5.0, SenseChannel.Auditory );
		var action = Assert.Single( d.StepOnce() );
		Assert.Equal( ActionKind.Wait, action.Kind );
		Assert.Equal( "t1", d.State.CurrentTargetId ); // still tracked as target evidence
	}

	[Fact]
	public void TouchAndDamageEvidenceTriggerAttack()
	{
		var touch = AttackDriver( 2.0, SenseChannel.Touch );
		Assert.Equal( ActionKind.Attack, Assert.Single( touch.StepOnce() ).Kind );
		var damage = AttackDriver( 2.0, SenseChannel.Damage );
		Assert.Equal( ActionKind.Attack, Assert.Single( damage.StepOnce() ).Kind );
	}

	[Fact]
	public void ChaseTransitionsToAttackInRange()
	{
		var d = AttackDriver( 5.0 );
		var chase = Assert.Single( d.StepOnce() ); // tick 0: 5 m > AttackRange -> chase
		Assert.Equal( ActionKind.Chase, chase.Kind );
		Assert.Equal( 1.0, chase.SpeedScale );
		Assert.Equal( new Vec3( 5.0, 0.0, 0.0 ), chase.Destination );
		Assert.True( d.Tele( "chase" ) );

		d.World.Targets[0].Position = new Vec3( 2.0, 0.0, 0.0 );
		d.World.CurrentStimuli.Clear();
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 1.0, targetId: "t1", x: 2.0 ) );
		d.AckLast( ActionStatus.Succeeded );
		var next = Assert.Single( d.StepOnce() ); // tick 1: 2 m -> attack
		Assert.Equal( ActionKind.Attack, next.Kind );
	}

	[Fact]
	public void LostChaseHandsOffToSearch()
	{
		var cfg = new SboxTwoBrains.EffectiveConfig();
		cfg.Combat.ChaseGiveUpSeconds = 1.0;
		var d = new AgentDriver( 1, cfg );
		d.World.Targets.Add( Snap.Target( "t1", 10.0, 0.0, visible: true ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 1.0, targetId: "t1", x: 10.0 ) );
		d.World.NavCandidates.Add( Snap.Node( "n1", "R1", 10.0, 0.0, route: 4.0 ) );
		Assert.Equal( ActionKind.Chase, d.StepOnce()[0].Kind ); // tick 0
		d.World.CurrentStimuli.Clear();
		for ( int t = 0; t < 12; t++ ) d.StepAck(); // ticks 1..12: chase continuation, then loss
		Assert.True( d.Hist( "chase_lost" ) );
		Assert.Equal( "", d.State.CurrentTargetId );
		Assert.True( d.Hist( "search_start" ) ); // search takes over from memory
	}
}

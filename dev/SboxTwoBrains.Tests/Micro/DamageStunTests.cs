using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Micro;

/// <summary>DamageStun module: stun gauge accumulation, stagger, suppression while staggering.</summary>
public sealed class DamageStunTests
{
	[Fact]
	public void LargeHealthDropStaggers()
	{
		var d = new AgentDriver();
		d.StepOnce(); // tick 0: baseline prev_health = 1.0
		d.World.Monster.HealthFraction = 0.4; // drop 0.6 -> stun 1.2 - 0.1 decay = 1.1 >= 1.0
		var actions = d.StepOnce(); // tick 1: stagger
		var action = Assert.Single( actions );
		Assert.Equal( ActionKind.Wait, action.Kind );
		Assert.Equal( 0.0, action.SpeedScale );
		Assert.True( d.Tele( "stagger" ) );
		Assert.Equal( 1.5, d.State.Timers["stagger"], 10 ); // AttackCooldownSeconds
		Assert.Equal( 0.0, d.State.Gauges["stun"] ); // consumed
		Assert.Equal( 0.4, d.State.Gauges["prev_health"] );
	}

	[Fact]
	public void SmallDropsAccumulateIntoStagger()
	{
		var d = new AgentDriver();
		d.StepOnce(); // baseline
		bool staggered = false;
		for ( int t = 1; t <= 15 && !staggered; t++ )
		{
			d.World.Monster.HealthFraction = System.Math.Max( 0.0, 1.0 - 0.1 * t );
			d.AckLast( ActionStatus.Succeeded );
			d.StepOnce();
			staggered = d.Tele( "stagger" );
		}
		Assert.True( staggered );
	}

	[Fact]
	public void StaggerSuppressesOtherModulesUntilExpired()
	{
		var d = new AgentDriver();
		d.StepOnce(); // tick 0: baseline
		d.World.Monster.HealthFraction = 0.4;
		d.StepOnce(); // tick 1: stagger (Wait a1-0)
		Assert.True( d.Hist( "stagger" ) );

		// a point-blank target appears; the stagger still suppresses the attack
		d.World.Targets.Add( Snap.Target( "t1", 2.0, 0.0, visible: true ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 1.0, targetId: "t1", x: 2.0 ) );
		d.AckLast( ActionStatus.Succeeded );
		d.Step( 5 ); // ticks 2..6: stagger active (1.5 s)
		Assert.False( d.Hist( "attack_commit" ) );

		d.Step( 12 ); // ticks 7..18: stagger expires at tick 16
		Assert.True( d.Hist( "attack_commit" ) );
	}
}

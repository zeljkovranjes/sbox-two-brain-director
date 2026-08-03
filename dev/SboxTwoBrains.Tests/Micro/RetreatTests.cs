using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Micro;

/// <summary>Retreat module: health/deterrent/gauge triggers and the retreat cooldown.</summary>
public sealed class RetreatTests
{
	[Fact]
	public void LowHealthTriggersRetreat()
	{
		var d = new AgentDriver();
		d.World.Monster.HealthFraction = 0.2;
		d.World.NavCandidates.Add( Snap.Node( "n_far", "R0", 50.0, 0.0 ) );
		var actions = d.StepOnce();
		var action = Assert.Single( actions );
		Assert.Equal( ActionKind.Retreat, action.Kind );
		Assert.Equal( "n_far", action.NodeId ); // farthest candidate from the threat/monster
		Assert.True( d.Tele( "retreat_start" ) );
		Assert.Equal( 10.0, d.State.Timers["retreat_cd"], 10 );
		Assert.Contains( "retreat", d.State.Motivations );
	}

	[Fact]
	public void RetreatCooldownSuppressesRetrigger()
	{
		var d = new AgentDriver();
		d.World.Monster.HealthFraction = 0.2;
		d.World.NavCandidates.Add( Snap.Node( "n_far", "R0", 50.0, 0.0 ) );
		d.StepOnce(); // tick 0: retreat
		d.AckLast( ActionStatus.Succeeded );
		for ( int i = 0; i < 5; i++ ) d.StepAck(); // ticks 1..5
		Assert.Equal( 1, d.HistCount( "retreat_start" ) );
	}

	[Fact]
	public void DeterrentExposureTriggersRetreat()
	{
		var d = new AgentDriver();
		d.World.NavCandidates.Add( Snap.Node( "n_far", "R0", 60.0, 0.0 ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "d1", SenseChannel.Damage, 0.9, subtype: "deterrent" ) );
		// exposure accumulates dt per tick; DeterrentRetreatSeconds = 2.0 -> ~20 ticks
		bool retreated = false;
		for ( int t = 0; t < 25 && !retreated; t++ )
		{
			d.StepOnce();
			retreated = d.Tele( "retreat_start" );
		}
		Assert.True( retreated );
		Assert.True( d.State.Timers["deterrent_exposure"] >= 2.0 );
	}

	[Fact]
	public void VisibleDeterrentUserCountsAsExposure()
	{
		var d = new AgentDriver();
		d.World.NavCandidates.Add( Snap.Node( "n_far", "R0", 60.0, 0.0 ) );
		var t = Snap.Target( "t1", 4.0, 0.0, visible: true );
		t.IsUsingDeterrent = true;
		d.World.Targets.Add( t );
		bool retreated = false;
		for ( int i = 0; i < 25 && !retreated; i++ )
		{
			d.StepOnce();
			retreated = d.Tele( "retreat_start" );
		}
		Assert.True( retreated );
	}

	[Fact]
	public void RetreatGaugeRisesUnderThreatWithRecentDamage()
	{
		var d = new AgentDriver();
		d.World.Monster.LastDamageTick = 0;
		d.World.NavCandidates.Add( Snap.Node( "n_far", "R0", 60.0, 0.0 ) );
		d.World.Targets.Add( Snap.Target( "t1", 5.0, 0.0, threat: 0.8, visible: true ) ); // dangerous + close + visible
		long retreatTick = -1;
		for ( int t = 0; t < 60 && retreatTick < 0; t++ )
		{
			d.StepOnce();
			if ( d.Tele( "retreat_start" ) ) retreatTick = d.Tick - 1;
		}
		Assert.True( retreatTick >= 40, "retreat fired at tick " + retreatTick );
	}
}

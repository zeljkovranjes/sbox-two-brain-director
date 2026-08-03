using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Micro;

/// <summary>SuspectResponse and HidingTarget modules.</summary>
public sealed class SuspectHidingTests
{
	[Fact]
	public void LostGlimpseTriggersSuspectResponse()
	{
		var d = new AgentDriver();
		d.World.Targets.Add( Snap.Target( "t1", 45.0, 0.0, visible: true ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 1.0, targetId: "t1", x: 45.0 ) );
		Assert.Equal( ActionKind.Wait, Assert.Single( d.StepOnce() ).Kind ); // tick 0: 45 m > give-up -> no engage

		d.World.CurrentStimuli.Clear();
		var action = Assert.Single( d.StepOnce() ); // tick 1: suspect response
		Assert.Equal( ActionKind.MoveTo, action.Kind );
		Assert.Equal( "t1", action.TargetId );
		Assert.Equal( new Vec3( 45.0, 0.0, 0.0 ), action.Destination );
		Assert.Equal( d.Cfg.Movement.SpeedFast, action.SpeedScale );
		Assert.True( d.Tele( "suspect_response" ) );
	}

	[Fact]
	public void SuspectResponseFiresOnceThenSearchTakesOver()
	{
		var d = new AgentDriver();
		d.World.Targets.Add( Snap.Target( "t1", 45.0, 0.0, visible: true ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 1.0, targetId: "t1", x: 45.0 ) );
		d.World.NavCandidates.Add( Snap.Node( "n1", "R1", 44.0, 0.0, route: 60.0 ) ); // route > give-up, so no engage
		d.StepOnce(); // tick 0: idle
		d.World.CurrentStimuli.Clear();
		d.StepOnce(); // tick 1: suspect response
		d.AckLast( ActionStatus.Succeeded );
		var action = Assert.Single( d.StepOnce() ); // tick 2: no re-issue -> search
		Assert.Equal( ActionKind.Search, action.Kind );
		Assert.True( d.Hist( "search_start" ) );
		Assert.Equal( 1, d.HistCount( "suspect_response" ) );
	}

	[Fact]
	public void HidingTargetProducesRegionScopedSearch()
	{
		var d = new AgentDriver();
		d.World.Targets.Add( Snap.Target( "t1", 45.0, 0.0, region: "R3" ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 0.9, targetId: "t1", x: 45.0, region: "R3" ) );
		d.StepOnce(); // tick 0: glimpse (45 m > give-up -> idle)
		d.World.CurrentStimuli.Clear();
		d.World.Targets[0].IsHiding = true;
		d.StepOnce(); // tick 1: suspect response (higher priority)
		d.AckLast( ActionStatus.Succeeded );
		var action = Assert.Single( d.StepOnce() ); // tick 2: hiding-target search
		Assert.Equal( ActionKind.Search, action.Kind );
		Assert.Equal( "R3", action.RegionId );
		Assert.Equal( "t1", action.TargetId );
		Assert.True( d.Tele( "hiding_target" ) );
	}

	[Fact]
	public void HidingTargetWithoutMemoryIsIneligible()
	{
		var d = new AgentDriver();
		var t = Snap.Target( "t1", 10.0, 0.0 );
		t.IsHiding = true;
		d.World.Targets.Add( t );
		var action = Assert.Single( d.StepOnce() );
		Assert.Equal( ActionKind.Wait, action.Kind ); // no remembered evidence -> idle
		Assert.False( d.Hist( "hiding_target" ) );
	}
}

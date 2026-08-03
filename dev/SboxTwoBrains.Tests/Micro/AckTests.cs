using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Micro;

/// <summary>All six host acknowledgement statuses and their escalation effects.</summary>
public sealed class AckTests
{
	[Fact]
	public void UnknownAckIsTelemetryNotFatal()
	{
		var d = new AgentDriver();
		d.Ack( "zz-unknown", ActionStatus.Succeeded );
		d.StepOnce();
		Assert.True( d.Tele( "ack_unknown" ) );
	}

	[Fact]
	public void SucceededClearsPendingAndAwaiting()
	{
		var d = new AgentDriver();
		var issued = d.StepOnce()[0];
		d.AckLast( ActionStatus.Succeeded );
		d.StepOnce();
		Assert.False( d.State.PendingActions.ContainsKey( issued.ActionId ) );
		Assert.NotEqual( issued.ActionId, d.State.AwaitingActionId );
	}

	[Fact]
	public void PartiallySucceededEmitsPartialCode()
	{
		var d = new AgentDriver();
		var issued = d.StepOnce()[0];
		d.AckLast( ActionStatus.PartiallySucceeded );
		d.StepOnce();
		Assert.True( d.Tele( "action_partial" ) );
		Assert.NotEqual( issued.ActionId, d.State.AwaitingActionId );
		Assert.False( d.State.PendingActions.ContainsKey( issued.ActionId ) );
	}

	[Fact]
	public void RejectedAttackStartsAttackBan()
	{
		var d = new AgentDriver();
		d.World.Targets.Add( Snap.Target( "t1", 2.0, 0.0, visible: true ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 1.0, targetId: "t1", x: 2.0 ) );
		Assert.Equal( ActionKind.Attack, d.StepOnce()[0].Kind ); // tick 0
		d.AckLast( ActionStatus.Rejected );
		d.StepOnce(); // tick 1
		Assert.True( d.Tele( "action_rejected" ) );
		Assert.Equal( 4.9, d.State.Timers["attack_ban"], 10 ); // 5.0 s set, then aged one tick

		for ( int i = 0; i < 10; i++ ) d.StepOnce();
		Assert.Equal( 1, d.HistCount( "attack_commit" ) ); // no further attacks while banned
	}

	[Fact]
	public void FailedMovementCountsNavFailure()
	{
		var d = new AgentDriver();
		d.World.NavCandidates.Add( Snap.Node( "n1", "R1", 10.0, 0.0 ) );
		d.Macro = Snap.Macro( region: "R1", roles: new[] { "stalker" } );
		d.StepOnce();
		d.AckLast( ActionStatus.Failed );
		d.StepOnce();
		Assert.True( d.Tele( "action_failed" ) );
		Assert.Equal( 1, d.State.ConsecutiveNavFailures );
		Assert.Equal( 1, d.State.LastNavFailureTick );
	}

	[Fact]
	public void InterruptedDoesNotEscalate()
	{
		var d = new AgentDriver();
		d.World.NavCandidates.Add( Snap.Node( "n1", "R1", 10.0, 0.0 ) );
		d.Macro = Snap.Macro( region: "R1", roles: new[] { "stalker" } );
		d.StepOnce();
		d.AckLast( ActionStatus.Interrupted );
		var actions = d.StepOnce();
		Assert.True( d.Tele( "action_interrupted" ) );
		Assert.Equal( 0, d.State.ConsecutiveNavFailures );
		Assert.Single( actions ); // free to act again
	}

	[Fact]
	public void RejectedIngressStartsIngressBan()
	{
		var d = new AgentDriver();
		d.World.OffstageRegions.Add( new OffstageRegion
		{
			RegionId = "OFF1",
			NodeIds = { "on1" },
			IngressIds = { "ing1" },
			AdjacentRegionIds = { "R1" },
		} );
		d.World.IngressPoints.Add( Snap.Ingress( "ing1", 2.0, 0.0 ) );
		d.World.NavCandidates.Add( Snap.Node( "on1", "OFF1", 0.0, 50.0, kind: NavCandidateKind.OffstageNode ) );
		d.Macro = Snap.Macro( region: "R1", roles: new[] { "sweeper" } );

		Assert.Equal( ActionKind.UseIngress, d.StepOnce()[0].Kind ); // tick 0: entry
		d.AckLast( ActionStatus.Rejected );
		var actions = d.StepOnce(); // tick 1
		Assert.True( d.Tele( "action_rejected" ) );
		Assert.True( d.State.Timers["ingress_ban_ing1"] > 19.8 );
		var action = Assert.Single( actions );
		Assert.Equal( ActionKind.Wait, action.Kind ); // banned ingress is no longer picked
	}
}

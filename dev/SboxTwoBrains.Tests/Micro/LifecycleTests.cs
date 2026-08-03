using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Micro;

/// <summary>Lifecycle module: death handling, nav-failure escalation, lapses, deferrals.</summary>
public sealed class LifecycleTests
{
	private static AgentDriver StalkDriver()
	{
		var d = new AgentDriver();
		d.World.NavCandidates.Add( Snap.Node( "n1", "R1", 10.0, 0.0 ) );
		d.Macro = Snap.Macro( region: "R1", roles: new[] { "stalker" } );
		return d;
	}

	[Fact]
	public void DeathClearsPendingAndSuppressesActions()
	{
		var d = new AgentDriver();
		Assert.Single( d.StepOnce() ); // tick 0: idle wait
		d.World.Monster.Lifecycle = MonsterLifecycle.Dead;
		var actions = d.StepOnce(); // tick 1: dead
		Assert.Empty( actions );
		Assert.Empty( d.State.PendingActions );
		Assert.Equal( "", d.State.AwaitingActionId );
		Assert.True( d.Tele( "lifecycle_inactive" ) );

		d.World.Monster.Lifecycle = MonsterLifecycle.Alive;
		Assert.Single( d.StepOnce() ); // tick 2: resumes
	}

	[Fact]
	public void ThreeNavFailuresTriggerRecovery()
	{
		var d = StalkDriver();
		Assert.Equal( ActionKind.Stalk, d.StepOnce()[0].Kind ); // tick 0
		d.AckLast( ActionStatus.Rejected );
		d.StepOnce(); // tick 1: failure 1, re-issue
		d.AckLast( ActionStatus.Rejected );
		d.StepOnce(); // tick 2: failure 2, re-issue
		d.AckLast( ActionStatus.Rejected );
		var actions = d.StepOnce(); // tick 3: failure 3 -> recovery

		var action = Assert.Single( actions );
		Assert.Equal( ActionKind.Wait, action.Kind );
		Assert.Equal( "6", action.Param ); // backoff = 2 s * 3 failures
		Assert.True( d.Tele( "nav_recovery" ) );
		Assert.Equal( 6.0, d.State.Timers["nav_backoff"], 10 );
		Assert.Equal( 0, d.State.ConsecutiveNavFailures ); // reset after issuing
		Assert.Equal( "", d.State.CurrentTargetId );

		// while the backoff runs, movement is refused with telemetry
		d.AckLast( ActionStatus.Succeeded );
		var blocked = d.StepOnce(); // tick 4
		Assert.Empty( blocked );
		Assert.True( d.Tele( "action_infeasible" ) );
	}

	[Fact]
	public void ExpiredActionLapses()
	{
		var d = new AgentDriver();
		var wait = Assert.Single( d.StepOnce() ); // tick 0: idle wait, expiry 20
		Assert.Equal( 20, wait.ExpiryTick );
		d.Step( 20 ); // ticks 1..20: still pending (expiry not < tick)
		Assert.False( d.Hist( "action_lapsed" ) );
		var actions = d.StepOnce(); // tick 21: lapse
		Assert.True( d.Tele( "action_lapsed" ) );
		Assert.Equal( 0, d.State.ConsecutiveNavFailures ); // Wait is not a movement kind
		Assert.Single( actions ); // a fresh idle wait is issued
	}

	[Fact]
	public void ExpiredMovementActionCountsAsNavFailure()
	{
		var d = StalkDriver();
		var stalk = Assert.Single( d.StepOnce() ); // tick 0: stalk, 10 s default timeout
		Assert.Equal( 100, stalk.ExpiryTick );
		d.Step( 100 ); // ticks 1..100: pending
		var actions = d.StepOnce(); // tick 101: lapse
		Assert.True( d.Tele( "action_lapsed" ) );
		Assert.Equal( 1, d.State.ConsecutiveNavFailures );
		Assert.Equal( 101, d.State.LastNavFailureTick );
		Assert.Single( actions ); // stalk re-issued
	}

	[Fact]
	public void DeferredOnceExtendsThenSecondDeferralFails()
	{
		var d = StalkDriver();
		var stalk = Assert.Single( d.StepOnce() ); // tick 0: expiry 100
		d.Ack( stalk.ActionId, ActionStatus.Deferred );
		d.StepOnce(); // tick 1: first deferral extends by the original interval
		Assert.Equal( 200, d.State.PendingActions[stalk.ActionId] );
		Assert.Equal( stalk.ActionId, d.State.AwaitingActionId );
		Assert.False( d.Hist( "action_failed" ) );

		d.Ack( stalk.ActionId, ActionStatus.Deferred );
		var actions = d.StepOnce(); // tick 2: second deferral -> failure
		Assert.True( d.Tele( "action_failed" ) );
		Assert.False( d.State.PendingActions.ContainsKey( stalk.ActionId ) );
		Assert.Equal( 1, d.State.ConsecutiveNavFailures );
		Assert.Single( actions ); // stalk re-issued
	}
}

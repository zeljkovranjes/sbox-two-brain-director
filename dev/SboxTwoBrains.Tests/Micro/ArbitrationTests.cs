using System.Collections.Generic;
using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Micro;

/// <summary>Module arbitration: priority, preemption, awaiting rule, config order, ids, expiries.</summary>
public sealed class ArbitrationTests
{
	[Fact]
	public void EarlierModuleWinsOverLater()
	{
		var d = new AgentDriver();
		d.World.Monster.HealthFraction = 0.2; // retreat motivation
		d.World.NavCandidates.Add( Snap.Node( "n_far", "R0", 50.0, 0.0 ) );
		d.World.Targets.Add( Snap.Target( "t1", 5.0, 0.0, visible: true ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 1.0, targetId: "t1", x: 5.0 ) );

		var actions = d.StepOnce();
		var action = Assert.Single( actions );
		Assert.Equal( ActionKind.Retreat, action.Kind ); // Retreat (4) beats Attack (7)
		Assert.Equal( "n_far", action.NodeId );
		Assert.True( d.Tele( "retreat_start" ) );
	}

	[Fact]
	public void HigherPriorityModulePreemptsAwaitedAction()
	{
		var d = new AgentDriver();
		d.World.CurrentStimuli.Add( Snap.Stim( "st1", SenseChannel.Auditory, 0.9, x: 30.0, region: "R2" ) );
		d.StepOnce(); // tick 0: investigate react (Wait), awaiting a0-0
		Assert.Equal( "investigate_react", d.LastActions[0].ReasonCode );
		d.AckLast( ActionStatus.Succeeded );
		d.StepOnce(); // tick 1: investigate approach (MoveTo), awaiting a1-0
		Assert.Equal( ActionKind.MoveTo, d.LastActions[0].Kind );

		// a dangerous-free close target appears: Attack (7) outranks Investigate (10)
		d.World.Targets.Add( Snap.Target( "t1", 2.0, 0.0, visible: true ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s2", SenseChannel.Visual, 1.0, targetId: "t1", x: 2.0 ) );
		var actions = d.StepOnce(); // tick 2: preempt
		var action = Assert.Single( actions );
		Assert.Equal( ActionKind.Attack, action.Kind );
		Assert.True( d.Tele( "preempt" ) );
		Assert.Equal( action.ActionId, d.State.AwaitingActionId );
		Assert.True( d.State.PendingActions.ContainsKey( "a1-0" ) ); // old action stays pending
	}

	[Fact]
	public void AwaitingActionBlocksNewEmissions()
	{
		var d = new AgentDriver();
		var first = Assert.Single( d.StepOnce() ); // tick 0: idle Wait
		var second = d.StepOnce(); // tick 1: no ack -> still awaiting
		Assert.Empty( second );
		Assert.Equal( first.ActionId, d.State.AwaitingActionId );

		d.Ack( first.ActionId, ActionStatus.Succeeded );
		var third = Assert.Single( d.StepOnce() ); // tick 2: free to emit again
		Assert.NotEqual( first.ActionId, third.ActionId );
	}

	[Fact]
	public void ConfigOrderOverridesDefault()
	{
		var cfg = new SboxTwoBrains.EffectiveConfig();
		cfg.Modules.Order = new[] { "Idle", "Attack" };
		var d = new AgentDriver( 1, cfg );
		d.World.Targets.Add( Snap.Target( "t1", 2.0, 0.0, visible: true ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 1.0, targetId: "t1", x: 2.0 ) );
		var action = Assert.Single( d.StepOnce() );
		Assert.Equal( ActionKind.Wait, action.Kind ); // Idle now outranks Attack
	}

	[Fact]
	public void DisabledModuleIsSkipped()
	{
		var cfg = new SboxTwoBrains.EffectiveConfig();
		cfg.Modules.Disabled = new[] { "Investigate" };
		var d = new AgentDriver( 1, cfg );
		d.World.CurrentStimuli.Add( Snap.Stim( "st1", SenseChannel.Auditory, 0.9, x: 10.0, region: "R1" ) );
		d.World.NavCandidates.Add( Snap.Node( "n1", "R1", 10.0, 0.0, route: 4.0 ) );
		var action = Assert.Single( d.StepOnce() );
		Assert.Equal( ActionKind.Search, action.Kind ); // Search takes over with Investigate disabled
		Assert.False( d.Hist( "investigate_react" ) );
	}

	[Fact]
	public void UnknownModuleNameIsTelemetryNotFailure()
	{
		var cfg = new SboxTwoBrains.EffectiveConfig();
		cfg.Modules.Order = new[] { "Bogus", "Idle" };
		var d = new AgentDriver( 1, cfg );
		var action = Assert.Single( d.StepOnce() );
		Assert.Equal( ActionKind.Wait, action.Kind );
		Assert.True( d.Tele( "module_unknown" ) );
	}

	[Fact]
	public void ActionIdsFollowTickOrdinalConvention()
	{
		var d = new AgentDriver();
		var ids = new HashSet<string>( System.StringComparer.Ordinal );
		for ( int i = 0; i < 6; i++ )
		{
			var action = Assert.Single( d.StepOnce() );
			Assert.Equal( "a" + d.State.Counters["action_tick"] + "-0", action.ActionId );
			Assert.True( ids.Add( action.ActionId ) );
			d.AckLast( ActionStatus.Succeeded );
		}
	}

	[Fact]
	public void ExpiryTicksAreInTheFuture()
	{
		var d = new AgentDriver();
		var idle = Assert.Single( d.StepOnce() ); // Wait 1 s + 1 s slack -> 2.0 s -> 20 ticks
		Assert.Equal( 20, idle.ExpiryTick );

		var d2 = new AgentDriver();
		d2.World.Targets.Add( Snap.Target( "t1", 2.0, 0.0, visible: true ) );
		d2.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 1.0, targetId: "t1", x: 2.0 ) );
		var attack = Assert.Single( d2.StepOnce() ); // cooldown 1.5 + 2 s -> 3.5 s -> 35 ticks
		Assert.Equal( ActionKind.Attack, attack.Kind );
		Assert.Equal( 35, attack.ExpiryTick );
	}
}

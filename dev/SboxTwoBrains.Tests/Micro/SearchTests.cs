using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Micro;

/// <summary>Search module: node selection, revisit penalty, episode limits, cooldown.</summary>
public sealed class SearchTests
{
	private static AgentDriver SearchDriver( System.Action<SboxTwoBrains.EffectiveConfig> tweak = null )
	{
		var cfg = new SboxTwoBrains.EffectiveConfig();
		cfg.Modules.Disabled = new[] { "Investigate" }; // keep Investigate from shadowing Search
		tweak?.Invoke( cfg );
		var d = new AgentDriver( 1, cfg );
		d.World.CurrentStimuli.Add( Snap.Stim( "st1", SenseChannel.Auditory, 0.9, x: 10.0, region: "R1" ) );
		d.World.NavCandidates.Add( Snap.Node( "n1", "R1", 12.0, 0.0, route: 5.0 ) );
		d.World.NavCandidates.Add( Snap.Node( "n2", "R1", 9.0, 0.0, route: 3.0 ) );
		d.World.NavCandidates.Add( Snap.Node( "n3", "R1", 20.0, 0.0 ) ); // no route -> planar 20
		return d;
	}

	[Fact]
	public void SelectsNearestNodeByRouteDistance()
	{
		var d = SearchDriver();
		var action = Assert.Single( d.StepOnce() );
		Assert.Equal( ActionKind.Search, action.Kind );
		Assert.Equal( "n2", action.NodeId ); // route 3 < route 5 < planar 20
		Assert.Equal( "R1", action.RegionId );
		Assert.Equal( d.Cfg.Movement.SpeedFast, action.SpeedScale );
		Assert.True( d.Tele( "search_start" ) );
	}

	[Fact]
	public void RevisitPenaltySkipsRecentlyVisitedNodes()
	{
		var d = SearchDriver();
		d.StepOnce(); // tick 0: n2
		d.AckLast( ActionStatus.Succeeded );
		Assert.Equal( "n1", d.StepOnce()[0].NodeId ); // tick 1: n2 visited -> n1
		d.AckLast( ActionStatus.Succeeded );
		Assert.Equal( "n3", d.StepOnce()[0].NodeId ); // tick 2: n1 visited -> n3
	}

	[Fact]
	public void EpisodeEndsAfterMaxNodes()
	{
		var d = SearchDriver( cfg => cfg.Search.MaxNodesPerSearch = 2 );
		d.StepOnce(); // tick 0: node 1
		d.StepAck(); // tick 1: node 2
		var action = Assert.Single( d.StepAck() ); // tick 2: episode over -> idle
		Assert.Equal( ActionKind.Wait, action.Kind );
		Assert.True( d.Hist( "search_end" ) );
		Assert.True( d.State.Timers["search_cooldown"] > 0.0 );
		Assert.Equal( 0, d.State.Counters["search_nodes"] );
	}

	[Fact]
	public void EpisodeEndsOnGiveUpTimer()
	{
		var d = SearchDriver( cfg => cfg.Search.GiveUpSeconds = 0.5 );
		d.StepOnce(); // tick 0: episode starts, action issued, never acked
		d.Step( 7 ); // tick 5: give-up (0.5 s) -> search_end
		Assert.True( d.Hist( "search_end" ) );
	}

	[Fact]
	public void CooldownPreventsImmediateRestart()
	{
		var d = SearchDriver( cfg => cfg.Search.MaxNodesPerSearch = 1 );
		d.StepOnce(); // tick 0: node 1 of 1
		d.StepAck(); // tick 1: max nodes -> search_end
		Assert.True( d.Hist( "search_end" ) );
		for ( int t = 0; t < 5; t++ )
		{
			var actions = d.StepAck();
			foreach ( var a in actions ) Assert.NotEqual( ActionKind.Search, a.Kind );
		}
	}
}

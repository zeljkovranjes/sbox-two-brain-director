using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Micro;

/// <summary>Stalk and Ambush modules (macro-biased behaviours).</summary>
public sealed class StalkAmbushTests
{
	[Fact]
	public void StalkMovesTowardCandidateRegion()
	{
		var d = new AgentDriver();
		d.World.NavCandidates.Add( Snap.Node( "n1", "R1", 10.0, 0.0, route: 8.0 ) );
		d.World.NavCandidates.Add( Snap.Node( "n2", "R1", 5.0, 0.0, route: 4.0 ) );
		d.Macro = Snap.Macro( region: "R1", roles: new[] { "stalker" } );
		var action = Assert.Single( d.StepOnce() );
		Assert.Equal( ActionKind.Stalk, action.Kind );
		Assert.Equal( "n2", action.NodeId ); // nearest by route distance
		Assert.Equal( "R1", action.RegionId );
		Assert.True( d.Tele( "stalk" ) );

		// the bias is latched: later ticks keep stalking without a fresh decision
		d.Macro = null;
		d.AckLast( ActionStatus.Succeeded );
		Assert.Equal( ActionKind.Stalk, Assert.Single( d.StepOnce() ).Kind );
	}

	[Fact]
	public void StalkRespectsMacroExclusions()
	{
		var d = new AgentDriver();
		d.World.NavCandidates.Add( Snap.Node( "n1", "R1", 5.0, 0.0 ) ); // nearer, but excluded
		d.World.NavCandidates.Add( Snap.Node( "n2", "R1", 10.0, 0.0 ) );
		d.World.ExclusionZones.Add( new ExclusionZone { ZoneId = "z1", Center = new Vec3( 5.0, 0.0, 0.0 ), Radius = 3.0 } );
		d.Macro = Snap.Macro( region: "R1", roles: new[] { "stalker" }, exclusions: new[] { "z1" } );
		var action = Assert.Single( d.StepOnce() );
		Assert.Equal( "n2", action.NodeId );
	}

	[Fact]
	public void StalkRequiresStalkerRole()
	{
		var d = new AgentDriver();
		d.World.NavCandidates.Add( Snap.Node( "n1", "R1", 5.0, 0.0 ) );
		d.Macro = Snap.Macro( region: "R1", roles: new string[0] );
		var action = Assert.Single( d.StepOnce() );
		Assert.Equal( ActionKind.Wait, action.Kind );
	}

	[Fact]
	public void AmbushStartsHoldsAndTimesOut()
	{
		var cfg = new SboxTwoBrains.EffectiveConfig();
		cfg.Pressure.AmbushTimeoutSeconds = 1.0;
		var d = new AgentDriver( 1, cfg );
		d.World.Targets.Add( Snap.Target( "t1", 50.0, 0.0, visible: true ) );
		d.World.CurrentStimuli.Add( Snap.Stim( "s1", SenseChannel.Visual, 0.9, targetId: "t1", x: 50.0 ) );
		d.World.NavCandidates.Add( Snap.Node( "n1", "R1", 52.0, 0.0 ) );
		d.World.NavCandidates.Add( Snap.Node( "n2", "R1", 49.0, 0.0 ) );
		d.Macro = Snap.Macro( region: "R1", roles: new[] { "ambusher" } );
		d.StepOnce(); // tick 0: current evidence (beyond give-up) -> idle

		d.World.CurrentStimuli.Clear();
		var action = Assert.Single( d.StepOnce() ); // tick 1: remembered evidence -> ambush
		Assert.Equal( ActionKind.Ambush, action.Kind );
		Assert.Equal( "n2", action.NodeId ); // nearest planar to the remembered position
		Assert.True( d.Tele( "ambush_start" ) );
		Assert.Equal( 1.0, d.State.Timers["ambush"], 10 );

		d.StepAck(); // tick 2: holding (no new action while the ambush timer runs)
		Assert.Empty( d.LastActions );
		d.Step( 10 ); // ticks 3..12: timeout at tick 11
		Assert.True( d.Hist( "ambush_timeout" ) );
		Assert.True( d.Hist( "suspect_response" ) ); // falls through after the timeout
	}
}

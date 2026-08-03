using System.Collections.Generic;
using System.Globalization;
using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Micro;

/// <summary>Offstage module: ingress entry, sweep with dwells, exit on window expiry.</summary>
public sealed class OffstageTests
{
	private static AgentDriver SweepDriver()
	{
		var cfg = new SboxTwoBrains.EffectiveConfig();
		cfg.Pressure.SweepDurationSeconds = 1.0;
		cfg.Offstage.NodeDwellMinSeconds = 0.2;
		cfg.Offstage.NodeDwellMaxSeconds = 0.3;
		var d = new AgentDriver( 1, cfg );
		d.World.OffstageRegions.Add( new OffstageRegion
		{
			RegionId = "OFF1",
			NodeIds = { "on1", "on2" },
			IngressIds = { "ing1" },
			AdjacentRegionIds = { "R1" },
		} );
		d.World.IngressPoints.Add( Snap.Ingress( "ing1", 2.0, 0.0 ) );
		d.World.NavCandidates.Add( Snap.Node( "on1", "OFF1", 0.0, 50.0, kind: NavCandidateKind.OffstageNode ) );
		d.World.NavCandidates.Add( Snap.Node( "on2", "OFF1", 10.0, 50.0, kind: NavCandidateKind.OffstageNode ) );
		d.Macro = Snap.Macro( region: "R1", roles: new[] { "sweeper" } );
		return d;
	}

	[Fact]
	public void IngressSweepDwellExitFlow()
	{
		var d = SweepDriver();
		var dwellParams = new List<double>();
		var moveNodes = new List<string>();
		for ( int t = 0; t < 20; t++ )
		{
			var actions = d.StepOnce();
			if ( actions.Count == 0 ) continue;
			var a = actions[0];
			if ( a.Kind == ActionKind.Wait && a.ReasonCode == "sweep_dwell" && a.Param != null )
				dwellParams.Add( double.Parse( a.Param, CultureInfo.InvariantCulture ) );
			if ( a.Kind == ActionKind.MoveTo && a.NodeId != null ) moveNodes.Add( a.NodeId );
			d.Ack( a.ActionId, ActionStatus.Succeeded );
			if ( a.Kind == ActionKind.UseIngress )
				d.World.Monster.Presence = d.State.Flags.Contains( "offstage" ) ? StagePresence.Frontstage : StagePresence.Offstage;
		}

		Assert.True( d.Hist( "ingress_use" ) ); // entry (and later exit/re-entry)
		Assert.True( d.Hist( "sweep_move" ) );
		Assert.True( d.Hist( "sweep_dwell" ) );
		Assert.Equal( 1, d.HistCount( "sweep_end" ) );
		Assert.Contains( "on1", moveNodes );
		foreach ( var dwell in dwellParams )
			Assert.InRange( dwell, 0.2, 0.3 );

		// ordering: entry before sweep before end
		int entry = d.History.FindIndex( e => e.Code == "ingress_use" );
		int sweep = d.History.FindIndex( e => e.Code == "sweep_move" );
		int end = d.History.FindIndex( e => e.Code == "sweep_end" );
		Assert.True( entry >= 0 && sweep > entry && end > sweep );
	}

	[Fact]
	public void EntryUsesMacroIngressConstraints()
	{
		var d = SweepDriver();
		d.World.IngressPoints.Add( Snap.Ingress( "ing2", 1.0, 0.0 ) ); // nearer but not suggested
		d.World.OffstageRegions[0].IngressIds.Add( "ing2" );
		d.Macro = Snap.Macro( region: "R1", roles: new[] { "sweeper" }, ingress: new[] { "ing1" } );
		var action = Assert.Single( d.StepOnce() );
		Assert.Equal( ActionKind.UseIngress, action.Kind );
		Assert.Equal( "ing1", action.IngressId );
		Assert.True( d.Tele( "ingress_use" ) );
	}

	[Fact]
	public void EntryRequiresSweeperRoleAndAggressiveMode()
	{
		var d = SweepDriver();
		d.Macro = Snap.Macro( region: "R1", mode: PressureMode.Normal, roles: new[] { "sweeper" } );
		Assert.Equal( ActionKind.Wait, Assert.Single( d.StepOnce() ).Kind );

		var d2 = SweepDriver();
		d2.Macro = Snap.Macro( region: "R1", roles: new[] { "stalker" } );
		// stalker role routes to Stalk instead; with no frontstage nodes nothing happens
		Assert.Equal( ActionKind.Wait, Assert.Single( d2.StepOnce() ).Kind );
	}
}

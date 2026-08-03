using System.Collections.Generic;
using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Micro;

/// <summary>Seeded property loops: invariants must hold no matter what the host does.</summary>
public sealed class PropertyTests
{
	[Fact]
	public void InvariantsHoldAcrossTwoThousandSeededTicks()
	{
		var d = new AgentDriver( 7 );
		var host = new DeterministicRng( 99 );
		d.World.NavCandidates.Add( Snap.Node( "n1", "R0", 5.0, 0.0, route: 3.0 ) );
		d.World.NavCandidates.Add( Snap.Node( "n2", "R1", 15.0, 0.0, route: 8.0 ) );
		d.World.NavCandidates.Add( Snap.Node( "n3", "R1", 25.0, 0.0 ) );
		d.World.NavCandidates.Add( Snap.Node( "on1", "OFF1", 0.0, 50.0, kind: NavCandidateKind.OffstageNode ) );
		d.World.IngressPoints.Add( Snap.Ingress( "ing1", 3.0, 0.0 ) );
		d.World.OffstageRegions.Add( new OffstageRegion
		{
			RegionId = "OFF1",
			NodeIds = { "on1" },
			IngressIds = { "ing1" },
			AdjacentRegionIds = { "R1" },
		} );

		var ids = new HashSet<string>( System.StringComparer.Ordinal );
		string deferredId = null;
		for ( int t = 0; t < 2000; t++ )
		{
			var w = d.World;
			w.CurrentStimuli.Clear();
			w.Targets.Clear();
			double roll = host.NextDouble();
			if ( roll < 0.25 )
			{
				double dist = 2.0 + host.NextDouble() * 40.0;
				w.Targets.Add( Snap.Target( "t1", dist, 0.0, threat: host.NextDouble(), visible: true ) );
				w.CurrentStimuli.Add( Snap.Stim( "vs" + ( t % 7 ), SenseChannel.Visual, 0.4 + 0.6 * host.NextDouble(), targetId: "t1", x: dist ) );
			}
			else if ( roll < 0.45 )
			{
				w.CurrentStimuli.Add( Snap.Stim( "au" + ( t % 5 ), SenseChannel.Auditory, 0.3 + 0.7 * host.NextDouble(), x: 10.0 + host.NextDouble() * 20.0, region: "R1" ) );
			}
			if ( host.NextDouble() < 0.10 )
				w.Monster.HealthFraction = 0.3 + 0.7 * host.NextDouble();
			if ( host.NextDouble() < 0.05 )
				w.Monster.CanAttack = !w.Monster.CanAttack;
			if ( host.NextDouble() < 0.03 )
				d.Macro = Snap.Macro( "R1", roles: new[] { host.NextChance( 0.5 ) ? "stalker" : "sweeper" }, expiry: d.Tick + 30 );

			// deterministic host ack policy driven by the test-side rng
			foreach ( var a in d.LastActions )
			{
				int pick = host.NextInt( 6 );
				if ( pick == (int)ActionStatus.Deferred )
				{
					d.Ack( a.ActionId, ActionStatus.Deferred );
					deferredId = a.ActionId;
				}
				else
				{
					d.Ack( a.ActionId, (ActionStatus)pick );
				}
			}
			if ( deferredId != null && host.NextChance( 0.3 ) )
			{
				d.Ack( deferredId, ActionStatus.Deferred ); // second deferral -> failure path
				deferredId = null;
			}

			d.StepOnce();
			long tick = d.Tick - 1;

			Assert.True( d.LastActions.Count <= 1, "more than one action at tick " + tick );
			foreach ( var a in d.LastActions )
			{
				Assert.True( a.ExpiryTick > tick, "non-future expiry at tick " + tick );
				Assert.True( ids.Add( a.ActionId ), "duplicate id " + a.ActionId );
				Assert.Equal( "a" + tick + "-0", a.ActionId );
			}
			foreach ( var kv in d.State.Timers )
				Assert.True( kv.Value >= 0.0, "timer " + kv.Key + " negative" );
			foreach ( var m in d.State.Memories )
			{
				Assert.InRange( m.DecayedConfidence, 0.0, 1.0 );
				Assert.InRange( m.BaseConfidence, 0.0, 1.0 );
			}
			if ( d.State.AwaitingActionId.Length > 0 )
				Assert.True( d.State.PendingActions.ContainsKey( d.State.AwaitingActionId ), "awaiting id not pending" );
			Assert.True( d.State.ConsecutiveNavFailures >= 0 );
		}
	}
}

using System.Collections.Generic;
using System.Globalization;
using SboxTwoBrains;

namespace TwoBrains.Examples;

/// <summary>
/// The omniscience switch: identical worlds, identical seed — the only difference is
/// WorldSnapshot.OmniscientTargets. Without it the monster must decide from its own
/// perception memory and, sensing nothing, idles. With it the host grants perfect target
/// knowledge: telemetry records omniscience_active (never silent) and the monster pursues.
/// Proves: omniscience_active appears only when the host opts in, and behavior diverges
/// accordingly.
/// </summary>
public static class OmnisciencePolicyExample
{
    public static ExampleResult Run()
    {
        var blind = RunScenario( false );
        var omniscient = RunScenario( true );

        var lines = new List<string>
        {
            "same scenario twice; prey 15 m away, never visible, no stimuli:",
            "  without omniscience: " + blind,
            "  with omniscience:    " + omniscient,
        };

        bool blindHadOmniscience = blind.Contains( "omniscience_active" );
        bool omniHadOmniscience = omniscient.Contains( "omniscience_active" );
        bool blindChased = blind.Contains( "Chase" );
        bool omniChased = omniscient.Contains( "Chase" );

        if ( blindHadOmniscience )
            return ExampleResult.Fail( lines, "omniscience_active appeared without the host switch" );
        if ( !omniHadOmniscience )
            return ExampleResult.Fail( lines, "omniscience_active missing when the host switch was set" );
        if ( blindChased || !omniChased )
            return ExampleResult.Fail( lines, "expected pursuit only under omniscience" );
        return ExampleResult.Pass( lines );
    }

    /// <summary>Runs 120 ticks; returns a compact digest of distinct codes and action kinds.</summary>
    private static string RunScenario( bool omniscientTargets )
    {
        var catalogue = new ProfileCatalogue().Add( new MonsterProfileConfig { Name = "demo" } );
        var system = new TwoBrainsSystem( catalogue, "demo", seed: 42UL );

        var codes = new SortedSet<string>( System.StringComparer.Ordinal );
        var kinds = new SortedSet<string>( System.StringComparer.Ordinal );
        var pendingAcks = new List<ActionResult>();
        const double dt = 1.0 / 60.0;

        for ( long tick = 0; tick < 120; tick++ )
        {
            var snapshot = new WorldSnapshot
            {
                TickIndex = tick,
                DeltaTimeSeconds = dt,
                Monster = new MonsterSnapshot { MonsterId = "m1", Position = Vec3.Zero, RegionId = "hall" },
                OmniscientTargets = omniscientTargets,
            };
            snapshot.Targets.Add( new TargetSnapshot { TargetId = "p1", Position = new Vec3( 15.0, 0.0, 0.0 ), RegionId = "hall", IsVisible = false } );
            snapshot.Acknowledgements.AddRange( pendingAcks );
            pendingAcks.Clear();

            var batch = system.Tick( snapshot );
            foreach ( var t in batch.Telemetry )
                codes.Add( t.Code );
            foreach ( var a in batch.Actions )
            {
                kinds.Add( a.Kind.ToString() );
                pendingAcks.Add( new ActionResult { ActionId = a.ActionId, Status = ActionStatus.Succeeded, ResultTick = tick } );
            }
        }

        return string.Format( CultureInfo.InvariantCulture, "actions [{0}], codes [{1}]",
            string.Join( ", ", kinds ), string.Join( ", ", codes ) );
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using SboxTwoBrains;

namespace TwoBrains.Examples;

/// <summary>
/// Telemetry inspection: runs a short visible-prey scenario and digests the telemetry
/// stream — event counts per category and per reason code, then the last 10 events raw.
/// Telemetry is the supported way to observe the policy (docs/TUNING.md): watch codes,
/// not eyeballing. Proves: the expected macro codes (candidate_latched,
/// mode_aggressive_start, opportunity_offered) are present in the stream.
/// </summary>
public static class TelemetryInspectionExample
{
    public static ExampleResult Run()
    {
        var catalogue = new ProfileCatalogue().Add( new MonsterProfileConfig
        {
            Name = "demo",
            Pressure = new PressureSection { FillSeconds = 0.5, AggressiveThresholdProgression = 0.9, CooldownSeconds = 5.0 },
        } );
        var system = new TwoBrainsSystem( catalogue, "demo", seed: 42UL );

        var monsterPos = Vec3.Zero;
        var pendingAcks = new List<ActionResult>();
        var all = new List<TelemetryEvent>();
        const double dt = 1.0 / 60.0;

        for ( long tick = 0; tick < 150; tick++ )
        {
            var targetPos = new Vec3( 10.0 + 1.5 * tick * dt, 0.0, 0.0 );
            var snapshot = new WorldSnapshot
            {
                TickIndex = tick,
                DeltaTimeSeconds = dt,
                Monster = new MonsterSnapshot { MonsterId = "m1", Position = monsterPos, RegionId = "hall" },
            };
            snapshot.Targets.Add( new TargetSnapshot { TargetId = "p1", Position = targetPos, RegionId = "hall", IsVisible = true } );
            snapshot.Acknowledgements.AddRange( pendingAcks );
            pendingAcks.Clear();

            var batch = system.Tick( snapshot );
            all.AddRange( batch.Telemetry );
            foreach ( var action in batch.Actions )
            {
                if ( action.Destination is Vec3 dest )
                {
                    var to = dest - monsterPos;
                    double distance = to.Length();
                    if ( distance > 0.0 )
                    {
                        double step = Math.Min( distance, 4.0 * action.SpeedScale * dt );
                        monsterPos = monsterPos + to * ( step / distance );
                    }
                }
                pendingAcks.Add( new ActionResult { ActionId = action.ActionId, Status = ActionStatus.Succeeded, ResultTick = tick } );
            }
            if ( batch.Macro != null && batch.Macro.OpportunityId.Length > 0 )
                pendingAcks.Add( new ActionResult { ActionId = batch.Macro.OpportunityId, Status = ActionStatus.Succeeded, ResultTick = tick } );
        }

        // Digest: counts per category, then per category+code, ordinal-sorted.
        var perCategory = new SortedDictionary<string, int>( StringComparer.Ordinal );
        var perCode = new SortedDictionary<string, int>( StringComparer.Ordinal );
        foreach ( var e in all )
        {
            perCategory[e.Category] = perCategory.TryGetValue( e.Category, out int c ) ? c + 1 : 1;
            string key = e.Category + "/" + e.Code;
            perCode[key] = perCode.TryGetValue( key, out int k ) ? k + 1 : 1;
        }

        var lines = new List<string>
        {
            string.Format( CultureInfo.InvariantCulture, "{0} telemetry events over 150 ticks.", all.Count ),
        };
        foreach ( var kv in perCategory )
            lines.Add( string.Format( CultureInfo.InvariantCulture, "category {0,-10} {1,4} events", kv.Key, kv.Value ) );
        lines.Add( "counts per reason code:" );
        foreach ( var kv in perCode )
            lines.Add( string.Format( CultureInfo.InvariantCulture, "  {0,-36} x{1}", kv.Key, kv.Value ) );
        lines.Add( "last 10 events:" );
        for ( int i = Math.Max( 0, all.Count - 10 ); i < all.Count; i++ )
            lines.Add( string.Format( CultureInfo.InvariantCulture, "  tick {0,3}  [{1,-10}] {2,-24} {3}", all[i].Tick, all[i].Category, all[i].Code, all[i].Message ) );

        foreach ( var expected in new[] { "candidate_latched", "mode_aggressive_start", "opportunity_offered" } )
        {
            if ( !perCode.ContainsKey( "macro/" + expected ) )
                return ExampleResult.Fail( lines, "expected telemetry code " + expected );
        }
        return ExampleResult.Pass( lines );
    }
}

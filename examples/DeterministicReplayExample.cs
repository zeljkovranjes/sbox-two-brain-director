using System;
using System.Collections.Generic;
using System.Globalization;
using SboxTwoBrains;

namespace TwoBrains.Examples;

/// <summary>
/// Deterministic replay: the same world, seed and host policy run twice for 200 ticks;
/// every per-tick DecisionBatch is serialized to canonical JSON and compared byte for byte,
/// together with the per-tick StateHash. Proves: identical inputs produce byte-identical
/// decisions — the foundation under save/replay, networking lockstep and regression tests.
/// </summary>
public static class DeterministicReplayExample
{
    private const int Ticks = 200;

    public static ExampleResult Run()
    {
        var runA = RunOnce();
        var runB = RunOnce();

        int firstMismatch = -1;
        for ( int i = 0; i < runA.Count; i++ )
        {
            if ( runA[i] != runB[i] )
            {
                firstMismatch = i;
                break;
            }
        }

        ulong finalHashA = CanonicalJson.Hash( runA[runA.Count - 1] );
        var lines = new List<string>
        {
            string.Format( CultureInfo.InvariantCulture, "two independent runs x {0} ticks; per-tick canonical JSON + StateHash compared byte for byte.", Ticks ),
            string.Format( CultureInfo.InvariantCulture, "run A final-batch FNV-1a hash prefix: {0:X16}", finalHashA ),
            firstMismatch < 0 ? "all 200 ticks byte-identical." : "first mismatch at tick " + firstMismatch,
        };

        if ( firstMismatch >= 0 )
            return ExampleResult.Fail( lines, "runs diverged at tick " + firstMismatch );
        return ExampleResult.Pass( lines );
    }

    /// <summary>One full deterministic run; returns the canonical JSON of every batch.</summary>
    private static List<string> RunOnce()
    {
        var catalogue = new ProfileCatalogue().Add( new MonsterProfileConfig
        {
            Name = "replay-demo",
            Pressure = new PressureSection { FillSeconds = 0.5, AggressiveThresholdProgression = 0.9, CooldownSeconds = 5.0 },
        } );
        var system = new TwoBrainsSystem( catalogue, "replay-demo", seed: 42UL );

        var monsterPos = Vec3.Zero;
        var pendingAcks = new List<ActionResult>();
        var json = new List<string>( Ticks );
        const double dt = 1.0 / 60.0;

        for ( long tick = 0; tick < Ticks; tick++ )
        {
            var targetPos = new Vec3( 10.0 + 1.5 * tick * dt, 0.0, 0.0 ); // prey walks away at 1.5 m/s
            var snapshot = new WorldSnapshot
            {
                TickIndex = tick,
                DeltaTimeSeconds = dt,
                Monster = new MonsterSnapshot { MonsterId = "m1", Position = monsterPos, RegionId = "hall" },
            };
            snapshot.Targets.Add( new TargetSnapshot
            {
                TargetId = "p1",
                Position = targetPos,
                RegionId = "hall",
                IsVisible = monsterPos.PlanarDistanceTo( targetPos ) < 25.0,
            } );
            snapshot.Acknowledgements.AddRange( pendingAcks );
            pendingAcks.Clear();

            var batch = system.Tick( snapshot );
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

            json.Add( CanonicalJson.ToJson( batch ) ); // StateHash is part of the serialized batch
        }
        return json;
    }
}

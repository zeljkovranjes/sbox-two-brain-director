using System;
using System.Collections.Generic;
using System.Globalization;
using SboxTwoBrains;

namespace TwoBrains.Examples;

/// <summary>
/// Save/restore: an uninterrupted 200-tick run is the reference. A second run pauses at
/// tick 100, captures a SavedStateEnvelope, round-trips it through canonical JSON, restores
/// it into a BRAND-NEW system (same catalogue, profile and seed) and continues. The host
/// keeps its own world state; only the brain is serialized. Proves: output from tick 100
/// onward is byte-identical to the uninterrupted run.
/// </summary>
public static class SaveRestoreExample
{
    private const int Ticks = 200;
    private const int SaveAtTick = 100;

    public static ExampleResult Run()
    {
        var reference = RunWorld( false, out _ );
        var restored = RunWorld( true, out int savedBytes );

        int compared = 0;
        int firstMismatch = -1;
        for ( int i = SaveAtTick; i < Ticks; i++ )
        {
            compared++;
            if ( reference[i] != restored[i] )
            {
                firstMismatch = i;
                break;
            }
        }

        var lines = new List<string>
        {
            string.Format( CultureInfo.InvariantCulture, "saved at tick {0} ({1} bytes of canonical JSON), restored into a new system, resumed at tick {2}.",
                SaveAtTick, savedBytes, SaveAtTick ),
            string.Format( CultureInfo.InvariantCulture, "compared canonical batch JSON for ticks {0}..{1} ({2} batches): {3}",
                SaveAtTick, Ticks - 1, compared, firstMismatch < 0 ? "all identical" : "mismatch at tick " + firstMismatch ),
        };

        if ( firstMismatch >= 0 )
            return ExampleResult.Fail( lines, "restored run diverged at tick " + firstMismatch );
        return ExampleResult.Pass( lines );
    }

    /// <summary>Runs the world; when <paramref name="saveAndRestore"/> is set, swaps the brain for a deserialized copy of itself at SaveAtTick.</summary>
    private static List<string> RunWorld( bool saveAndRestore, out int savedBytes )
    {
        var catalogue = new ProfileCatalogue().Add( new MonsterProfileConfig
        {
            Name = "save-demo",
            Pressure = new PressureSection { FillSeconds = 0.5, AggressiveThresholdProgression = 0.9, CooldownSeconds = 5.0 },
        } );
        var system = new TwoBrainsSystem( catalogue, "save-demo", seed: 42UL );

        var monsterPos = Vec3.Zero;
        var pendingAcks = new List<ActionResult>();
        var json = new List<string>( Ticks );
        savedBytes = 0;
        const double dt = 1.0 / 60.0;

        for ( long tick = 0; tick < Ticks; tick++ )
        {
            if ( saveAndRestore && tick == SaveAtTick )
            {
                // Persist anywhere, then resume from the persisted bytes — not the live object.
                string savedJson = CanonicalJson.ToJson( system.CaptureState() );
                savedBytes = savedJson.Length;
                system = new TwoBrainsSystem( catalogue, "save-demo", seed: 42UL );
                system.RestoreState( CanonicalJson.FromJson<SavedStateEnvelope>( savedJson ) );
            }

            var targetPos = new Vec3( 10.0 + 1.5 * tick * dt, 0.0, 0.0 );
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

            json.Add( CanonicalJson.ToJson( batch ) );
        }
        return json;
    }
}

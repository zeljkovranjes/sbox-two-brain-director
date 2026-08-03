using System;
using System.Collections.Generic;
using System.Globalization;
using SboxTwoBrains;

namespace TwoBrains.Examples;

/// <summary>
/// The whole host contract in one small file, no helpers: build a catalogue, construct the
/// system, then loop 120 ticks of hand-built snapshots — executing action requests by hand
/// and acknowledging them (and the macro opportunity) on the NEXT tick.
/// Proves: the gauge fills while a candidate is latched and fires an Aggressive transition
/// (telemetry: candidate_latched -> mode_aggressive_start -> opportunity_offered).
/// </summary>
public static class MinimalManualHostExample
{
    public static ExampleResult Run()
    {
        // 1. Catalogue: one demo profile tuned for a fast first transition (fill 0.5 s,
        //    threshold 0.9 — with dt 1/60 the gauge crosses the threshold near tick 67).
        var catalogue = new ProfileCatalogue().Add( new MonsterProfileConfig
        {
            Name = "demo",
            Pressure = new PressureSection { FillSeconds = 0.5, AggressiveThresholdProgression = 0.9, CooldownSeconds = 5.0 },
        } );
        var system = new TwoBrainsSystem( catalogue, "demo", seed: 42UL );

        // 2. World: the monster in "hall", one visible prey 12 m away.
        var monsterPos = Vec3.Zero;
        var targetPos = new Vec3( 12.0, 0.0, 0.0 );
        var pendingAcks = new List<ActionResult>();
        var transitions = new List<string>();
        const double dt = 1.0 / 60.0;

        for ( long tick = 0; tick < 120; tick++ )
        {
            // 3. Snapshot: world state plus the acknowledgements produced since last tick.
            var snapshot = new WorldSnapshot
            {
                TickIndex = tick,
                DeltaTimeSeconds = dt,
                Monster = new MonsterSnapshot { MonsterId = "m1", Position = monsterPos, RegionId = "hall" },
            };
            snapshot.Targets.Add( new TargetSnapshot { TargetId = "p1", Position = targetPos, RegionId = "hall", IsVisible = true } );
            snapshot.Acknowledgements.AddRange( pendingAcks );
            pendingAcks.Clear();

            DecisionBatch batch = system.Tick( snapshot );
            foreach ( var t in batch.Telemetry )
            {
                if ( t.Code == "candidate_latched" || t.Code == "mode_aggressive_start" || t.Code == "opportunity_offered" || t.Code == "opportunity_completed" )
                    transitions.Add( string.Format( CultureInfo.InvariantCulture, "tick {0,3}  {1,-24} {2}", t.Tick, t.Code, t.Message ) );
            }

            // 4. Execute: walk toward destinations at 4 m/s scaled; ack on the next tick.
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

            // 5. The macro opportunity is acknowledged over the same channel.
            if ( batch.Macro != null && batch.Macro.OpportunityId.Length > 0 )
                pendingAcks.Add( new ActionResult { ActionId = batch.Macro.OpportunityId, Status = ActionStatus.Succeeded, ResultTick = tick } );
        }

        var lines = new List<string> { "Macro transitions over 120 hand-driven ticks (target visible, prey stationary):" };
        lines.AddRange( transitions );
        lines.Add( string.Format( CultureInfo.InvariantCulture, "Final: mode={0} progression={1:F3} completed={2}",
            system.MacroState.Mode, system.MacroState.Progression, system.MacroState.CompletedOpportunities ) );

        bool aggressive = transitions.Exists( l => l.Contains( "mode_aggressive_start" ) );
        if ( !aggressive )
            return ExampleResult.Fail( lines, "expected an aggressive transition within 120 ticks" );
        return ExampleResult.Pass( lines );
    }
}

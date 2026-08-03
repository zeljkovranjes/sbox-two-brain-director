using System.Collections.Generic;
using System.Globalization;
using SboxTwoBrains;

namespace TwoBrains.Examples;

/// <summary>
/// Pressure tuning knobs in action (docs/TUNING.md): MaxOpportunities caps the session's
/// aggressive cycles — after one completed opportunity the refilled gauge hits the
/// quota_blocked wall instead of a new transition. Then the prey leaves: the latch clears,
/// the DecreaseDelaySeconds grace runs, and the gauge drains to zero at 1/DecreaseSeconds.
/// Proves: quota_blocked telemetry appears and the gauge decreases exactly to zero.
/// </summary>
public static class PressureTuningExample
{
    public static ExampleResult Run()
    {
        var catalogue = new ProfileCatalogue().Add( new MonsterProfileConfig
        {
            Name = "tuning-demo",
            Pressure = new PressureSection
            {
                FillSeconds = 0.5,
                AggressiveThresholdProgression = 0.9,
                CooldownSeconds = 1.0,
                MaxOpportunities = 1, // one set-piece per session, then the quota wall
                DecreaseSeconds = 1.0,
                DecreaseDelaySeconds = 0.5,
            },
        } );
        var system = new TwoBrainsSystem( catalogue, "tuning-demo", seed: 42UL );

        var pendingAcks = new List<ActionResult>();
        var lines = new List<string> { "quota exhaustion, then gauge decrease after the prey escapes:" };
        var target = new TargetSnapshot { TargetId = "p1", Position = new Vec3( 10.0, 0.0, 0.0 ), RegionId = "hall", IsVisible = true };
        long firstBlockedAt = -1;
        long clearedAt = -1;
        bool completed = false;
        const double dt = 1.0 / 60.0;

        for ( long tick = 0; tick < 600; tick++ )
        {
            // The prey escapes 30 ticks after the quota wall first shows.
            if ( firstBlockedAt >= 0 && tick == firstBlockedAt + 30 )
                target.IsValid = false;

            var snapshot = new WorldSnapshot
            {
                TickIndex = tick,
                DeltaTimeSeconds = dt,
                Monster = new MonsterSnapshot { MonsterId = "m1", Position = Vec3.Zero, RegionId = "hall" },
            };
            snapshot.Targets.Add( target );
            snapshot.Acknowledgements.AddRange( pendingAcks );
            pendingAcks.Clear();

            var batch = system.Tick( snapshot );
            foreach ( var a in batch.Actions )
                pendingAcks.Add( new ActionResult { ActionId = a.ActionId, Status = ActionStatus.Succeeded, ResultTick = tick } );
            if ( batch.Macro != null && batch.Macro.OpportunityId.Length > 0 )
                pendingAcks.Add( new ActionResult { ActionId = batch.Macro.OpportunityId, Status = ActionStatus.Succeeded, ResultTick = tick } );

            foreach ( var t in batch.Telemetry )
            {
                if ( t.Code == "opportunity_completed" && !completed )
                {
                    completed = true;
                    lines.Add( string.Format( CultureInfo.InvariantCulture, "tick {0,3}  opportunity_completed (count now {1}/{2})",
                        tick, system.MacroState.CompletedOpportunities, system.Config.Pressure.MaxOpportunities ) );
                }
                else if ( t.Code == "quota_blocked" && firstBlockedAt < 0 )
                {
                    firstBlockedAt = t.Tick;
                    lines.Add( string.Format( CultureInfo.InvariantCulture, "tick {0,3}  quota_blocked — gauge full but the session cap is spent ({1})", t.Tick, t.Message ) );
                }
                else if ( t.Code == "candidate_cleared" && clearedAt < 0 )
                {
                    clearedAt = t.Tick;
                    lines.Add( string.Format( CultureInfo.InvariantCulture, "tick {0,3}  candidate_cleared — grace {1:0.#}s, then decrease at 1/{2:0.#}s",
                        t.Tick, system.Config.Pressure.DecreaseDelaySeconds, system.Config.Pressure.DecreaseSeconds ) );
                }
            }

            // Sample the gauge once per 10 ticks after the latch clears (grace, then drain).
            if ( clearedAt >= 0 && system.MacroState.Progression > 0.0 && tick % 10 == 0 )
                lines.Add( string.Format( CultureInfo.InvariantCulture, "tick {0,3}  progression: {1:F3}{2}", tick, system.MacroState.Progression,
                    system.MacroState.DecreaseDelayRemaining > 0.0 ? " (grace)" : "" ) );

            if ( clearedAt >= 0 && system.MacroState.Progression == 0.0 )
            {
                lines.Add( string.Format( CultureInfo.InvariantCulture, "tick {0,3}  progression reached zero.", tick ) );
                break;
            }
        }

        if ( !completed )
            return ExampleResult.Fail( lines, "expected one completed opportunity" );
        if ( firstBlockedAt < 0 )
            return ExampleResult.Fail( lines, "expected quota_blocked telemetry after the cap" );
        if ( clearedAt < 0 )
            return ExampleResult.Fail( lines, "expected candidate_cleared after the prey escaped" );
        if ( system.MacroState.Progression != 0.0 )
            return ExampleResult.Fail( lines, "expected the gauge to decrease to exactly zero" );
        return ExampleResult.Pass( lines );
    }
}

using System.Collections.Generic;
using System.Globalization;
using SboxTwoBrains;

namespace TwoBrains.Examples;

/// <summary>
/// Offstage staging: a world with one offstage region ("vents") adjacent to the pressured
/// region ("hall") and one approved ingress point. When the macro goes Aggressive with the
/// "sweeper" role, the monster takes the vent, sweeps the offstage nodes with dwells, and
/// leaves when the sweep window ends. Proves: a UseIngress action is issued and offstage
/// sweep telemetry (ingress_use / sweep_move / sweep_dwell / sweep_end) appears.
/// </summary>
public static class OffstageStalkerExample
{
    public static ExampleResult Run()
    {
        // Fast transition (fill 0.5 s / threshold 0.9) and a short 1.5 s sweep window so the
        // whole arc fits in a few hundred ticks; dwells are short for the same reason.
        var catalogue = new ProfileCatalogue().Add( new MonsterProfileConfig
        {
            Name = "sweeper-demo",
            Pressure = new PressureSection { FillSeconds = 0.5, AggressiveThresholdProgression = 0.9, CooldownSeconds = 2.0, SweepDurationSeconds = 1.5 },
            Offstage = new OffstageSection { NodeDwellMinSeconds = 0.2, NodeDwellMaxSeconds = 0.4 },
        } );
        var system = new TwoBrainsSystem( catalogue, "sweeper-demo", seed: 42UL );

        // World. The prey stays invisible in "hall"; the macro pressures its region anyway
        // (candidacy is about viability, not visibility). No frontstage nodes exist, so the
        // Stalk module yields and the Offstage module owns the aggressive window.
        var monsterPos = Vec3.Zero;
        var presence = StagePresence.Frontstage;
        var target = new TargetSnapshot { TargetId = "p1", Position = new Vec3( 20.0, 0.0, 0.0 ), RegionId = "hall", IsVisible = false };

        var pendingAcks = new List<ActionResult>();
        var executing = new List<ActionRequest>();
        var lines = new List<string> { "Ingress/sweep trace (prey invisible in 'hall', vent leads to 'vents'):" };
        var telemetryCodes = new HashSet<string>();
        int ingressActions = 0;
        const double dt = 1.0 / 60.0;

        for ( long tick = 0; tick < 900; tick++ )
        {
            // Execute in-flight requests: walk to MoveTo destinations; ingress traverses instantly.
            for ( int i = executing.Count - 1; i >= 0; i-- )
            {
                var req = executing[i];
                bool done = true;
                if ( req.Kind == ActionKind.MoveTo && req.Destination is Vec3 dest )
                {
                    var to = dest - monsterPos;
                    double distance = to.Length();
                    double step = 6.0 * req.SpeedScale * dt;
                    if ( distance > step && distance > 0.25 )
                    {
                        monsterPos = monsterPos + to * ( step / distance );
                        done = false;
                    }
                }
                if ( done )
                {
                    pendingAcks.Add( new ActionResult { ActionId = req.ActionId, Status = ActionStatus.Succeeded, ResultTick = tick } );
                    if ( req.Kind == ActionKind.UseIngress )
                        presence = presence == StagePresence.Frontstage ? StagePresence.Offstage : StagePresence.Frontstage;
                    executing.RemoveAt( i );
                }
            }

            var snapshot = new WorldSnapshot
            {
                TickIndex = tick,
                DeltaTimeSeconds = dt,
                Monster = new MonsterSnapshot { MonsterId = "m1", Position = monsterPos, RegionId = "hall", Presence = presence },
            };
            snapshot.Targets.Add( target );
            snapshot.NavCandidates.Add( new NavCandidate { NodeId = "on1", Kind = NavCandidateKind.OffstageNode, Position = new Vec3( 1.0, 0.0, 1.0 ), RegionId = "vents" } );
            snapshot.NavCandidates.Add( new NavCandidate { NodeId = "on2", Kind = NavCandidateKind.OffstageNode, Position = new Vec3( 2.0, 0.0, 1.0 ), RegionId = "vents" } );
            snapshot.OffstageRegions.Add( new OffstageRegion { RegionId = "vents", NodeIds = { "on1", "on2" }, IngressIds = { "vent1" }, AdjacentRegionIds = { "hall" } } );
            snapshot.IngressPoints.Add( new IngressPoint { IngressId = "vent1", Kind = IngressKind.Vent, Position = new Vec3( 1.0, 0.0, 0.0 ), RegionId = "hall", OffstageNodeId = "on1", Usable = true } );
            snapshot.Acknowledgements.AddRange( pendingAcks );
            pendingAcks.Clear();

            var batch = system.Tick( snapshot );
            executing.AddRange( batch.Actions );
            foreach ( var a in batch.Actions )
            {
                if ( a.Kind == ActionKind.UseIngress )
                {
                    ingressActions++;
                    lines.Add( string.Format( CultureInfo.InvariantCulture, "tick {0,3}  UseIngress {1} (presence -> {2})",
                        tick, a.IngressId, presence == StagePresence.Frontstage ? "offstage" : "frontstage" ) );
                }
            }
            foreach ( var t in batch.Telemetry )
            {
                if ( ( t.Code == "sweep_move" || t.Code == "sweep_dwell" || t.Code == "sweep_end" || t.Code == "ingress_use" ) && telemetryCodes.Add( t.Code ) )
                    lines.Add( string.Format( CultureInfo.InvariantCulture, "tick {0,3}  telemetry {1,-12} {2}", t.Tick, t.Code, t.Message ) );
            }
        }

        if ( ingressActions == 0 )
            return ExampleResult.Fail( lines, "expected at least one UseIngress action" );
        if ( !telemetryCodes.Contains( "ingress_use" ) || !telemetryCodes.Contains( "sweep_move" ) )
            return ExampleResult.Fail( lines, "expected ingress_use and sweep_move telemetry" );
        return ExampleResult.Pass( lines );
    }
}

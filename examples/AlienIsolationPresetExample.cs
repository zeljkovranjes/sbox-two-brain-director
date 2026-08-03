using System;
using System.Collections.Generic;
using System.Globalization;
using SboxTwoBrains;

namespace TwoBrains.Examples;

/// <summary>
/// The Alien: Isolation-inspired compatibility preset: 12 verbatim decoded intensity
/// records, a ready-made catalogue, and the headliner profile that inherits DEFAULT.
/// The values are proven decoded data, but their runtime semantics are a strong
/// reconstruction — see docs/EVIDENCE.md for confidence labels before tuning against them.
/// Proves: the preset values survive mapping (fill=3 / cooldown=25 / max=4 for DEFAULT),
/// the headliner resolves, and a 200-tick scenario runs deterministically on it.
/// </summary>
public static class AlienIsolationPresetExample
{
    public static ExampleResult Run()
    {
        var lines = new List<string>
        {
            "the 12 decoded intensity records (verbatim data; semantics: docs/EVIDENCE.md):",
            string.Format( CultureInfo.InvariantCulture, "  {0,-22} {1,4} {2,8} {3,4}", "name", "fill", "cooldown", "max" ),
        };
        foreach ( var record in AlienIsolationPresets.All() )
        {
            lines.Add( string.Format( CultureInfo.InvariantCulture, "  {0,-22} {1,4:0.#} {2,8:0.#} {3,4:0.#}",
                record.Name, record.MenaceGaugeSecondsToFill, record.MenaceCoolDownTime, record.MaxMenaces ) );
        }

        var catalogue = AlienIsolationPresets.CreateCatalogue();
        var headliner = catalogue.Resolve( AlienIsolationPresets.InspiredProfileName );
        lines.Add( string.Format( CultureInfo.InvariantCulture,
            "headliner '{0}' resolves: fill={1:0.#}s cooldown={2:0.#}s maxOpportunities={3} (inherited from DEFAULT).",
            AlienIsolationPresets.InspiredProfileName, headliner.Pressure.FillSeconds,
            headliner.Pressure.CooldownSeconds, headliner.Pressure.MaxOpportunities ) );

        // A short scenario on the headliner: visible prey walking away, next-tick acks.
        var batches = RunPresetScenario( catalogue, out bool latched );
        lines.Add( string.Format( CultureInfo.InvariantCulture,
            "200-tick preset scenario: {0} batches, candidate_latched={1}, final batch hash={2:X16}.",
            batches.Count, latched, CanonicalJson.Hash( batches[batches.Count - 1] ) ) );

        // Spot-check the verbatim values through the mapping (DEFAULT is the chain root).
        var def = catalogue.Resolve( "DEFAULT" );
        if ( def.Pressure.FillSeconds != 3.0 || def.Pressure.CooldownSeconds != 25.0 || def.Pressure.MaxOpportunities != 4 )
            return ExampleResult.Fail( lines, "DEFAULT preset values did not survive mapping" );
        if ( headliner.Pressure.FillSeconds != 3.0 )
            return ExampleResult.Fail( lines, "headliner should inherit DEFAULT fill" );
        if ( AlienIsolationPresets.All().Length != 12 )
            return ExampleResult.Fail( lines, "expected exactly 12 usable records" );
        if ( !latched )
            return ExampleResult.Fail( lines, "expected the preset director to latch a candidate" );
        return ExampleResult.Pass( lines );
    }

    /// <summary>200 ticks on the headliner profile; returns per-tick canonical batch JSON.</summary>
    private static List<string> RunPresetScenario( ProfileCatalogue catalogue, out bool latched )
    {
        var system = new TwoBrainsSystem( catalogue, AlienIsolationPresets.InspiredProfileName, seed: 42UL );
        var monsterPos = Vec3.Zero;
        var pendingAcks = new List<ActionResult>();
        var json = new List<string>( 200 );
        latched = false;
        const double dt = 1.0 / 60.0;

        for ( long tick = 0; tick < 200; tick++ )
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
            foreach ( var t in batch.Telemetry )
                if ( t.Code == "candidate_latched" )
                    latched = true;
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

using System.Collections.Generic;
using System.Globalization;
using SboxTwoBrains;

namespace TwoBrains.Examples;

/// <summary>
/// Game-defined senses: games add their own channels via SenseChannel.GameDefined plus a
/// Subtype string — here a "thermal" ping and a faint "radio" blip. The GameDefined
/// perception channel applies its confidence threshold like any built-in sense, so the
/// strong thermal trace is investigated while the sub-threshold radio blip never registers.
/// Proves: the agent investigates the game-defined channel (investigate_react and an action
/// carrying the thermal stimulus id) and ignores the weak one.
/// </summary>
public static class CustomSensesExample
{
    public static ExampleResult Run()
    {
        var catalogue = new ProfileCatalogue().Add( new MonsterProfileConfig { Name = "demo" } );
        var system = new TwoBrainsSystem( catalogue, "demo", seed: 42UL );

        var monsterPos = Vec3.Zero;
        var pendingAcks = new List<ActionResult>();
        var executing = new List<ActionRequest>();
        var chain = new List<string>();
        var stimulusActions = new HashSet<string>();
        bool investigatedDone = false;
        const double dt = 1.0 / 60.0;

        for ( long tick = 0; tick < 300 && !investigatedDone; tick++ )
        {
            // Advance in-flight movement (fixed speed 6 m/s at scale 1), ack next tick.
            for ( int i = executing.Count - 1; i >= 0; i-- )
            {
                var req = executing[i];
                bool done = true;
                if ( req.Destination is Vec3 dest )
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
                    executing.RemoveAt( i );
                }
            }

            var snapshot = new WorldSnapshot
            {
                TickIndex = tick,
                DeltaTimeSeconds = dt,
                Monster = new MonsterSnapshot { MonsterId = "m1", Position = monsterPos, RegionId = "lab" },
            };
            if ( tick == 5 )
            {
                snapshot.CurrentStimuli.Add( new Stimulus
                {
                    StimulusId = "thermal-1", Channel = SenseChannel.GameDefined, Subtype = "thermal",
                    Position = new Vec3( 4.0, 0.0, 0.0 ), RegionId = "lab", Confidence = 0.9, CreatedTick = tick, LastConfirmedTick = tick,
                } );
                snapshot.CurrentStimuli.Add( new Stimulus
                {
                    StimulusId = "radio-1", Channel = SenseChannel.GameDefined, Subtype = "radio",
                    Position = new Vec3( 3.0, 0.0, 2.0 ), RegionId = "lab", Confidence = 0.1, CreatedTick = tick, LastConfirmedTick = tick,
                } );
            }
            snapshot.Acknowledgements.AddRange( pendingAcks );
            pendingAcks.Clear();

            var batch = system.Tick( snapshot );
            executing.AddRange( batch.Actions );
            foreach ( var a in batch.Actions )
            {
                if ( !string.IsNullOrEmpty( a.StimulusId ) )
                    stimulusActions.Add( a.StimulusId );
            }
            foreach ( var t in batch.Telemetry )
            {
                if ( t.Code.StartsWith( "investigate_", System.StringComparison.Ordinal ) )
                    chain.Add( string.Format( CultureInfo.InvariantCulture, "tick {0,3}  {1,-22} {2}", t.Tick, t.Code, t.Message ) );
                if ( t.Code == "investigate_done" )
                    investigatedDone = true;
            }
        }

        var lines = new List<string>
        {
            "two GameDefined stimuli at tick 5: 'thermal' (confidence 0.9) and 'radio' (0.1, below the 0.3 channel threshold).",
            "investigation chain:",
        };
        lines.AddRange( chain );
        lines.Add( "stimulus ids referenced by actions: " + ( stimulusActions.Count > 0 ? string.Join( ", ", stimulusActions ) : "none" ) );

        if ( !stimulusActions.Contains( "thermal-1" ) )
            return ExampleResult.Fail( lines, "expected an action carrying the thermal stimulus id" );
        if ( stimulusActions.Contains( "radio-1" ) )
            return ExampleResult.Fail( lines, "the sub-threshold radio blip should never be acted on" );
        if ( chain.Count == 0 )
            return ExampleResult.Fail( lines, "expected investigate_* telemetry for the thermal trace" );
        return ExampleResult.Pass( lines );
    }
}

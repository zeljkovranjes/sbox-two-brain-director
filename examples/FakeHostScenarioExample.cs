using System.Collections.Generic;
using System.Globalization;
using SboxTwoBrains;

namespace TwoBrains.Examples;

/// <summary>
/// A minimal embedded fake host (~60 lines, distilled from dev/SboxTwoBrains.Tests/FakeHost
/// — examples are self-contained, so the tiny bits are copied, not referenced) running a
/// lost-sight-into-search scenario: the prey is never visible, but one footstep noise pulls
/// the monster through investigate (react -> approach -> inspect -> done) into a systematic
/// search of the region. Proves: ActionKind.Search actions appear after the handoff.
/// </summary>
public static class FakeHostScenarioExample
{
    /// <summary>Fixed-speed movement, next-tick acks; all randomness lives in the system.</summary>
    private sealed class MiniHost
    {
        public TwoBrainsSystem System;
        public Vec3 MonsterPos;
        public long Tick;
        public const double Dt = 1.0 / 60.0;
        public const double MaxSpeed = 6.0; // m/s at SpeedScale 1
        public readonly List<TargetSnapshot> Targets = new List<TargetSnapshot>();
        public readonly List<NavCandidate> Nav = new List<NavCandidate>();
        public readonly List<Stimulus> Stimuli = new List<Stimulus>();
        private readonly List<ActionRequest> _executing = new List<ActionRequest>();
        private readonly List<ActionResult> _acks = new List<ActionResult>();

        public DecisionBatch Step()
        {
            // Advance executions; movement acks on arrival, everything else next tick.
            _acks.Clear();
            for ( int i = _executing.Count - 1; i >= 0; i-- )
            {
                var req = _executing[i];
                bool done = true;
                if ( req.Destination is Vec3 dest )
                {
                    var to = dest - MonsterPos;
                    double distance = to.Length();
                    double step = MaxSpeed * req.SpeedScale * Dt;
                    if ( distance > step && distance > 0.5 )
                    {
                        MonsterPos = MonsterPos + to * ( step / distance );
                        done = false; // still walking — no ack yet, like a real engine
                    }
                    else
                    {
                        MonsterPos = dest;
                    }
                }
                if ( done )
                {
                    _acks.Add( new ActionResult { ActionId = req.ActionId, Status = ActionStatus.Succeeded, Detail = "done", ResultTick = Tick } );
                    _executing.RemoveAt( i );
                }
            }

            var snapshot = new WorldSnapshot
            {
                TickIndex = Tick,
                DeltaTimeSeconds = Dt,
                Monster = new MonsterSnapshot { MonsterId = "m1", Position = MonsterPos, RegionId = "corridor" },
            };
            snapshot.Targets.AddRange( Targets );
            snapshot.CurrentStimuli.AddRange( Stimuli );
            snapshot.NavCandidates.AddRange( Nav );
            snapshot.Acknowledgements.AddRange( _acks );
            Stimuli.Clear();

            var batch = System.Tick( snapshot );
            _executing.AddRange( batch.Actions );
            Tick++;
            return batch;
        }
    }

    public static ExampleResult Run()
    {
        var catalogue = new ProfileCatalogue().Add( new MonsterProfileConfig { Name = "demo" } );
        var host = new MiniHost { System = new TwoBrainsSystem( catalogue, "demo", seed: 42UL ) };

        // The prey is in "hall" but never visible; only its footstep gives it away.
        host.Targets.Add( new TargetSnapshot { TargetId = "p1", Position = new Vec3( 14.0, 0.0, 0.0 ), RegionId = "hall", IsVisible = false } );
        host.Nav.Add( new NavCandidate { NodeId = "n1", Position = new Vec3( 5.0, 0.0, 2.0 ), RegionId = "hall" } );
        host.Nav.Add( new NavCandidate { NodeId = "n2", Position = new Vec3( 9.0, 0.0, 3.0 ), RegionId = "hall" } );

        var seen = new List<string>();
        var firstKindAt = new Dictionary<ActionKind, long>();
        bool searchIssued = false;
        for ( int i = 0; i < 600 && !searchIssued; i++ )
        {
            if ( host.Tick == 5 )
            {
                host.Stimuli.Add( new Stimulus
                {
                    StimulusId = "footstep-1", Channel = SenseChannel.Auditory, Subtype = "footstep",
                    Position = new Vec3( 6.0, 0.0, 1.0 ), RegionId = "hall", Confidence = 0.9,
                    CreatedTick = 5, LastConfirmedTick = 5,
                } );
            }
            var batch = host.Step();
            foreach ( var a in batch.Actions )
            {
                if ( !firstKindAt.ContainsKey( a.Kind ) )
                {
                    firstKindAt.Add( a.Kind, a.ReasonCode == "idle" ? -1 : batch.TickIndex );
                    if ( a.ReasonCode != "idle" )
                        seen.Add( string.Format( CultureInfo.InvariantCulture, "tick {0,3}  first {1,-11} (reason={2})", batch.TickIndex, a.Kind, a.ReasonCode ) );
                }
                if ( a.Kind == ActionKind.Search )
                    searchIssued = true;
            }
        }

        var lines = new List<string>
        {
            "Prey never visible; one auditory footstep at tick 5. Action first-appearances:",
        };
        lines.AddRange( seen );
        lines.Add( searchIssued
            ? "Search episode began after investigate handed off (investigate_done -> search_start)."
            : "No search episode observed." );

        if ( !searchIssued )
            return ExampleResult.Fail( lines, "expected ActionKind.Search after the investigate handoff" );
        if ( !firstKindAt.ContainsKey( ActionKind.MoveTo ) )
            return ExampleResult.Fail( lines, "expected an investigate-approach MoveTo first" );
        return ExampleResult.Pass( lines );
    }
}

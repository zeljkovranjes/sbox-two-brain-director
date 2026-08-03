using System.Collections.Generic;
using System.Globalization;
using SboxTwoBrains;

namespace TwoBrains.Examples;

/// <summary>
/// Directive showcase: a scripted encounter drives the policy with explicit orders at fixed
/// ticks — SetPressureMode, SetProgression, ResetPressure (macro) and ForceWithdrawal,
/// PlayScriptedSequence (micro). Every override lands in telemetry as a reason-coded event.
/// Proves: script_set_mode and script_withdrawal telemetry appear exactly when ordered.
/// </summary>
public static class ScriptedEncounterExample
{
    public static ExampleResult Run()
    {
        var catalogue = new ProfileCatalogue().Add( new MonsterProfileConfig { Name = "script-demo" } );
        var system = new TwoBrainsSystem( catalogue, "script-demo", seed: 42UL );

        // The encounter script: tick -> directives delivered in that tick's snapshot.
        var script = new Dictionary<long, ScriptDirective[]>
        {
            [10] = new[] { new ScriptDirective { DirectiveId = "d-mode", Kind = ScriptDirectiveKind.SetPressureMode, Mode = PressureMode.Aggressive, Progression = 0.5 } },
            [20] = new[] { new ScriptDirective { DirectiveId = "d-prog", Kind = ScriptDirectiveKind.SetProgression, Progression = 0.75 } },
            [30] = new[] { new ScriptDirective { DirectiveId = "d-reset", Kind = ScriptDirectiveKind.ResetPressure, ResetGauge = false } },
            [40] = new[] { new ScriptDirective { DirectiveId = "d-flee", Kind = ScriptDirectiveKind.ForceWithdrawal } },
            [50] = new[] { new ScriptDirective { DirectiveId = "d-roar", Kind = ScriptDirectiveKind.PlayScriptedSequence, SequenceName = "intro_roar" } },
        };

        var pendingAcks = new List<ActionResult>();
        var overrides = new List<string>();
        var seenCodes = new HashSet<string>();
        var scriptedActions = new List<string>();
        const double dt = 1.0 / 60.0;

        for ( long tick = 0; tick < 70; tick++ )
        {
            var snapshot = new WorldSnapshot
            {
                TickIndex = tick,
                DeltaTimeSeconds = dt,
                Monster = new MonsterSnapshot { MonsterId = "m1", Position = Vec3.Zero, RegionId = "stage" },
            };
            if ( script.TryGetValue( tick, out var directives ) )
                snapshot.Directives.AddRange( directives );
            snapshot.Acknowledgements.AddRange( pendingAcks );
            pendingAcks.Clear();

            var batch = system.Tick( snapshot );
            foreach ( var t in batch.Telemetry )
            {
                if ( t.Code.StartsWith( "script_", System.StringComparison.Ordinal ) || t.Code == "reset" || t.Code.StartsWith( "opportunity_", System.StringComparison.Ordinal ) )
                {
                    seenCodes.Add( t.Code );
                    overrides.Add( string.Format( CultureInfo.InvariantCulture, "tick {0,3}  [{1,-10}] {2,-22} {3}", t.Tick, t.Category, t.Code, t.Message ) );
                }
            }
            foreach ( var a in batch.Actions )
            {
                if ( a.Kind == ActionKind.Scripted || a.Kind == ActionKind.Retreat )
                    scriptedActions.Add( string.Format( CultureInfo.InvariantCulture, "tick {0,3}  action {1,-9} param={2}", tick, a.Kind, a.Param ?? "-" ) );
                pendingAcks.Add( new ActionResult { ActionId = a.ActionId, Status = ActionStatus.Succeeded, ResultTick = tick } );
            }
        }

        var lines = new List<string> { "Override telemetry emitted by the five directives:" };
        lines.AddRange( overrides );
        if ( scriptedActions.Count > 0 )
        {
            lines.Add( "Directive-driven action requests:" );
            lines.AddRange( scriptedActions );
        }

        if ( !seenCodes.Contains( "script_set_mode" ) )
            return ExampleResult.Fail( lines, "expected script_set_mode telemetry" );
        if ( !seenCodes.Contains( "script_withdrawal" ) )
            return ExampleResult.Fail( lines, "expected script_withdrawal telemetry" );
        if ( !seenCodes.Contains( "script_set_progression" ) || !seenCodes.Contains( "reset" ) || !seenCodes.Contains( "script_sequence" ) )
            return ExampleResult.Fail( lines, "expected script_set_progression, reset and script_sequence telemetry" );
        return ExampleResult.Pass( lines );
    }
}

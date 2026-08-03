using System;
using System.Collections.Generic;

namespace TwoBrains.Examples;

/// <summary>
/// Outcome of one example: a pass/fail self-check flag plus the short paragraph of summary
/// lines the runner prints. Examples never write to the console directly — they return
/// lines so the runner owns all output formatting and the exit code.
/// </summary>
public sealed class ExampleResult
{
    public bool Success { get; private set; }
    public List<string> Lines { get; private set; } = new List<string>();

    public static ExampleResult Pass( List<string> lines )
    {
        return new ExampleResult { Success = true, Lines = lines };
    }

    public static ExampleResult Fail( List<string> lines, string why )
    {
        lines.Add( "SELF-CHECK FAILED: " + why );
        return new ExampleResult { Success = false, Lines = lines };
    }
}

/// <summary>
/// Runs every example in a fixed order, prints its summary paragraph, and exits non-zero
/// when any self-check fails. No external dependencies — the core is compiled in as
/// shared sources (see the csproj).
/// </summary>
internal static class Program
{
    private static int Main()
    {
        // Fixed order: basics first, then scenarios, then configuration and determinism.
        var examples = new KeyValuePair<string, Func<ExampleResult>>[]
        {
            Pair( "Minimal manual host", MinimalManualHostExample.Run ),
            Pair( "Fake host: lost sight into search", FakeHostScenarioExample.Run ),
            Pair( "Archetype: stalker", StalkerArchetypeExample.Run ),
            Pair( "Archetype: brute", BruteArchetypeExample.Run ),
            Pair( "Archetype: lurker", LurkerArchetypeExample.Run ),
            Pair( "Offstage stalker sweep", OffstageStalkerExample.Run ),
            Pair( "Scripted encounter directives", ScriptedEncounterExample.Run ),
            Pair( "Deterministic replay", DeterministicReplayExample.Run ),
            Pair( "Save and restore", SaveRestoreExample.Run ),
            Pair( "Telemetry inspection", TelemetryInspectionExample.Run ),
            Pair( "Config inheritance and modifiers", ConfigInheritanceExample.Run ),
            Pair( "Pressure tuning: quota and decrease", PressureTuningExample.Run ),
            Pair( "Omniscience policy", OmnisciencePolicyExample.Run ),
            Pair( "Custom (game-defined) senses", CustomSensesExample.Run ),
            Pair( "Alien: Isolation-inspired preset", AlienIsolationPresetExample.Run ),
        };

        int passed = 0;
        foreach ( var example in examples )
        {
            Console.WriteLine( "=== " + example.Key + " ===" );
            ExampleResult result;
            try
            {
                result = example.Value();
            }
            catch ( Exception ex )
            {
                result = ExampleResult.Fail( new List<string>(), "unhandled " + ex.GetType().Name + ": " + ex.Message );
            }
            foreach ( var line in result.Lines )
                Console.WriteLine( "  " + line );
            Console.WriteLine( result.Success ? "  -> PASS" : "  -> FAIL" );
            Console.WriteLine();
            if ( result.Success )
                passed++;
        }

        Console.WriteLine( string.Format( System.Globalization.CultureInfo.InvariantCulture,
            "{0}/{1} examples passed.", passed, examples.Length ) );
        return passed == examples.Length ? 0 : 1;
    }

    private static KeyValuePair<string, Func<ExampleResult>> Pair( string name, Func<ExampleResult> run )
    {
        return new KeyValuePair<string, Func<ExampleResult>>( name, run );
    }
}

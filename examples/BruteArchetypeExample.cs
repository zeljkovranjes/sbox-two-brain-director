using System.Collections.Generic;
using SboxTwoBrains;

namespace TwoBrains.Examples;

/// <summary>
/// Archetype sketch 2/3 (docs/TUNING.md): the brute — fast pressure cycles, no subtlety.
/// Configured purely via MonsterProfileConfig inheritance (BasedOn "stalker"), no core
/// changes. Proves: small section deltas produce a distinct, valid effective config.
/// </summary>
public static class BruteArchetypeExample
{
    internal static MonsterProfileConfig CreateProfile()
    {
        return new MonsterProfileConfig
        {
            Name = "brute",
            BasedOn = "stalker",
            // Fast cycles, no subtlety: charges in, shrugs off deterrence.
            Pressure = new PressureSection { FillSeconds = 2.0, CooldownSeconds = 15.0, MaxOpportunities = 6 },
            Threat = new ThreatSection { FlankChance = 0.0, AimedWeaponHesitationSeconds = 0.0, DeterrentRetreatSeconds = 6.0 },
            Combat = new CombatSection { AttackRange = 3.0, ChaseGiveUpDistance = 60.0, AttackBanSeconds = 2.0 },
        };
    }

    public static ExampleResult Run()
    {
        var catalogue = StalkerArchetypeExample.CreateCatalogue();
        var cfg = catalogue.Resolve( "brute" );
        var lines = new List<string>
        {
            "brute — charges in, shrugs off deterrence; relentless pressure, never flanks.",
            StalkerArchetypeExample.Highlights( cfg ),
        };
        var failure = ArchetypeChecks.VerifyAll( catalogue, lines );
        return failure != null ? ExampleResult.Fail( lines, failure ) : ExampleResult.Pass( lines );
    }
}

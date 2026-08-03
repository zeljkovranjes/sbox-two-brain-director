using System.Collections.Generic;
using SboxTwoBrains;

namespace TwoBrains.Examples;

/// <summary>
/// Archetype sketch 3/3 (docs/TUNING.md): the lurker — slow pressure cycles, long auditory
/// memory, lives offstage. Configured purely via MonsterProfileConfig inheritance
/// (BasedOn "stalker"), no core changes. Proves: perception/offstage deltas compose with
/// inherited pressure fields into a distinct, valid effective config.
/// </summary>
public static class LurkerArchetypeExample
{
    internal static MonsterProfileConfig CreateProfile()
    {
        return new MonsterProfileConfig
        {
            Name = "lurker",
            BasedOn = "stalker",
            // Slow cycles, long memory, lives offstage.
            Pressure = new PressureSection { FillSeconds = 5.0, CooldownSeconds = 40.0, SweepIdleMinSeconds = 15.0, SweepIdleMaxSeconds = 60.0 },
            Perception = new PerceptionSection
            {
                Auditory = new PerceptionChannelSection { Threshold = 0.2, DecayHalfLifeSeconds = 30.0 },
            },
            Offstage = new OffstageSection { NodeDwellMinSeconds = 20.0, NodeDwellMaxSeconds = 90.0, KilltrapEnabled = true },
        };
    }

    public static ExampleResult Run()
    {
        var catalogue = StalkerArchetypeExample.CreateCatalogue();
        var cfg = catalogue.Resolve( "lurker" );
        var lines = new List<string>
        {
            "lurker — slow cycles, long memory, lives offstage; hears everything twice.",
            StalkerArchetypeExample.Highlights( cfg ),
        };
        var failure = ArchetypeChecks.VerifyAll( catalogue, lines );
        return failure != null ? ExampleResult.Fail( lines, failure ) : ExampleResult.Pass( lines );
    }
}

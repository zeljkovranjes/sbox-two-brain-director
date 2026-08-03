using System.Collections.Generic;
using System.Globalization;
using SboxTwoBrains;

namespace TwoBrains.Examples;

/// <summary>
/// Configuration inheritance: a BasedOn chain (base -> stalker -> stalker-elite) resolves
/// root-first with child-wins per field, and ResolveWithModifier stacks an additive
/// difficulty delta on the pressure section with documented clamps (fill >= 0.5, max
/// opportunities >= 1, spatial >= 10). Proves: child overrides win, unset fields inherit,
/// and the modifier clamps instead of underflowing.
/// </summary>
public static class ConfigInheritanceExample
{
    public static ExampleResult Run()
    {
        var catalogue = new ProfileCatalogue()
            .Add( new MonsterProfileConfig
            {
                Name = "base",
                Pressure = new PressureSection { FillSeconds = 3.0, MaxOpportunities = 4 },
                Threat = new ThreatSection { FlankChance = 0.2 },
            } )
            .Add( new MonsterProfileConfig
            {
                Name = "stalker",
                BasedOn = "base",
                Perception = new PerceptionSection { Auditory = new PerceptionChannelSection { Threshold = 0.25, DecayHalfLifeSeconds = 25.0 } },
            } )
            .Add( new MonsterProfileConfig
            {
                Name = "stalker-elite",
                BasedOn = "stalker",
                Pressure = new PressureSection { FillSeconds = 1.5 },
                Threat = new ThreatSection { FlankChance = 0.6 },
            } )
            .Add( new MonsterProfileConfig
            {
                Name = "hard", // additive difficulty delta, not a full profile
                Pressure = new PressureSection { FillSeconds = -5.0, MaxOpportunities = 1 },
            } );

        var elite = catalogue.Resolve( "stalker-elite" );
        var hard = catalogue.ResolveWithModifier( "stalker-elite", "hard" );

        var lines = new List<string>
        {
            "chain base -> stalker -> stalker-elite (child wins per field; null inherits):",
            "  " + Pick( elite ),
            string.Format( CultureInfo.InvariantCulture, "  resolved hash: {0:X16}", elite.ComputeHash() ),
            "stalker-elite + hard (additive pressure delta, clamped):",
            "  " + Pick( hard ),
            string.Format( CultureInfo.InvariantCulture, "  resolved hash: {0:X16}", hard.ComputeHash() ),
        };

        // Child-wins: elite's own fields beat the ancestors'.
        if ( elite.Pressure.FillSeconds != 1.5 || elite.Threat.FlankChance != 0.6 )
            return ExampleResult.Fail( lines, "child overrides did not win" );
        // Inheritance: fields only the ancestors set flow down the chain.
        if ( elite.Pressure.MaxOpportunities != 4 || elite.Perception.Auditory.Threshold != 0.25 || elite.Pressure.CooldownSeconds != 25.0 )
            return ExampleResult.Fail( lines, "ancestor fields did not inherit" );
        // Modifier: 1.5 + (-5.0) clamps to the 0.5 floor; 4 + 1 stacks.
        if ( hard.Pressure.FillSeconds != EffectiveConfig.ResolvedPressure.MinFillSeconds || hard.Pressure.MaxOpportunities != 5 )
            return ExampleResult.Fail( lines, "modifier did not add and clamp" );
        return ExampleResult.Pass( lines );
    }

    /// <summary>One compact line of effective values (a curated excerpt of Describe()).</summary>
    private static string Pick( EffectiveConfig cfg )
    {
        return string.Format( CultureInfo.InvariantCulture,
            "fill={0:0.##}s cooldown={1:0.#}s maxOpportunities={2} flank={3:0.##} auditoryThreshold={4:0.##}",
            cfg.Pressure.FillSeconds, cfg.Pressure.CooldownSeconds, cfg.Pressure.MaxOpportunities,
            cfg.Threat.FlankChance, cfg.Perception.Auditory.Threshold );
    }
}

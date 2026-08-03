using System.Collections.Generic;
using System.Globalization;
using SboxTwoBrains;

namespace TwoBrains.Examples;

/// <summary>
/// Archetype sketch 1/3 (docs/TUNING.md): the stalker is the shared root of the trio —
/// the pure generic baseline with zero overrides. Brute and lurker inherit from it via
/// BasedOn. Proves: a no-delta profile resolves, validates, and has its own config hash.
/// </summary>
public static class StalkerArchetypeExample
{
    /// <summary>The shared root: every field falls through to the generic baseline.</summary>
    internal static MonsterProfileConfig CreateProfile()
    {
        return new MonsterProfileConfig { Name = "stalker" };
    }

    /// <summary>All three archetypes in one catalogue; siblings reuse this builder.</summary>
    internal static ProfileCatalogue CreateCatalogue()
    {
        return new ProfileCatalogue()
            .Add( CreateProfile() )
            .Add( BruteArchetypeExample.CreateProfile() )
            .Add( LurkerArchetypeExample.CreateProfile() );
    }

    public static ExampleResult Run()
    {
        var catalogue = CreateCatalogue();
        var cfg = catalogue.Resolve( "stalker" );
        var lines = new List<string>
        {
            "stalker — the patient baseline: shadows prey at range, waits for the gauge.",
            Highlights( cfg ),
        };
        var failure = ArchetypeChecks.VerifyAll( catalogue, lines );
        return failure != null ? ExampleResult.Fail( lines, failure ) : ExampleResult.Pass( lines );
    }

    internal static string Highlights( EffectiveConfig cfg )
    {
        return string.Format( CultureInfo.InvariantCulture,
            "fill={0:0.#}s cooldown={1:0.#}s attackCooldown={2:0.#}s flankChance={3:0.##} hash={4:X16}",
            cfg.Pressure.FillSeconds, cfg.Pressure.CooldownSeconds, cfg.Combat.AttackCooldownSeconds,
            cfg.Threat.FlankChance, cfg.ComputeHash() );
    }
}

/// <summary>Shared self-checks for the archetype trio: all validate, all hashes differ.</summary>
internal static class ArchetypeChecks
{
    internal static string VerifyAll( ProfileCatalogue catalogue, List<string> lines )
    {
        var hashes = new Dictionary<string, ulong>();
        foreach ( var name in new[] { "stalker", "brute", "lurker" } )
        {
            var cfg = catalogue.Resolve( name );
            var errors = cfg.Validate();
            if ( errors.Count > 0 )
                return "profile '" + name + "' failed validation: " + errors[0];
            hashes[name] = cfg.ComputeHash();
        }
        if ( hashes["stalker"] == hashes["brute"] || hashes["stalker"] == hashes["lurker"] || hashes["brute"] == hashes["lurker"] )
            return "archetype config hashes must be pairwise distinct";
        lines.Add( "all three archetypes validate; ComputeHash pairwise distinct." );
        return null;
    }
}

using System;
using System.Collections.Generic;

namespace TwoBrains.Core.Config;

/// <summary>
/// Named profile store with deterministic single-chain inheritance resolution.
/// Resolution walks BasedOn links root-first (child wins per field), rejects cycles and
/// missing parents with actionable errors, applies the generic baseline for any field no
/// profile in the chain set, and validates the result once at startup.
/// </summary>
public sealed class ProfileCatalogue
{
	private readonly Dictionary<string, MonsterProfileConfig> _profiles = new Dictionary<string, MonsterProfileConfig>( StringComparer.Ordinal );

	/// <summary>Registers or replaces a profile. Names are case-sensitive.</summary>
	public ProfileCatalogue Add( MonsterProfileConfig profile )
	{
		if ( profile == null ) throw new ArgumentNullException( nameof( profile ) );
		if ( string.IsNullOrEmpty( profile.Name ) )
			throw new ConfigException( "Profile requires a non-empty Name." );
		_profiles[profile.Name] = profile;
		return this;
	}

	public bool Contains( string name ) => name != null && _profiles.ContainsKey( name );

	/// <summary>Profile names in ordinal sort order (deterministic enumeration).</summary>
	public List<string> Names()
	{
		var names = new List<string>( _profiles.Keys );
		names.Sort( StringComparer.Ordinal );
		return names;
	}

	/// <summary>Resolves the inheritance chain for <paramref name="name"/> into an EffectiveConfig.</summary>
	public EffectiveConfig Resolve( string name )
	{
		var chain = BuildChain( name );
		var effective = new EffectiveConfig();
		foreach ( var profile in chain )
			Apply( effective, profile );
		effective.ProfileName = name;
		effective.ConfigVersion = chain[chain.Count - 1].ConfigVersion ?? "1";
		return effective.Validated();
	}

	/// <summary>
	/// Resolves <paramref name="baseName"/> and applies an additive difficulty/player modifier
	/// profile on top, then clamps the combined pressure fields. Mirrors the research's
	/// effective-config rule: fill clamped &gt;= 0.5, max opportunities &gt;= 1, spatial values &gt;= 10.
	/// Only Pressure fields combine additively; other sections override as usual.
	/// </summary>
	public EffectiveConfig ResolveWithModifier( string baseName, string modifierName )
	{
		var effective = Resolve( baseName );
		if ( modifierName == null )
			return effective;
		var modifierChain = BuildChain( modifierName );
		foreach ( var profile in modifierChain )
			ApplyModifier( effective, profile );
		var p = effective.Pressure;
		p.FillSeconds = Math.Max( p.FillSeconds, EffectiveConfig.ResolvedPressure.MinFillSeconds );
		p.MaxOpportunities = Math.Max( p.MaxOpportunities, 1 );
		p.ExclusionFirstMin = Math.Max( p.ExclusionFirstMin, 0 );
		p.ExclusionFirstMax = Math.Max( p.ExclusionFirstMax, 0 );
		p.ExclusionSubsequentMin = Math.Max( p.ExclusionSubsequentMin, 0 );
		p.ExclusionSubsequentMax = Math.Max( p.ExclusionSubsequentMax, 0 );
		// research: the derived spatial pair is clamped to at least 10
		p.SweepMinDistance = Math.Max( p.SweepMinDistance, EffectiveConfig.ResolvedPressure.MinSpatialValue );
		p.SweepMaxDistance = Math.Max( p.SweepMaxDistance, EffectiveConfig.ResolvedPressure.MinSpatialValue );
		effective.ProfileName = baseName + "+" + modifierName;
		return effective.Validated();
	}

	private List<MonsterProfileConfig> BuildChain( string name )
	{
		if ( string.IsNullOrEmpty( name ) )
			throw new ConfigException( "Profile name is required." );
		var chain = new List<MonsterProfileConfig>();
		var seen = new HashSet<string>( StringComparer.Ordinal );
		string current = name;
		while ( current != null )
		{
			if ( !seen.Add( current ) )
				throw new ConfigException( "Profile inheritance cycle detected at '" + current + "' (chain: " + string.Join( " -> ", chain.ConvertAll( c => c.Name ) ) + " -> " + current + ")." );
			if ( !_profiles.TryGetValue( current, out var profile ) )
				throw new ConfigException( "Profile '" + current + "' not found (referenced while resolving '" + name + "')." );
			chain.Add( profile );
			current = profile.BasedOn;
		}
		chain.Reverse(); // root first: ancestors applied before descendants
		return chain;
	}

	private static void Apply( EffectiveConfig e, MonsterProfileConfig cfg )
	{
		if ( cfg.Pressure != null ) ApplyPressure( e.Pressure, cfg.Pressure );
		if ( cfg.Perception != null ) ApplyPerception( e.Perception, cfg.Perception );
		if ( cfg.Search != null ) ApplySearch( e.Search, cfg.Search );
		if ( cfg.Threat != null ) ApplyThreat( e.Threat, cfg.Threat );
		if ( cfg.Combat != null ) ApplyCombat( e.Combat, cfg.Combat );
		if ( cfg.Offstage != null ) ApplyOffstage( e.Offstage, cfg.Offstage );
		if ( cfg.Modules != null ) ApplyModules( e.Modules, cfg.Modules );
		if ( cfg.Movement != null ) ApplyMovement( e.Movement, cfg.Movement );
	}

	private static void ApplyPressure( EffectiveConfig.ResolvedPressure r, PressureSection s )
	{
		if ( s.FillSeconds.HasValue ) r.FillSeconds = s.FillSeconds.Value;
		if ( s.CooldownSeconds.HasValue ) r.CooldownSeconds = s.CooldownSeconds.Value;
		if ( s.DecreaseSeconds.HasValue ) r.DecreaseSeconds = s.DecreaseSeconds.Value;
		if ( s.DecreaseDelaySeconds.HasValue ) r.DecreaseDelaySeconds = s.DecreaseDelaySeconds.Value;
		if ( s.MaxOpportunities.HasValue ) r.MaxOpportunities = s.MaxOpportunities.Value;
		if ( s.EventQuotaMin.HasValue ) r.EventQuotaMin = s.EventQuotaMin.Value;
		if ( s.EventQuotaMax.HasValue ) r.EventQuotaMax = s.EventQuotaMax.Value;
		if ( s.AggressiveThresholdProgression.HasValue ) r.AggressiveThresholdProgression = s.AggressiveThresholdProgression.Value;
		if ( s.StartProgression.HasValue ) r.StartProgression = s.StartProgression.Value;
		if ( s.SweepDurationSeconds.HasValue ) r.SweepDurationSeconds = s.SweepDurationSeconds.Value;
		if ( s.SweepMinDistance.HasValue ) r.SweepMinDistance = s.SweepMinDistance.Value;
		if ( s.SweepMaxDistance.HasValue ) r.SweepMaxDistance = s.SweepMaxDistance.Value;
		if ( s.SweepIdleMinSeconds.HasValue ) r.SweepIdleMinSeconds = s.SweepIdleMinSeconds.Value;
		if ( s.SweepIdleMaxSeconds.HasValue ) r.SweepIdleMaxSeconds = s.SweepIdleMaxSeconds.Value;
		if ( s.AmbushTimeoutSeconds.HasValue ) r.AmbushTimeoutSeconds = s.AmbushTimeoutSeconds.Value;
		if ( s.KilltrapSeconds.HasValue ) r.KilltrapSeconds = s.KilltrapSeconds.Value;
		if ( s.RoleTimeoutMinSeconds.HasValue ) r.RoleTimeoutMinSeconds = s.RoleTimeoutMinSeconds.Value;
		if ( s.RoleTimeoutMaxSeconds.HasValue ) r.RoleTimeoutMaxSeconds = s.RoleTimeoutMaxSeconds.Value;
		if ( s.ExclusionFirstMin.HasValue ) r.ExclusionFirstMin = s.ExclusionFirstMin.Value;
		if ( s.ExclusionFirstMax.HasValue ) r.ExclusionFirstMax = s.ExclusionFirstMax.Value;
		if ( s.ExclusionSubsequentMin.HasValue ) r.ExclusionSubsequentMin = s.ExclusionSubsequentMin.Value;
		if ( s.ExclusionSubsequentMax.HasValue ) r.ExclusionSubsequentMax = s.ExclusionSubsequentMax.Value;
		if ( s.IngressAttractMinSeconds.HasValue ) r.IngressAttractMinSeconds = s.IngressAttractMinSeconds.Value;
		if ( s.IngressAttractMaxSeconds.HasValue ) r.IngressAttractMaxSeconds = s.IngressAttractMaxSeconds.Value;
		if ( s.SweepBoxHalfWidth.HasValue ) r.SweepBoxHalfWidth = s.SweepBoxHalfWidth.Value;
		if ( s.SweepBoxMinHalfLength.HasValue ) r.SweepBoxMinHalfLength = s.SweepBoxMinHalfLength.Value;
		if ( s.OpportunityExpirySeconds.HasValue ) r.OpportunityExpirySeconds = s.OpportunityExpirySeconds.Value;
	}

	private static void ApplyPerception( EffectiveConfig.ResolvedPerception r, PerceptionSection s )
	{
		if ( s.MemoryCapacity.HasValue ) r.MemoryCapacity = s.MemoryCapacity.Value;
		if ( s.CombineMode.HasValue ) r.CombineMode = s.CombineMode.Value;
		if ( s.RecentConfirmationSeconds.HasValue ) r.RecentConfirmationSeconds = s.RecentConfirmationSeconds.Value;
		if ( s.Visual != null ) ApplyChannel( r.Visual, s.Visual );
		if ( s.Auditory != null ) ApplyChannel( r.Auditory, s.Auditory );
		if ( s.Touch != null ) ApplyChannel( r.Touch, s.Touch );
		if ( s.Damage != null ) ApplyChannel( r.Damage, s.Damage );
		if ( s.Light != null ) ApplyChannel( r.Light, s.Light );
		if ( s.GameDefined != null ) ApplyChannel( r.GameDefined, s.GameDefined );
	}

	private static void ApplyChannel( EffectiveConfig.ResolvedPerceptionChannel r, PerceptionChannelSection s )
	{
		if ( s.Threshold.HasValue ) r.Threshold = s.Threshold.Value;
		if ( s.DecayHalfLifeSeconds.HasValue ) r.DecayHalfLifeSeconds = s.DecayHalfLifeSeconds.Value;
		if ( s.MaxAgeSeconds.HasValue ) r.MaxAgeSeconds = s.MaxAgeSeconds.Value;
		if ( s.Weight.HasValue ) r.Weight = s.Weight.Value;
	}

	private static void ApplySearch( EffectiveConfig.ResolvedSearch r, SearchSection s )
	{
		if ( s.SystematicWindowSeconds.HasValue ) r.SystematicWindowSeconds = s.SystematicWindowSeconds.Value;
		if ( s.NodeRevisitPenaltySeconds.HasValue ) r.NodeRevisitPenaltySeconds = s.NodeRevisitPenaltySeconds.Value;
		if ( s.GiveUpSeconds.HasValue ) r.GiveUpSeconds = s.GiveUpSeconds.Value;
		if ( s.MaxNodesPerSearch.HasValue ) r.MaxNodesPerSearch = s.MaxNodesPerSearch.Value;
		if ( s.NodeReachDistance.HasValue ) r.NodeReachDistance = s.NodeReachDistance.Value;
	}

	private static void ApplyThreat( EffectiveConfig.ResolvedThreat r, ThreatSection s )
	{
		if ( s.CloseDistance.HasValue ) r.CloseDistance = s.CloseDistance.Value;
		if ( s.VeryCloseDistance.HasValue ) r.VeryCloseDistance = s.VeryCloseDistance.Value;
		if ( s.AimedWeaponHesitationSeconds.HasValue ) r.AimedWeaponHesitationSeconds = s.AimedWeaponHesitationSeconds.Value;
		if ( s.VisualRetentionSeconds.HasValue ) r.VisualRetentionSeconds = s.VisualRetentionSeconds.Value;
		if ( s.FlankChance.HasValue ) r.FlankChance = s.FlankChance.Value;
		if ( s.ThreatTimeoutSeconds.HasValue ) r.ThreatTimeoutSeconds = s.ThreatTimeoutSeconds.Value;
		if ( s.DeterrentRetreatSeconds.HasValue ) r.DeterrentRetreatSeconds = s.DeterrentRetreatSeconds.Value;
		if ( s.DangerousThreatRating.HasValue ) r.DangerousThreatRating = s.DangerousThreatRating.Value;
	}

	private static void ApplyCombat( EffectiveConfig.ResolvedCombat r, CombatSection s )
	{
		if ( s.AttackRange.HasValue ) r.AttackRange = s.AttackRange.Value;
		if ( s.ChaseGiveUpDistance.HasValue ) r.ChaseGiveUpDistance = s.ChaseGiveUpDistance.Value;
		if ( s.ChaseGiveUpSeconds.HasValue ) r.ChaseGiveUpSeconds = s.ChaseGiveUpSeconds.Value;
		if ( s.AttackCooldownSeconds.HasValue ) r.AttackCooldownSeconds = s.AttackCooldownSeconds.Value;
		if ( s.FlankIngressSeconds.HasValue ) r.FlankIngressSeconds = s.FlankIngressSeconds.Value;
		if ( s.AttackBanSeconds.HasValue ) r.AttackBanSeconds = s.AttackBanSeconds.Value;
	}

	private static void ApplyOffstage( EffectiveConfig.ResolvedOffstage r, OffstageSection s )
	{
		if ( s.IngressBanSeconds.HasValue ) r.IngressBanSeconds = s.IngressBanSeconds.Value;
		if ( s.NodeDwellMinSeconds.HasValue ) r.NodeDwellMinSeconds = s.NodeDwellMinSeconds.Value;
		if ( s.NodeDwellMaxSeconds.HasValue ) r.NodeDwellMaxSeconds = s.NodeDwellMaxSeconds.Value;
		if ( s.PreferIngressNearPressure.HasValue ) r.PreferIngressNearPressure = s.PreferIngressNearPressure.Value;
		if ( s.KilltrapEnabled.HasValue ) r.KilltrapEnabled = s.KilltrapEnabled.Value;
		if ( s.IngressTimeoutSeconds.HasValue ) r.IngressTimeoutSeconds = s.IngressTimeoutSeconds.Value;
	}

	private static void ApplyModules( EffectiveConfig.ResolvedModules r, ModulesSection s )
	{
		if ( s.Order != null ) r.Order = CopyStrings( s.Order );
		if ( s.Disabled != null ) r.Disabled = CopyStrings( s.Disabled );
	}

	private static string[] CopyStrings( string[] source )
	{
		var copy = new string[source.Length];
		for ( int i = 0; i < source.Length; i++ )
			copy[i] = source[i];
		return copy;
	}

	private static void ApplyMovement( EffectiveConfig.ResolvedMovement r, MovementSection s )
	{
		if ( s.SpeedSlow.HasValue ) r.SpeedSlow = s.SpeedSlow.Value;
		if ( s.SpeedFast.HasValue ) r.SpeedFast = s.SpeedFast.Value;
		if ( s.SpeedFastest.HasValue ) r.SpeedFastest = s.SpeedFastest.Value;
		if ( s.InvestigateFacingSeconds.HasValue ) r.InvestigateFacingSeconds = s.InvestigateFacingSeconds.Value;
	}

	/// <summary>Additive merge used for difficulty/player modifiers (pressure fields only).</summary>
	private static void ApplyModifier( EffectiveConfig e, MonsterProfileConfig cfg )
	{
		var r = e.Pressure;
		var s = cfg.Pressure;
		if ( s != null )
		{
			if ( s.FillSeconds.HasValue ) r.FillSeconds += s.FillSeconds.Value;
			if ( s.CooldownSeconds.HasValue ) r.CooldownSeconds += s.CooldownSeconds.Value;
			if ( s.DecreaseSeconds.HasValue ) r.DecreaseSeconds += s.DecreaseSeconds.Value;
			if ( s.MaxOpportunities.HasValue ) r.MaxOpportunities += s.MaxOpportunities.Value;
			if ( s.SweepMinDistance.HasValue ) r.SweepMinDistance += s.SweepMinDistance.Value;
			if ( s.SweepMaxDistance.HasValue ) r.SweepMaxDistance += s.SweepMaxDistance.Value;
			if ( s.ExclusionFirstMin.HasValue ) r.ExclusionFirstMin += s.ExclusionFirstMin.Value;
			if ( s.ExclusionFirstMax.HasValue ) r.ExclusionFirstMax += s.ExclusionFirstMax.Value;
		}
		// Non-pressure sections of a modifier behave like ordinary overrides.
		if ( cfg.Perception != null ) ApplyPerception( e.Perception, cfg.Perception );
		if ( cfg.Search != null ) ApplySearch( e.Search, cfg.Search );
		if ( cfg.Threat != null ) ApplyThreat( e.Threat, cfg.Threat );
		if ( cfg.Combat != null ) ApplyCombat( e.Combat, cfg.Combat );
		if ( cfg.Offstage != null ) ApplyOffstage( e.Offstage, cfg.Offstage );
		if ( cfg.Movement != null ) ApplyMovement( e.Movement, cfg.Movement );
	}
}

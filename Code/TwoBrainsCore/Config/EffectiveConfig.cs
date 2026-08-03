using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TwoBrains.Core.Config;

/// <summary>
/// Fully resolved, validated configuration: every value present, ranges checked, defaults
/// applied. This is the only config shape policy code may read. <see cref="Describe"/>
/// exposes the final effective values deterministically; <see cref="ComputeHash"/> gives a
/// stable identity used by save/replay.
/// </summary>
public sealed class EffectiveConfig
{
	// ---- generic baseline defaults (NOT research-derived; see Compat for the preset) ----
	public sealed class ResolvedPressure
	{
		public double FillSeconds = 3.0;
		public double CooldownSeconds = 25.0;
		public double DecreaseSeconds = 20.0;
		public double DecreaseDelaySeconds = 0.0;
		public int MaxOpportunities = 4;
		public int EventQuotaMin = 0;
		public int EventQuotaMax = 0;
		public double AggressiveThresholdProgression = 1.0;
		public double StartProgression = 0.0;
		public double SweepDurationSeconds = 50.0;
		public double SweepMinDistance = 10.0;
		public double SweepMaxDistance = 60.0;
		public double SweepIdleMinSeconds = 5.0;
		public double SweepIdleMaxSeconds = 40.0;
		public double AmbushTimeoutSeconds = 40.0;
		public double KilltrapSeconds = 10.0;
		public double RoleTimeoutMinSeconds = 45.0;
		public double RoleTimeoutMaxSeconds = 120.0;
		public double ExclusionFirstMin = 8.0;
		public double ExclusionFirstMax = 21.0;
		public double ExclusionSubsequentMin = 0.0;
		public double ExclusionSubsequentMax = 16.0;
		public double IngressAttractMinSeconds = 20.0;
		public double IngressAttractMaxSeconds = 20.0;
		public double SweepBoxHalfWidth = 3.0;
		public double SweepBoxMinHalfLength = 16.0;
		public double OpportunityExpirySeconds = 30.0;

		/// <summary>Hard floor applied to fill seconds (research: effective fill clamped ≥ 0.5).</summary>
		public const double MinFillSeconds = 0.5;
		/// <summary>Hard floor applied to spatial derived values (research: clamped ≥ 10).</summary>
		public const double MinSpatialValue = 10.0;
	}

	public sealed class ResolvedPerceptionChannel
	{
		public double Threshold = 0.3;
		public double DecayHalfLifeSeconds = 20.0;
		public double MaxAgeSeconds = 120.0;
		public double Weight = 1.0;
	}

	public sealed class ResolvedPerception
	{
		public int MemoryCapacity = 32;
		public MemoryCombineMode CombineMode = MemoryCombineMode.Max;
		public double RecentConfirmationSeconds = 0.5;
		public ResolvedPerceptionChannel Visual = new ResolvedPerceptionChannel { Threshold = 0.3, DecayHalfLifeSeconds = 30.0, MaxAgeSeconds = 180.0, Weight = 1.0 };
		public ResolvedPerceptionChannel Auditory = new ResolvedPerceptionChannel { Threshold = 0.3, DecayHalfLifeSeconds = 15.0, MaxAgeSeconds = 90.0, Weight = 0.8 };
		public ResolvedPerceptionChannel Touch = new ResolvedPerceptionChannel { Threshold = 0.5, DecayHalfLifeSeconds = 60.0, MaxAgeSeconds = 300.0, Weight = 1.0 };
		public ResolvedPerceptionChannel Damage = new ResolvedPerceptionChannel { Threshold = 0.5, DecayHalfLifeSeconds = 60.0, MaxAgeSeconds = 300.0, Weight = 1.0 };
		public ResolvedPerceptionChannel Light = new ResolvedPerceptionChannel { Threshold = 0.4, DecayHalfLifeSeconds = 10.0, MaxAgeSeconds = 60.0, Weight = 0.6 };
		public ResolvedPerceptionChannel GameDefined = new ResolvedPerceptionChannel { Threshold = 0.3, DecayHalfLifeSeconds = 20.0, MaxAgeSeconds = 120.0, Weight = 1.0 };
	}

	public sealed class ResolvedSearch
	{
		public double SystematicWindowSeconds = 20.0;
		public double NodeRevisitPenaltySeconds = 30.0;
		public double GiveUpSeconds = 120.0;
		public int MaxNodesPerSearch = 8;
		public double NodeReachDistance = 2.0;
	}

	public sealed class ResolvedThreat
	{
		public double CloseDistance = 8.0;
		public double VeryCloseDistance = 5.0;
		public double AimedWeaponHesitationSeconds = 0.5;
		public double VisualRetentionSeconds = 0.5;
		public double FlankChance = 0.2;
		public double ThreatTimeoutSeconds = 30.0;
		public double DeterrentRetreatSeconds = 2.0;
		public double DangerousThreatRating = 0.5;
	}

	public sealed class ResolvedCombat
	{
		public double AttackRange = 2.5;
		public double ChaseGiveUpDistance = 40.0;
		public double ChaseGiveUpSeconds = 8.0;
		public double AttackCooldownSeconds = 1.5;
		public double FlankIngressSeconds = 20.0;
		public double AttackBanSeconds = 5.0;
	}

	public sealed class ResolvedOffstage
	{
		public double IngressBanSeconds = 20.0;
		public double NodeDwellMinSeconds = 5.0;
		public double NodeDwellMaxSeconds = 40.0;
		public bool PreferIngressNearPressure = true;
		public bool KilltrapEnabled = true;
		public double IngressTimeoutSeconds = 10.0;
	}

	public sealed class ResolvedModules
	{
		/// <summary>Arbitration order, earlier wins. Names match Micro module registry.</summary>
		public string[] Order = new string[0];
		public string[] Disabled = new string[0];
	}

	public sealed class ResolvedMovement
	{
		public double SpeedSlow = 0.35;
		public double SpeedFast = 0.7;
		public double SpeedFastest = 1.0;
		public double InvestigateFacingSeconds = 2.0;
	}

	public string ProfileName = "";
	public string ConfigVersion = "1";
	public ResolvedPressure Pressure = new ResolvedPressure();
	public ResolvedPerception Perception = new ResolvedPerception();
	public ResolvedSearch Search = new ResolvedSearch();
	public ResolvedThreat Threat = new ResolvedThreat();
	public ResolvedCombat Combat = new ResolvedCombat();
	public ResolvedOffstage Offstage = new ResolvedOffstage();
	public ResolvedModules Modules = new ResolvedModules();
	public ResolvedMovement Movement = new ResolvedMovement();

	/// <summary>Validates all resolved values against documented ranges. Returns error lines.</summary>
	public List<string> Validate()
	{
		var errors = new List<string>();
		var p = Pressure;
		Check( errors, "Pressure.FillSeconds", p.FillSeconds, 0.5, 600 );
		Check( errors, "Pressure.CooldownSeconds", p.CooldownSeconds, 0, 600 );
		Check( errors, "Pressure.DecreaseSeconds", p.DecreaseSeconds, 0.1, 600 );
		Check( errors, "Pressure.DecreaseDelaySeconds", p.DecreaseDelaySeconds, 0, 600 );
		Check( errors, "Pressure.MaxOpportunities", p.MaxOpportunities, 1, 100 );
		Check( errors, "Pressure.EventQuotaMin", p.EventQuotaMin, 0, 100 );
		Check( errors, "Pressure.EventQuotaMax", p.EventQuotaMax, 0, 100 );
		if ( p.EventQuotaMax < p.EventQuotaMin ) errors.Add( "Pressure.EventQuotaMax must be >= EventQuotaMin." );
		Check( errors, "Pressure.AggressiveThresholdProgression", p.AggressiveThresholdProgression, 0.0001, 1.0 );
		Check( errors, "Pressure.StartProgression", p.StartProgression, 0.0, 1.0 );
		Check( errors, "Pressure.SweepDurationSeconds", p.SweepDurationSeconds, 1, 600 );
		Check( errors, "Pressure.SweepMinDistance", p.SweepMinDistance, 0, 1000 );
		Check( errors, "Pressure.SweepMaxDistance", p.SweepMaxDistance, 0, 1000 );
		if ( p.SweepMaxDistance < p.SweepMinDistance ) errors.Add( "Pressure.SweepMaxDistance must be >= SweepMinDistance." );
		Check( errors, "Pressure.SweepIdleMinSeconds", p.SweepIdleMinSeconds, 0, 600 );
		Check( errors, "Pressure.SweepIdleMaxSeconds", p.SweepIdleMaxSeconds, 0, 600 );
		if ( p.SweepIdleMaxSeconds < p.SweepIdleMinSeconds ) errors.Add( "Pressure.SweepIdleMaxSeconds must be >= SweepIdleMinSeconds." );
		Check( errors, "Pressure.AmbushTimeoutSeconds", p.AmbushTimeoutSeconds, 1, 600 );
		Check( errors, "Pressure.KilltrapSeconds", p.KilltrapSeconds, 0, 600 );
		Check( errors, "Pressure.RoleTimeoutMinSeconds", p.RoleTimeoutMinSeconds, 0, 600 );
		Check( errors, "Pressure.RoleTimeoutMaxSeconds", p.RoleTimeoutMaxSeconds, 0, 600 );
		Check( errors, "Pressure.ExclusionFirstMin", p.ExclusionFirstMin, 0, 1000 );
		Check( errors, "Pressure.ExclusionFirstMax", p.ExclusionFirstMax, 0, 1000 );
		Check( errors, "Pressure.ExclusionSubsequentMin", p.ExclusionSubsequentMin, 0, 1000 );
		Check( errors, "Pressure.ExclusionSubsequentMax", p.ExclusionSubsequentMax, 0, 1000 );
		Check( errors, "Pressure.IngressAttractMinSeconds", p.IngressAttractMinSeconds, 0, 600 );
		Check( errors, "Pressure.IngressAttractMaxSeconds", p.IngressAttractMaxSeconds, 0, 600 );
		Check( errors, "Pressure.SweepBoxHalfWidth", p.SweepBoxHalfWidth, 0, 100 );
		Check( errors, "Pressure.SweepBoxMinHalfLength", p.SweepBoxMinHalfLength, 0, 1000 );
		Check( errors, "Pressure.OpportunityExpirySeconds", p.OpportunityExpirySeconds, 1, 600 );

		var pe = Perception;
		Check( errors, "Perception.MemoryCapacity", pe.MemoryCapacity, 1, 256 );
		Check( errors, "Perception.RecentConfirmationSeconds", pe.RecentConfirmationSeconds, 0, 60 );
		ValidateChannel( errors, "Visual", pe.Visual );
		ValidateChannel( errors, "Auditory", pe.Auditory );
		ValidateChannel( errors, "Touch", pe.Touch );
		ValidateChannel( errors, "Damage", pe.Damage );
		ValidateChannel( errors, "Light", pe.Light );
		ValidateChannel( errors, "GameDefined", pe.GameDefined );

		var s = Search;
		Check( errors, "Search.SystematicWindowSeconds", s.SystematicWindowSeconds, 1, 600 );
		Check( errors, "Search.NodeRevisitPenaltySeconds", s.NodeRevisitPenaltySeconds, 0, 600 );
		Check( errors, "Search.GiveUpSeconds", s.GiveUpSeconds, 1, 3600 );
		Check( errors, "Search.MaxNodesPerSearch", s.MaxNodesPerSearch, 1, 64 );
		Check( errors, "Search.NodeReachDistance", s.NodeReachDistance, 0.1, 100 );

		var t = Threat;
		Check( errors, "Threat.CloseDistance", t.CloseDistance, 0.1, 1000 );
		Check( errors, "Threat.VeryCloseDistance", t.VeryCloseDistance, 0.1, 1000 );
		if ( t.VeryCloseDistance > t.CloseDistance ) errors.Add( "Threat.VeryCloseDistance must be <= CloseDistance." );
		Check( errors, "Threat.AimedWeaponHesitationSeconds", t.AimedWeaponHesitationSeconds, 0, 60 );
		Check( errors, "Threat.VisualRetentionSeconds", t.VisualRetentionSeconds, 0, 60 );
		Check( errors, "Threat.FlankChance", t.FlankChance, 0, 1 );
		Check( errors, "Threat.ThreatTimeoutSeconds", t.ThreatTimeoutSeconds, 1, 3600 );
		Check( errors, "Threat.DeterrentRetreatSeconds", t.DeterrentRetreatSeconds, 0, 600 );
		Check( errors, "Threat.DangerousThreatRating", t.DangerousThreatRating, 0, 1 );

		var c = Combat;
		Check( errors, "Combat.AttackRange", c.AttackRange, 0.1, 100 );
		Check( errors, "Combat.ChaseGiveUpDistance", c.ChaseGiveUpDistance, 1, 1000 );
		Check( errors, "Combat.ChaseGiveUpSeconds", c.ChaseGiveUpSeconds, 0.1, 600 );
		Check( errors, "Combat.AttackCooldownSeconds", c.AttackCooldownSeconds, 0, 600 );
		Check( errors, "Combat.FlankIngressSeconds", c.FlankIngressSeconds, 0, 600 );
		Check( errors, "Combat.AttackBanSeconds", c.AttackBanSeconds, 0, 600 );

		var o = Offstage;
		Check( errors, "Offstage.IngressBanSeconds", o.IngressBanSeconds, 0, 600 );
		Check( errors, "Offstage.NodeDwellMinSeconds", o.NodeDwellMinSeconds, 0, 600 );
		Check( errors, "Offstage.NodeDwellMaxSeconds", o.NodeDwellMaxSeconds, 0, 600 );
		if ( o.NodeDwellMaxSeconds < o.NodeDwellMinSeconds ) errors.Add( "Offstage.NodeDwellMaxSeconds must be >= NodeDwellMinSeconds." );
		Check( errors, "Offstage.IngressTimeoutSeconds", o.IngressTimeoutSeconds, 1, 120 );

		var m = Movement;
		Check( errors, "Movement.SpeedSlow", m.SpeedSlow, 0, 1 );
		Check( errors, "Movement.SpeedFast", m.SpeedFast, 0, 1 );
		Check( errors, "Movement.SpeedFastest", m.SpeedFastest, 0, 1 );
		Check( errors, "Movement.InvestigateFacingSeconds", m.InvestigateFacingSeconds, 0, 60 );

		return errors;
	}

	private static void ValidateChannel( List<string> errors, string name, ResolvedPerceptionChannel ch )
	{
		Check( errors, "Perception." + name + ".Threshold", ch.Threshold, 0, 1 );
		Check( errors, "Perception." + name + ".DecayHalfLifeSeconds", ch.DecayHalfLifeSeconds, 0.1, 3600 );
		Check( errors, "Perception." + name + ".MaxAgeSeconds", ch.MaxAgeSeconds, 1, 3600 );
		Check( errors, "Perception." + name + ".Weight", ch.Weight, 0, 4 );
	}

	private static void Check( List<string> errors, string name, double value, double min, double max )
	{
		if ( double.IsNaN( value ) || double.IsInfinity( value ) || value < min || value > max )
			errors.Add( string.Format( CultureInfo.InvariantCulture, "{0}={1:R} out of range [{2}, {3}].", name, value, min, max ) );
	}

	private static void Check( List<string> errors, string name, int value, int min, int max )
	{
		if ( value < min || value > max )
			errors.Add( string.Format( CultureInfo.InvariantCulture, "{0}={1} out of range [{2}, {3}].", name, value, min, max ) );
	}

	/// <summary>Throws <see cref="ConfigException"/> listing every violation, or returns this.</summary>
	public EffectiveConfig Validated()
	{
		var errors = Validate();
		if ( errors.Count > 0 )
			throw new ConfigException( "Profile '" + ProfileName + "' failed validation:\n - " + string.Join( "\n - ", errors ) );
		return this;
	}

	/// <summary>Final effective values as sorted key=value lines (deterministic, invariant culture).</summary>
	public string Describe()
	{
		var lines = new List<string>();
		var p = Pressure;
		Add( lines, "Pressure.FillSeconds", p.FillSeconds ); Add( lines, "Pressure.CooldownSeconds", p.CooldownSeconds );
		Add( lines, "Pressure.DecreaseSeconds", p.DecreaseSeconds ); Add( lines, "Pressure.DecreaseDelaySeconds", p.DecreaseDelaySeconds );
		Add( lines, "Pressure.MaxOpportunities", p.MaxOpportunities ); Add( lines, "Pressure.EventQuotaMin", p.EventQuotaMin );
		Add( lines, "Pressure.EventQuotaMax", p.EventQuotaMax ); Add( lines, "Pressure.AggressiveThresholdProgression", p.AggressiveThresholdProgression );
		Add( lines, "Pressure.StartProgression", p.StartProgression ); Add( lines, "Pressure.SweepDurationSeconds", p.SweepDurationSeconds );
		Add( lines, "Pressure.SweepMinDistance", p.SweepMinDistance ); Add( lines, "Pressure.SweepMaxDistance", p.SweepMaxDistance );
		Add( lines, "Pressure.SweepIdleMinSeconds", p.SweepIdleMinSeconds ); Add( lines, "Pressure.SweepIdleMaxSeconds", p.SweepIdleMaxSeconds );
		Add( lines, "Pressure.AmbushTimeoutSeconds", p.AmbushTimeoutSeconds ); Add( lines, "Pressure.KilltrapSeconds", p.KilltrapSeconds );
		Add( lines, "Pressure.RoleTimeoutMinSeconds", p.RoleTimeoutMinSeconds ); Add( lines, "Pressure.RoleTimeoutMaxSeconds", p.RoleTimeoutMaxSeconds );
		Add( lines, "Pressure.ExclusionFirstMin", p.ExclusionFirstMin ); Add( lines, "Pressure.ExclusionFirstMax", p.ExclusionFirstMax );
		Add( lines, "Pressure.ExclusionSubsequentMin", p.ExclusionSubsequentMin ); Add( lines, "Pressure.ExclusionSubsequentMax", p.ExclusionSubsequentMax );
		Add( lines, "Pressure.IngressAttractMinSeconds", p.IngressAttractMinSeconds ); Add( lines, "Pressure.IngressAttractMaxSeconds", p.IngressAttractMaxSeconds );
		Add( lines, "Pressure.SweepBoxHalfWidth", p.SweepBoxHalfWidth ); Add( lines, "Pressure.SweepBoxMinHalfLength", p.SweepBoxMinHalfLength );
		Add( lines, "Pressure.OpportunityExpirySeconds", p.OpportunityExpirySeconds );
		var pe = Perception;
		Add( lines, "Perception.MemoryCapacity", pe.MemoryCapacity ); Add( lines, "Perception.CombineMode", pe.CombineMode.ToString() );
		Add( lines, "Perception.RecentConfirmationSeconds", pe.RecentConfirmationSeconds );
		DescribeChannel( lines, "Visual", pe.Visual ); DescribeChannel( lines, "Auditory", pe.Auditory );
		DescribeChannel( lines, "Touch", pe.Touch ); DescribeChannel( lines, "Damage", pe.Damage );
		DescribeChannel( lines, "Light", pe.Light ); DescribeChannel( lines, "GameDefined", pe.GameDefined );
		var s = Search;
		Add( lines, "Search.SystematicWindowSeconds", s.SystematicWindowSeconds ); Add( lines, "Search.NodeRevisitPenaltySeconds", s.NodeRevisitPenaltySeconds );
		Add( lines, "Search.GiveUpSeconds", s.GiveUpSeconds ); Add( lines, "Search.MaxNodesPerSearch", s.MaxNodesPerSearch );
		Add( lines, "Search.NodeReachDistance", s.NodeReachDistance );
		var t = Threat;
		Add( lines, "Threat.CloseDistance", t.CloseDistance ); Add( lines, "Threat.VeryCloseDistance", t.VeryCloseDistance );
		Add( lines, "Threat.AimedWeaponHesitationSeconds", t.AimedWeaponHesitationSeconds ); Add( lines, "Threat.VisualRetentionSeconds", t.VisualRetentionSeconds );
		Add( lines, "Threat.FlankChance", t.FlankChance ); Add( lines, "Threat.ThreatTimeoutSeconds", t.ThreatTimeoutSeconds );
		Add( lines, "Threat.DeterrentRetreatSeconds", t.DeterrentRetreatSeconds ); Add( lines, "Threat.DangerousThreatRating", t.DangerousThreatRating );
		var c = Combat;
		Add( lines, "Combat.AttackRange", c.AttackRange ); Add( lines, "Combat.ChaseGiveUpDistance", c.ChaseGiveUpDistance );
		Add( lines, "Combat.ChaseGiveUpSeconds", c.ChaseGiveUpSeconds ); Add( lines, "Combat.AttackCooldownSeconds", c.AttackCooldownSeconds );
		Add( lines, "Combat.FlankIngressSeconds", c.FlankIngressSeconds ); Add( lines, "Combat.AttackBanSeconds", c.AttackBanSeconds );
		var o = Offstage;
		Add( lines, "Offstage.IngressBanSeconds", o.IngressBanSeconds ); Add( lines, "Offstage.NodeDwellMinSeconds", o.NodeDwellMinSeconds );
		Add( lines, "Offstage.NodeDwellMaxSeconds", o.NodeDwellMaxSeconds ); Add( lines, "Offstage.PreferIngressNearPressure", o.PreferIngressNearPressure );
		Add( lines, "Offstage.KilltrapEnabled", o.KilltrapEnabled ); Add( lines, "Offstage.IngressTimeoutSeconds", o.IngressTimeoutSeconds );
		var mo = Modules;
		lines.Add( "Modules.Order=" + string.Join( ",", mo.Order ) );
		lines.Add( "Modules.Disabled=" + string.Join( ",", mo.Disabled ) );
		var mv = Movement;
		Add( lines, "Movement.SpeedSlow", mv.SpeedSlow ); Add( lines, "Movement.SpeedFast", mv.SpeedFast );
		Add( lines, "Movement.SpeedFastest", mv.SpeedFastest ); Add( lines, "Movement.InvestigateFacingSeconds", mv.InvestigateFacingSeconds );
		lines.Sort( System.StringComparer.Ordinal );
		var sb = new StringBuilder();
		foreach ( var line in lines )
			sb.Append( line ).Append( '\n' );
		return sb.ToString();
	}

	private static void DescribeChannel( List<string> lines, string name, ResolvedPerceptionChannel ch )
	{
		Add( lines, "Perception." + name + ".Threshold", ch.Threshold );
		Add( lines, "Perception." + name + ".DecayHalfLifeSeconds", ch.DecayHalfLifeSeconds );
		Add( lines, "Perception." + name + ".MaxAgeSeconds", ch.MaxAgeSeconds );
		Add( lines, "Perception." + name + ".Weight", ch.Weight );
	}

	private static void Add( List<string> lines, string key, double value )
		=> lines.Add( key + "=" + value.ToString( "R", CultureInfo.InvariantCulture ) );
	private static void Add( List<string> lines, string key, int value )
		=> lines.Add( key + "=" + value.ToString( CultureInfo.InvariantCulture ) );
	private static void Add( List<string> lines, string key, bool value )
		=> lines.Add( key + "=" + (value ? "true" : "false") );
	private static void Add( List<string> lines, string key, string value )
		=> lines.Add( key + "=" + value );

	/// <summary>FNV-1a 64-bit hash of <see cref="Describe"/>; stable config identity for save/replay.</summary>
	public ulong ComputeHash()
	{
		const ulong offset = 14695981039346656037UL;
		const ulong prime = 1099511628211UL;
		ulong hash = offset;
		foreach ( char ch in Describe() )
		{
			hash ^= ch;
			hash *= prime;
		}
		return hash;
	}
}

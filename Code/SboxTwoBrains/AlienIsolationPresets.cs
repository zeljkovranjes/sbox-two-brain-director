using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// The Alien: Isolation-inspired compatibility preset: the 13 shipped intensity
/// configurations as verbatim records, micro-behavior tuning constants from the recovered
/// behavior trees, and a ready-made <see cref="ProfileCatalogue"/>.
///
/// RESEARCH-DERIVED — this is the ONLY namespace where game-specific names and recovered
/// constants appear. Values are proven decoded data; runtime semantics are strong
/// reconstruction unless the evidence map says otherwise. Do not present as exact parity.
/// Micro constants below come from the authored behavior trees (MICRO_BRAIN.MD):
/// threat distances 20/8/5/14/12/9.5, 0.5 s recent-visual retention, 20/80 percentage
/// branches, 25/30/120 s timers, suspicious-item proximity 15/8 with 2 s facing,
/// systematic-search 20 s window, vent distance tests 1.5/2/4.
/// </summary>
public static class AlienIsolationPresets
{
	/// <summary>Headline profile name in the preset catalogue.</summary>
	public const string InspiredProfileName = "ALIENISOLATIONINSPIRED";

	public static readonly AlienIsolationConfigRecord Default = new AlienIsolationConfigRecord
	{
		Name = "DEFAULT",
		VentAttractTimeMax = 120, VentAttractTimeMin = 20, AmbushTimeout = 30,
		DecreaseSweepDuration = 30, IncreaseSweepDuration = 50, KilltrapTime = 40,
		MaxDistance = 40, MaxIdleTime = 10, MaxMenaces = 4, MeanceDeemedTime = 40,
		MenaceCoolDownTime = 25, MenaceGaugeDecreaseTime = 20, MenaceGaugeSecondsToFill = 3,
		MinDistance = 10, MinIdleTime = 5,
		NearObjectiveExclusionRadiusFirstStalkMax = 15, NearObjectiveExclusionRadiusFirstStalkMin = 5,
		NearObjectiveExclusionRadiusSubsequentStalkMax = 10, NearObjectiveExclusionRadiusSubsequentStalkMin = 5,
		NearTargetExclusionRadiusFirstStalkMax = 15, NearTargetExclusionRadiusFirstStalkMin = 0,
		NearTargetExclusionRadiusSubsequentStalkMax = 10, NearTargetExclusionRadiusSubsequentStalkMin = 0,
		RoleTimeoutMax = 60, RoleTimeoutMin = 45, SweepBoxHalfWidth = 16, SweepBoxMinHalfLength = 15,
	};

	public static readonly AlienIsolationConfigRecord Mild = new AlienIsolationConfigRecord
	{
		Name = "MILD", TemplateName = "DEFAULT",
		VentAttractTimeMax = 180, VentAttractTimeMin = 40, AmbushTimeout = 30,
		DecreaseSweepDuration = 30, IncreaseSweepDuration = 70, KilltrapTime = 40,
		MaxDistance = 40, MaxIdleTime = 10, MaxMenaces = 3, MeanceDeemedTime = 20,
		MenaceCoolDownTime = 30, MenaceGaugeDecreaseTime = 30, MenaceGaugeSecondsToFill = 2,
		MinDistance = 10, MinIdleTime = 5,
		NearObjectiveExclusionRadiusFirstStalkMax = 29, NearObjectiveExclusionRadiusFirstStalkMin = 12,
		NearObjectiveExclusionRadiusSubsequentStalkMax = 24, NearObjectiveExclusionRadiusSubsequentStalkMin = 10,
		NearTargetExclusionRadiusFirstStalkMax = 29, NearTargetExclusionRadiusFirstStalkMin = 8,
		NearTargetExclusionRadiusSubsequentStalkMax = 24, NearTargetExclusionRadiusSubsequentStalkMin = 6,
		RoleTimeoutMax = 100, RoleTimeoutMin = 60, SweepBoxHalfWidth = 18, SweepBoxMinHalfLength = 18,
	};

	public static readonly AlienIsolationConfigRecord Moderate = new AlienIsolationConfigRecord
	{
		Name = "MODERATE", TemplateName = "DEFAULT",
		VentAttractTimeMax = 120, VentAttractTimeMin = 35, AmbushTimeout = 30,
		DecreaseSweepDuration = 30, IncreaseSweepDuration = 50, KilltrapTime = 40,
		MaxDistance = 40, MaxIdleTime = 10, MaxMenaces = 4, MeanceDeemedTime = 40,
		MenaceCoolDownTime = 25, MenaceGaugeDecreaseTime = 20, MenaceGaugeSecondsToFill = 3,
		MinDistance = 10, MinIdleTime = 5,
		NearObjectiveExclusionRadiusFirstStalkMax = 21, NearObjectiveExclusionRadiusFirstStalkMin = 8,
		NearObjectiveExclusionRadiusSubsequentStalkMax = 16, NearObjectiveExclusionRadiusSubsequentStalkMin = 8,
		NearTargetExclusionRadiusFirstStalkMax = 21, NearTargetExclusionRadiusFirstStalkMin = 0,
		NearTargetExclusionRadiusSubsequentStalkMax = 16, NearTargetExclusionRadiusSubsequentStalkMin = 0,
		RoleTimeoutMax = 60, RoleTimeoutMin = 45, SweepBoxHalfWidth = 16, SweepBoxMinHalfLength = 14,
	};

	public static readonly AlienIsolationConfigRecord ModeratelyIntense = new AlienIsolationConfigRecord
	{
		Name = "MODERATELY_INTENSE", TemplateName = "DEFAULT",
		VentAttractTimeMax = 120, VentAttractTimeMin = 25, AmbushTimeout = 30,
		DecreaseSweepDuration = 30, IncreaseSweepDuration = 45, KilltrapTime = 40,
		MaxDistance = 40, MaxIdleTime = 10, MaxMenaces = 5, MeanceDeemedTime = 45,
		MenaceCoolDownTime = 20, MenaceGaugeDecreaseTime = 15, MenaceGaugeSecondsToFill = 3,
		MinDistance = 10, MinIdleTime = 5,
		NearObjectiveExclusionRadiusFirstStalkMax = 17, NearObjectiveExclusionRadiusFirstStalkMin = 6,
		NearObjectiveExclusionRadiusSubsequentStalkMax = 12, NearObjectiveExclusionRadiusSubsequentStalkMin = 6,
		NearTargetExclusionRadiusFirstStalkMax = 17, NearTargetExclusionRadiusFirstStalkMin = 0,
		NearTargetExclusionRadiusSubsequentStalkMax = 12, NearTargetExclusionRadiusSubsequentStalkMin = 0,
		RoleTimeoutMax = 80, RoleTimeoutMin = 50, SweepBoxHalfWidth = 14, SweepBoxMinHalfLength = 12,
	};

	public static readonly AlienIsolationConfigRecord Intense = new AlienIsolationConfigRecord
	{
		Name = "INTENSE", TemplateName = "DEFAULT",
		VentAttractTimeMax = 60, VentAttractTimeMin = 20, AmbushTimeout = 30,
		DecreaseSweepDuration = 30, IncreaseSweepDuration = 40, KilltrapTime = 40,
		MaxDistance = 40, MaxIdleTime = 10, MaxMenaces = 5, MeanceDeemedTime = 50,
		MenaceCoolDownTime = 15, MenaceGaugeDecreaseTime = 10, MenaceGaugeSecondsToFill = 4,
		MinDistance = 10, MinIdleTime = 5,
		NearObjectiveExclusionRadiusFirstStalkMax = 13, NearObjectiveExclusionRadiusFirstStalkMin = 4,
		NearObjectiveExclusionRadiusSubsequentStalkMax = 8, NearObjectiveExclusionRadiusSubsequentStalkMin = 4,
		NearTargetExclusionRadiusFirstStalkMax = 13, NearTargetExclusionRadiusFirstStalkMin = 0,
		NearTargetExclusionRadiusSubsequentStalkMax = 8, NearTargetExclusionRadiusSubsequentStalkMin = 0,
		RoleTimeoutMax = 40, RoleTimeoutMin = 25, SweepBoxHalfWidth = 12, SweepBoxMinHalfLength = 10,
	};

	public static readonly AlienIsolationConfigRecord BackstageAlert = new AlienIsolationConfigRecord
	{
		Name = "BACKSTAGEALERT", TemplateName = "MODERATE",
		VentAttractTimeMax = 120, VentAttractTimeMin = 35, AmbushTimeout = 30,
		DecreaseSweepDuration = 30, IncreaseSweepDuration = 50, KilltrapTime = 25,
		MaxDistance = 35, MaxIdleTime = 10, MaxMenaces = 1, MeanceDeemedTime = 40,
		MenaceCoolDownTime = 25, MenaceGaugeDecreaseTime = 20, MenaceGaugeSecondsToFill = 3,
		MinDistance = 5, MinIdleTime = 5,
		NearObjectiveExclusionRadiusFirstStalkMax = 21, NearObjectiveExclusionRadiusFirstStalkMin = 8,
		NearObjectiveExclusionRadiusSubsequentStalkMax = 16, NearObjectiveExclusionRadiusSubsequentStalkMin = 8,
		NearTargetExclusionRadiusFirstStalkMax = 21, NearTargetExclusionRadiusFirstStalkMin = 0,
		NearTargetExclusionRadiusSubsequentStalkMax = 16, NearTargetExclusionRadiusSubsequentStalkMin = 0,
		RoleTimeoutMax = 400, RoleTimeoutMin = 200, SweepBoxHalfWidth = 16, SweepBoxMinHalfLength = 14,
	};

	public static readonly AlienIsolationConfigRecord BackstageHold = new AlienIsolationConfigRecord
	{
		Name = "BACKSTAGEHOLD", TemplateName = "MODERATE",
		VentAttractTimeMax = 120, VentAttractTimeMin = 35, AmbushTimeout = 45,
		DecreaseSweepDuration = 30, IncreaseSweepDuration = 50, KilltrapTime = 90,
		MaxDistance = 35, MaxIdleTime = 5, MaxMenaces = 1, MeanceDeemedTime = 40,
		MenaceCoolDownTime = 25, MenaceGaugeDecreaseTime = 20, MenaceGaugeSecondsToFill = 3,
		MinDistance = 5, MinIdleTime = 1,
		NearObjectiveExclusionRadiusFirstStalkMax = 21, NearObjectiveExclusionRadiusFirstStalkMin = 8,
		NearObjectiveExclusionRadiusSubsequentStalkMax = 16, NearObjectiveExclusionRadiusSubsequentStalkMin = 8,
		NearTargetExclusionRadiusFirstStalkMax = 21, NearTargetExclusionRadiusFirstStalkMin = 0,
		NearTargetExclusionRadiusSubsequentStalkMax = 16, NearTargetExclusionRadiusSubsequentStalkMin = 0,
		RoleTimeoutMax = -1, RoleTimeoutMin = -1, SweepBoxHalfWidth = 16, SweepBoxMinHalfLength = 14,
	};

	public static readonly AlienIsolationConfigRecord BackstageHoldMild = new AlienIsolationConfigRecord
	{
		Name = "BACKSTAGEHOLD_MILD", TemplateName = "MILD",
		VentAttractTimeMax = 180, VentAttractTimeMin = 40, AmbushTimeout = 30,
		DecreaseSweepDuration = 30, IncreaseSweepDuration = 70, KilltrapTime = 90,
		MaxDistance = 35, MaxIdleTime = 5, MaxMenaces = 1, MeanceDeemedTime = 20,
		MenaceCoolDownTime = 30, MenaceGaugeDecreaseTime = 30, MenaceGaugeSecondsToFill = 2,
		MinDistance = 5, MinIdleTime = 1,
		NearObjectiveExclusionRadiusFirstStalkMax = 29, NearObjectiveExclusionRadiusFirstStalkMin = 12,
		NearObjectiveExclusionRadiusSubsequentStalkMax = 24, NearObjectiveExclusionRadiusSubsequentStalkMin = 10,
		NearTargetExclusionRadiusFirstStalkMax = 29, NearTargetExclusionRadiusFirstStalkMin = 8,
		NearTargetExclusionRadiusSubsequentStalkMax = 24, NearTargetExclusionRadiusSubsequentStalkMin = 6,
		RoleTimeoutMax = -1, RoleTimeoutMin = -1, SweepBoxHalfWidth = 18, SweepBoxMinHalfLength = 18,
	};

	public static readonly AlienIsolationConfigRecord BackstageHoldVeryClose = new AlienIsolationConfigRecord
	{
		Name = "BACKSTAGEHOLD_VCLOSE", TemplateName = "BACKSTAGEHOLD",
		VentAttractTimeMax = 120, VentAttractTimeMin = 35, AmbushTimeout = 45,
		DecreaseSweepDuration = 30, IncreaseSweepDuration = 50, KilltrapTime = -1,
		MaxDistance = 5, MaxIdleTime = 5, MaxMenaces = 1, MeanceDeemedTime = 40,
		MenaceCoolDownTime = 25, MenaceGaugeDecreaseTime = 20, MenaceGaugeSecondsToFill = 3,
		MinDistance = 1, MinIdleTime = 1,
		NearObjectiveExclusionRadiusFirstStalkMax = 21, NearObjectiveExclusionRadiusFirstStalkMin = 8,
		NearObjectiveExclusionRadiusSubsequentStalkMax = 16, NearObjectiveExclusionRadiusSubsequentStalkMin = 8,
		NearTargetExclusionRadiusFirstStalkMax = 21, NearTargetExclusionRadiusFirstStalkMin = 0,
		NearTargetExclusionRadiusSubsequentStalkMax = 16, NearTargetExclusionRadiusSubsequentStalkMin = 0,
		RoleTimeoutMax = -1, RoleTimeoutMin = -1, SweepBoxHalfWidth = 16, SweepBoxMinHalfLength = 14,
	};

	/// <summary>Shipped file spells this BACSTAGEHOLD_CLOSE (missing 'K'); name preserved.</summary>
	public static readonly AlienIsolationConfigRecord BackstageHoldClose = new AlienIsolationConfigRecord
	{
		Name = "BACSTAGEHOLD_CLOSE", TemplateName = "BACKSTAGEHOLD",
		VentAttractTimeMax = 120, VentAttractTimeMin = 35, AmbushTimeout = 45,
		DecreaseSweepDuration = 30, IncreaseSweepDuration = 50, KilltrapTime = -1,
		MaxDistance = 30, MaxIdleTime = 5, MaxMenaces = 1, MeanceDeemedTime = 40,
		MenaceCoolDownTime = 25, MenaceGaugeDecreaseTime = 20, MenaceGaugeSecondsToFill = 3,
		MinDistance = 5, MinIdleTime = 1,
		NearObjectiveExclusionRadiusFirstStalkMax = 21, NearObjectiveExclusionRadiusFirstStalkMin = 8,
		NearObjectiveExclusionRadiusSubsequentStalkMax = 16, NearObjectiveExclusionRadiusSubsequentStalkMin = 8,
		NearTargetExclusionRadiusFirstStalkMax = 21, NearTargetExclusionRadiusFirstStalkMin = 0,
		NearTargetExclusionRadiusSubsequentStalkMax = 16, NearTargetExclusionRadiusSubsequentStalkMin = 0,
		RoleTimeoutMax = -1, RoleTimeoutMin = -1, SweepBoxHalfWidth = 16, SweepBoxMinHalfLength = 14,
	};

	public static readonly AlienIsolationConfigRecord Canteen = new AlienIsolationConfigRecord
	{
		Name = "CANTEEN", TemplateName = "MILD",
		VentAttractTimeMax = 180, VentAttractTimeMin = 40, AmbushTimeout = 30,
		DecreaseSweepDuration = 30, IncreaseSweepDuration = 70, KilltrapTime = 40,
		MaxDistance = 40, MaxIdleTime = 10, MaxMenaces = 1, MeanceDeemedTime = 20,
		MenaceCoolDownTime = 30, MenaceGaugeDecreaseTime = 30, MenaceGaugeSecondsToFill = 2,
		MinDistance = 10, MinIdleTime = 5,
		NearObjectiveExclusionRadiusFirstStalkMax = 29, NearObjectiveExclusionRadiusFirstStalkMin = 12,
		NearObjectiveExclusionRadiusSubsequentStalkMax = 24, NearObjectiveExclusionRadiusSubsequentStalkMin = 10,
		NearTargetExclusionRadiusFirstStalkMax = 29, NearTargetExclusionRadiusFirstStalkMin = 8,
		NearTargetExclusionRadiusSubsequentStalkMax = 24, NearTargetExclusionRadiusSubsequentStalkMin = 6,
		RoleTimeoutMax = 100, RoleTimeoutMin = 60, SweepBoxHalfWidth = 18, SweepBoxMinHalfLength = 18,
	};

	public static readonly AlienIsolationConfigRecord CrewExpendableVent = new AlienIsolationConfigRecord
	{
		Name = "CREWEXPENDABLE_VENT", TemplateName = "INTENSE",
		VentAttractTimeMax = 30, VentAttractTimeMin = 20, AmbushTimeout = 30,
		DecreaseSweepDuration = 5, IncreaseSweepDuration = 5, KilltrapTime = 40,
		MaxDistance = 20, MaxIdleTime = 40, MaxMenaces = 5, MeanceDeemedTime = 50,
		MenaceCoolDownTime = 15, MenaceGaugeDecreaseTime = 10, MenaceGaugeSecondsToFill = 4,
		MinDistance = 10, MinIdleTime = 30,
		NearObjectiveExclusionRadiusFirstStalkMax = 13, NearObjectiveExclusionRadiusFirstStalkMin = 4,
		NearObjectiveExclusionRadiusSubsequentStalkMax = 8, NearObjectiveExclusionRadiusSubsequentStalkMin = 4,
		NearTargetExclusionRadiusFirstStalkMax = 13, NearTargetExclusionRadiusFirstStalkMin = 0,
		NearTargetExclusionRadiusSubsequentStalkMax = 8, NearTargetExclusionRadiusSubsequentStalkMin = 0,
		RoleTimeoutMax = 60, RoleTimeoutMin = 40, SweepBoxHalfWidth = 12, SweepBoxMinHalfLength = 10,
	};

	/// <summary>All 12 usable decoded records in stable order (the 13th shipped file is an empty master index).</summary>
	public static AlienIsolationConfigRecord[] All() => new[]
	{
		Default, Mild, Moderate, ModeratelyIntense, Intense,
		BackstageAlert, BackstageHold, BackstageHoldMild, BackstageHoldVeryClose, BackstageHoldClose,
		Canteen, CrewExpendableVent,
	};

	/// <summary>
	/// Builds a catalogue containing one profile per preset record (pressure sections from
	/// the verbatim data, micro sections from the recovered behavior constants) plus the
	/// headliner <see cref="InspiredProfileName"/> profile based on DEFAULT.
	/// </summary>
	public static ProfileCatalogue CreateCatalogue()
	{
		var catalogue = new ProfileCatalogue();
		foreach ( var record in All() )
			catalogue.Add( ToProfile( record ) );
		catalogue.Add( new MonsterProfileConfig
		{
			Name = InspiredProfileName,
			BasedOn = Default.Name,
			ConfigVersion = "aio-inspired-1",
		} );
		return catalogue;
	}

	/// <summary>Maps a record to a full profile, including recovered micro-behavior tuning.</summary>
	public static MonsterProfileConfig ToProfile( AlienIsolationConfigRecord record )
	{
		return new MonsterProfileConfig
		{
			Name = record.Name,
			BasedOn = string.IsNullOrEmpty( record.TemplateName ) ? null : record.TemplateName,
			ConfigVersion = "aio-inspired-1",
			Pressure = record.ToPressureSection(),
			// Micro tuning below is from the authored behavior trees (see class remarks).
			Threat = new ThreatSection
			{
				CloseDistance = 8.0,
				VeryCloseDistance = 5.0,
				AimedWeaponHesitationSeconds = 0.5,
				VisualRetentionSeconds = 0.5,
				FlankChance = 0.2, // shipped 20/80 percentage branch
				ThreatTimeoutSeconds = 30.0,
				DeterrentRetreatSeconds = 2.0,
			},
			Search = new SearchSection
			{
				SystematicWindowSeconds = 20.0, // shipped systematic-search precondition window
			},
			Movement = new MovementSection
			{
				InvestigateFacingSeconds = 2.0, // shipped facing action duration
			},
		};
	}
}

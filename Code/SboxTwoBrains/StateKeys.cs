namespace SboxTwoBrains;

/// <summary>
/// Well-known keys used inside <see cref="MicroState"/> containers. Countdown timers are aged
/// every tick and removed at zero; entries whose keys carry a stamp/marker prefix hold tick
/// stamps or accumulators and are never aged (see <see cref="IsAgedTimer"/>).
/// </summary>
internal static class StateKeys
{
	// countdown timers (aged every tick, removed when they reach zero)
	internal const string AttackCooldown = "attack_cd";
	internal const string AttackBan = "attack_ban";
	internal const string Stagger = "stagger";
	internal const string Hesitate = "hesitate";
	internal const string NavBackoff = "nav_backoff";
	internal const string RetreatCooldown = "retreat_cd";
	internal const string ThreatEpisode = "threat_episode";
	internal const string Ambush = "ambush";
	internal const string SearchEpisode = "search_episode";
	internal const string SearchCooldown = "search_cooldown";
	internal const string Sweep = "sweep";
	internal const string IngressBanPrefix = "ingress_ban_";

	// non-aged entries in State.Timers: tick stamps, deferral markers and accumulators
	internal const string VisitedPrefix = "visited_";
	internal const string DeferredPrefix = "deferred_";
	internal const string DeterrentExposure = "deterrent_exposure";

	// gauges
	internal const string Stun = "stun";
	internal const string Retreat = "retreat";
	internal const string PrevHealth = "prev_health";

	// counters
	internal const string ActionOrdinal = "action_ordinal";
	internal const string ActionTick = "action_tick";
	internal const string SearchNodes = "search_nodes";

	// flags
	internal const string OmniscienceOn = "omniscience_on";
	internal const string DespawnRequested = "despawn_requested";
	internal const string Offstage = "offstage";
	internal const string FlankRolled = "flank_rolled";
	internal const string ThreatEpisodeActive = "threat_episode_active";
	internal const string AmbushActive = "ambush_active";
	internal const string SearchActive = "search_active";
	internal const string SweepActive = "sweep_active";
	internal const string SweepDwellFlag = "sweep_dwell";
	internal const string Chasing = "chasing";
	internal const string SuspectResponded = "suspect_responded";
	internal const string ForcedRetreat = "forced_retreat";
	internal const string LifecycleInactiveFlag = "lc_inactive";

	/// <summary>True for countdown timers that age every tick; false for stamps/markers.</summary>
	internal static bool IsAgedTimer( string key )
	{
		return !key.StartsWith( VisitedPrefix, System.StringComparison.Ordinal )
			&& !key.StartsWith( DeferredPrefix, System.StringComparison.Ordinal )
			&& key != DeterrentExposure;
	}
}

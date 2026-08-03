using System;
using System.Collections.Generic;
using System.Globalization;
using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Macro pressure controller ("when and where should tension happen"). Consumes immutable
/// world snapshots and returns declarative <see cref="PressureDecision"/>s carrying mode,
/// progression, urgency, candidate region, allowed roles, constraints and expiry —
/// never target coordinates or movement instructions. The micro layer keeps local choice.
///
/// Update contract (called by TwoBrainsSystem in tick order):
/// 1. <see cref="ApplyOpportunityResults"/> — host acks of earlier opportunities.
/// 2. <see cref="ApplyDirectives"/> — script overrides (recorded as telemetry).
/// 3. <see cref="Tick"/> — ages timers, advances/decreases the gauge, evaluates mode
///    transitions, nominates at most one opportunity per tick.
///
/// Clean-room model (docs/EVIDENCE_MATRIX.md rows 2-8): gauge fills while a viable candidate
/// is latched with (1 - p) / max(fill, 0.5) * dt; decreases after a grace delay; Normal →
/// Aggressive at the threshold when quota/cooldown allow; Aggressive completes when the
/// sweep window ends, incrementing the completed count and feeding the optional event
/// quota. Urgency is a clean-room derivation (aggressive = 1; otherwise progression,
/// halved during cooldown). Exclusion margins and ingress-attract windows mirror the
/// research's first/subsequent stalk radii and vent-attract timers as explicit policy.
///
/// Determinism: all randomness flows through the injected <see cref="DeterministicRng"/>;
/// state is fully captured/restored via <see cref="CaptureState"/>/<see cref="RestoreState"/>.
/// The only non-serialized fields are intra-tick handoffs (pending decision reason and a
/// queued ForceOpportunity directive); both are always clear at tick boundaries, so a
/// capture taken after <see cref="Tick"/> restores exactly.
/// </summary>
public sealed class PressureDirector
{
	private const string Category = "macro";

	// Stable machine-readable telemetry codes (reason codes for every state transition).
	private const string CodeCandidateLatched = "candidate_latched";
	private const string CodeCandidateCleared = "candidate_cleared";
	private const string CodeModeAggressiveStart = "mode_aggressive_start";
	private const string CodeOpportunityOffered = "opportunity_offered";
	private const string CodeOpportunityCompleted = "opportunity_completed";
	private const string CodeOpportunityExpired = "opportunity_expired";
	private const string CodeOpportunityRejected = "opportunity_rejected";
	private const string CodeQuotaEvent = "quota_event";
	private const string CodeQuotaBlocked = "quota_blocked";
	private const string CodeReset = "reset";
	private const string CodeScriptSetMode = "script_set_mode";
	private const string CodeScriptSetProgression = "script_set_progression";
	private const string CodeScriptForcedOpportunity = "script_forced_opportunity";
	private const string CodeAckUnknown = "ack_unknown";

	private readonly DeterministicRng _rng;
	private PressureState _state;

	// Intra-tick handoff only (never serialized; always null/false after Tick returns).
	private string _pendingDecisionReason;
	private bool _forceQueued;
	private string _forceRegionId = "";
	private bool _latchSuppressedThisTick;

	/// <summary>Creates a director with fresh state. Rng must be a dedicated fork.</summary>
	public PressureDirector( DeterministicRng rng )
	{
		_rng = rng ?? throw new ArgumentNullException( nameof( rng ) );
		_state = new PressureState();
	}

	/// <summary>Live state (mutable only inside the director).</summary>
	public PressureState State => _state;

	/// <summary>Host acknowledgement of a pending opportunity (routed by the facade).</summary>
	public void ApplyOpportunityResults( TickContext ctx, EffectiveConfig cfg, IReadOnlyList<ActionResult> results, List<TelemetryEvent> telemetry )
	{
		if ( cfg == null ) throw new ArgumentNullException( nameof( cfg ) );
		if ( results == null ) throw new ArgumentNullException( nameof( results ) );
		if ( telemetry == null ) throw new ArgumentNullException( nameof( telemetry ) );

		for ( int i = 0; i < results.Count; i++ )
		{
			var result = results[i];
			if ( result == null )
				continue;
			if ( _state.PendingOpportunityId.Length == 0 || result.ActionId != _state.PendingOpportunityId )
			{
				Emit( ctx.TickIndex, telemetry, CodeAckUnknown, "id=" + ( result.ActionId ?? "" ) );
				continue;
			}

			switch ( result.Status )
			{
				case ActionStatus.Succeeded:
				case ActionStatus.PartiallySucceeded:
					CompleteOpportunity( ctx, cfg, telemetry );
					_pendingDecisionReason = CodeOpportunityCompleted;
					break;
				case ActionStatus.Rejected:
					RejectOpportunity( ctx, cfg, telemetry, "rejected", result.Detail );
					break;
				case ActionStatus.Interrupted:
					RejectOpportunity( ctx, cfg, telemetry, "interrupted", result.Detail );
					break;
				case ActionStatus.Failed:
					RejectOpportunity( ctx, cfg, telemetry, "failed", result.Detail );
					break;
				case ActionStatus.Deferred:
					// Non-terminal: one extension by the original interval, then no-ops.
					if ( !_state.PendingDeferExtensionUsed )
					{
						_state.OpportunityExpiryTick += ExpiryIntervalTicks( cfg.Pressure, ctx.DeltaTimeSeconds );
						_state.PendingDeferExtensionUsed = true;
					}
					break;
			}
		}
	}

	/// <summary>Script overrides for this tick (SetPressureMode/SetProgression/ResetPressure/ForceOpportunity).</summary>
	public void ApplyDirectives( TickContext ctx, EffectiveConfig cfg, IReadOnlyList<ScriptDirective> directives, List<TelemetryEvent> telemetry )
	{
		if ( cfg == null ) throw new ArgumentNullException( nameof( cfg ) );
		if ( directives == null ) throw new ArgumentNullException( nameof( directives ) );
		if ( telemetry == null ) throw new ArgumentNullException( nameof( telemetry ) );

		for ( int i = 0; i < directives.Count; i++ )
		{
			var d = directives[i];
			if ( d == null )
				continue;
			switch ( d.Kind )
			{
				case ScriptDirectiveKind.SetPressureMode:
					_state.Mode = d.Mode;
					_state.LastTransitionTick = ctx.TickIndex;
					if ( d.Progression > 0.0 )
						_state.Progression = Clamp01( d.Progression );
					if ( d.ResetGauge )
						ResetToStart( ctx, cfg, d.Mode == PressureMode.Aggressive, telemetry );
					Emit( ctx.TickIndex, telemetry, CodeScriptSetMode, string.Format( CultureInfo.InvariantCulture,
						"override mode={0} progression={1:F3} reset={2}", d.Mode, d.Progression, d.ResetGauge ) );
					break;
				case ScriptDirectiveKind.SetProgression:
					_state.Progression = Clamp01( d.Progression );
					Emit( ctx.TickIndex, telemetry, CodeScriptSetProgression, string.Format( CultureInfo.InvariantCulture,
						"override p={0:F3}", _state.Progression ) );
					break;
				case ScriptDirectiveKind.ResetPressure:
					ResetToStart( ctx, cfg, d.ResetGauge, telemetry );
					break;
				case ScriptDirectiveKind.ForceOpportunity:
					// Needs this tick's snapshot for candidate viability; consumed by Tick.
					_forceQueued = true;
					_forceRegionId = d.RegionId ?? "";
					break;
			}
		}
	}

	/// <summary>
	/// Advances pressure for one tick. Returns a <see cref="PressureDecision"/> only when
	/// something changed (mode transition, new opportunity, quota event, reset); else null.
	/// Appends telemetry for every transition with reason codes.
	/// </summary>
	public PressureDecision Tick( TickContext ctx, WorldSnapshot world, EffectiveConfig cfg, List<TelemetryEvent> telemetry )
	{
		if ( world == null ) throw new ArgumentNullException( nameof( world ) );
		if ( cfg == null ) throw new ArgumentNullException( nameof( cfg ) );
		if ( telemetry == null ) throw new ArgumentNullException( nameof( telemetry ) );

		var s = _state;
		var p = cfg.Pressure;
		double dt = ctx.DeltaTimeSeconds;

		// 1. Age all timers by the explicit tick delta.
		AgeTimers( s, dt );

		// 2. Lapse a pending opportunity whose expiry tick has passed without an ack.
		if ( s.PendingOpportunityId.Length > 0 && s.OpportunityExpiryTick < ctx.TickIndex )
		{
			string expiredId = s.PendingOpportunityId;
			s.PendingOpportunityId = "";
			s.PendingDeferExtensionUsed = false;
			if ( s.Mode == PressureMode.Aggressive )
			{
				s.Mode = PressureMode.Normal;
				s.LastTransitionTick = ctx.TickIndex;
				s.SweepSecondsRemaining = 0.0;
			}
			s.Progression = p.StartProgression;
			s.CooldownRemaining = p.CooldownSeconds;
			Emit( ctx.TickIndex, telemetry, CodeOpportunityExpired, "id=" + expiredId );
			_pendingDecisionReason = CodeOpportunityExpired;
		}

		// 3. Candidate evaluation with hysteresis on the latched region. A host rejection
		// this tick suppresses re-latching until the next tick (the decision reports "").
		List<string> excludedZoneIds = null;
		bool monsterAlive = world.Monster != null && world.Monster.Lifecycle == MonsterLifecycle.Alive;
		if ( s.Enabled && monsterAlive )
			excludedZoneIds = EvaluateCandidates( ctx, world, cfg, telemetry, _latchSuppressedThisTick );
		_latchSuppressedThisTick = false;

		// 3.5 Queued ForceOpportunity directive (needs this tick's snapshot).
		if ( _forceQueued )
		{
			_forceQueued = false;
			TryForceOpportunity( ctx, world, cfg, telemetry, monsterAlive );
		}

		// 4. Gauge update: fill while eligible, decrease after the grace delay otherwise.
		if ( s.CandidateLatched && s.Mode == PressureMode.Normal && s.CooldownRemaining <= 0.0 && s.Enabled )
		{
			double fill = Math.Max( p.FillSeconds, EffectiveConfig.ResolvedPressure.MinFillSeconds );
			s.Progression = Clamp01( s.Progression + ( 1.0 - s.Progression ) / fill * dt );
		}
		else if ( ( !s.CandidateLatched || !s.Enabled ) && s.Progression > 0.0 && s.DecreaseDelayRemaining <= 0.0 )
		{
			s.Progression = Math.Max( 0.0, s.Progression - dt / p.DecreaseSeconds );
		}

		// 5. Normal → Aggressive when the gauge is ready and quota/cooldown allow.
		if ( s.Mode == PressureMode.Normal && s.CandidateLatched && s.Progression >= p.AggressiveThresholdProgression
			&& s.CooldownRemaining <= 0.0 && s.Enabled )
		{
			if ( s.CompletedOpportunities < p.MaxOpportunities )
			{
				StartAggressiveOpportunity( ctx, cfg, telemetry );
				_pendingDecisionReason = CodeModeAggressiveStart;
			}
			else
			{
				Emit( ctx.TickIndex, telemetry, CodeQuotaBlocked, string.Format( CultureInfo.InvariantCulture,
					"count={0}/{1}", s.CompletedOpportunities, p.MaxOpportunities ) );
			}
		}

		// 6. Aggressive opportunity completes when the sweep window ends.
		if ( s.Mode == PressureMode.Aggressive && s.SweepSecondsRemaining <= 0.0 )
		{
			CompleteOpportunity( ctx, cfg, telemetry );
			_pendingDecisionReason = CodeOpportunityCompleted;
		}

		// 7. A decision is emitted only on transition ticks.
		if ( _pendingDecisionReason == null )
			return null;
		var decision = BuildDecision( ctx, world, cfg, _pendingDecisionReason, excludedZoneIds );
		_pendingDecisionReason = null;
		return decision;
	}

	/// <summary>Full/empty reset: clears count/latches and starts a fresh cycle (start mode per argument).</summary>
	public void ResetToStart( TickContext ctx, EffectiveConfig cfg, bool startAggressive, List<TelemetryEvent> telemetry )
	{
		if ( cfg == null ) throw new ArgumentNullException( nameof( cfg ) );
		if ( telemetry == null ) throw new ArgumentNullException( nameof( telemetry ) );

		var s = _state;
		var p = cfg.Pressure;
		s.Mode = startAggressive ? PressureMode.Aggressive : PressureMode.Normal;
		s.Progression = startAggressive ? 1.0 : p.StartProgression;
		s.CompletedOpportunities = 0;
		s.EventQuotaProgress = 0;
		s.EventQuotaTarget = p.EventQuotaMax > 0 ? RollQuotaTarget( p ) : 0;
		s.CandidateLatched = false;
		s.ActiveCandidateId = "";
		s.CooldownRemaining = 0.0;
		s.DecreaseDelayRemaining = 0.0;
		s.PendingOpportunityId = "";
		s.PendingDeferExtensionUsed = false;
		s.OpportunityExpiryTick = 0;
		s.SweepSecondsRemaining = startAggressive ? p.SweepDurationSeconds : 0.0;
		s.IngressAttractRemaining = 0.0;
		s.LastTransitionTick = ctx.TickIndex;
		Emit( ctx.TickIndex, telemetry, CodeReset, startAggressive ? "start=aggressive" : "start=normal" );
		if ( startAggressive )
			StartAggressiveOpportunity( ctx, cfg, telemetry );
		_pendingDecisionReason = CodeReset;
	}

	/// <summary>Canonical JSON of complete state (rng words are saved by the facade).</summary>
	public string CaptureState() => CanonicalJson.ToJson( _state );

	/// <summary>Restores state previously produced by <see cref="CaptureState"/>.</summary>
	public void RestoreState( string json )
	{
		if ( json == null ) throw new ArgumentNullException( nameof( json ) );
		var restored = CanonicalJson.FromJson<PressureState>( json ) ?? new PressureState();
		restored.ActiveCandidateId = restored.ActiveCandidateId ?? "";
		restored.PendingOpportunityId = restored.PendingOpportunityId ?? "";
		if ( restored.RecentReasons == null )
			restored.RecentReasons = new List<string>();
		while ( restored.RecentReasons.Count > PressureState.MaxRecentReasons )
			restored.RecentReasons.RemoveAt( 0 );
		// Rebuild with an ordinal comparer: deserialization uses the culture-sensitive default,
		// which would make canonical JSON order (and therefore replay hashes) culture-dependent.
		var bans = new SortedDictionary<string, double>( StringComparer.Ordinal );
		if ( restored.IngressBanRemaining != null )
		{
			foreach ( var kv in restored.IngressBanRemaining )
			{
				if ( kv.Key != null )
					bans[kv.Key] = kv.Value;
			}
		}
		restored.IngressBanRemaining = bans;
		_state = restored;
		_pendingDecisionReason = null;
		_forceQueued = false;
		_forceRegionId = "";
		_latchSuppressedThisTick = false;
	}

	// ---- candidate evaluation (step 3) ----

	/// <summary>
	/// Evaluates candidacy and updates the latch. Returns the ids of active zones that
	/// excluded at least one viable target this tick (diagnostics), or null when none.
	/// </summary>
	private List<string> EvaluateCandidates( TickContext ctx, WorldSnapshot world, EffectiveConfig cfg, List<TelemetryEvent> telemetry, bool suppressRelatch )
	{
		var s = _state;
		var p = cfg.Pressure;
		double margin = s.CompletedOpportunities == 0 ? p.ExclusionFirstMin : p.ExclusionSubsequentMin;

		List<string> excludedZoneIds = null;
		var best = SelectBestTarget( world, margin, ref excludedZoneIds, out bool anyViableInLatched );
		string selectedRegion = best == null ? null : ( best.RegionId ?? "" );

		if ( s.CandidateLatched )
		{
			if ( anyViableInLatched )
			{
				// Hysteresis: a viable latched region is kept even if another scores better.
				selectedRegion = s.ActiveCandidateId;
			}
			else
			{
				s.CandidateLatched = false;
				s.ActiveCandidateId = "";
				s.DecreaseDelayRemaining = p.DecreaseDelaySeconds;
				Emit( ctx.TickIndex, telemetry, CodeCandidateCleared, "candidate lost" );
			}
		}

		if ( !s.CandidateLatched && !suppressRelatch && selectedRegion != null )
		{
			s.ActiveCandidateId = selectedRegion;
			s.CandidateLatched = true;
			s.IngressAttractRemaining = _rng.NextRange( p.IngressAttractMinSeconds, p.IngressAttractMaxSeconds );
			Emit( ctx.TickIndex, telemetry, CodeCandidateLatched, "candidate=" + selectedRegion );
		}

		return excludedZoneIds;
	}

	/// <summary>
	/// Picks the best viable target deterministically: non-excluded regions first, then
	/// nearest planar distance from the monster, then ordinal target id. Pure selection;
	/// does not mutate state. Also reports whether the latched region still has any viable
	/// target (hysteresis input) and records the excluding zone ids.
	/// </summary>
	private TargetSnapshot SelectBestTarget( WorldSnapshot world, double margin, ref List<string> excludedZoneIds, out bool anyViableInLatched )
	{
		anyViableInLatched = false;
		var targets = world.Targets;
		if ( targets == null )
			return null;

		Vec3 monsterPos = world.Monster != null ? world.Monster.Position : Vec3.Zero;
		TargetSnapshot best = null;
		bool bestExcluded = false;
		double bestDistance = 0.0;

		for ( int i = 0; i < targets.Count; i++ )
		{
			var t = targets[i];
			if ( !IsViable( t ) )
				continue;
			if ( _state.CandidateLatched && ( t.RegionId ?? "" ) == _state.ActiveCandidateId )
				anyViableInLatched = true;

			bool excluded = false;
			var zones = world.ExclusionZones;
			if ( zones != null )
			{
				for ( int z = 0; z < zones.Count; z++ )
				{
					var zone = zones[z];
					if ( zone == null || !zone.Active )
						continue;
					if ( zone.Kind != ExclusionKind.Target && zone.Kind != ExclusionKind.Objective )
						continue;
					if ( zone.Center.PlanarDistanceTo( t.Position ) <= zone.Radius + margin )
					{
						excluded = true;
						if ( excludedZoneIds == null )
							excludedZoneIds = new List<string>();
						if ( zone.ZoneId != null && !excludedZoneIds.Contains( zone.ZoneId ) )
							excludedZoneIds.Add( zone.ZoneId );
					}
				}
			}

			double distance = monsterPos.PlanarDistanceTo( t.Position );
			if ( best == null
				|| ( excluded != bestExcluded && !excluded )
				|| ( excluded == bestExcluded && distance < bestDistance )
				|| ( excluded == bestExcluded && distance == bestDistance
					&& StringComparer.Ordinal.Compare( t.TargetId ?? "", best.TargetId ?? "" ) < 0 ) )
			{
				best = t;
				bestExcluded = excluded;
				bestDistance = distance;
			}
		}
		return best;
	}

	private static bool IsViable( TargetSnapshot t )
		=> t != null && t.IsValid && t.IsAlive && t.PressureEligible;

	// ---- transitions ----

	/// <summary>Queued ForceOpportunity: forces the Normal → Aggressive transition when enabled and a candidate is viable.</summary>
	private void TryForceOpportunity( TickContext ctx, WorldSnapshot world, EffectiveConfig cfg, List<TelemetryEvent> telemetry, bool monsterAlive )
	{
		var s = _state;
		string preferredRegion = _forceRegionId;
		_forceRegionId = "";
		if ( !s.Enabled || s.Mode != PressureMode.Normal || !monsterAlive )
			return;

		string region = null;
		var targets = world.Targets;
		if ( preferredRegion.Length > 0 )
		{
			// The directive's region is forced: it must contain a viable target.
			if ( targets != null )
			{
				for ( int i = 0; i < targets.Count; i++ )
				{
					if ( IsViable( targets[i] ) && ( targets[i].RegionId ?? "" ) == preferredRegion )
					{
						region = preferredRegion;
						break;
					}
				}
			}
		}
		else
		{
			double margin = s.CompletedOpportunities == 0 ? cfg.Pressure.ExclusionFirstMin : cfg.Pressure.ExclusionSubsequentMin;
			List<string> ignored = null;
			var best = SelectBestTarget( world, margin, ref ignored, out _ );
			region = best == null ? null : ( best.RegionId ?? "" );
		}

		if ( region == null )
			return;

		if ( !s.CandidateLatched || s.ActiveCandidateId != region )
		{
			s.ActiveCandidateId = region;
			s.CandidateLatched = true;
			s.IngressAttractRemaining = _rng.NextRange( cfg.Pressure.IngressAttractMinSeconds, cfg.Pressure.IngressAttractMaxSeconds );
			Emit( ctx.TickIndex, telemetry, CodeCandidateLatched, "forced candidate=" + region );
		}

		Emit( ctx.TickIndex, telemetry, CodeScriptForcedOpportunity, "forced region=" + region );
		StartAggressiveOpportunity( ctx, cfg, telemetry );
		_pendingDecisionReason = CodeScriptForcedOpportunity;
	}

	/// <summary>Transition body shared by threshold crossing, forced opportunities and aggressive resets.</summary>
	private void StartAggressiveOpportunity( TickContext ctx, EffectiveConfig cfg, List<TelemetryEvent> telemetry )
	{
		var s = _state;
		var p = cfg.Pressure;
		s.Mode = PressureMode.Aggressive;
		s.LastTransitionTick = ctx.TickIndex;
		s.SweepSecondsRemaining = p.SweepDurationSeconds;
		s.PendingOpportunityId = string.Concat(
			"op",
			ctx.TickIndex.ToString( CultureInfo.InvariantCulture ),
			"-",
			s.CompletedOpportunities.ToString( CultureInfo.InvariantCulture ) );
		s.OpportunityExpiryTick = ctx.TickIndex + ExpiryIntervalTicks( p, ctx.DeltaTimeSeconds );
		s.PendingDeferExtensionUsed = false;
		Emit( ctx.TickIndex, telemetry, CodeModeAggressiveStart, string.Format( CultureInfo.InvariantCulture,
			"p={0:F3} candidate={1}", s.Progression, s.ActiveCandidateId ) );
		Emit( ctx.TickIndex, telemetry, CodeOpportunityOffered, string.Format( CultureInfo.InvariantCulture,
			"id={0} expiry={1}", s.PendingOpportunityId, s.OpportunityExpiryTick ) );
	}

	/// <summary>Completion body shared by sweep end and successful host acks.</summary>
	private void CompleteOpportunity( TickContext ctx, EffectiveConfig cfg, List<TelemetryEvent> telemetry )
	{
		var s = _state;
		var p = cfg.Pressure;
		if ( s.Mode == PressureMode.Aggressive )
		{
			s.Mode = PressureMode.Normal;
			s.LastTransitionTick = ctx.TickIndex;
		}
		s.SweepSecondsRemaining = 0.0;
		s.CompletedOpportunities++;
		s.Progression = p.StartProgression;
		s.CooldownRemaining = p.CooldownSeconds;
		string completedId = s.PendingOpportunityId;
		s.PendingOpportunityId = "";
		s.PendingDeferExtensionUsed = false;
		Emit( ctx.TickIndex, telemetry, CodeOpportunityCompleted, string.Format( CultureInfo.InvariantCulture,
			"id={0} count={1}", completedId, s.CompletedOpportunities ) );

		if ( p.EventQuotaMax > 0 )
		{
			s.EventQuotaProgress++;
			if ( s.EventQuotaTarget == 0 )
				s.EventQuotaTarget = RollQuotaTarget( p );
			if ( s.EventQuotaProgress >= s.EventQuotaTarget )
			{
				s.CompletedOpportunities = 0;
				s.EventQuotaProgress = 0;
				s.EventQuotaTarget = RollQuotaTarget( p );
				s.CooldownRemaining = 0.0;
				Emit( ctx.TickIndex, telemetry, CodeQuotaEvent, "quota reached; counters reset" );
			}
		}
	}

	/// <summary>Rejection body shared by host Rejected/Interrupted/Failed acks (no count increment).</summary>
	private void RejectOpportunity( TickContext ctx, EffectiveConfig cfg, List<TelemetryEvent> telemetry, string statusText, string detail )
	{
		var s = _state;
		var p = cfg.Pressure;
		if ( s.Mode == PressureMode.Aggressive )
		{
			s.Mode = PressureMode.Normal;
			s.LastTransitionTick = ctx.TickIndex;
		}
		s.SweepSecondsRemaining = 0.0;
		string rejectedId = s.PendingOpportunityId;
		s.PendingOpportunityId = "";
		s.PendingDeferExtensionUsed = false;
		s.CandidateLatched = false;
		s.ActiveCandidateId = "";
		s.DecreaseDelayRemaining = p.DecreaseDelaySeconds;
		s.Progression = p.StartProgression;
		s.CooldownRemaining = p.CooldownSeconds;
		string message = "id=" + rejectedId + " status=" + statusText;
		if ( !string.IsNullOrEmpty( detail ) )
			message += " detail=" + detail;
		Emit( ctx.TickIndex, telemetry, CodeOpportunityRejected, message );
		_pendingDecisionReason = CodeOpportunityRejected;
		_latchSuppressedThisTick = true; // host refused this region; do not re-latch it this tick
	}

	// ---- helpers ----

	private static void AgeTimers( PressureState s, double dt )
	{
		if ( s.CooldownRemaining > 0.0 )
			s.CooldownRemaining = Math.Max( 0.0, s.CooldownRemaining - dt );
		if ( s.DecreaseDelayRemaining > 0.0 )
			s.DecreaseDelayRemaining = Math.Max( 0.0, s.DecreaseDelayRemaining - dt );
		if ( s.SweepSecondsRemaining > 0.0 )
			s.SweepSecondsRemaining = Math.Max( 0.0, s.SweepSecondsRemaining - dt );
		if ( s.IngressAttractRemaining > 0.0 )
			s.IngressAttractRemaining = Math.Max( 0.0, s.IngressAttractRemaining - dt );

		var bans = s.IngressBanRemaining;
		if ( bans.Count == 0 )
			return;
		List<string> agedKeys = null;
		List<double> agedValues = null;
		foreach ( var kv in bans )
		{
			double remaining = kv.Value - dt;
			if ( agedKeys == null )
			{
				agedKeys = new List<string>();
				agedValues = new List<double>();
			}
			agedKeys.Add( kv.Key );
			agedValues.Add( remaining );
		}
		if ( agedKeys == null )
			return;
		for ( int i = 0; i < agedKeys.Count; i++ )
		{
			if ( agedValues[i] <= 0.0 )
				bans.Remove( agedKeys[i] );
			else
				bans[agedKeys[i]] = agedValues[i];
		}
	}

	private int RollQuotaTarget( EffectiveConfig.ResolvedPressure p )
	{
		int target = _rng.NextInt( p.EventQuotaMin, p.EventQuotaMax + 1 );
		return target < 1 ? 1 : target;
	}

	/// <summary>Whole ticks an opportunity stays valid; manual ceiling (positive inputs only).</summary>
	private static long ExpiryIntervalTicks( EffectiveConfig.ResolvedPressure p, double dt )
	{
		double raw = p.OpportunityExpirySeconds / dt;
		long ticks = (long)raw;
		if ( ticks < raw )
			ticks++;
		if ( ticks < 1 )
			ticks = 1;
		return ticks;
	}

	private static double Clamp01( double value )
		=> value < 0.0 ? 0.0 : ( value > 1.0 ? 1.0 : value );

	private void Emit( long tick, List<TelemetryEvent> telemetry, string code, string message )
	{
		telemetry.Add( new TelemetryEvent( tick, Category, code, message ) );
		var reasons = _state.RecentReasons;
		reasons.Add( code );
		while ( reasons.Count > PressureState.MaxRecentReasons )
			reasons.RemoveAt( 0 );
	}

	// ---- decision construction ----

	private PressureDecision BuildDecision( TickContext ctx, WorldSnapshot world, EffectiveConfig cfg, string reasonCode, List<string> excludedZoneIds )
	{
		var s = _state;
		var p = cfg.Pressure;
		bool aggressive = s.Mode == PressureMode.Aggressive;
		var evidence = new string[]
		{
			string.Format( CultureInfo.InvariantCulture, "fill={0:F3} p={1:F3} candidate={2} count={3}/{4}",
				p.FillSeconds, s.Progression, s.ActiveCandidateId.Length > 0 ? s.ActiveCandidateId : "-",
				s.CompletedOpportunities, p.MaxOpportunities ),
			string.Format( CultureInfo.InvariantCulture, "cooldown={0:F3} sweep={1:F3} quota={2}/{3}",
				s.CooldownRemaining, s.SweepSecondsRemaining, s.EventQuotaProgress, s.EventQuotaTarget ),
		};
		return new PressureDecision
		{
			OpportunityId = s.PendingOpportunityId,
			Mode = s.Mode,
			Progression = s.Progression,
			Urgency = aggressive ? 1.0 : s.Progression * ( s.CooldownRemaining > 0.0 ? 0.5 : 1.0 ),
			CandidateRegionId = s.ActiveCandidateId,
			AllowedRoles = aggressive ? new string[] { "stalker", "ambusher", "sweeper" } : new string[] { "stalker" },
			IngressConstraints = ComputeIngressConstraints( ctx, world, s ),
			ExclusionConstraints = excludedZoneIds == null ? new string[0] : excludedZoneIds.ToArray(),
			ExpiryTick = s.OpportunityExpiryTick,
			ReasonCode = reasonCode,
			Evidence = evidence,
		};
	}

	/// <summary>
	/// Ingress hints toward the latched candidate region: direct region match or offstage
	/// adjacency; must be usable, off host cooldown and not banned. Ordinal order.
	/// </summary>
	private static string[] ComputeIngressConstraints( TickContext ctx, WorldSnapshot world, PressureState s )
	{
		if ( !s.CandidateLatched || s.ActiveCandidateId.Length == 0 )
			return new string[0];
		var ingress = world.IngressPoints;
		if ( ingress == null || ingress.Count == 0 )
			return new string[0];

		var ids = new List<string>();
		for ( int i = 0; i < ingress.Count; i++ )
		{
			var point = ingress[i];
			if ( point == null || point.IngressId == null )
				continue;
			if ( !point.Usable )
				continue;
			if ( point.CooldownUntilTick > ctx.TickIndex )
				continue;
			if ( s.IngressBanRemaining.ContainsKey( point.IngressId ) )
				continue;

			bool leads = point.RegionId == s.ActiveCandidateId;
			if ( !leads )
			{
				var regions = world.OffstageRegions;
				if ( regions != null )
				{
					for ( int r = 0; r < regions.Count; r++ )
					{
						var region = regions[r];
						if ( region == null )
							continue;
						if ( region.AdjacentRegionIds != null && region.IngressIds != null
							&& region.AdjacentRegionIds.Contains( s.ActiveCandidateId )
							&& region.IngressIds.Contains( point.IngressId ) )
						{
							leads = true;
							break;
						}
					}
				}
			}
			if ( leads )
				ids.Add( point.IngressId );
		}
		ids.Sort( StringComparer.Ordinal );
		return ids.ToArray();
	}
}

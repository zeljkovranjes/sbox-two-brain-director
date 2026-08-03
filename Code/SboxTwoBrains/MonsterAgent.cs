using System;
using System.Collections.Generic;
using System.Text.Json;
using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Micro autonomous agent ("what can I do right now from local perception and navigation").
/// Consumes immutable world snapshots plus the macro <see cref="PressureDecision"/> bias and
/// returns declarative <see cref="ActionRequest"/>s. Never moves entities directly.
///
/// Arbitration: ordered guarded modules (lifecycle/recovery → script → damage/stun →
/// retreat → threat response → ambush → attack → suspect/hide → investigate → search →
/// stalk/offstage → idle). Each module returns Ineligible, Running, Succeeded, Failed, or
/// an action request. First non-Ineligible module wins the tick; at most one new action is
/// emitted per tick. While a previous action is still awaited, only a strictly
/// higher-priority module may emit (preemption); everything else returns an empty batch.
///
/// Action id convention: ids are "a{tick}-{n}" with n a per-tick ordinal starting at 0
/// (kept in State.Counters), so ids are unique, stable and replay-deterministic.
///
/// Update contract (called by TwoBrainsSystem in tick order):
/// 1. <see cref="ApplyActionResults"/> — host acks; failures feed timers/flags/recovery.
/// 2. <see cref="ApplyDirectives"/> — script overrides (withdrawal, sequence, despawn).
/// 3. <see cref="Tick"/> — ages perception memory + timers, updates senses vs memory,
///    arbitrates modules, emits declarative requests.
///
/// Perception: current evidence (this tick's stimuli) and remembered evidence (decaying
/// <see cref="MemoryRecord"/>s) stay distinct. No omniscient target location unless
/// <see cref="WorldSnapshot.OmniscientTargets"/> is set (telemetry-visible). The macro bias
/// is latched in <see cref="MicroState.LastMacro"/> until its ExpiryTick because macro
/// decisions arrive episodically while modules need the bias every tick.
/// </summary>
public sealed class MonsterAgent
{
	private static readonly string[] DefaultOrder =
	{
		"Lifecycle", "ScriptOverride", "DamageStun", "Retreat", "ThreatResponse", "Ambush",
		"Attack", "SuspectResponse", "HidingTarget", "Investigate", "Search", "Stalk",
		"Offstage", "Idle",
	};

	private readonly DeterministicRng _rng;
	private readonly Dictionary<string, IAgentModule> _byName = new Dictionary<string, IAgentModule>( StringComparer.Ordinal );
	private MicroState _state = new MicroState();

	// CanonicalJson options plus the Vec3 round-trip fix (written bytes stay canonical).
	private static readonly JsonSerializerOptions StateOptions = CreateStateOptions();

	private static JsonSerializerOptions CreateStateOptions()
	{
		var options = new JsonSerializerOptions( CanonicalJson.Options );
		options.Converters.Add( new Vec3JsonConverter() );
		return options;
	}

	/// <summary>Creates an agent with fresh state. Rng must be a dedicated fork.</summary>
	public MonsterAgent( DeterministicRng rng )
	{
		_rng = rng ?? throw new ArgumentNullException( nameof( rng ) );
		Register( new LifecycleModule() );
		Register( new ScriptOverrideModule() );
		Register( new DamageStunModule() );
		Register( new RetreatModule() );
		Register( new ThreatResponseModule() );
		Register( new AmbushModule() );
		Register( new AttackModule() );
		Register( new SuspectResponseModule() );
		Register( new HidingTargetModule() );
		Register( new InvestigateModule() );
		Register( new SearchModule() );
		Register( new StalkModule() );
		Register( new OffstageModule() );
		Register( new IdleModule() );
	}

	private void Register( IAgentModule module ) => _byName.Add( module.Name, module );

	/// <summary>Live state (mutable only inside the agent).</summary>
	public MicroState State => _state;

	/// <summary>Host acknowledgements of earlier action requests.</summary>
	public void ApplyActionResults( TickContext ctx, EffectiveConfig cfg, IReadOnlyList<ActionResult> results, List<TelemetryEvent> telemetry )
	{
		if ( results == null || results.Count == 0 ) return;
		foreach ( var r in results )
		{
			if ( r == null || string.IsNullOrEmpty( r.ActionId ) ) continue;
			string id = r.ActionId;
			if ( !_state.PendingActions.TryGetValue( id, out long expiry ) )
			{
				telemetry.Add( new TelemetryEvent( ctx.TickIndex, "action", ReasonCodes.AckUnknown, id ) );
				continue;
			}
			_state.PendingMeta.TryGetValue( id, out PendingActionMeta meta );
			switch ( r.Status )
			{
				case ActionStatus.Succeeded:
				case ActionStatus.PartiallySucceeded:
					RemovePending( id );
					ClearAwaiting( id );
					if ( r.Status == ActionStatus.PartiallySucceeded )
						telemetry.Add( new TelemetryEvent( ctx.TickIndex, "action", ReasonCodes.ActionPartial, id ) );
					if ( meta != null && meta.Kind == ActionKind.UseIngress )
						IngressSucceeded( ctx, meta, telemetry );
					if ( meta != null && meta.Kind == ActionKind.Scripted )
						_state.ActiveScriptedSequence = "";
					break;
				case ActionStatus.Rejected:
					RemovePending( id );
					ClearAwaiting( id );
					ApplyFailure( ctx, cfg, meta );
					telemetry.Add( new TelemetryEvent( ctx.TickIndex, "action", ReasonCodes.ActionRejected, id + ( r.Detail != null ? " " + r.Detail : "" ) ) );
					break;
				case ActionStatus.Failed:
					RemovePending( id );
					ClearAwaiting( id );
					ApplyFailure( ctx, cfg, meta );
					telemetry.Add( new TelemetryEvent( ctx.TickIndex, "action", ReasonCodes.ActionFailed, id + ( r.Detail != null ? " " + r.Detail : "" ) ) );
					break;
				case ActionStatus.Interrupted:
					RemovePending( id );
					ClearAwaiting( id );
					telemetry.Add( new TelemetryEvent( ctx.TickIndex, "action", ReasonCodes.ActionInterrupted, id ) );
					break;
				case ActionStatus.Deferred:
					if ( _state.Timers.ContainsKey( StateKeys.DeferredPrefix + id ) )
					{
						// a second deferral is treated as a failure
						RemovePending( id );
						ClearAwaiting( id );
						ApplyFailure( ctx, cfg, meta );
						telemetry.Add( new TelemetryEvent( ctx.TickIndex, "action", ReasonCodes.ActionFailed, id + " deferred twice" ) );
					}
					else
					{
						_state.Timers[StateKeys.DeferredPrefix + id] = 1.0;
						long interval = meta != null && meta.IntervalTicks > 0 ? meta.IntervalTicks : 1;
						_state.PendingActions[id] = expiry + interval;
					}
					break;
			}
		}
	}

	private void RemovePending( string id )
	{
		_state.PendingActions.Remove( id );
		_state.PendingMeta.Remove( id );
		_state.Timers.Remove( StateKeys.DeferredPrefix + id );
	}

	private void ClearAwaiting( string id )
	{
		if ( id != _state.AwaitingActionId ) return;
		_state.AwaitingActionId = "";
		_state.ActiveModule = "";
		_state.ActiveIngressId = "";
	}

	private void ApplyFailure( TickContext ctx, EffectiveConfig cfg, PendingActionMeta meta )
	{
		if ( meta == null ) return;
		if ( meta.Kind == ActionKind.Attack )
			_state.Timers[StateKeys.AttackBan] = cfg.Combat.AttackBanSeconds;
		if ( meta.Kind == ActionKind.UseIngress && meta.IngressId.Length > 0 )
			_state.Timers[StateKeys.IngressBanPrefix + meta.IngressId] = cfg.Offstage.IngressBanSeconds;
		if ( AgentContext.IsMovementKind( meta.Kind ) )
		{
			_state.ConsecutiveNavFailures++;
			_state.LastNavFailureTick = ctx.TickIndex;
		}
	}

	private void IngressSucceeded( TickContext ctx, PendingActionMeta meta, List<TelemetryEvent> telemetry )
	{
		// every ingress traversal crosses the stage boundary
		if ( _state.Flags.Contains( StateKeys.Offstage ) )
		{
			_state.Flags.Remove( StateKeys.Offstage );
			_state.Flags.Remove( StateKeys.SweepActive );
			_state.Flags.Remove( StateKeys.SweepDwellFlag );
			telemetry.Add( new TelemetryEvent( ctx.TickIndex, "micro", ReasonCodes.SweepEnd, "ingress=" + meta.IngressId ) );
		}
		else
		{
			_state.Flags.Add( StateKeys.Offstage );
		}
	}

	/// <summary>Script overrides for this tick (ForceWithdrawal/PlayScriptedSequence/Despawn).</summary>
	public void ApplyDirectives( TickContext ctx, EffectiveConfig cfg, IReadOnlyList<ScriptDirective> directives, List<TelemetryEvent> telemetry )
	{
		if ( directives == null || directives.Count == 0 ) return;
		foreach ( var d in directives )
		{
			if ( d == null ) continue;
			switch ( d.Kind )
			{
				case ScriptDirectiveKind.ForceWithdrawal:
					_state.Motivations.Add( "retreat" );
					_state.Gauges[StateKeys.Retreat] = 1.0;
					_state.Flags.Add( StateKeys.ForcedRetreat );
					telemetry.Add( new TelemetryEvent( ctx.TickIndex, "micro", ReasonCodes.ScriptWithdrawal, "forced withdrawal" ) );
					break;
				case ScriptDirectiveKind.PlayScriptedSequence:
					_state.ActiveScriptedSequence = d.SequenceName ?? "";
					telemetry.Add( new TelemetryEvent( ctx.TickIndex, "micro", ReasonCodes.ScriptSequence, "sequence=" + ( d.SequenceName ?? "" ) ) );
					break;
				case ScriptDirectiveKind.Despawn:
					_state.Flags.Add( StateKeys.DespawnRequested );
					telemetry.Add( new TelemetryEvent( ctx.TickIndex, "micro", ReasonCodes.DespawnRequested, "despawn requested" ) );
					break;
			}
		}
	}

	/// <summary>
	/// Advances the agent for one tick and returns declarative action requests (possibly
	/// empty). <paramref name="macro"/> is this tick's macro bias and may be null.
	/// Appends telemetry for every module transition, sense gate and feasibility refusal.
	/// </summary>
	public List<ActionRequest> Tick( TickContext ctx, WorldSnapshot world, PressureDecision macro, EffectiveConfig cfg, List<TelemetryEvent> telemetry )
	{
		if ( world == null ) throw new ArgumentNullException( nameof( world ) );
		if ( cfg == null ) throw new ArgumentNullException( nameof( cfg ) );
		if ( telemetry == null ) throw new ArgumentNullException( nameof( telemetry ) );

		// latch the macro bias until its expiry (decisions arrive episodically)
		if ( macro != null ) _state.LastMacro = macro;
		if ( _state.LastMacro != null && _state.LastMacro.ExpiryTick <= ctx.TickIndex ) _state.LastMacro = null;

		var ac = new AgentContext( ctx, world, _state.LastMacro, cfg, _state, _rng, telemetry );

		// 1. age timers, maintain gauges/exposure, update perception memory + target evidence
		AgeTimers( ac );
		UpdateGauges( ac );
		Perception.ClearConfirmations( _state );
		Perception.Merge( ac );
		Perception.DecayAndPrune( ac );
		UpdateOmniscience( ac );
		ac.RefreshBestTarget();

		// 2. lapse expired pending actions
		LapsePending( ac );

		// 3. derive motivation flags
		UpdateMotivations( ac );

		// 4. arbitrate modules in effective order
		return Arbitrate( ac );
	}

	// ---- tick phase 1: aging ----

	private void AgeTimers( AgentContext ac )
	{
		if ( _state.Timers.Count == 0 ) return;
		List<KeyValuePair<string, double>> updates = null;
		List<string> expired = null;
		foreach ( var kv in _state.Timers )
		{
			if ( !StateKeys.IsAgedTimer( kv.Key ) ) continue;
			double v = kv.Value - ac.Dt;
			if ( v <= 0.0 ) ( expired ??= new List<string>() ).Add( kv.Key );
			else ( updates ??= new List<KeyValuePair<string, double>>() ).Add( new KeyValuePair<string, double>( kv.Key, v ) );
		}
		if ( updates != null )
			foreach ( var kv in updates ) _state.Timers[kv.Key] = kv.Value;
		if ( expired != null )
			foreach ( var key in expired ) _state.Timers.Remove( key );
	}

	private void UpdateGauges( AgentContext ac )
	{
		double dt = ac.Dt;
		// stun gauge (DamageStun module): damage drops add 2× the drop, decay 1.0 per second
		double health = ac.Monster.HealthFraction;
		double prev = _state.Gauges.TryGetValue( StateKeys.PrevHealth, out double ph ) ? ph : health;
		double stun = _state.Gauges.TryGetValue( StateKeys.Stun, out double sg ) ? sg : 0.0;
		double drop = prev - health;
		if ( drop > 0.0 ) stun += drop * 2.0;
		stun -= dt * 1.0;
		if ( stun < 0.0 ) stun = 0.0;
		_state.Gauges[StateKeys.Stun] = stun;
		_state.Gauges[StateKeys.PrevHealth] = health;

		// deterrent exposure accumulator (non-aged timer entry)
		bool exposed = false;
		foreach ( var stim in ac.World.CurrentStimuli )
			if ( stim != null && stim.Channel == SenseChannel.Damage && stim.Subtype == "deterrent" ) { exposed = true; break; }
		if ( !exposed )
		{
			foreach ( var t in ac.World.Targets )
			{
				if ( t != null && t.IsUsingDeterrent && t.IsVisible && t.IsValid && t.IsAlive
					&& ac.Monster.Position.PlanarDistanceTo( t.Position ) <= ac.Cfg.Threat.CloseDistance )
				{
					exposed = true;
					break;
				}
			}
		}
		_state.Timers[StateKeys.DeterrentExposure] = exposed ? ac.Timer( StateKeys.DeterrentExposure ) + dt : 0.0;

		// retreat gauge: rises under threat motivation with recent damage, decays otherwise
		double retreat = _state.Gauges.TryGetValue( StateKeys.Retreat, out double rg ) ? rg : 0.0;
		bool recentDamage = ac.Monster.LastDamageTick >= 0 && ( ac.TickIndex - ac.Monster.LastDamageTick ) * dt <= 5.0;
		if ( _state.Motivations.Contains( "threat" ) && recentDamage )
			retreat += dt * 0.2;
		else
		{
			retreat -= dt * 0.05;
			if ( retreat < 0.0 ) retreat = 0.0;
		}
		_state.Gauges[StateKeys.Retreat] = retreat;
	}

	private void UpdateOmniscience( AgentContext ac )
	{
		if ( ac.World.OmniscientTargets )
		{
			if ( _state.Flags.Add( StateKeys.OmniscienceOn ) )
				ac.EmitPerception( ReasonCodes.OmniscienceActive, "host granted omniscient target knowledge" );
		}
		else
		{
			_state.Flags.Remove( StateKeys.OmniscienceOn );
		}
	}

	// ---- tick phase 2: lapse ----

	private void LapsePending( AgentContext ac )
	{
		if ( _state.PendingActions.Count == 0 ) return;
		List<string> expired = null;
		foreach ( var kv in _state.PendingActions )
			if ( kv.Value < ac.TickIndex ) ( expired ??= new List<string>() ).Add( kv.Key );
		if ( expired == null ) return;
		foreach ( var id in expired )
		{
			_state.PendingActions.Remove( id );
			_state.PendingMeta.TryGetValue( id, out PendingActionMeta meta );
			_state.PendingMeta.Remove( id );
			_state.Timers.Remove( StateKeys.DeferredPrefix + id );
			ac.EmitAction( ReasonCodes.ActionLapsed, id + ( meta != null ? " " + meta.Kind : "" ) );
			if ( id == _state.AwaitingActionId )
			{
				_state.AwaitingActionId = "";
				_state.ActiveModule = "";
				_state.ActiveIngressId = "";
				if ( meta != null && AgentContext.IsMovementKind( meta.Kind ) )
				{
					_state.ConsecutiveNavFailures++;
					_state.LastNavFailureTick = ac.TickIndex;
				}
			}
		}
	}

	// ---- tick phase 3: motivations ----

	private void UpdateMotivations( AgentContext ac )
	{
		var m = _state.Motivations;
		m.Clear();
		var best = ac.BestTarget();
		if ( best != null && best.IsCurrent && AttackFeasible( ac, best ) ) m.Add( "attack" );
		if ( Threatened( ac ) ) m.Add( "threat" );
		if ( ac.Monster.HealthFraction < 0.35
			|| ac.Timer( StateKeys.DeterrentExposure ) >= ac.Cfg.Threat.DeterrentRetreatSeconds
			|| ac.Gauge( StateKeys.Retreat ) >= 1.0
			|| _state.Flags.Contains( StateKeys.ForcedRetreat ) ) m.Add( "retreat" );
		if ( _state.Memories.Count > 0 && ( best == null || !best.IsCurrent ) ) m.Add( "search" );
		if ( ac.BestUnattributed() != null ) m.Add( "investigate" );
		var macro = ac.Macro;
		if ( macro != null && !string.IsNullOrEmpty( macro.CandidateRegionId ) ) m.Add( "stalk" );
		if ( macro != null && macro.Mode == PressureMode.Aggressive && ac.HasRole( "sweeper" ) ) m.Add( "offstage" );
		if ( _state.ActiveScriptedSequence.Length > 0 ) m.Add( "script" );
	}

	private bool Threatened( AgentContext ac )
	{
		foreach ( var t in ac.World.Targets )
		{
			if ( t == null || !t.IsValid || !t.IsAlive ) continue;
			if ( t.IsAimingAtMonster ) return true;
			if ( t.IsVisible && t.ThreatRating >= ac.Cfg.Threat.DangerousThreatRating
				&& ac.Monster.Position.PlanarDistanceTo( t.Position ) <= ac.Cfg.Threat.CloseDistance ) return true;
		}
		return ac.Timer( StateKeys.DeterrentExposure ) > 0.0;
	}

	private bool AttackFeasible( AgentContext ac, TargetEvidence best )
	{
		if ( !ac.Monster.CanAttack ) return false;
		if ( ac.TimerActive( StateKeys.AttackCooldown ) || ac.TimerActive( StateKeys.AttackBan ) ) return false;
		var t = ac.FindTarget( best.TargetId );
		if ( t == null || !t.IsValid || !t.IsAlive ) return false;
		return ac.RouteOrPlanar( best.Position, best.RegionId ) <= ac.Cfg.Combat.ChaseGiveUpDistance;
	}

	// ---- tick phase 4: arbitration ----

	private List<IAgentModule> ResolveOrder( AgentContext ac )
	{
		var order = ac.Cfg.Modules.Order;
		var names = order != null && order.Length > 0 ? order : DefaultOrder;
		var disabled = ac.Cfg.Modules.Disabled;
		var list = new List<IAgentModule>( names.Length );
		foreach ( var name in names )
		{
			if ( name == null ) continue;
			bool off = false;
			if ( disabled != null )
				foreach ( var d in disabled )
					if ( d == name ) { off = true; break; }
			if ( off ) continue;
			if ( _byName.TryGetValue( name, out var module ) ) list.Add( module );
			else ac.Emit( ReasonCodes.ModuleUnknown, name );
		}
		return list;
	}

	private List<ActionRequest> Arbitrate( AgentContext ac )
	{
		var empty = new List<ActionRequest>();
		var modules = ResolveOrder( ac );
		string awaitingId = _state.AwaitingActionId;
		bool awaiting = awaitingId.Length > 0 && _state.PendingActions.ContainsKey( awaitingId );
		int awaitingIndex = modules.Count; // an unknown issuer blocks every emission
		if ( awaiting )
		{
			for ( int i = 0; i < modules.Count; i++ )
				if ( modules[i].Name == _state.ActiveModule ) { awaitingIndex = i; break; }
		}

		for ( int i = 0; i < modules.Count; i++ )
		{
			var module = modules[i];
			ac.MayEmit = !awaiting || i < awaitingIndex;
			ac.ModuleName = module.Name;
			var result = module.Evaluate( ac );
			if ( result == null || result.Status == ModuleStatus.Ineligible ) continue;
			if ( result.Action == null ) return empty;
			if ( awaiting && i >= awaitingIndex ) return empty; // outstanding action blocks this priority
			if ( !ac.CheckFeasibility( result.Action, out string why ) )
			{
				ac.EmitAction( ReasonCodes.ActionInfeasible, module.Name + ": " + why );
				return empty;
			}
			if ( awaiting )
				ac.EmitAction( ReasonCodes.Preempt, module.Name + " preempts " + awaitingId );
			var action = Commit( ac, module.Name, result );
			if ( result.ReasonCode.Length > 0 )
				ac.Emit( result.ReasonCode, action.Kind + " id=" + action.ActionId );
			return new List<ActionRequest> { action };
		}
		return empty;
	}

	private ActionRequest Commit( AgentContext ac, string moduleName, ModuleResult result )
	{
		var s = _state;
		long ordinal = 0;
		if ( s.Counters.TryGetValue( StateKeys.ActionTick, out long actionTick ) && actionTick == ac.TickIndex )
			ordinal = ( s.Counters.TryGetValue( StateKeys.ActionOrdinal, out long o ) ? o : 0 ) + 1;
		s.Counters[StateKeys.ActionTick] = ac.TickIndex;
		s.Counters[StateKeys.ActionOrdinal] = ordinal;

		var draft = result.Action;
		draft.ActionId = "a" + ac.TickIndex + "-" + ordinal;
		double timeout = result.TimeoutSeconds > 0.0 ? result.TimeoutSeconds : 10.0;
		long interval = ac.TicksFor( timeout );
		draft.ExpiryTick = ac.TickIndex + interval;
		if ( draft.ReasonCode.Length == 0 ) draft.ReasonCode = result.ReasonCode;

		s.PendingActions[draft.ActionId] = draft.ExpiryTick;
		s.PendingMeta[draft.ActionId] = new PendingActionMeta
		{
			Kind = draft.Kind,
			IntervalTicks = interval,
			TargetId = draft.TargetId ?? "",
			NodeId = draft.NodeId ?? "",
			RegionId = draft.RegionId ?? "",
			IngressId = draft.IngressId ?? "",
			StimulusId = draft.StimulusId ?? "",
			Param = draft.Param ?? "",
			Destination = draft.Destination,
		};
		s.AwaitingActionId = draft.ActionId;
		s.ActiveModule = moduleName;
		if ( draft.Kind == ActionKind.UseIngress ) s.ActiveIngressId = draft.IngressId ?? "";
		return draft;
	}

	/// <summary>Resets runtime state (lifecycle reset); keeps nothing from the previous episode.</summary>
	public void Reset( TickContext ctx, EffectiveConfig cfg, List<TelemetryEvent> telemetry )
	{
		_state = new MicroState();
		if ( telemetry != null )
			telemetry.Add( new TelemetryEvent( ctx.TickIndex, "micro", ReasonCodes.MicroReset, "micro state reset" ) );
	}

	/// <summary>Canonical JSON of complete state (rng words are saved by the facade).</summary>
	public string CaptureState() => JsonSerializer.Serialize( _state, StateOptions );

	/// <summary>Restores state previously produced by <see cref="CaptureState"/>.</summary>
	public void RestoreState( string json )
	{
		if ( json == null ) throw new ArgumentNullException( nameof( json ) );
		var s = JsonSerializer.Deserialize<MicroState>( json, StateOptions ) ?? new MicroState();
		s.Memories ??= new List<MemoryRecord>();
		s.Motivations = s.Motivations == null ? new SortedSet<string>( StringComparer.Ordinal ) : new SortedSet<string>( s.Motivations, StringComparer.Ordinal );
		s.PendingActions = s.PendingActions == null ? new SortedDictionary<string, long>( StringComparer.Ordinal ) : new SortedDictionary<string, long>( s.PendingActions, StringComparer.Ordinal );
		s.PendingMeta = s.PendingMeta == null ? new SortedDictionary<string, PendingActionMeta>( StringComparer.Ordinal ) : new SortedDictionary<string, PendingActionMeta>( s.PendingMeta, StringComparer.Ordinal );
		s.Timers = s.Timers == null ? new SortedDictionary<string, double>( StringComparer.Ordinal ) : new SortedDictionary<string, double>( s.Timers, StringComparer.Ordinal );
		s.Gauges = s.Gauges == null ? new SortedDictionary<string, double>( StringComparer.Ordinal ) : new SortedDictionary<string, double>( s.Gauges, StringComparer.Ordinal );
		s.Counters = s.Counters == null ? new SortedDictionary<string, long>( StringComparer.Ordinal ) : new SortedDictionary<string, long>( s.Counters, StringComparer.Ordinal );
		s.Flags = s.Flags == null ? new SortedSet<string>( StringComparer.Ordinal ) : new SortedSet<string>( s.Flags, StringComparer.Ordinal );
		s.ActiveModule ??= "";
		s.AwaitingActionId ??= "";
		s.CurrentTargetId ??= "";
		s.LastSearchRegionId ??= "";
		s.InvestigationStimulusId ??= "";
		s.ActiveIngressId ??= "";
		s.ActiveScriptedSequence ??= "";
		_state = s;
	}
}

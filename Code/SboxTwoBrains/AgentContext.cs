using System.Collections.Generic;
using System.Globalization;
using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>Where a piece of target evidence came from (current senses, memory, omniscience).</summary>
internal enum EvidenceSource
{
	None = 0,
	CurrentVisual = 1,
	CurrentOther = 2,
	Memory = 3,
	Omniscient = 4,
}

/// <summary>One resolved piece of evidence (target-attributed or unattributed).</summary>
internal sealed class TargetEvidence
{
	public EvidenceSource Source = EvidenceSource.None;
	public string TargetId = "";
	public string StimulusId = "";
	public Vec3 Position;
	public string RegionId = "";
	public double Confidence;
	public SenseChannel Channel;

	/// <summary>True when this evidence comes from the current tick (not memory).</summary>
	public bool IsCurrent => Source == EvidenceSource.CurrentVisual || Source == EvidenceSource.CurrentOther || Source == EvidenceSource.Omniscient;
}

/// <summary>
/// Per-tick carrier handed to every module: tick context, world snapshot, the effective
/// (latched) macro bias, config, live state, the deterministic RNG fork and the telemetry
/// sink. Also hosts the shared deterministic queries — evidence selection, route/planar
/// distance lookups, node/ingress selection helpers and the feasibility gates every
/// action-emitting module must pass. A fresh instance is created per tick; nothing here
/// persists between ticks except through <see cref="MicroState"/>.
/// </summary>
internal sealed class AgentContext
{
	public readonly TickContext Tick;
	public readonly WorldSnapshot World;
	public readonly PressureDecision Macro;
	public readonly EffectiveConfig Cfg;
	public readonly MicroState State;
	public readonly DeterministicRng Rng;
	public readonly List<TelemetryEvent> Telemetry;

	/// <summary>Set by the arbitrator: false while an awaited action blocks this module's priority.</summary>
	public bool MayEmit = true;

	/// <summary>Name of the module currently being evaluated (diagnostics).</summary>
	public string ModuleName = "";

	private TargetEvidence _bestTarget;
	private TargetEvidence _bestUnattributed;
	private bool _unattributedComputed;

	public AgentContext( TickContext tick, WorldSnapshot world, PressureDecision macro, EffectiveConfig cfg, MicroState state, DeterministicRng rng, List<TelemetryEvent> telemetry )
	{
		Tick = tick;
		World = world;
		Macro = macro;
		Cfg = cfg;
		State = state;
		Rng = rng;
		Telemetry = telemetry;
	}

	public long TickIndex => Tick.TickIndex;
	public double Dt => Tick.DeltaTimeSeconds;
	public MonsterSnapshot Monster => World.Monster;

	// ---- telemetry ----

	public void Emit( string code, string message ) => Telemetry.Add( new TelemetryEvent( TickIndex, "micro", code, message ) );
	public void EmitAction( string code, string message ) => Telemetry.Add( new TelemetryEvent( TickIndex, "action", code, message ) );
	public void EmitPerception( string code, string message ) => Telemetry.Add( new TelemetryEvent( TickIndex, "perception", code, message ) );

	// ---- small state accessors ----

	public double Timer( string key ) => State.Timers.TryGetValue( key, out double value ) ? value : 0.0;
	public bool TimerActive( string key ) => State.Timers.ContainsKey( key );
	public double Gauge( string key ) => State.Gauges.TryGetValue( key, out double value ) ? value : 0.0;

	public bool HasRole( string role )
	{
		if ( Macro == null || Macro.AllowedRoles == null ) return false;
		foreach ( var r in Macro.AllowedRoles )
			if ( r == role ) return true;
		return false;
	}

	/// <summary>
	/// True when a destination region is compatible with the monster's current stage.
	/// An offstage monster may not path into frontstage space — there is no navmesh route
	/// through sealed offstage boundaries, so such moves wedge the agent. Those modules
	/// must yield (Ineligible) and let the Offstage module egress via an ingress first.
	/// Unknown/empty regions are allowed (planar fallback remains possible).
	/// </summary>
	public bool StageCompatible( string regionId )
	{
		if ( Monster.Presence != StagePresence.Offstage )
			return true;
		if ( string.IsNullOrEmpty( regionId ) )
			return true;
		foreach ( var region in World.OffstageRegions )
			if ( region.RegionId == regionId )
				return true;
		return false;
	}

	// ---- perception channel helpers ----

	public EffectiveConfig.ResolvedPerceptionChannel ChannelCfg( SenseChannel channel )
	{
		switch ( channel )
		{
			case SenseChannel.Visual: return Cfg.Perception.Visual;
			case SenseChannel.Auditory: return Cfg.Perception.Auditory;
			case SenseChannel.Touch: return Cfg.Perception.Touch;
			case SenseChannel.Damage: return Cfg.Perception.Damage;
			case SenseChannel.Light: return Cfg.Perception.Light;
			default: return Cfg.Perception.GameDefined;
		}
	}

	/// <summary>
	/// Sense activation: any current stimulus on the channel at or above its threshold, plus
	/// the active latch — a memory on the channel confirmed within RecentConfirmationSeconds
	/// that is still at or above threshold keeps the sense engaged briefly.
	/// </summary>
	public bool SenseActive( SenseChannel channel )
	{
		double threshold = ChannelCfg( channel ).Threshold;
		foreach ( var s in World.CurrentStimuli )
			if ( s != null && s.Channel == channel && s.Confidence >= threshold )
				return true;
		foreach ( var m in State.Memories )
		{
			if ( m == null || m.Channel != channel || m.DecayedConfidence < threshold ) continue;
			if ( ( TickIndex - m.LastConfirmedTick ) * Dt <= Cfg.Perception.RecentConfirmationSeconds )
				return true;
		}
		return false;
	}

	// ---- target evidence ----

	/// <summary>
	/// Resolves the best target evidence for this tick and applies the current-evidence side
	/// effects (CurrentTargetId / LastSensedTargetTick / LastSensedTargetPosition). Called
	/// once per tick by the agent after memory maintenance.
	/// </summary>
	public void RefreshBestTarget()
	{
		_bestTarget = ComputeBestTarget();
		if ( _bestTarget != null && _bestTarget.IsCurrent )
		{
			if ( _bestTarget.TargetId != State.CurrentTargetId )
				State.Flags.Remove( StateKeys.SuspectResponded );
			State.CurrentTargetId = _bestTarget.TargetId;
			State.LastSensedTargetTick = TickIndex;
			State.LastSensedTargetPosition = _bestTarget.Position;
		}
		if ( State.CurrentTargetId.Length > 0 )
		{
			var t = FindTarget( State.CurrentTargetId );
			if ( t != null && ( !t.IsValid || !t.IsAlive ) )
			{
				State.CurrentTargetId = "";
				State.Flags.Remove( StateKeys.Chasing );
				State.Flags.Remove( StateKeys.SuspectResponded );
			}
		}
	}

	/// <summary>Best target evidence resolved this tick; null when there is none.</summary>
	public TargetEvidence BestTarget() => _bestTarget;

	private TargetEvidence ComputeBestTarget()
	{
		if ( World.OmniscientTargets )
		{
			TargetSnapshot pick = null;
			double pickDist = 0.0;
			foreach ( var t in World.Targets )
			{
				if ( t == null || !t.IsValid || !t.IsAlive ) continue;
				double d = Monster.Position.PlanarDistanceTo( t.Position );
				if ( pick == null || d < pickDist || ( d == pickDist && string.CompareOrdinal( t.TargetId, pick.TargetId ) < 0 ) )
				{
					pick = t;
					pickDist = d;
				}
			}
			if ( pick != null )
			{
				return new TargetEvidence
				{
					Source = EvidenceSource.Omniscient,
					TargetId = pick.TargetId,
					Position = pick.Position,
					RegionId = pick.RegionId ?? "",
					Confidence = 1.0,
					Channel = SenseChannel.Visual,
				};
			}
		}

		// (a) current visual stimulus attributed to a target, highest confidence
		TargetEvidence best = null;
		foreach ( var s in World.CurrentStimuli )
		{
			if ( s == null || s.Channel != SenseChannel.Visual || string.IsNullOrEmpty( s.TargetId ) ) continue;
			if ( !TargetUsable( s.TargetId ) ) continue;
			if ( best == null || s.Confidence > best.Confidence || ( s.Confidence == best.Confidence && string.CompareOrdinal( s.StimulusId, best.StimulusId ) < 0 ) )
				best = FromStimulus( s, EvidenceSource.CurrentVisual );
		}
		if ( best != null ) return best;

		// (b) current stimulus of any channel attributed to a target, highest confidence
		foreach ( var s in World.CurrentStimuli )
		{
			if ( s == null || string.IsNullOrEmpty( s.TargetId ) ) continue;
			if ( !TargetUsable( s.TargetId ) ) continue;
			if ( best == null || s.Confidence > best.Confidence || ( s.Confidence == best.Confidence && string.CompareOrdinal( s.StimulusId, best.StimulusId ) < 0 ) )
				best = FromStimulus( s, EvidenceSource.CurrentOther );
		}
		if ( best != null ) return best;

		// (c) remembered evidence attributed to a target, highest decayed confidence
		foreach ( var m in State.Memories )
		{
			if ( m == null || string.IsNullOrEmpty( m.TargetId ) ) continue;
			if ( !TargetUsable( m.TargetId ) ) continue;
			if ( best == null || m.DecayedConfidence > best.Confidence || ( m.DecayedConfidence == best.Confidence && string.CompareOrdinal( m.StimulusId, best.StimulusId ) < 0 ) )
			{
				best = new TargetEvidence
				{
					Source = EvidenceSource.Memory,
					TargetId = m.TargetId,
					StimulusId = m.StimulusId,
					Position = m.Position,
					RegionId = m.RegionId ?? "",
					Confidence = m.DecayedConfidence,
					Channel = m.Channel,
				};
			}
		}
		return best;
	}

	private bool TargetUsable( string targetId )
	{
		var t = FindTarget( targetId );
		return t == null || ( t.IsValid && t.IsAlive );
	}

	private static TargetEvidence FromStimulus( Stimulus s, EvidenceSource source )
	{
		return new TargetEvidence
		{
			Source = source,
			TargetId = s.TargetId,
			StimulusId = s.StimulusId,
			Position = s.Position,
			RegionId = s.RegionId ?? "",
			Confidence = s.Confidence,
			Channel = s.Channel,
		};
	}

	/// <summary>
	/// Best unattributed evidence (no TargetId) at or above its channel threshold: current
	/// stimuli first (highest confidence), then memories (highest decayed confidence).
	/// </summary>
	public TargetEvidence BestUnattributed()
	{
		if ( _unattributedComputed ) return _bestUnattributed;
		_unattributedComputed = true;
		TargetEvidence best = null;
		foreach ( var s in World.CurrentStimuli )
		{
			if ( s == null || !string.IsNullOrEmpty( s.TargetId ) ) continue;
			if ( s.Confidence < ChannelCfg( s.Channel ).Threshold ) continue;
			if ( best == null || s.Confidence > best.Confidence || ( s.Confidence == best.Confidence && string.CompareOrdinal( s.StimulusId, best.StimulusId ) < 0 ) )
				best = FromStimulus( s, EvidenceSource.CurrentOther );
		}
		if ( best == null )
		{
			foreach ( var m in State.Memories )
			{
				if ( m == null || !string.IsNullOrEmpty( m.TargetId ) ) continue;
				if ( m.DecayedConfidence < ChannelCfg( m.Channel ).Threshold ) continue;
				if ( best == null || m.DecayedConfidence > best.Confidence || ( m.DecayedConfidence == best.Confidence && string.CompareOrdinal( m.StimulusId, best.StimulusId ) < 0 ) )
				{
					best = new TargetEvidence
					{
						Source = EvidenceSource.Memory,
						StimulusId = m.StimulusId,
						Position = m.Position,
						RegionId = m.RegionId ?? "",
						Confidence = m.DecayedConfidence,
						Channel = m.Channel,
					};
				}
			}
		}
		if ( best != null ) best.TargetId = "";
		_bestUnattributed = best;
		return best;
	}

	/// <summary>Locates a stimulus id among current stimuli first, then memories.</summary>
	public TargetEvidence FindStimulusOrMemory( string stimulusId )
	{
		foreach ( var s in World.CurrentStimuli )
			if ( s != null && s.StimulusId == stimulusId )
				return FromStimulus( s, EvidenceSource.CurrentOther );
		foreach ( var m in State.Memories )
		{
			if ( m != null && m.StimulusId == stimulusId )
			{
				return new TargetEvidence
				{
					Source = EvidenceSource.Memory,
					TargetId = m.TargetId ?? "",
					StimulusId = m.StimulusId,
					Position = m.Position,
					RegionId = m.RegionId ?? "",
					Confidence = m.DecayedConfidence,
					Channel = m.Channel,
				};
			}
		}
		return null;
	}

	// ---- world lookups ----

	public TargetSnapshot FindTarget( string targetId )
	{
		if ( string.IsNullOrEmpty( targetId ) ) return null;
		foreach ( var t in World.Targets )
			if ( t != null && t.TargetId == targetId ) return t;
		return null;
	}

	public NavCandidate FindNode( string nodeId )
	{
		if ( string.IsNullOrEmpty( nodeId ) ) return null;
		foreach ( var n in World.NavCandidates )
			if ( n != null && n.NodeId == nodeId ) return n;
		return null;
	}

	public IngressPoint FindIngress( string ingressId )
	{
		if ( string.IsNullOrEmpty( ingressId ) ) return null;
		foreach ( var i in World.IngressPoints )
			if ( i != null && i.IngressId == ingressId ) return i;
		return null;
	}

	// ---- distance helpers (route distance when the host supplies it, else planar) ----

	/// <summary>Route distance to a position via the nearest reachable candidate in its region.</summary>
	public bool TryRouteDistance( Vec3 to, string regionId, out double distance )
	{
		distance = 0.0;
		if ( string.IsNullOrEmpty( regionId ) ) return false;
		NavCandidate pick = null;
		double pickPlanar = 0.0;
		foreach ( var n in World.NavCandidates )
		{
			if ( n == null || !n.Reachable || n.RegionId != regionId ) continue;
			double d = n.Position.PlanarDistanceTo( to );
			if ( pick == null || d < pickPlanar || ( d == pickPlanar && string.CompareOrdinal( n.NodeId, pick.NodeId ) < 0 ) )
			{
				pick = n;
				pickPlanar = d;
			}
		}
		if ( pick == null || pick.RouteDistance < 0.0 ) return false;
		distance = pick.RouteDistance;
		return true;
	}

	public double RouteOrPlanar( Vec3 to, string regionId )
	{
		return TryRouteDistance( to, regionId, out double d ) ? d : Monster.Position.PlanarDistanceTo( to );
	}

	/// <summary>Movement timeout: route-distance based (~0.5 m/s worst case plus slack) or 10 s.</summary>
	public double MovementTimeout( Vec3 to, string regionId )
	{
		return TryRouteDistance( to, regionId, out double d ) ? d * 2.0 + 2.0 : 10.0;
	}

	// ---- navigation selection helpers (deterministic: metric first, ordinal id tie-break) ----

	/// <summary>Reachable candidates of a kind in a region, nearest-first (route or planar), ordinal tie-break.</summary>
	public List<NavCandidate> SortedReachableNodes( string regionId, NavCandidateKind kind )
	{
		var list = new List<NavCandidate>();
		foreach ( var n in World.NavCandidates )
		{
			if ( n == null || !n.Reachable || n.Kind != kind ) continue;
			if ( regionId != null && n.RegionId != regionId ) continue;
			list.Add( n );
		}
		var mp = Monster.Position;
		list.Sort( ( a, b ) =>
		{
			double da = a.RouteDistance >= 0.0 ? a.RouteDistance : mp.PlanarDistanceTo( a.Position );
			double db = b.RouteDistance >= 0.0 ? b.RouteDistance : mp.PlanarDistanceTo( b.Position );
			int c = da.CompareTo( db );
			return c != 0 ? c : string.CompareOrdinal( a.NodeId, b.NodeId );
		} );
		return list;
	}

	/// <summary>Reachable frontstage candidate in a region nearest planar to a position.</summary>
	public NavCandidate NearestNodeTo( Vec3 pos, string regionId )
	{
		NavCandidate pick = null;
		double pickDist = 0.0;
		foreach ( var n in World.NavCandidates )
		{
			if ( n == null || !n.Reachable || n.Kind != NavCandidateKind.FrontstageNode ) continue;
			if ( regionId != null && n.RegionId != regionId ) continue;
			double d = n.Position.PlanarDistanceTo( pos );
			if ( pick == null || d < pickDist || ( d == pickDist && string.CompareOrdinal( n.NodeId, pick.NodeId ) < 0 ) )
			{
				pick = n;
				pickDist = d;
			}
		}
		return pick;
	}

	/// <summary>Reachable frontstage candidate (any region) farthest planar from a position.</summary>
	public NavCandidate FarthestNodeFrom( Vec3 pos )
	{
		NavCandidate pick = null;
		double pickDist = 0.0;
		foreach ( var n in World.NavCandidates )
		{
			if ( n == null || !n.Reachable || n.Kind != NavCandidateKind.FrontstageNode ) continue;
			double d = n.Position.PlanarDistanceTo( pos );
			if ( pick == null || d > pickDist || ( d == pickDist && string.CompareOrdinal( n.NodeId, pick.NodeId ) < 0 ) )
			{
				pick = n;
				pickDist = d;
			}
		}
		return pick;
	}

	/// <summary>True when the node was visited within the given number of seconds.</summary>
	public bool NodeRecentlyVisited( string nodeId, double seconds )
	{
		if ( !State.Timers.TryGetValue( StateKeys.VisitedPrefix + nodeId, out double stamp ) ) return false;
		return ( TickIndex - (long)stamp ) * Dt < seconds;
	}

	public void MarkNodeVisited( string nodeId ) => State.Timers[StateKeys.VisitedPrefix + nodeId] = TickIndex;

	/// <summary>Tick the node was last marked visited; -1 when never.</summary>
	public long NodeVisitedTick( string nodeId )
	{
		return State.Timers.TryGetValue( StateKeys.VisitedPrefix + nodeId, out double stamp ) ? (long)stamp : -1;
	}

	/// <summary>True when a position lies inside an active exclusion zone listed by the macro.</summary>
	public bool InsideExclusion( Vec3 pos )
	{
		if ( Macro == null || Macro.ExclusionConstraints == null || Macro.ExclusionConstraints.Length == 0 ) return false;
		foreach ( var zone in World.ExclusionZones )
		{
			if ( zone == null || !zone.Active ) continue;
			bool listed = false;
			foreach ( var id in Macro.ExclusionConstraints )
				if ( id == zone.ZoneId ) { listed = true; break; }
			if ( !listed ) continue;
			if ( pos.PlanarDistanceTo( zone.Center ) <= zone.Radius ) return true;
		}
		return false;
	}

	// ---- ingress helpers ----

	/// <summary>Usable right now: host flag, host cooldown elapsed and no local ban timer.</summary>
	public bool IngressUsable( IngressPoint ing )
	{
		if ( ing == null || !ing.Usable ) return false;
		if ( ing.CooldownUntilTick >= 0 && TickIndex < ing.CooldownUntilTick ) return false;
		if ( State.Timers.ContainsKey( StateKeys.IngressBanPrefix + ing.IngressId ) ) return false;
		return true;
	}

	/// <summary>All usable ingress points, nearest-first by planar distance, ordinal id tie-break.</summary>
	public List<IngressPoint> UsableIngresses()
	{
		var list = new List<IngressPoint>();
		foreach ( var ing in World.IngressPoints )
			if ( IngressUsable( ing ) ) list.Add( ing );
		var mp = Monster.Position;
		list.Sort( ( a, b ) =>
		{
			int c = mp.PlanarDistanceTo( a.Position ).CompareTo( mp.PlanarDistanceTo( b.Position ) );
			return c != 0 ? c : string.CompareOrdinal( a.IngressId, b.IngressId );
		} );
		return list;
	}

	public IngressPoint NearestUsableIngress()
	{
		var list = UsableIngresses();
		return list.Count > 0 ? list[0] : null;
	}

	/// <summary>Nearest usable ingress in a region; any region when <paramref name="regionId"/> is empty.</summary>
	public IngressPoint IngressToward( string regionId )
	{
		IngressPoint pick = null;
		double pickDist = 0.0;
		foreach ( var ing in World.IngressPoints )
		{
			if ( !IngressUsable( ing ) ) continue;
			if ( !string.IsNullOrEmpty( regionId ) && ing.RegionId != regionId ) continue;
			double d = Monster.Position.PlanarDistanceTo( ing.Position );
			if ( pick == null || d < pickDist || ( d == pickDist && string.CompareOrdinal( ing.IngressId, pick.IngressId ) < 0 ) )
			{
				pick = ing;
				pickDist = d;
			}
		}
		return pick;
	}

	// ---- action drafting and feasibility ----

	public ActionRequest Draft( ActionKind kind, string reasonCode )
	{
		return new ActionRequest { Kind = kind, ReasonCode = reasonCode ?? "" };
	}

	/// <summary>Kinds that move the monster through the world (CanMove / nav-backoff gated).</summary>
	public static bool IsMovementKind( ActionKind kind )
	{
		return kind == ActionKind.MoveTo || kind == ActionKind.Search || kind == ActionKind.Investigate
			|| kind == ActionKind.Stalk || kind == ActionKind.Ambush || kind == ActionKind.Chase
			|| kind == ActionKind.Retreat;
	}

	/// <summary>
	/// Feasibility gates applied to every action before it is issued. On refusal the reason is
	/// returned for telemetry and the action is dropped (the module keeps Running).
	/// </summary>
	public bool CheckFeasibility( ActionRequest a, out string reason )
	{
		reason = "";
		if ( Monster.Lifecycle != MonsterLifecycle.Alive ) { reason = "monster_not_alive"; return false; }
		bool movement = IsMovementKind( a.Kind );
		if ( movement && !Monster.CanMove ) { reason = "cannot_move"; return false; }
		if ( movement && TimerActive( StateKeys.NavBackoff ) ) { reason = "nav_backoff"; return false; }
		if ( a.Kind == ActionKind.Attack && !Monster.CanAttack ) { reason = "cannot_attack"; return false; }
		if ( a.Kind == ActionKind.UseIngress )
		{
			if ( !Monster.CanTraverseIngress ) { reason = "cannot_traverse"; return false; }
			var ing = FindIngress( a.IngressId );
			if ( ing == null || !IngressUsable( ing ) ) { reason = "ingress_unusable"; return false; }
		}
		if ( a.Kind == ActionKind.Scripted && !Monster.CanPlayScripted ) { reason = "cannot_script"; return false; }
		if ( !string.IsNullOrEmpty( a.TargetId ) )
		{
			var t = FindTarget( a.TargetId );
			if ( t == null || !t.IsValid || !t.IsAlive ) { reason = "target_invalid"; return false; }
		}
		if ( !string.IsNullOrEmpty( a.NodeId ) )
		{
			var n = FindNode( a.NodeId );
			if ( n == null || !n.Reachable ) { reason = "node_unreachable"; return false; }
		}
		if ( IsDuplicateOfAwaited( a ) ) { reason = "duplicate_awaited"; return false; }
		return true;
	}

	private bool IsDuplicateOfAwaited( ActionRequest a )
	{
		string awaiting = State.AwaitingActionId;
		if ( awaiting.Length == 0 ) return false;
		if ( !State.PendingMeta.TryGetValue( awaiting, out PendingActionMeta m ) ) return false;
		return m.Kind == a.Kind
			&& m.TargetId == ( a.TargetId ?? "" )
			&& m.NodeId == ( a.NodeId ?? "" )
			&& m.RegionId == ( a.RegionId ?? "" )
			&& m.IngressId == ( a.IngressId ?? "" )
			&& m.StimulusId == ( a.StimulusId ?? "" )
			&& m.Param == ( a.Param ?? "" )
			&& m.Destination.Equals( a.Destination );
	}

	/// <summary>Ceil of a duration in whole ticks, minimum 1 (uses only + * / and casts).</summary>
	public long TicksFor( double seconds )
	{
		long t = (long)( seconds / Dt );
		if ( t * Dt < seconds ) t++;
		if ( t < 1 ) t = 1;
		return t;
	}

	/// <summary>Canonical formatting for durations carried in ActionRequest.Param.</summary>
	public static string FormatSeconds( double seconds ) => seconds.ToString( "R", CultureInfo.InvariantCulture );
}

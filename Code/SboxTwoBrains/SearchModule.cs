using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Priority 11 — systematic search. While memories exist (or the last search region is
/// still inside the systematic window) and there is no current target evidence, the monster
/// sweeps reachable frontstage nodes in the best memory's region, nearest-first, skipping
/// nodes visited within the revisit penalty. An episode ends after MaxNodesPerSearch nodes,
/// on GiveUpSeconds, or when candidates run out; a cooldown prevents instant restarts.
/// </summary>
internal sealed class SearchModule : IAgentModule
{
	public string Name => "Search";

	public ModuleResult Evaluate( AgentContext ac )
	{
		var s = ac.State;
		var best = ac.BestTarget();
		if ( best != null && best.IsCurrent ) return ModuleResult.Ineligible();
		if ( ac.TimerActive( StateKeys.SearchCooldown ) ) return ModuleResult.Ineligible();

		bool active = s.Flags.Contains( StateKeys.SearchActive );
		if ( active && !ac.TimerActive( StateKeys.SearchEpisode ) )
			return EndEpisode( ac, "give_up" );
		long nodes = s.Counters.TryGetValue( StateKeys.SearchNodes, out long counted ) ? counted : 0;
		if ( active && nodes >= ac.Cfg.Search.MaxNodesPerSearch )
			return EndEpisode( ac, "max_nodes" );
		if ( !active )
		{
			bool window = s.LastSearchTick >= 0 && ( ac.TickIndex - s.LastSearchTick ) * ac.Dt <= ac.Cfg.Search.SystematicWindowSeconds;
			if ( s.Memories.Count == 0 && !window ) return ModuleResult.Ineligible();
		}

		string region = BestMemoryRegion( ac );
		if ( string.IsNullOrEmpty( region ) )
			region = s.LastSearchRegionId.Length > 0 ? s.LastSearchRegionId : ac.Monster.RegionId;

		NavCandidate pick = null;
		foreach ( var n in ac.SortedReachableNodes( region, NavCandidateKind.FrontstageNode ) )
		{
			if ( ac.NodeRecentlyVisited( n.NodeId, ac.Cfg.Search.NodeRevisitPenaltySeconds ) ) continue;
			pick = n;
			break;
		}
		if ( pick == null )
		{
			if ( active ) return EndEpisode( ac, "exhausted" );
			return ModuleResult.Ineligible();
		}

		if ( !active )
		{
			s.Flags.Add( StateKeys.SearchActive );
			s.Timers[StateKeys.SearchEpisode] = ac.Cfg.Search.GiveUpSeconds;
			s.Counters[StateKeys.SearchNodes] = 0;
			nodes = 0;
			s.LastSearchRegionId = region;
			ac.Emit( ReasonCodes.SearchStart, "region=" + region );
		}
		if ( !ac.MayEmit ) return ModuleResult.Running();

		ac.MarkNodeVisited( pick.NodeId );
		s.Counters[StateKeys.SearchNodes] = nodes + 1;
		var draft = ac.Draft( ActionKind.Search, ReasonCodes.Search );
		draft.RegionId = region;
		draft.NodeId = pick.NodeId;
		draft.Destination = pick.Position;
		draft.SpeedScale = ac.Cfg.Movement.SpeedFast;
		return ModuleResult.Act( draft, ac.MovementTimeout( pick.Position, region ) );
	}

	private static string BestMemoryRegion( AgentContext ac )
	{
		MemoryRecord best = null;
		foreach ( var m in ac.State.Memories )
		{
			if ( m == null ) continue;
			if ( best == null || m.DecayedConfidence > best.DecayedConfidence
				|| ( m.DecayedConfidence == best.DecayedConfidence && string.CompareOrdinal( m.StimulusId, best.StimulusId ) < 0 ) )
				best = m;
		}
		return best != null ? best.RegionId : null;
	}

	private static ModuleResult EndEpisode( AgentContext ac, string why )
	{
		var s = ac.State;
		s.Flags.Remove( StateKeys.SearchActive );
		s.Timers.Remove( StateKeys.SearchEpisode );
		s.Counters[StateKeys.SearchNodes] = 0;
		s.LastSearchTick = ac.TickIndex;
		s.Motivations.Remove( "search" );
		s.Timers[StateKeys.SearchCooldown] = ac.Cfg.Search.NodeRevisitPenaltySeconds;
		ac.Emit( ReasonCodes.SearchEnd, why );
		return ModuleResult.Ineligible();
	}
}

using System.Collections.Generic;
using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Priority 13 — offstage staging. Entry requires an aggressive macro decision with the
/// "sweeper" role and an offstage region adjacent to the nominated candidate region: the
/// monster takes a macro-suggested (or adjacent, nearest-first) usable ingress. While
/// offstage it sweeps: alternating moves to the least-recently-visited offstage node with
/// seeded-random dwells, until the sweep window expires and it exits via the nearest
/// ingress. Stage transitions are acknowledged by the host (see ApplyActionResults).
/// </summary>
internal sealed class OffstageModule : IAgentModule
{
	public string Name => "Offstage";

	public ModuleResult Evaluate( AgentContext ac )
	{
		var s = ac.State;
		bool offstage = s.Flags.Contains( StateKeys.Offstage ) || ac.Monster.Presence == StagePresence.Offstage;
		var macro = ac.Macro;
		bool macroOk = macro != null && macro.Mode == PressureMode.Aggressive && ac.HasRole( "sweeper" );

		if ( !offstage )
		{
			if ( !macroOk ) return ModuleResult.Ineligible();
			if ( ac.Monster.Presence != StagePresence.Frontstage ) return ModuleResult.Ineligible();
			if ( !ac.Monster.CanTraverseIngress ) return ModuleResult.Ineligible();
			if ( string.IsNullOrEmpty( macro.CandidateRegionId ) ) return ModuleResult.Ineligible();
			bool adjacent = false;
			foreach ( var r in ac.World.OffstageRegions )
			{
				if ( r != null && r.AdjacentRegionIds.Contains( macro.CandidateRegionId ) )
				{
					adjacent = true;
					break;
				}
			}
			if ( !adjacent ) return ModuleResult.Ineligible();

			var pick = PickIngress( ac, macro.IngressConstraints );
			if ( pick == null )
			{
				var ids = new List<string>();
				foreach ( var r in ac.World.OffstageRegions )
					if ( r != null && r.AdjacentRegionIds.Contains( macro.CandidateRegionId ) )
						ids.AddRange( r.IngressIds );
				pick = PickIngress( ac, ids );
			}
			if ( pick == null ) return ModuleResult.Ineligible();
			if ( !ac.MayEmit ) return ModuleResult.Running();

			var draft = ac.Draft( ActionKind.UseIngress, ReasonCodes.IngressUse );
			draft.IngressId = pick.IngressId;
			draft.Destination = pick.Position;
			return ModuleResult.Act( draft, ac.Cfg.Offstage.IngressTimeoutSeconds );
		}

		// offstage: continue or finish the sweep (an active sweep survives macro expiry)
		if ( !macroOk && !s.Flags.Contains( StateKeys.SweepActive ) ) return ModuleResult.Ineligible();
		if ( !s.Flags.Contains( StateKeys.SweepActive ) )
		{
			s.Flags.Add( StateKeys.SweepActive );
			s.Timers[StateKeys.Sweep] = ac.Cfg.Pressure.SweepDurationSeconds;
		}
		if ( !ac.TimerActive( StateKeys.Sweep ) )
		{
			// sweep window expired: exit via the nearest usable ingress
			var exit = ac.NearestUsableIngress();
			if ( exit == null ) return ModuleResult.Ineligible();
			if ( !ac.MayEmit ) return ModuleResult.Running();
			var draft = ac.Draft( ActionKind.UseIngress, ReasonCodes.IngressUse );
			draft.IngressId = exit.IngressId;
			draft.Destination = exit.Position;
			return ModuleResult.Act( draft, ac.Cfg.Offstage.IngressTimeoutSeconds );
		}
		if ( !ac.MayEmit ) return ModuleResult.Running();

		if ( !s.Flags.Contains( StateKeys.SweepDwellFlag ) )
		{
			NavCandidate node = null;
			long nodeStamp = 0;
			foreach ( var n in ac.World.NavCandidates )
			{
				if ( n == null || !n.Reachable || n.Kind != NavCandidateKind.OffstageNode ) continue;
				long stamp = ac.NodeVisitedTick( n.NodeId );
				if ( node == null || stamp < nodeStamp || ( stamp == nodeStamp && string.CompareOrdinal( n.NodeId, node.NodeId ) < 0 ) )
				{
					node = n;
					nodeStamp = stamp;
				}
			}
			if ( node == null ) return ModuleResult.Ineligible();
			s.Flags.Add( StateKeys.SweepDwellFlag );
			ac.MarkNodeVisited( node.NodeId );
			var draft = ac.Draft( ActionKind.MoveTo, ReasonCodes.SweepMove );
			draft.NodeId = node.NodeId;
			draft.Destination = node.Position;
			draft.SpeedScale = ac.Cfg.Movement.SpeedSlow;
			return ModuleResult.Act( draft, ac.MovementTimeout( node.Position, node.RegionId ) );
		}

		s.Flags.Remove( StateKeys.SweepDwellFlag );
		double dwell = ac.Rng.NextRange( ac.Cfg.Offstage.NodeDwellMinSeconds, ac.Cfg.Offstage.NodeDwellMaxSeconds );
		var wait = ac.Draft( ActionKind.Wait, ReasonCodes.SweepDwell );
		wait.SpeedScale = 0.0;
		wait.Param = AgentContext.FormatSeconds( dwell );
		return ModuleResult.Act( wait, dwell + 1.0 );
	}

	private static IngressPoint PickIngress( AgentContext ac, IReadOnlyList<string> ids )
	{
		if ( ids == null || ids.Count == 0 ) return null;
		IngressPoint pick = null;
		double pickDist = 0.0;
		foreach ( var id in ids )
		{
			var ing = ac.FindIngress( id );
			if ( !ac.IngressUsable( ing ) ) continue;
			double d = ac.Monster.Position.PlanarDistanceTo( ing.Position );
			if ( pick == null || d < pickDist || ( d == pickDist && string.CompareOrdinal( ing.IngressId, pick.IngressId ) < 0 ) )
			{
				pick = ing;
				pickDist = d;
			}
		}
		return pick;
	}
}

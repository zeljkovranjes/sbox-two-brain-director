using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Priority 12 — macro-biased frontstage stalk. With the "stalker" role, a nominated
/// candidate region and no target evidence (current or remembered), the monster shadows the
/// region: it moves to the nearest reachable candidate in the region that is not inside an
/// active exclusion zone listed by the macro decision.
/// </summary>
internal sealed class StalkModule : IAgentModule
{
	public string Name => "Stalk";

	public ModuleResult Evaluate( AgentContext ac )
	{
		var macro = ac.Macro;
		if ( macro == null || string.IsNullOrEmpty( macro.CandidateRegionId ) ) return ModuleResult.Ineligible();
		if ( !ac.HasRole( "stalker" ) ) return ModuleResult.Ineligible();
		if ( ac.BestTarget() != null ) return ModuleResult.Ineligible();
		if ( ac.Monster.Presence != StagePresence.Frontstage ) return ModuleResult.Ineligible();

		NavCandidate pick = null;
		foreach ( var n in ac.SortedReachableNodes( macro.CandidateRegionId, NavCandidateKind.FrontstageNode ) )
		{
			if ( ac.InsideExclusion( n.Position ) ) continue;
			pick = n;
			break;
		}
		if ( pick == null ) return ModuleResult.Ineligible();
		if ( !ac.MayEmit ) return ModuleResult.Running();

		var draft = ac.Draft( ActionKind.Stalk, ReasonCodes.Stalk );
		draft.RegionId = macro.CandidateRegionId;
		draft.NodeId = pick.NodeId;
		draft.Destination = pick.Position;
		draft.SpeedScale = ac.Cfg.Movement.SpeedFast;
		return ModuleResult.Act( draft, ac.MovementTimeout( pick.Position, macro.CandidateRegionId ) );
	}
}

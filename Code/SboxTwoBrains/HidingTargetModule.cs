using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Priority 9 — hiding target. When a participant is hiding and remembered evidence
/// attributed to it exists within the systematic window, a region-scoped Search is issued
/// toward the remembered position (search consumes region + memory, never exact coordinates
/// of a hidden target).
/// </summary>
internal sealed class HidingTargetModule : IAgentModule
{
	public string Name => "HidingTarget";

	public ModuleResult Evaluate( AgentContext ac )
	{
		var s = ac.State;
		TargetSnapshot hiding = null;
		MemoryRecord evidence = null;
		foreach ( var t in ac.World.Targets )
		{
			if ( t == null || !t.IsHiding || !t.IsValid || !t.IsAlive ) continue;
			MemoryRecord best = null;
			foreach ( var m in s.Memories )
			{
				if ( m == null || m.TargetId != t.TargetId ) continue;
				if ( ( ac.TickIndex - m.LastConfirmedTick ) * ac.Dt > ac.Cfg.Search.SystematicWindowSeconds ) continue;
				if ( best == null || m.DecayedConfidence > best.DecayedConfidence
					|| ( m.DecayedConfidence == best.DecayedConfidence && string.CompareOrdinal( m.StimulusId, best.StimulusId ) < 0 ) )
					best = m;
			}
			if ( best == null ) continue;
			if ( hiding == null || string.CompareOrdinal( t.TargetId, hiding.TargetId ) < 0 )
			{
				hiding = t;
				evidence = best;
			}
		}
		if ( hiding == null ) return ModuleResult.Ineligible();
		if ( !ac.MayEmit ) return ModuleResult.Running();

		var draft = ac.Draft( ActionKind.Search, ReasonCodes.HidingTarget );
		draft.TargetId = hiding.TargetId;
		draft.RegionId = evidence.RegionId ?? "";
		draft.Destination = evidence.Position;
		draft.SpeedScale = ac.Cfg.Movement.SpeedFast;
		return ModuleResult.Act( draft, ac.MovementTimeout( evidence.Position, evidence.RegionId ) );
	}
}

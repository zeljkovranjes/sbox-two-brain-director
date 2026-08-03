using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Priority 8 — suspect-target response. A target that was sensed within the systematic
/// window but is not currently evidenced is re-approached once per loss episode: a fast
/// MoveTo to its last sensed position. Afterwards lower modules (search) take over.
/// </summary>
internal sealed class SuspectResponseModule : IAgentModule
{
	public string Name => "SuspectResponse";

	public ModuleResult Evaluate( AgentContext ac )
	{
		var s = ac.State;
		var best = ac.BestTarget();
		if ( best != null && best.IsCurrent ) return ModuleResult.Ineligible();
		string tid = s.CurrentTargetId;
		if ( tid.Length == 0 || !s.LastSensedTargetPosition.HasValue || s.LastSensedTargetTick < 0 ) return ModuleResult.Ineligible();
		if ( ( ac.TickIndex - s.LastSensedTargetTick ) * ac.Dt > ac.Cfg.Search.SystematicWindowSeconds ) return ModuleResult.Ineligible();
		if ( s.Flags.Contains( StateKeys.SuspectResponded ) ) return ModuleResult.Ineligible();

		MemoryRecord remembered = null;
		foreach ( var m in s.Memories )
		{
			if ( m == null || m.TargetId != tid ) continue;
			if ( remembered == null || m.DecayedConfidence > remembered.DecayedConfidence
				|| ( m.DecayedConfidence == remembered.DecayedConfidence && string.CompareOrdinal( m.StimulusId, remembered.StimulusId ) < 0 ) )
				remembered = m;
		}
		if ( remembered == null ) return ModuleResult.Ineligible();
		if ( !ac.MayEmit ) return ModuleResult.Running();

		s.Flags.Add( StateKeys.SuspectResponded );
		var pos = s.LastSensedTargetPosition.Value;
		var draft = ac.Draft( ActionKind.MoveTo, ReasonCodes.SuspectResponse );
		draft.TargetId = tid;
		draft.Destination = pos;
		draft.SpeedScale = ac.Cfg.Movement.SpeedFast;
		return ModuleResult.Act( draft, ac.MovementTimeout( pos, remembered.RegionId ) );
	}
}

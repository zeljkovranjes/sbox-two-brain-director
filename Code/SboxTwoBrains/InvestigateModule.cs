using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Priority 10 — staged suspicious-stimulus investigation: 0 react (brief stationary Wait),
/// 1 approach (slow MoveTo), 2 inspect (facing Wait), 3 hand off to systematic search. The
/// tracked stimulus id is held in state; losing the stimulus mid-stage resets the machine.
/// </summary>
internal sealed class InvestigateModule : IAgentModule
{
	private const double ReactSeconds = 0.5;

	public string Name => "Investigate";

	public ModuleResult Evaluate( AgentContext ac )
	{
		var s = ac.State;
		if ( s.InvestigationStage == 0 )
		{
			var best = ac.BestUnattributed();
			if ( best == null ) return ModuleResult.Ineligible();
			if ( !ac.MayEmit ) return ModuleResult.Running();
			s.InvestigationStimulusId = best.StimulusId;
			s.InvestigationStage = 1;
			var react = ac.Draft( ActionKind.Wait, ReasonCodes.InvestigateReact );
			react.SpeedScale = 0.0;
			react.StimulusId = best.StimulusId;
			react.Param = AgentContext.FormatSeconds( ReactSeconds );
			return ModuleResult.Act( react, ReactSeconds + 1.0 );
		}

		var tracked = ac.FindStimulusOrMemory( s.InvestigationStimulusId );
		if ( tracked == null )
		{
			s.InvestigationStage = 0;
			s.InvestigationStimulusId = "";
			ac.Emit( ReasonCodes.InvestigateReset, "stimulus lost mid-stage" );
			return ModuleResult.Ineligible();
		}

		if ( s.InvestigationStage == 1 )
		{
			if ( !ac.MayEmit ) return ModuleResult.Running();
			s.InvestigationStage = 2;
			var approach = ac.Draft( ActionKind.MoveTo, ReasonCodes.InvestigateApproach );
			approach.Destination = tracked.Position;
			approach.StimulusId = tracked.StimulusId;
			approach.SpeedScale = ac.Cfg.Movement.SpeedSlow;
			return ModuleResult.Act( approach, ac.MovementTimeout( tracked.Position, tracked.RegionId ) );
		}
		if ( s.InvestigationStage == 2 )
		{
			if ( !ac.MayEmit ) return ModuleResult.Running();
			s.InvestigationStage = 3;
			double facing = ac.Cfg.Movement.InvestigateFacingSeconds;
			var inspect = ac.Draft( ActionKind.Wait, ReasonCodes.InvestigateInspect );
			inspect.SpeedScale = 0.0;
			inspect.StimulusId = tracked.StimulusId;
			inspect.Param = AgentContext.FormatSeconds( facing );
			return ModuleResult.Act( inspect, facing + 1.0 );
		}

		// stage 3: hand off — search takes over from the next module in order
		s.InvestigationStage = 0;
		s.InvestigationStimulusId = "";
		ac.Emit( ReasonCodes.InvestigateDone, "handing off to search" );
		return ModuleResult.Ineligible();
	}
}

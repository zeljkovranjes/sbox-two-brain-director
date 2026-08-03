using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Priority 6 — ambush. With the macro "ambusher" role, remembered target evidence and no
/// current contact, the monster moves to the reachable candidate in the remembered region
/// nearest the remembered position and holds there until the ambush timeout.
/// </summary>
internal sealed class AmbushModule : IAgentModule
{
	public string Name => "Ambush";

	public ModuleResult Evaluate( AgentContext ac )
	{
		var s = ac.State;
		if ( s.Flags.Contains( StateKeys.AmbushActive ) )
		{
			if ( !ac.TimerActive( StateKeys.Ambush ) )
			{
				s.Flags.Remove( StateKeys.AmbushActive );
				ac.Emit( ReasonCodes.AmbushTimeout, "ambush timed out" );
				return ModuleResult.Ineligible();
			}
			return ModuleResult.Running(); // holding the ambush position
		}

		var macro = ac.Macro;
		if ( macro == null || !ac.HasRole( "ambusher" ) ) return ModuleResult.Ineligible();
		var best = ac.BestTarget();
		if ( best == null || best.Source != EvidenceSource.Memory ) return ModuleResult.Ineligible();
		if ( ac.Monster.Presence != StagePresence.Frontstage ) return ModuleResult.Ineligible();
		var node = ac.NearestNodeTo( best.Position, best.RegionId );
		if ( node == null ) return ModuleResult.Ineligible();
		if ( !ac.MayEmit ) return ModuleResult.Running();

		s.Flags.Add( StateKeys.AmbushActive );
		s.Timers[StateKeys.Ambush] = ac.Cfg.Pressure.AmbushTimeoutSeconds;
		var draft = ac.Draft( ActionKind.Ambush, ReasonCodes.AmbushStart );
		draft.TargetId = best.TargetId;
		draft.NodeId = node.NodeId;
		draft.RegionId = node.RegionId ?? "";
		draft.Destination = node.Position;
		draft.SpeedScale = ac.Cfg.Movement.SpeedFast;
		return ModuleResult.Act( draft, ac.MovementTimeout( node.Position, node.RegionId ) );
	}
}

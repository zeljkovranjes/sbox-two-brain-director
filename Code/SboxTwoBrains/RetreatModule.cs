using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Priority 4 — retreat/withdrawal. Eligible while the "retreat" motivation is set (low
/// health, persisted deterrent exposure or a full retreat gauge — see motivation derivation)
/// and the retreat cooldown is clear. Destination: the nearest usable ingress when
/// traversal is allowed, otherwise the reachable nav candidate farthest from the threat.
/// </summary>
internal sealed class RetreatModule : IAgentModule
{
	private const double RetreatCooldownSeconds = 10.0;

	public string Name => "Retreat";

	public ModuleResult Evaluate( AgentContext ac )
	{
		var s = ac.State;
		if ( !s.Motivations.Contains( "retreat" ) ) return ModuleResult.Ineligible();
		if ( ac.TimerActive( StateKeys.RetreatCooldown ) ) return ModuleResult.Ineligible();
		if ( !ac.MayEmit ) return ModuleResult.Running();

		ActionRequest draft = null;
		string region = "";
		if ( ac.Monster.CanTraverseIngress )
		{
			var ing = ac.NearestUsableIngress();
			if ( ing != null )
			{
				draft = ac.Draft( ActionKind.Retreat, ReasonCodes.RetreatStart );
				draft.IngressId = ing.IngressId;
				draft.Destination = ing.Position;
				region = ing.RegionId ?? "";
			}
		}
		if ( draft == null )
		{
			Vec3 away = s.LastSensedTargetPosition ?? RememberedTargetPosition( ac ) ?? ac.Monster.Position;
			var node = ac.FarthestNodeFrom( away );
			if ( node != null )
			{
				draft = ac.Draft( ActionKind.Retreat, ReasonCodes.RetreatStart );
				draft.NodeId = node.NodeId;
				draft.Destination = node.Position;
				region = node.RegionId ?? "";
			}
		}
		if ( draft == null || !draft.Destination.HasValue ) return ModuleResult.Ineligible();

		draft.SpeedScale = ac.Cfg.Movement.SpeedFastest;
		s.Timers[StateKeys.RetreatCooldown] = RetreatCooldownSeconds;
		s.Flags.Remove( StateKeys.ForcedRetreat ); // the forced withdrawal has been acted on
		return ModuleResult.Act( draft, ac.MovementTimeout( draft.Destination.Value, region ) );
	}

	private static Vec3? RememberedTargetPosition( AgentContext ac )
	{
		MemoryRecord best = null;
		foreach ( var m in ac.State.Memories )
		{
			if ( m == null || string.IsNullOrEmpty( m.TargetId ) ) continue;
			if ( best == null || m.DecayedConfidence > best.DecayedConfidence
				|| ( m.DecayedConfidence == best.DecayedConfidence && string.CompareOrdinal( m.StimulusId, best.StimulusId ) < 0 ) )
				best = m;
		}
		return best != null ? best.Position : (Vec3?)null;
	}
}

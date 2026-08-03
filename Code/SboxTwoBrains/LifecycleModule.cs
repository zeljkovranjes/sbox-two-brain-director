using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Priority 1 — lifecycle and pathfinding-failure recovery. While the monster is not alive
/// all pending/awaited requests are dropped and the tick ends. Three consecutive navigation
/// failures clear the target/investigation state, open a backoff window and issue a
/// recovery Wait; movement is refused while the backoff timer runs.
/// </summary>
internal sealed class LifecycleModule : IAgentModule
{
	public string Name => "Lifecycle";

	public ModuleResult Evaluate( AgentContext ac )
	{
		var s = ac.State;
		if ( ac.Monster.Lifecycle != MonsterLifecycle.Alive )
		{
			if ( s.PendingActions.Count > 0 || s.AwaitingActionId.Length > 0 )
			{
				s.PendingActions.Clear();
				s.PendingMeta.Clear();
				s.AwaitingActionId = "";
				s.ActiveModule = "";
				s.ActiveIngressId = "";
				var stale = new System.Collections.Generic.List<string>();
				foreach ( var key in s.Timers.Keys )
					if ( key.StartsWith( StateKeys.DeferredPrefix, System.StringComparison.Ordinal ) ) stale.Add( key );
				foreach ( var key in stale ) s.Timers.Remove( key );
			}
			if ( s.Flags.Add( StateKeys.LifecycleInactiveFlag ) )
				ac.Emit( ReasonCodes.LifecycleInactive, "lifecycle=" + ac.Monster.Lifecycle );
			return ModuleResult.Running();
		}
		s.Flags.Remove( StateKeys.LifecycleInactiveFlag );

		if ( s.ConsecutiveNavFailures >= 3 )
		{
			if ( !ac.MayEmit ) return ModuleResult.Running();
			s.CurrentTargetId = "";
			s.InvestigationStage = 0;
			s.InvestigationStimulusId = "";
			s.Flags.Remove( StateKeys.Chasing );
			s.Flags.Remove( StateKeys.SuspectResponded );
			double backoff = 2.0 * s.ConsecutiveNavFailures;
			if ( backoff > 30.0 ) backoff = 30.0;
			s.Timers[StateKeys.NavBackoff] = backoff;
			var draft = ac.Draft( ActionKind.Wait, ReasonCodes.NavRecovery );
			draft.SpeedScale = 0.0;
			draft.Param = AgentContext.FormatSeconds( backoff );
			var result = ModuleResult.Act( draft, backoff + 1.0 );
			// counter resets only after the recovery action has been issued
			s.ConsecutiveNavFailures = 0;
			return result;
		}
		return ModuleResult.Ineligible();
	}
}

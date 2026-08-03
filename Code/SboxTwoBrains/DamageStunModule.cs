using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Priority 3 — damage stun. The stun gauge itself is maintained in the agent's aging phase
/// (drops add damage × 2, linear decay of 1.0/s). When the gauge reaches 1.0 the monster
/// staggers: the gauge is consumed, a stagger timer starts and a stationary Wait is issued.
/// While staggering the module stays Running and nothing else acts.
/// </summary>
internal sealed class DamageStunModule : IAgentModule
{
	public string Name => "DamageStun";

	public ModuleResult Evaluate( AgentContext ac )
	{
		var s = ac.State;
		if ( ac.TimerActive( StateKeys.Stagger ) )
			return ModuleResult.Running();
		if ( ac.Gauge( StateKeys.Stun ) >= 1.0 )
		{
			if ( !ac.MayEmit ) return ModuleResult.Running();
			double seconds = ac.Cfg.Combat.AttackCooldownSeconds;
			s.Timers[StateKeys.Stagger] = seconds;
			s.Gauges[StateKeys.Stun] = 0.0;
			var draft = ac.Draft( ActionKind.Wait, ReasonCodes.Stagger );
			draft.SpeedScale = 0.0;
			draft.Param = AgentContext.FormatSeconds( seconds );
			return ModuleResult.Act( draft, seconds + 1.0 );
		}
		return ModuleResult.Ineligible();
	}
}

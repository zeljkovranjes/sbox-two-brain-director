using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>Priority 14 — fallback. Always eligible: idles in place for one second.</summary>
internal sealed class IdleModule : IAgentModule
{
	private const double IdleSeconds = 1.0;

	public string Name => "Idle";

	public ModuleResult Evaluate( AgentContext ac )
	{
		if ( !ac.MayEmit ) return ModuleResult.Running();
		var draft = ac.Draft( ActionKind.Wait, ReasonCodes.Idle );
		draft.SpeedScale = 0.0;
		draft.Param = AgentContext.FormatSeconds( IdleSeconds );
		return ModuleResult.Act( draft, IdleSeconds + 1.0 );
	}
}

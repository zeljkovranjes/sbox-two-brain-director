using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Priority 2 — host/cinematic override. A requested despawn blocks all further decisions
/// (the host performs the actual despawn). An active scripted sequence is re-issued as a
/// Scripted action until the host acknowledges it (Succeeded clears the sequence).
/// </summary>
internal sealed class ScriptOverrideModule : IAgentModule
{
	private const double ScriptedTimeoutSeconds = 60.0;

	public string Name => "ScriptOverride";

	public ModuleResult Evaluate( AgentContext ac )
	{
		var s = ac.State;
		if ( s.Flags.Contains( StateKeys.DespawnRequested ) )
			return ModuleResult.Running(); // stand by for host-driven despawn
		if ( s.ActiveScriptedSequence.Length > 0 )
		{
			if ( !ac.MayEmit ) return ModuleResult.Running();
			var draft = ac.Draft( ActionKind.Scripted, ReasonCodes.ScriptSequence );
			draft.Param = s.ActiveScriptedSequence;
			return ModuleResult.Act( draft, ScriptedTimeoutSeconds );
		}
		return ModuleResult.Ineligible();
	}
}

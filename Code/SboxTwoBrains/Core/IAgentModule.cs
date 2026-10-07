namespace SboxTwoBrains.Core;

/// <summary>
/// One guarded behaviour module in the micro arbitration list. Modules are stateless; all
/// durable state lives in <see cref="MicroState"/> so save/restore needs no module hooks.
/// Evaluate must not emit actions when <see cref="AgentContext.MayEmit"/> is false and must
/// not consume RNG or mutate episode state on that path.
/// </summary>
internal interface IAgentModule
{
	/// <summary>Stable registry name used by Modules.Order/Disabled config.</summary>
	string Name { get; }

	ModuleResult Evaluate( AgentContext ac );
}

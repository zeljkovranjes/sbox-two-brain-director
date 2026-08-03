using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>Module evaluation outcome in the micro arbitration.</summary>
internal enum ModuleStatus
{
	/// <summary>Gate failed; the next module in order is evaluated.</summary>
	Ineligible = 0,
	/// <summary>The module owns the tick (with or without a new action).</summary>
	Running = 1,
	/// <summary>Terminal success (carried for contract parity; modules rarely return it).</summary>
	Succeeded = 2,
	/// <summary>Terminal failure (carried for contract parity; modules rarely return it).</summary>
	Failed = 3,
}

/// <summary>
/// What a module returns to the arbitrator. <see cref="Action"/> is a draft (no id/expiry);
/// the arbitrator assigns the deterministic id, computes the expiry from
/// <see cref="TimeoutSeconds"/>, applies the shared feasibility gates and commits it.
/// </summary>
internal sealed class ModuleResult
{
	public ModuleStatus Status;
	public ActionRequest Action;
	public double TimeoutSeconds;
	public string ReasonCode = "";

	public static ModuleResult Ineligible() => new ModuleResult { Status = ModuleStatus.Ineligible };

	public static ModuleResult Running() => new ModuleResult { Status = ModuleStatus.Running };

	public static ModuleResult Act( ActionRequest draft, double timeoutSeconds )
	{
		return new ModuleResult
		{
			Status = ModuleStatus.Running,
			Action = draft,
			TimeoutSeconds = timeoutSeconds,
			ReasonCode = draft != null ? draft.ReasonCode ?? "" : "",
		};
	}
}

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

namespace SboxTwoBrains.Core;

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

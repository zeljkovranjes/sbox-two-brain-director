namespace SboxTwoBrains.Core;

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

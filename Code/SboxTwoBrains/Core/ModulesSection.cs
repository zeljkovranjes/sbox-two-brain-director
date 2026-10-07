namespace SboxTwoBrains.Core;

/// <summary>Ordered module arbitration and enablement.</summary>
public sealed class ModulesSection
{
	/// <summary>Module names in arbitration order (earlier wins). Empty = built-in default order.</summary>
	public string[] Order { get; set; }
	/// <summary>Module names force-disabled for this profile.</summary>
	public string[] Disabled { get; set; }
}

namespace SboxTwoBrains;

/// <summary>
/// An inheritable monster profile. Any null field means "inherit"; the built-in generic
/// baseline supplies the final default for every field. Inheritance must be acyclic;
/// resolution order is deterministic (ancestors applied root-first, child wins).
/// </summary>
public sealed class MonsterProfileConfig
{
	/// <summary>Unique profile name within its catalogue.</summary>
	public string Name { get; set; } = "";

	/// <summary>Optional parent profile name (single inheritance chain).</summary>
	public string BasedOn { get; set; }

	/// <summary>Optional schema/config version marker saved with state.</summary>
	public string ConfigVersion { get; set; } = "1";

	public PressureSection Pressure { get; set; }
	public PerceptionSection Perception { get; set; }
	public SearchSection Search { get; set; }
	public ThreatSection Threat { get; set; }
	public CombatSection Combat { get; set; }
	public OffstageSection Offstage { get; set; }
	public ModulesSection Modules { get; set; }
	public MovementSection Movement { get; set; }
}

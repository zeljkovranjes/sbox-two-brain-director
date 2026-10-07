namespace SboxTwoBrains.Core;

/// <summary>Lifecycle of the monster itself, as reported by the host.</summary>
public enum MonsterLifecycle
{
	Alive = 0,
	Dead = 1,
	Suspended = 2,
	Despawning = 3,
}

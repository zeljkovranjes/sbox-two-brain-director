using System;
using System.Threading.Tasks;
using Sandbox;
using SboxTwoBrains.Core;

namespace SboxTwoBrains.Components;

/// <summary>
/// Abstraction between the deterministic two-brain core and YOUR monster. The core emits
/// declarative <see cref="ActionRequest"/>s; <see cref="TwoBrainsComponent"/> translates them
/// into calls on this interface and translates your completions back into
/// <see cref="ActionResult"/> acknowledgements.
///
/// Implement this on your own monster component (or subclass <see cref="MonsterDriverBase"/>)
/// and map each call onto your animation/navigation stack. All positions are s&amp;box world
/// units; the adapter handles the metres/units conversion at the core boundary.
/// </summary>
public interface IMonsterDriver
{
	/// <summary>Current world position of the monster (s&amp;box units).</summary>
	Vector3 Position { get; }

	/// <summary>False while dead/dying; the core suspends most behaviour when not alive.</summary>
	bool IsAlive { get; }

	/// <summary>
	/// Move toward <paramref name="dest"/> at the given speed scale (0..1 of your locomotion
	/// maximum). The returned task completes with true on arrival, false on failure/cancel.
	/// Starting a new move should cancel any move already in progress.
	/// </summary>
	Task<bool> MoveToAsync( Vector3 dest, float speedScale );

	/// <summary>
	/// Traverse an approved ingress point (vent/door/tunnel) by id — typically a teleport or a
	/// short scripted traversal. Returns false when the ingress is unknown or unusable.
	/// </summary>
	bool TryTraverseIngress( string ingressId );

	/// <summary>Play a threat display (roar, flinch, hesitation). Fire-and-forget.</summary>
	void PlayThreat();

	/// <summary>Play an attack against the given target id. Fire-and-forget.</summary>
	void PlayAttack( string targetId );

	/// <summary>Idle in place for roughly <paramref name="seconds"/>. Fire-and-forget.</summary>
	void PlayWait( float seconds );

	/// <summary>Run a named scripted sequence (cinematic control). Fire-and-forget.</summary>
	void PlayScripted( string name );

	/// <summary>Remove the monster from the world (despawn directive).</summary>
	void Despawn();
}

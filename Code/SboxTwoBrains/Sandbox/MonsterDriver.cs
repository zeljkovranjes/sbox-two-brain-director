using System;
using System.Threading.Tasks;
using Sandbox;

namespace SboxTwoBrains.Sandbox;

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

/// <summary>
/// Minimal working <see cref="IMonsterDriver"/>: moves the GameObject with a
/// <see cref="NavMeshAgent"/> when one is present, otherwise lerps the Transform directly,
/// and treats ingress traversal as an instant teleport.
///
/// This base intentionally knows nothing about YOUR animation or combat stack — subclass it
/// and override the Play* methods (and <see cref="IsAlive"/>) to hook in your model/animgraph,
/// damage and sound. Everything here is defensive: missing agents, destroyed objects and
/// driver-in-progress cancellation degrade to honest failures instead of exceptions.
/// </summary>
[Title( "Monster Driver Base" )]
[Category( "AI" )]
public class MonsterDriverBase : Component, IMonsterDriver
{
	/// <summary>Fallback transform-lerp speed in units/second when no NavMeshAgent is present.</summary>
	[Property] public float BaseSpeed { get; set; } = 240.0f;

	/// <summary>Distance (units) at which a move counts as arrived.</summary>
	[Property] public float ArriveDistance { get; set; } = 24.0f;

	/// <summary>Hard time limit for one move; expiry reports failure to the core.</summary>
	[Property] public float MoveTimeoutSeconds { get; set; } = 30.0f;

	private NavMeshAgent _agent;
	private TaskCompletionSource<bool> _pendingMove;
	private Vector3 _moveDestination;
	private float _moveDeadline;
	private float _moveNavGraceUntil;

	/// <inheritdoc/>
	public virtual Vector3 Position => WorldPosition;

	/// <inheritdoc/>
	public virtual bool IsAlive => true;

	protected override void OnStart()
	{
		_agent = Components.Get<NavMeshAgent>();
	}

	/// <inheritdoc/>
	public virtual Task<bool> MoveToAsync( Vector3 dest, float speedScale )
	{
		CancelPendingMove();
		if ( !IsAlive )
			return Task.FromResult( false );

		if ( _agent is null || !_agent.IsValid )
			_agent = Components.Get<NavMeshAgent>();

		var completion = new TaskCompletionSource<bool>();
		_pendingMove = completion;
		_moveDestination = dest;
		_moveDeadline = Time.Now + Math.Max( 1.0f, MoveTimeoutSeconds );
		_moveNavGraceUntil = Time.Now + 0.25f;

		if ( _agent is not null && _agent.IsValid && _agent.Enabled )
		{
			_agent.MaxSpeed = Math.Max( 1.0f, BaseSpeed ) * Math.Clamp( speedScale, 0.05f, 1.0f );
			try
			{
				_agent.MoveTo( dest );
			}
			catch ( Exception )
			{
				// Agent refused the destination; fail honestly rather than hanging the core.
				CompletePendingMove( false );
			}
		}

		return completion.Task;
	}

	/// <inheritdoc/>
	public virtual bool TryTraverseIngress( string ingressId )
	{
		if ( string.IsNullOrEmpty( ingressId ) )
			return false;

		TwoBrainsIngress found = null;
		foreach ( var ingress in Scene.GetAllComponents<TwoBrainsIngress>() )
		{
			if ( ingress is null || !ingress.IsValid )
				continue;
			var id = string.IsNullOrEmpty( ingress.IngressId ) ? ingress.GameObject?.Name : ingress.IngressId;
			if ( string.Equals( id, ingressId, StringComparison.Ordinal ) )
			{
				found = ingress;
				break;
			}
		}

		if ( found is null )
			return false;

		CancelPendingMove();
		var dest = found.WorldPosition;
		if ( _agent is not null && _agent.IsValid && _agent.Enabled )
		{
			_agent.Stop();
			_agent.SetAgentPosition( dest );
		}
		WorldPosition = dest;
		return true;
	}

	/// <summary>Base implementation does nothing. Override to trigger your threat animation/sound.</summary>
	public virtual void PlayThreat() { }

	/// <summary>Base implementation does nothing. Override to trigger your attack animation/damage.</summary>
	public virtual void PlayAttack( string targetId ) { }

	/// <summary>Base implementation does nothing. Override to play an idle/suspicious loop.</summary>
	public virtual void PlayWait( float seconds ) { }

	/// <summary>Base implementation does nothing. Override to run your named cinematic sequence.</summary>
	public virtual void PlayScripted( string name ) { }

	/// <summary>Base implementation destroys the GameObject. Override for pooled/remote despawns.</summary>
	public virtual void Despawn()
	{
		GameObject.Destroy();
	}

	protected override void OnUpdate()
	{
		if ( _pendingMove is null )
			return;

		var toDest = _moveDestination - WorldPosition;
		if ( toDest.Length <= Math.Max( 1.0f, ArriveDistance ) )
		{
			CompletePendingMove( true );
			return;
		}

		if ( Time.Now >= _moveDeadline )
		{
			CompletePendingMove( false );
			return;
		}

		if ( _agent is not null && _agent.IsValid && _agent.Enabled )
		{
			// The agent drives the transform. Past the start-up grace, a non-navigating agent
			// means the path failed or was abandoned — report failure instead of waiting out
			// the whole timeout.
			if ( Time.Now >= _moveNavGraceUntil && !_agent.IsNavigating )
				CompletePendingMove( false );
			return;
		}

		// No nav agent: conservative straight-line lerp at BaseSpeed.
		var step = Math.Max( 1.0f, BaseSpeed ) * Time.Delta;
		if ( toDest.Length <= step )
		{
			WorldPosition = _moveDestination;
			CompletePendingMove( true );
			return;
		}
		WorldPosition += toDest.Normal * step;
	}

	/// <summary>Cancels the in-flight move (if any) and stops the nav agent.</summary>
	protected void CancelPendingMove()
	{
		if ( _pendingMove is not null )
			CompletePendingMove( false );
		if ( _agent is not null && _agent.IsValid && _agent.Enabled && _agent.IsNavigating )
			_agent.Stop();
	}

	private void CompletePendingMove( bool succeeded )
	{
		var completion = _pendingMove;
		_pendingMove = null;
		completion?.TrySetResult( succeeded );
	}
}

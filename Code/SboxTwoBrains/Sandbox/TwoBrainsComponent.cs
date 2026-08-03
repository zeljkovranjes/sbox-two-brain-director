using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sandbox;
using SboxTwoBrains;

namespace SboxTwoBrains.Host;

/// <summary>
/// Unit conversion between s&amp;box world units and the core's metres. s&amp;box uses
/// Source-style units (1 unit = 1 inch); the deterministic core is specified in metres, and
/// every profile distance (attack range, sweep radius, exclusion radius) assumes metres.
/// </summary>
public static class SandboxVec
{
	/// <summary>s&amp;box units per metre (1 unit = 0.0254 m).</summary>
	public const float UnitsPerMetre = 39.3701f;

	/// <summary>Metres per s&amp;box unit.</summary>
	public const float MetresPerUnit = 0.0254f;

	/// <summary>s&amp;box Vector3 (units) → core Vec3 (metres).</summary>
	public static Vec3 ToCore( Vector3 v )
	{
		return new Vec3( v.x * MetresPerUnit, v.y * MetresPerUnit, v.z * MetresPerUnit );
	}

	/// <summary>Core Vec3 (metres) → s&amp;box Vector3 (units).</summary>
	public static Vector3 ToSbox( Vec3 v )
	{
		return new Vector3( (float)(v.X * UnitsPerMetre), (float)(v.Y * UnitsPerMetre), (float)(v.Z * UnitsPerMetre) );
	}

	/// <summary>s&amp;box units → metres.</summary>
	public static double ToCoreDistance( float units ) => units * MetresPerUnit;

	/// <summary>Metres → s&amp;box units.</summary>
	public static float ToSboxDistance( double metres ) => (float)(metres * UnitsPerMetre);
}

/// <summary>
/// The host adapter: owns a <see cref="TwoBrainsSystem"/> (the deterministic two-brain core),
/// builds a <see cref="WorldSnapshot"/> from the scene at the configured tick rate, dispatches
/// the returned <see cref="ActionRequest"/>s to an <see cref="IMonsterDriver"/>, and feeds the
/// driver's completions back as <see cref="ActionResult"/> acknowledgements on later ticks.
///
/// Scene wiring: drop this on the monster's GameObject next to an <see cref="IMonsterDriver"/>
/// implementation (<see cref="MonsterDriverBase"/> works out of the box), then scatter
/// <see cref="TwoBrainsNavNode"/>, <see cref="TwoBrainsIngress"/>,
/// <see cref="TwoBrainsOffstageRegion"/>, <see cref="TwoBrainsExclusionZone"/> and
/// <see cref="TwoBrainsTarget"/> markers through the level. Add a
/// <see cref="TwoBrainsDebugHudSpawner"/> anywhere for the live overlay.
/// </summary>
[Title( "Two-Brain Monster AI" )]
[Category( "AI" )]
public sealed class TwoBrainsComponent : Component
{
	/// <summary>Profile to resolve from the catalogue. The compat catalogue provides ALIENISOLATIONINSPIRED.</summary>
	[Property] public string ProfileName { get; set; } = "ALIENISOLATIONINSPIRED";

	/// <summary>Master seed. Same seed + same inputs = byte-identical decisions.</summary>
	[Property] public ulong Seed { get; set; } = 1337UL;

	/// <summary>Policy ticks per simulated second; each tick gets dt = 1/rate.</summary>
	[Property] public int TicksPerSecond { get; set; } = 20;

	/// <summary>Use the Alien: Isolation-inspired preset catalogue; off = one empty generic profile.</summary>
	[Property] public bool UseCompatCatalogue { get; set; } = true;

	/// <summary>Maximum core ticks run per fixed-update frame (catch-up cap).</summary>
	[Property] public int MaxTicksPerFrame { get; set; } = 1;

	/// <summary>Lets hosts gate debug output; the debug HUD shows DISABLED while off.</summary>
	[Property] public bool DebugEnabled { get; set; } = true;

	/// <summary>Radius in metres around the monster within which nav nodes are reported.</summary>
	[Property] public float NavNodeRadius { get; set; } = 60.0f;

	/// <summary>Eye height above the monster/target pivot for line-of-sight traces (metres).</summary>
	[Property] public float LosEyeHeight { get; set; } = 1.7f;

	/// <summary>The deterministic core. Public for diagnostics and the debug HUD.</summary>
	public TwoBrainsSystem System { get; private set; }

	/// <summary>The most recent decision batch returned by the core.</summary>
	public DecisionBatch LastBatch { get; private set; }

	/// <summary>
	/// The monster driver actions are dispatched to. Auto-wired in OnStart from a sibling
	/// component implementing <see cref="IMonsterDriver"/>; assign manually to override.
	/// </summary>
	public IMonsterDriver Driver { get; set; }

	private double _accumulator;
	private bool _broken;

	private readonly List<Stimulus> _pendingStimuli = new List<Stimulus>();
	private readonly List<ScriptDirective> _pendingDirectives = new List<ScriptDirective>();
	private readonly List<ActionResult> _pendingAcks = new List<ActionResult>();
	private readonly Dictionary<string, long> _ingressUseTicks = new Dictionary<string, long>( StringComparer.Ordinal );

	private ActiveMove _activeMove;

	private double _healthFraction = 1.0;
	private long _lastDamageTick = -1;
	private bool _despawnTriggered;

	private sealed class ActiveMove
	{
		public string ActionId;
		public Task<bool> Task;
	}

	protected override void OnStart()
	{
		try
		{
			ProfileCatalogue catalogue = UseCompatCatalogue
				? AlienIsolationPresets.CreateCatalogue()
				: new ProfileCatalogue();
			if ( !catalogue.Contains( ProfileName ) )
				catalogue.Add( new MonsterProfileConfig { Name = ProfileName } );
			System = new TwoBrainsSystem( catalogue, ProfileName, Seed );
		}
		catch ( Exception ex )
		{
			Log.Error( $"TwoBrainsComponent: failed to build system for profile '{ProfileName}': {ex.Message}" );
			System = null;
			_broken = true;
			return;
		}

		if ( Driver is null )
			Driver = Components.Get<IMonsterDriver>();
		if ( Driver is null )
			Log.Warning( $"TwoBrainsComponent on '{GameObject?.Name}': no IMonsterDriver sibling found; actions will be rejected until one is assigned." );
	}

	protected override void OnFixedUpdate()
	{
		if ( _broken || System is null )
			return;

		PollActiveMove();

		var rate = Math.Max( 1, TicksPerSecond );
		var dt = 1.0 / rate;
		_accumulator += Time.Delta;

		var budget = Math.Max( 1, MaxTicksPerFrame );
		var ticks = 0;
		while ( _accumulator >= dt && ticks < budget )
		{
			_accumulator -= dt;
			ticks++;
			TickOnce( dt );
			if ( _broken )
				return;
		}

		// Never let the accumulator spiral when the frame rate collapses; the sim stays
		// deterministic (fixed dt per tick) and simply runs slower than wall clock.
		if ( _accumulator > dt * 4.0 )
			_accumulator = dt;
	}

	private void TickOnce( double dt )
	{
		WorldSnapshot snapshot;
		try
		{
			snapshot = BuildSnapshot( dt );
		}
		catch ( Exception ex )
		{
			Log.Error( $"TwoBrainsComponent: snapshot build failed: {ex.Message}" );
			_broken = true;
			return;
		}

		DecisionBatch batch;
		try
		{
			batch = System.Tick( snapshot );
		}
		catch ( Exception ex )
		{
			Log.Error( $"TwoBrainsComponent: core tick {snapshot.TickIndex} failed: {ex.Message}" );
			_broken = true;
			return;
		}

		LastBatch = batch;
		DispatchActions( batch );

		// A Despawn script directive surfaces as a micro flag; act on it exactly once.
		if ( !_despawnTriggered && System.MicroState.Flags.Contains( StateKeys.DespawnRequested ) )
		{
			_despawnTriggered = true;
			try { Driver?.Despawn(); }
			catch ( Exception ex ) { Log.Warning( $"TwoBrainsComponent: driver Despawn threw: {ex.Message}" ); }
		}
	}

	// ------------------------------------------------------------------
	// Snapshot construction
	// ------------------------------------------------------------------

	private WorldSnapshot BuildSnapshot( double dt )
	{
		var tick = System.NextTickIndex;
		var monsterPos = DriverPosition();

		var snapshot = new WorldSnapshot
		{
			TickIndex = tick,
			DeltaTimeSeconds = dt,
			Monster = new MonsterSnapshot
			{
				MonsterId = GameObject?.Name ?? "monster",
				Position = SandboxVec.ToCore( monsterPos ),
				RegionId = ResolveRegionId( monsterPos, null ),
				Lifecycle = (Driver?.IsAlive ?? true) ? MonsterLifecycle.Alive : MonsterLifecycle.Dead,
				Presence = StagePresence.Frontstage,
				HealthFraction = _healthFraction,
				CurrentTargetId = EmptyToNull( System.MicroState.CurrentTargetId ),
				ActiveActionId = _activeMove?.ActionId,
				LastDamageTick = _lastDamageTick,
			},
			CurrentStimuli = new List<Stimulus>( _pendingStimuli ),
			Directives = new List<ScriptDirective>( _pendingDirectives ),
			Acknowledgements = new List<ActionResult>( _pendingAcks ),
		};
		_pendingStimuli.Clear();
		_pendingDirectives.Clear();
		_pendingAcks.Clear();

		CollectTargets( snapshot, monsterPos );
		CollectNavCandidates( snapshot, monsterPos );
		CollectIngressPoints( snapshot );
		CollectOffstageRegions( snapshot );
		CollectExclusionZones( snapshot );
		return snapshot;
	}

	private Vector3 DriverPosition()
	{
		try
		{
			if ( Driver is not null )
				return Driver.Position;
		}
		catch ( Exception ) { /* fall through to the transform */ }
		return WorldPosition;
	}

	private void CollectTargets( WorldSnapshot snapshot, Vector3 monsterPos )
	{
		foreach ( var target in Scene.GetAllComponents<TwoBrainsTarget>() )
		{
			if ( target is null || !target.IsValid )
				continue;
			var go = target.GameObject;
			if ( go is null || !go.Active )
				continue;

			var pos = target.WorldPosition;
			snapshot.Targets.Add( new TargetSnapshot
			{
				TargetId = go.Name,
				Position = SandboxVec.ToCore( pos ),
				RegionId = ResolveRegionId( pos, target.RegionId ),
				IsVisible = HasLineOfSight( monsterPos, pos ),
				IsArmed = target.IsArmed,
				IsHiding = target.IsHiding,
				PressureEligible = target.PressureEligible,
				ThreatRating = Clamp01( target.ThreatRating ),
				ObjectiveId = EmptyToNull( target.ObjectiveId ),
				ObjectiveProgress = Clamp01( target.ObjectiveProgress ),
			} );
		}
	}

	private void CollectNavCandidates( WorldSnapshot snapshot, Vector3 monsterPos )
	{
		var monsterCore = SandboxVec.ToCore( monsterPos );
		var radius = Math.Max( 1.0f, NavNodeRadius );
		foreach ( var node in Scene.GetAllComponents<TwoBrainsNavNode>() )
		{
			if ( node is null || !node.IsValid || node.GameObject is null || !node.GameObject.Active )
				continue;

			var nodeCore = SandboxVec.ToCore( node.WorldPosition );
			if ( monsterCore.DistanceTo( nodeCore ) > radius )
				continue;

			snapshot.NavCandidates.Add( new NavCandidate
			{
				NodeId = node.GameObject.Name,
				Kind = node.Kind,
				Position = nodeCore,
				RegionId = node.RegionId ?? "",
				Reachable = node.Reachable,
				// Honest "unknown": the adapter does not run path queries per node per tick.
				RouteDistance = -1.0,
				// Per-node LOS traces every tick are too expensive; report no claim.
				HasLineOfSight = false,
			} );
		}
	}

	private void CollectIngressPoints( WorldSnapshot snapshot )
	{
		var rate = Math.Max( 1, TicksPerSecond );
		foreach ( var ingress in Scene.GetAllComponents<TwoBrainsIngress>() )
		{
			if ( ingress is null || !ingress.IsValid || ingress.GameObject is null || !ingress.GameObject.Active )
				continue;

			var id = string.IsNullOrEmpty( ingress.IngressId ) ? ingress.GameObject.Name : ingress.IngressId;
			long cooldownUntil = -1;
			if ( ingress.CooldownSeconds > 0.0f && _ingressUseTicks.TryGetValue( id, out var usedTick ) )
				cooldownUntil = usedTick + (long)Math.Ceiling( ingress.CooldownSeconds * rate );

			snapshot.IngressPoints.Add( new IngressPoint
			{
				IngressId = id,
				Kind = ingress.Kind,
				Position = SandboxVec.ToCore( ingress.WorldPosition ),
				RegionId = ingress.RegionId ?? "",
				OffstageNodeId = ingress.OffstageNodeId ?? "",
				Usable = true,
				CooldownUntilTick = cooldownUntil,
			} );
		}
	}

	private void CollectOffstageRegions( WorldSnapshot snapshot )
	{
		foreach ( var region in Scene.GetAllComponents<TwoBrainsOffstageRegion>() )
		{
			if ( region is null || !region.IsValid || region.GameObject is null || !region.GameObject.Active )
				continue;

			snapshot.OffstageRegions.Add( new OffstageRegion
			{
				RegionId = string.IsNullOrEmpty( region.RegionId ) ? region.GameObject.Name : region.RegionId,
				NodeIds = new List<string>( region.NodeIds ),
				IngressIds = new List<string>( region.IngressIds ),
				AdjacentRegionIds = new List<string>( region.AdjacentRegionIds ),
			} );
		}
	}

	private void CollectExclusionZones( WorldSnapshot snapshot )
	{
		foreach ( var zone in Scene.GetAllComponents<TwoBrainsExclusionZone>() )
		{
			if ( zone is null || !zone.IsValid || zone.GameObject is null || !zone.GameObject.Active )
				continue;

			snapshot.ExclusionZones.Add( new ExclusionZone
			{
				ZoneId = string.IsNullOrEmpty( zone.ZoneId ) ? zone.GameObject.Name : zone.ZoneId,
				Kind = zone.Kind,
				Center = SandboxVec.ToCore( zone.WorldPosition ),
				Radius = Math.Max( 0.01f, zone.Radius ),
				Active = zone.ZoneActive,
			} );
		}
	}

	/// <summary>
	/// Explicit region id wins; otherwise the region of the nearest nav node within the nav
	/// radius; otherwise empty. Region ids are plain strings from the marker components.
	/// </summary>
	private string ResolveRegionId( Vector3 position, string explicitId )
	{
		if ( !string.IsNullOrEmpty( explicitId ) )
			return explicitId;

		var core = SandboxVec.ToCore( position );
		var radius = Math.Max( 1.0f, NavNodeRadius );
		string best = null;
		var bestDist = double.MaxValue;
		foreach ( var node in Scene.GetAllComponents<TwoBrainsNavNode>() )
		{
			if ( node is null || !node.IsValid || node.GameObject is null || !node.GameObject.Active )
				continue;
			if ( string.IsNullOrEmpty( node.RegionId ) )
				continue;

			var dist = core.DistanceTo( SandboxVec.ToCore( node.WorldPosition ) );
			if ( dist <= radius && dist < bestDist )
			{
				bestDist = dist;
				best = node.RegionId;
			}
		}
		return best ?? "";
	}

	/// <summary>
	/// Simple LOS trace from monster eye to target eye, ignoring the monster's own collider
	/// hierarchy and tolerating the target's own body at the far end. Any trace failure
	/// reports "not visible" — the conservative answer for a perception system.
	/// </summary>
	private bool HasLineOfSight( Vector3 from, Vector3 to )
	{
		try
		{
			var eye = Vector3.Up * SandboxVec.ToSboxDistance( LosEyeHeight );
			var start = from + eye;
			var end = to + eye;
			var trace = Scene.Trace.Ray( start, end )
				.IgnoreGameObjectHierarchy( GameObject )
				.Run();
			if ( !trace.Hit )
				return true;
			var total = (end - start).Length;
			return trace.Distance >= total - 48.0f;
		}
		catch ( Exception )
		{
			return false;
		}
	}

	// ------------------------------------------------------------------
	// Action dispatch and acknowledgement
	// ------------------------------------------------------------------

	private void DispatchActions( DecisionBatch batch )
	{
		foreach ( var action in batch.Actions )
		{
			try
			{
				DispatchAction( batch, action );
			}
			catch ( Exception ex )
			{
				EnqueueAck( action.ActionId, ActionStatus.Failed, "dispatch exception: " + ex.Message );
			}
		}
	}

	private void DispatchAction( DecisionBatch batch, ActionRequest action )
	{
		if ( IsMovementKind( action.Kind ) )
		{
			DispatchMovement( batch, action );
			return;
		}

		switch ( action.Kind )
		{
			case ActionKind.UseIngress:
			{
				if ( Driver is null ) { EnqueueAck( action.ActionId, ActionStatus.Rejected, "no driver" ); return; }
				if ( string.IsNullOrEmpty( action.IngressId ) ) { EnqueueAck( action.ActionId, ActionStatus.Rejected, "no ingress id" ); return; }
				var ok = false;
				try { ok = Driver.TryTraverseIngress( action.IngressId ); }
				catch ( Exception ex ) { EnqueueAck( action.ActionId, ActionStatus.Failed, "driver threw: " + ex.Message ); return; }
				if ( ok )
				{
					_ingressUseTicks[action.IngressId] = batch.TickIndex;
					EnqueueAck( action.ActionId, ActionStatus.Succeeded, null );
				}
				else
				{
					EnqueueAck( action.ActionId, ActionStatus.Failed, "ingress unusable" );
				}
				return;
			}
			case ActionKind.Threat:
				if ( Driver is null ) { EnqueueAck( action.ActionId, ActionStatus.Rejected, "no driver" ); return; }
				Driver.PlayThreat();
				EnqueueAck( action.ActionId, ActionStatus.Succeeded, null );
				return;
			case ActionKind.Attack:
				if ( Driver is null ) { EnqueueAck( action.ActionId, ActionStatus.Rejected, "no driver" ); return; }
				Driver.PlayAttack( action.TargetId );
				EnqueueAck( action.ActionId, ActionStatus.Succeeded, null );
				return;
			case ActionKind.Wait:
				if ( Driver is null ) { EnqueueAck( action.ActionId, ActionStatus.Rejected, "no driver" ); return; }
				Driver.PlayWait( WaitSeconds( batch, action ) );
				EnqueueAck( action.ActionId, ActionStatus.Succeeded, null );
				return;
			case ActionKind.Scripted:
				if ( Driver is null ) { EnqueueAck( action.ActionId, ActionStatus.Rejected, "no driver" ); return; }
				Driver.PlayScripted( action.Param ?? "" );
				EnqueueAck( action.ActionId, ActionStatus.Succeeded, null );
				return;
			default:
				EnqueueAck( action.ActionId, ActionStatus.Rejected, "unsupported kind " + action.Kind );
				return;
		}
	}

	private void DispatchMovement( DecisionBatch batch, ActionRequest action )
	{
		if ( Driver is null )
		{
			EnqueueAck( action.ActionId, ActionStatus.Rejected, "no driver" );
			return;
		}

		var dest = ResolveDestination( action );
		if ( dest is null )
		{
			// No destination anywhere (e.g. Ambush "hold here"): play a hold and acknowledge
			// success — rejecting would wrongly feed the core's nav-failure escalation.
			Driver.PlayWait( WaitSeconds( batch, action ) );
			EnqueueAck( action.ActionId, ActionStatus.Succeeded, "held position" );
			return;
		}

		// Supersede any movement still in flight; its completion is ignored from here on.
		if ( _activeMove is not null )
		{
			EnqueueAck( _activeMove.ActionId, ActionStatus.Interrupted, "superseded by " + action.ActionId );
			_activeMove = null;
		}

		Task<bool> task;
		try
		{
			task = Driver.MoveToAsync( dest.Value, (float)Clamp01( action.SpeedScale ) );
		}
		catch ( Exception ex )
		{
			EnqueueAck( action.ActionId, ActionStatus.Failed, "driver threw: " + ex.Message );
			return;
		}

		if ( task is null )
		{
			EnqueueAck( action.ActionId, ActionStatus.Failed, "driver returned null task" );
			return;
		}

		_activeMove = new ActiveMove { ActionId = action.ActionId, Task = task };
	}

	/// <summary>
	/// Destination for a movement action: the request's own Destination, else the referenced
	/// nav node's position, else the referenced target's position, else null (hold in place).
	/// </summary>
	private Vector3? ResolveDestination( ActionRequest action )
	{
		if ( action.Destination is Vec3 dest )
			return SandboxVec.ToSbox( dest );

		if ( !string.IsNullOrEmpty( action.NodeId ) )
		{
			foreach ( var node in Scene.GetAllComponents<TwoBrainsNavNode>() )
			{
				if ( node is null || !node.IsValid || node.GameObject is null )
					continue;
				if ( string.Equals( node.GameObject.Name, action.NodeId, StringComparison.Ordinal ) )
					return node.WorldPosition;
			}
		}

		if ( !string.IsNullOrEmpty( action.TargetId ) )
		{
			foreach ( var target in Scene.GetAllComponents<TwoBrainsTarget>() )
			{
				if ( target is null || !target.IsValid || target.GameObject is null )
					continue;
				if ( string.Equals( target.GameObject.Name, action.TargetId, StringComparison.Ordinal ) )
					return target.WorldPosition;
			}
		}

		return null;
	}

	/// <summary>Polls the in-flight driver move and converts its completion into an ack.</summary>
	private void PollActiveMove()
	{
		var move = _activeMove;
		if ( move is null || !move.Task.IsCompleted )
			return;

		_activeMove = null;
		if ( move.Task.IsFaulted || move.Task.IsCanceled )
		{
			EnqueueAck( move.ActionId, ActionStatus.Failed, "driver task faulted" );
			return;
		}

		var ok = false;
		try { ok = move.Task.Result; }
		catch ( Exception ) { ok = false; }
		EnqueueAck( move.ActionId, ok ? ActionStatus.Succeeded : ActionStatus.Failed, ok ? null : "driver reported failure" );
	}

	private void EnqueueAck( string actionId, ActionStatus status, string detail )
	{
		if ( string.IsNullOrEmpty( actionId ) )
			return;
		_pendingAcks.Add( new ActionResult
		{
			ActionId = actionId,
			Status = status,
			Detail = detail,
			ResultTick = System?.NextTickIndex ?? 0,
		} );
	}

	private float WaitSeconds( DecisionBatch batch, ActionRequest action )
	{
		var ticks = Math.Max( 1L, action.ExpiryTick - batch.TickIndex );
		return Math.Max( 0.05f, (float)(ticks / (double)Math.Max( 1, TicksPerSecond )) );
	}

	/// <summary>Movement kinds, mirroring the core's AgentContext.IsMovementKind.</summary>
	private static bool IsMovementKind( ActionKind kind )
	{
		return kind == ActionKind.MoveTo || kind == ActionKind.Search || kind == ActionKind.Investigate
			|| kind == ActionKind.Stalk || kind == ActionKind.Ambush || kind == ActionKind.Chase
			|| kind == ActionKind.Retreat;
	}

	// ------------------------------------------------------------------
	// Host-facing API
	// ------------------------------------------------------------------

	/// <summary>
	/// Reports a sensed event (sound, sighting, touch...) queued into the next tick's snapshot.
	/// The stimulus id is derived from channel/subtype/target so re-reporting the same logical
	/// event refreshes the core's memory of it instead of piling up duplicates.
	/// </summary>
	public void ReportStimulus( SenseChannel channel, Vector3 position, string regionId = null, double confidence = 1.0, string targetId = null, string subtype = null )
	{
		var tick = System?.NextTickIndex ?? 0;
		_pendingStimuli.Add( new Stimulus
		{
			StimulusId = channel + ":" + (subtype ?? "") + ":" + (targetId ?? ""),
			Channel = channel,
			Subtype = subtype,
			Position = SandboxVec.ToCore( position ),
			RegionId = regionId ?? "",
			Confidence = Clamp01( confidence ),
			TargetId = targetId,
			CreatedTick = tick,
			LastConfirmedTick = tick,
		} );
	}

	/// <summary>
	/// Reports damage to the monster as a health fraction lost (0.1 = 10%). Lowers the
	/// host-reported health, stamps the damage tick, and emits a Damage-channel stimulus.
	/// </summary>
	public void ReportDamage( double fraction )
	{
		if ( fraction < 0.0 )
			fraction = 0.0;
		_healthFraction = Math.Max( 0.0, _healthFraction - fraction );
		_lastDamageTick = System?.NextTickIndex ?? 0;
		ReportStimulus( SenseChannel.Damage, DriverPosition(), null, 1.0 );
	}

	/// <summary>Queues a script directive, consumed once on the next tick.</summary>
	public void IssueDirective( ScriptDirective directive )
	{
		if ( directive is not null )
			_pendingDirectives.Add( directive );
	}

	/// <summary>Canonical-JSON save of the complete deterministic core state, or null if not started.</summary>
	public string CaptureSave()
	{
		return System is null ? null : CanonicalJson.ToJson( System.CaptureState() );
	}

	/// <summary>
	/// Restores a save produced by <see cref="CaptureSave"/>. Only valid for the same profile
	/// and seed (the replay contract); clears all pending host-side queues.
	/// </summary>
	public void RestoreSave( string json )
	{
		if ( System is null || string.IsNullOrEmpty( json ) )
			return;

		System.RestoreState( CanonicalJson.FromJson<SavedStateEnvelope>( json ) );
		_pendingStimuli.Clear();
		_pendingDirectives.Clear();
		_pendingAcks.Clear();
		_activeMove = null;
		_accumulator = 0.0;
	}

	private static double Clamp01( double value )
	{
		return value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;
	}

	private static string EmptyToNull( string value )
	{
		return string.IsNullOrEmpty( value ) ? null : value;
	}
}

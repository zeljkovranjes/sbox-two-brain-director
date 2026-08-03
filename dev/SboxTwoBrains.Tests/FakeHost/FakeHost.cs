using System;
using System.Collections.Generic;
using SboxTwoBrains;

namespace SboxTwoBrains.Tests.FakeHost;

/// <summary>
/// In-memory deterministic host for integration tests and examples. Drives a
/// <see cref="TwoBrainsSystem"/> over explicit ticks: builds immutable world snapshots from
/// a simple simulated world, executes declarative <see cref="ActionRequest"/>s (movement at
/// fixed speed, ingress transitions, combat stubs), and returns <see cref="ActionResult"/>s
/// on a LATER tick, exactly like a real engine host must.
///
/// All randomness lives in the system under test; the host itself is fully deterministic
/// (fixed speed, explicit ack policy delegates).
/// </summary>
public sealed class FakeHost
{
	public sealed class FakeTarget
	{
		public string TargetId = "";
		public Vec3 Position;
		public string RegionId = "";
		public bool IsValid = true;
		public bool IsAlive = true;
		public bool IsVisible;
		public bool IsArmed;
		public bool IsAimingAtMonster;
		public bool IsUsingDeterrent;
		public bool IsHiding;
		public double ThreatRating;
		public double HealthFraction = 1.0;
		public double LightLevel = 0.5;
		public bool PressureEligible = true;
		public string ObjectiveId;
		public double ObjectiveProgress;
	}

	/// <summary>How the host resolves one action request.</summary>
	public delegate ActionStatus AckPolicy( ActionRequest request, FakeHost host );

	/// <summary>
	/// How the host resolves one macro opportunity offer. <paramref name="postponements"/>
	/// counts how many times this opportunity was already answered with
	/// <see cref="ActionStatus.Deferred"/> (lets policies express "defer once, then succeed").
	/// </summary>
	public delegate ActionStatus OpportunityAckPolicy( PressureDecision opportunity, FakeHost host, int postponements );

	private readonly List<ActionResult> _pendingAcks = new List<ActionResult>();
	private readonly List<ActiveExecution> _executions = new List<ActiveExecution>();
	private readonly List<Stimulus> _stimuli = new List<Stimulus>();
	private readonly List<ActionResult> _queuedOpportunityAcks = new List<ActionResult>();
	private string _trackedOpportunityId = "";
	private PressureDecision _trackedOpportunity;
	private int _opportunityPostponements;

	private sealed class ActiveExecution
	{
		public ActionRequest Request;
		public AckPolicy Policy;
		public int TicksActive;
	}

	public TwoBrainsSystem System { get; }
	public double DeltaTime { get; set; } = 1.0 / 60.0;
	public long TickIndex { get; private set; }

	// world state
	public Vec3 MonsterPosition { get; set; }
	public string MonsterRegionId { get; set; } = "";
	public double MonsterHealth { get; set; } = 1.0;
	public StagePresence MonsterPresence { get; set; } = StagePresence.Frontstage;
	public MonsterLifecycle Lifecycle { get; set; } = MonsterLifecycle.Alive;
	public string MonsterCurrentIngressId { get; set; }
	public long LastDamageTick { get; set; } = -1;
	public bool CanMove { get; set; } = true;
	public bool CanAttack { get; set; } = true;
	public bool CanTraverseIngress { get; set; } = true;
	public string[] MonsterFlags { get; set; } = Array.Empty<string>();

	public double MonsterMaxSpeed { get; set; } = 6.0; // metres/second at SpeedScale 1
	public bool OmniscientTargets { get; set; }

	public List<FakeTarget> Targets { get; } = new List<FakeTarget>();
	public List<NavCandidate> NavCandidates { get; } = new List<NavCandidate>();
	public List<OffstageRegion> OffstageRegions { get; } = new List<OffstageRegion>();
	public List<IngressPoint> IngressPoints { get; } = new List<IngressPoint>();
	public List<ExclusionZone> ExclusionZones { get; } = new List<ExclusionZone>();
	public List<ScriptDirective> PendingDirectives { get; } = new List<ScriptDirective>();

	/// <summary>Per-kind ack policy overrides; default is <see cref="DefaultPolicy"/>.</summary>
	public Dictionary<ActionKind, AckPolicy> Policies { get; } = new Dictionary<ActionKind, AckPolicy>();

	/// <summary>
	/// Macro-opportunity ack policy; null (the default) means the host never acknowledges
	/// opportunities, so they run to sweep end or expiry — the historical FakeHost behavior.
	/// When set, the policy is evaluated once per tick while the offer is pending and its
	/// answer is delivered inside the NEXT tick's snapshot, exactly like action acks.
	/// </summary>
	public OpportunityAckPolicy OpportunityPolicy { get; set; }

	/// <summary>Every batch the system produced, in order.</summary>
	public List<DecisionBatch> History { get; } = new List<DecisionBatch>();

	public FakeHost( TwoBrainsSystem system )
	{
		System = system ?? throw new ArgumentNullException( nameof( system ) );
	}

	/// <summary>Default execution: movement walks at fixed speed and succeeds on arrival; everything else succeeds next tick.</summary>
	public static ActionStatus DefaultPolicy( ActionRequest request, FakeHost host ) => ActionStatus.Succeeded;

	/// <summary>Queues a stimulus to appear in the NEXT snapshot (host sense pipeline).</summary>
	public void EmitStimulus( Stimulus stimulus )
	{
		stimulus.LastConfirmedTick = TickIndex;
		if ( stimulus.CreatedTick == 0 ) stimulus.CreatedTick = TickIndex;
		_stimuli.Add( stimulus );
	}

	public void EmitVisual( string stimulusId, string targetId, Vec3 position, string regionId, double confidence = 1.0 )
	{
		EmitStimulus( new Stimulus { StimulusId = stimulusId, Channel = SenseChannel.Visual, TargetId = targetId, Position = position, RegionId = regionId, Confidence = confidence } );
	}

	public void EmitNoise( string stimulusId, Vec3 position, string regionId, double confidence = 0.8, string subtype = "footstep" )
	{
		EmitStimulus( new Stimulus { StimulusId = stimulusId, Channel = SenseChannel.Auditory, Subtype = subtype, Position = position, RegionId = regionId, Confidence = confidence } );
	}

	public void DamageMonster( double fraction )
	{
		MonsterHealth = Math.Max( 0.0, MonsterHealth - fraction );
		LastDamageTick = TickIndex;
		EmitStimulus( new Stimulus { StimulusId = "dmg-" + TickIndex, Channel = SenseChannel.Damage, Position = MonsterPosition, RegionId = MonsterRegionId, Confidence = 1.0 } );
	}

	public FakeTarget AddTarget( string id, Vec3 position, string regionId, double threat = 0.0 )
	{
		var t = new FakeTarget { TargetId = id, Position = position, RegionId = regionId, ThreatRating = threat };
		Targets.Add( t );
		return t;
	}

	/// <summary>Advances the world one tick: serves pending acks, runs the system, starts executions.</summary>
	public DecisionBatch Step()
	{
		var snapshot = BuildSnapshot();
		var batch = System.Tick( snapshot );
		History.Add( batch );

		foreach ( var request in batch.Actions )
		{
			var policy = Policies.TryGetValue( request.Kind, out var p ) ? p : DefaultPolicy;
			_executions.Add( new ActiveExecution { Request = request, Policy = policy } );
		}

		TickIndex++;
		return batch;
	}

	public List<DecisionBatch> Run( int ticks )
	{
		var produced = new List<DecisionBatch>();
		for ( int i = 0; i < ticks; i++ )
			produced.Add( Step() );
		return produced;
	}

	private WorldSnapshot BuildSnapshot()
	{
		// advance active executions; produce acks for this tick
		_pendingAcks.Clear();
		foreach ( var exec in _executions )
			AdvanceExecution( exec );
		_executions.RemoveAll( e => e == null );

		var monster = new MonsterSnapshot
		{
			MonsterId = "monster",
			Position = MonsterPosition,
			RegionId = MonsterRegionId,
			Lifecycle = Lifecycle,
			Presence = MonsterPresence,
			HealthFraction = MonsterHealth,
			RouteAvailable = true,
			CurrentIngressId = MonsterCurrentIngressId,
			LastDamageTick = LastDamageTick,
			CanMove = CanMove,
			CanAttack = CanAttack,
			CanTraverseIngress = CanTraverseIngress,
			Flags = MonsterFlags,
		};

		var snapshot = new WorldSnapshot
		{
			TickIndex = TickIndex,
			DeltaTimeSeconds = DeltaTime,
			Monster = monster,
			OmniscientTargets = OmniscientTargets,
		};
		foreach ( var t in Targets )
		{
			snapshot.Targets.Add( new TargetSnapshot
			{
				TargetId = t.TargetId,
				IsValid = t.IsValid,
				IsAlive = t.IsAlive,
				Position = t.Position,
				RegionId = t.RegionId,
				IsVisible = t.IsVisible,
				IsArmed = t.IsArmed,
				IsAimingAtMonster = t.IsAimingAtMonster,
				IsUsingDeterrent = t.IsUsingDeterrent,
				IsHiding = t.IsHiding,
				ThreatRating = t.ThreatRating,
				HealthFraction = t.HealthFraction,
				LightLevel = t.LightLevel,
				PressureEligible = t.PressureEligible,
				ObjectiveId = t.ObjectiveId,
				ObjectiveProgress = t.ObjectiveProgress,
			} );
		}
		snapshot.CurrentStimuli.AddRange( _stimuli );
		_stimuli.Clear();
		snapshot.NavCandidates.AddRange( NavCandidates );
		snapshot.OffstageRegions.AddRange( OffstageRegions );
		snapshot.IngressPoints.AddRange( IngressPoints );
		snapshot.ExclusionZones.AddRange( ExclusionZones );
		snapshot.Directives.AddRange( PendingDirectives );
		PendingDirectives.Clear();
		snapshot.Acknowledgements.AddRange( _pendingAcks );
		return snapshot;
	}

	private void AdvanceExecution( ActiveExecution exec )
	{
		exec.TicksActive++;
		var request = exec.Request;
		var status = exec.Policy( request, this );

		// deferred stays in flight without completing
		if ( status == ActionStatus.Deferred )
		{
			if ( TickIndex > request.ExpiryTick )
			{
				Complete( exec, ActionStatus.Failed, "expired while deferred" );
			}
			else if ( exec.TicksActive == 1 )
			{
				_pendingAcks.Add( new ActionResult { ActionId = request.ActionId, Status = ActionStatus.Deferred, Detail = "host busy", ResultTick = TickIndex } );
			}
			return;
		}

		switch ( request.Kind )
		{
			case ActionKind.MoveTo:
			case ActionKind.Chase:
			case ActionKind.Retreat:
			case ActionKind.Investigate when request.Destination.HasValue:
			case ActionKind.Stalk when request.Destination.HasValue:
				AdvanceMovement( exec, status );
				return;
			default:
				Complete( exec, status, status.ToString() );
				return;
		}
	}

	private void AdvanceMovement( ActiveExecution exec, ActionStatus policyStatus )
	{
		if ( policyStatus != ActionStatus.Succeeded && policyStatus != ActionStatus.PartiallySucceeded )
		{
			Complete( exec, policyStatus, "policy " + policyStatus );
			return;
		}
		var destination = exec.Request.Destination ?? MonsterPosition;
		var speed = MonsterMaxSpeed * Math.Clamp( exec.Request.SpeedScale, 0.0, 1.0 ) * DeltaTime;
		var toTarget = destination - MonsterPosition;
		var distance = toTarget.Length();
		if ( distance <= Math.Max( speed, 0.5 ) )
		{
			MonsterPosition = destination;
			Complete( exec, ActionStatus.Succeeded, "arrived" );
			return;
		}
		MonsterPosition = MonsterPosition + toTarget * ( speed / distance );
		// still walking; no ack yet (acks arrive on later ticks, like a real engine)
	}

	private void Complete( ActiveExecution exec, ActionStatus status, string detail )
	{
		_pendingAcks.Add( new ActionResult { ActionId = exec.Request.ActionId, Status = status, Detail = detail, ResultTick = TickIndex } );
		if ( exec.Request.Kind == ActionKind.UseIngress && status == ActionStatus.Succeeded )
		{
			MonsterPresence = MonsterPresence == StagePresence.Frontstage ? StagePresence.Offstage : StagePresence.Frontstage;
		}
		_executions[_executions.IndexOf( exec )] = null;
	}

	/// <summary>Builds a simple grid of nav nodes for scenarios.</summary>
	public NavCandidate AddNode( string nodeId, double x, double y, double z, string regionId, bool reachable = true, NavCandidateKind kind = NavCandidateKind.FrontstageNode )
	{
		var node = new NavCandidate { NodeId = nodeId, Position = new Vec3( x, y, z ), RegionId = regionId, Reachable = reachable, Kind = kind, RouteDistance = MonsterPosition.DistanceTo( new Vec3( x, y, z ) ) };
		NavCandidates.Add( node );
		return node;
	}

	public IngressPoint AddIngress( string ingressId, double x, double y, double z, string regionId, string offstageNodeId, IngressKind kind = IngressKind.Vent )
	{
		var ingress = new IngressPoint { IngressId = ingressId, Position = new Vec3( x, y, z ), RegionId = regionId, OffstageNodeId = offstageNodeId, Kind = kind, Usable = true };
		IngressPoints.Add( ingress );
		return ingress;
	}
}

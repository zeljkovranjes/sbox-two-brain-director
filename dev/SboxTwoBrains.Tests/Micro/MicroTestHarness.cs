using System.Collections.Generic;
using SboxTwoBrains;

namespace SboxTwoBrains.Tests.Micro;

/// <summary>
/// Deterministic driver for a lone <see cref="MonsterAgent"/>: applies acks, then
/// directives, then ticks (the facade order) with a fixed dt of 0.1 s. The world snapshot
/// is mutated directly between steps; stimuli persist until cleared by the test.
/// </summary>
internal sealed class AgentDriver
{
	public readonly DeterministicRng Rng;
	public readonly MonsterAgent Agent;
	public EffectiveConfig Cfg;
	public long Tick;
	public double Dt = 0.1;
	public PressureDecision Macro;
	public WorldSnapshot World;
	public List<ActionRequest> LastActions = new List<ActionRequest>();
	public List<TelemetryEvent> LastTelemetry = new List<TelemetryEvent>();
	public readonly List<TelemetryEvent> History = new List<TelemetryEvent>();
	private readonly List<ActionResult> _acks = new List<ActionResult>();
	private readonly List<ScriptDirective> _directives = new List<ScriptDirective>();

	public AgentDriver( ulong seed = 1, EffectiveConfig cfg = null )
	{
		Rng = new DeterministicRng( seed );
		Agent = new MonsterAgent( Rng );
		Cfg = cfg ?? new EffectiveConfig();
		World = Snap.Basic();
	}

	public MicroState State => Agent.State;

	public void Ack( string actionId, ActionStatus status, string detail = null )
	{
		_acks.Add( new ActionResult { ActionId = actionId, Status = status, Detail = detail, ResultTick = Tick } );
	}

	public void AckLast( ActionStatus status )
	{
		foreach ( var a in LastActions ) Ack( a.ActionId, status );
	}

	public void Direct( ScriptDirective directive ) => _directives.Add( directive );

	public List<ActionRequest> Step( int count = 1 )
	{
		List<ActionRequest> result = null;
		for ( int i = 0; i < count; i++ ) result = StepOnce();
		return result ?? new List<ActionRequest>();
	}

	public List<ActionRequest> StepOnce()
	{
		World.TickIndex = Tick;
		World.DeltaTimeSeconds = Dt;
		World.Acknowledgements.Clear();
		World.Acknowledgements.AddRange( _acks );
		_acks.Clear();
		World.Directives.Clear();
		World.Directives.AddRange( _directives );
		_directives.Clear();
		var ctx = new TickContext( Tick, Dt );
		LastTelemetry = new List<TelemetryEvent>();
		Agent.ApplyActionResults( ctx, Cfg, World.Acknowledgements, LastTelemetry );
		Agent.ApplyDirectives( ctx, Cfg, World.Directives, LastTelemetry );
		LastActions = Agent.Tick( ctx, World, Macro, Cfg, LastTelemetry );
		History.AddRange( LastTelemetry );
		Tick++;
		return LastActions;
	}

	/// <summary>Ack every action just produced, then step (the common "host complies" loop).</summary>
	public List<ActionRequest> StepAck( ActionStatus status = ActionStatus.Succeeded )
	{
		AckLast( status );
		return StepOnce();
	}

	public bool Tele( string code ) => LastTelemetry.Exists( e => e.Code == code );
	public int TeleCount( string code ) => LastTelemetry.FindAll( e => e.Code == code ).Count;
	public bool Hist( string code ) => History.Exists( e => e.Code == code );
	public int HistCount( string code ) => History.FindAll( e => e.Code == code ).Count;

	/// <summary>Canonical JSON of this tick's output, used for replay/save-restore comparison.</summary>
	public string OutputJson() => CanonicalJson.ToJson( new TickOutput { Actions = LastActions, Telemetry = LastTelemetry } );

	/// <summary>Builds a read-only evaluation context over the driver's current state (sense probes).</summary>
	public AgentContext Probe()
	{
		return new AgentContext( new TickContext( Tick, Dt ), World, null, Cfg, State, Rng, new List<TelemetryEvent>() );
	}

	public sealed class TickOutput
	{
		public List<ActionRequest> Actions { get; set; }
		public List<TelemetryEvent> Telemetry { get; set; }
	}
}

/// <summary>Fluent-free snapshot piece builders with deterministic defaults.</summary>
internal static class Snap
{
	public static WorldSnapshot Basic( double x = 0.0, double z = 0.0, string region = "R0" )
	{
		return new WorldSnapshot
		{
			Monster = new MonsterSnapshot { MonsterId = "m1", Position = new Vec3( x, 0.0, z ), RegionId = region },
		};
	}

	public static TargetSnapshot Target( string id, double x, double z, double threat = 0.0, bool visible = false, string region = "R1" )
	{
		return new TargetSnapshot { TargetId = id, Position = new Vec3( x, 0.0, z ), ThreatRating = threat, IsVisible = visible, RegionId = region };
	}

	public static Stimulus Stim( string id, SenseChannel channel, double confidence, string targetId = null, double x = 0.0, double z = 0.0, string region = "R1", string subtype = null )
	{
		return new Stimulus { StimulusId = id, Channel = channel, Confidence = confidence, TargetId = targetId, Position = new Vec3( x, 0.0, z ), RegionId = region, Subtype = subtype };
	}

	public static NavCandidate Node( string id, string region, double x, double z, double route = -1.0, bool reachable = true, NavCandidateKind kind = NavCandidateKind.FrontstageNode )
	{
		return new NavCandidate { NodeId = id, RegionId = region, Position = new Vec3( x, 0.0, z ), RouteDistance = route, Reachable = reachable, Kind = kind };
	}

	public static IngressPoint Ingress( string id, double x, double z, string region = "R0", string offstageNode = "" )
	{
		return new IngressPoint { IngressId = id, Position = new Vec3( x, 0.0, z ), RegionId = region, OffstageNodeId = offstageNode };
	}

	public static PressureDecision Macro( string region = "R1", PressureMode mode = PressureMode.Aggressive, string[] roles = null, string[] ingress = null, string[] exclusions = null, long expiry = long.MaxValue )
	{
		return new PressureDecision
		{
			OpportunityId = "opp1",
			Mode = mode,
			CandidateRegionId = region,
			AllowedRoles = roles ?? new string[0],
			IngressConstraints = ingress ?? new string[0],
			ExclusionConstraints = exclusions ?? new string[0],
			ExpiryTick = expiry,
			ReasonCode = "test",
		};
	}
}

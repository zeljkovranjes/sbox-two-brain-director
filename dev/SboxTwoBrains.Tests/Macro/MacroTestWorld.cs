using System;
using System.Collections.Generic;
using SboxTwoBrains;

namespace SboxTwoBrains.Tests.Macro;

/// <summary>
/// Fluent <see cref="WorldSnapshot"/> builder for macro tests. Each instance builds one
/// tick's snapshot; the driver mirrors the facade's per-tick order (acks → directives → tick).
/// </summary>
internal sealed class WorldBuilder
{
	private readonly long _tick;
	private readonly double _dt;
	private MonsterSnapshot _monster = new MonsterSnapshot { MonsterId = "m", Lifecycle = MonsterLifecycle.Alive, Position = Vec3.Zero };
	private readonly List<TargetSnapshot> _targets = new List<TargetSnapshot>();
	private readonly List<ExclusionZone> _zones = new List<ExclusionZone>();
	private readonly List<IngressPoint> _ingress = new List<IngressPoint>();
	private readonly List<OffstageRegion> _offstage = new List<OffstageRegion>();
	private readonly List<ScriptDirective> _directives = new List<ScriptDirective>();
	private readonly List<ActionResult> _acks = new List<ActionResult>();

	private WorldBuilder( long tick, double dt )
	{
		_tick = tick;
		_dt = dt;
	}

	public static WorldBuilder At( long tick, double dt ) => new WorldBuilder( tick, dt );

	public WorldBuilder Monster( double x, double z, MonsterLifecycle lifecycle = MonsterLifecycle.Alive )
	{
		_monster = new MonsterSnapshot { MonsterId = "m", Lifecycle = lifecycle, Position = new Vec3( x, 0.0, z ) };
		return this;
	}

	public WorldBuilder Target( string id, string region, double x, double z, bool valid = true, bool alive = true, bool eligible = true )
	{
		_targets.Add( new TargetSnapshot
		{
			TargetId = id,
			RegionId = region,
			Position = new Vec3( x, 0.0, z ),
			IsValid = valid,
			IsAlive = alive,
			PressureEligible = eligible,
		} );
		return this;
	}

	public WorldBuilder Zone( string id, ExclusionKind kind, double x, double z, double radius, bool active = true )
	{
		_zones.Add( new ExclusionZone { ZoneId = id, Kind = kind, Center = new Vec3( x, 0.0, z ), Radius = radius, Active = active } );
		return this;
	}

	public WorldBuilder Ingress( string id, string region, bool usable = true, long cooldownUntilTick = -1 )
	{
		_ingress.Add( new IngressPoint { IngressId = id, RegionId = region, Usable = usable, CooldownUntilTick = cooldownUntilTick } );
		return this;
	}

	public WorldBuilder Offstage( string region, string[] ingressIds, string[] adjacentRegionIds )
	{
		_offstage.Add( new OffstageRegion
		{
			RegionId = region,
			IngressIds = new List<string>( ingressIds ),
			AdjacentRegionIds = new List<string>( adjacentRegionIds ),
		} );
		return this;
	}

	public WorldBuilder Directive( ScriptDirective directive )
	{
		_directives.Add( directive );
		return this;
	}

	public WorldBuilder Ack( string actionId, ActionStatus status, string detail = null )
	{
		_acks.Add( new ActionResult { ActionId = actionId, Status = status, Detail = detail, ResultTick = _tick } );
		return this;
	}

	public WorldSnapshot Build()
	{
		return new WorldSnapshot
		{
			TickIndex = _tick,
			DeltaTimeSeconds = _dt,
			Monster = _monster,
			Targets = _targets,
			ExclusionZones = _zones,
			IngressPoints = _ingress,
			OffstageRegions = _offstage,
			Directives = _directives,
			Acknowledgements = _acks,
		};
	}
}

/// <summary>Effective-config factory with fast, exactly-representable timing constants.</summary>
internal static class MacroConfigs
{
	/// <summary>
	/// Base test config: fill 1s, cooldown 0.25s, decrease 1s, no decrease delay, max 4,
	/// quota disabled, threshold 0.5, start 0, sweep 0.25s, expiry 5s, first margin 8m,
	/// subsequent margin 0m, ingress attract 1-2s. All timings are exact in binary with dt=0.25.
	/// </summary>
	public static EffectiveConfig Fast()
	{
		var cfg = new EffectiveConfig();
		var p = cfg.Pressure;
		p.FillSeconds = 1.0;
		p.CooldownSeconds = 0.25;
		p.DecreaseSeconds = 1.0;
		p.DecreaseDelaySeconds = 0.0;
		p.MaxOpportunities = 4;
		p.EventQuotaMin = 0;
		p.EventQuotaMax = 0;
		p.AggressiveThresholdProgression = 0.5;
		p.StartProgression = 0.0;
		p.SweepDurationSeconds = 0.25;
		p.ExclusionFirstMin = 8.0;
		p.ExclusionSubsequentMin = 0.0;
		p.IngressAttractMinSeconds = 1.0;
		p.IngressAttractMaxSeconds = 2.0;
		p.OpportunityExpirySeconds = 5.0;
		return cfg;
	}
}

/// <summary>Per-tick driver that mirrors the facade's call order against a bare director.</summary>
internal static class MacroDrive
{
	public static PressureDecision TickOne( PressureDirector director, EffectiveConfig cfg, WorldBuilder builder, List<TelemetryEvent> telemetry )
	{
		var world = builder.Build();
		var ctx = new TickContext( world.TickIndex, world.DeltaTimeSeconds );
		director.ApplyOpportunityResults( ctx, cfg, world.Acknowledgements, telemetry );
		director.ApplyDirectives( ctx, cfg, world.Directives, telemetry );
		return director.Tick( ctx, world, cfg, telemetry );
	}

	/// <summary>Runs <paramref name="count"/> ticks from <paramref name="worlds"/>; returns decisions per tick.</summary>
	public static List<PressureDecision> Run( PressureDirector director, EffectiveConfig cfg, Func<long, WorldBuilder> worlds, long firstTick, int count, List<TelemetryEvent> telemetry )
	{
		var decisions = new List<PressureDecision>( count );
		for ( int i = 0; i < count; i++ )
			decisions.Add( TickOne( director, cfg, worlds( firstTick + i ), telemetry ) );
		return decisions;
	}

	public static List<string> Codes( List<TelemetryEvent> telemetry )
	{
		var codes = new List<string>( telemetry.Count );
		foreach ( var e in telemetry )
			codes.Add( e.Code );
		return codes;
	}

	public static int CountCode( List<TelemetryEvent> telemetry, string code )
	{
		int n = 0;
		foreach ( var e in telemetry )
		{
			if ( e.Code == code )
				n++;
		}
		return n;
	}
}

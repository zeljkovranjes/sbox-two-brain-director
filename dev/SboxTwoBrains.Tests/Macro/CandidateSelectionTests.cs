using System.Collections.Generic;
using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Macro;

/// <summary>Candidate region selection: exclusion margins, hysteresis, ingress hints, urgency.</summary>
public sealed class CandidateSelectionTests
{
	private static PressureDecision RunToOffer( PressureDirector director, SboxTwoBrains.EffectiveConfig cfg, System.Func<long, WorldBuilder> worlds, List<TelemetryEvent> telemetry )
	{
		var decisions = MacroDrive.Run( director, cfg, worlds, 0, 3, telemetry );
		var offer = decisions[2];
		Assert.NotNull( offer );
		Assert.Equal( "mode_aggressive_start", offer.ReasonCode );
		return offer;
	}

	[Fact]
	public void Exclusion_FirstStalkMargin_ExcludesNearRegion()
	{
		var cfg = MacroConfigs.Fast(); // first margin 8m
		var director = new PressureDirector( new DeterministicRng( 71 ) );
		var telemetry = new List<TelemetryEvent>();

		var offer = RunToOffer( director, cfg, t => WorldBuilder.At( t, 0.25 )
			.Monster( 0, 0 )
			.Target( "ta", "ra", 12.0, 0.0 ) // 12 <= 5 + 8 → excluded
			.Target( "tb", "rb", 20.0, 0.0 ) // 20 > 13 → viable
			.Zone( "z1", ExclusionKind.Target, 0.0, 0.0, 5.0 ), telemetry );

		Assert.Equal( "rb", offer.CandidateRegionId );
		Assert.Equal( new string[] { "z1" }, offer.ExclusionConstraints );
	}

	[Fact]
	public void Exclusion_SubsequentStalkMargin_AllowsSameRegion()
	{
		var cfg = MacroConfigs.Fast(); // subsequent margin 0m
		var director = new PressureDirector( new DeterministicRng( 72 ) );
		var telemetry = new List<TelemetryEvent>();
		director.State.CompletedOpportunities = 1;

		var offer = RunToOffer( director, cfg, t => WorldBuilder.At( t, 0.25 )
			.Monster( 0, 0 )
			.Target( "ta", "ra", 12.0, 0.0 ) // 12 > 5 + 0 → not excluded, nearest
			.Target( "tb", "rb", 20.0, 0.0 )
			.Zone( "z1", ExclusionKind.Target, 0.0, 0.0, 5.0 ), telemetry );

		Assert.Equal( "ra", offer.CandidateRegionId );
		Assert.Empty( offer.ExclusionConstraints );
	}

	[Fact]
	public void Exclusion_AllExcluded_PicksNearestExcluded()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 73 ) );
		var telemetry = new List<TelemetryEvent>();

		var offer = RunToOffer( director, cfg, t => WorldBuilder.At( t, 0.25 )
			.Monster( 0, 0 )
			.Target( "ta", "a", 10.0, 0.0 )
			.Target( "tb", "b", 30.0, 0.0 )
			.Zone( "z1", ExclusionKind.Objective, 10.0, 0.0, 20.0 ), telemetry );

		Assert.Equal( "a", offer.CandidateRegionId );
		Assert.Equal( new string[] { "z1" }, offer.ExclusionConstraints );
	}

	[Fact]
	public void Exclusion_CustomKindAndInactiveZones_AreIgnored()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 74 ) );
		var telemetry = new List<TelemetryEvent>();

		var offer = RunToOffer( director, cfg, t => WorldBuilder.At( t, 0.25 )
			.Monster( 0, 0 )
			.Target( "ta", "a", 10.0, 0.0 )
			.Zone( "zc", ExclusionKind.Custom, 10.0, 0.0, 20.0 )
			.Zone( "zi", ExclusionKind.Target, 10.0, 0.0, 20.0, active: false ), telemetry );

		Assert.Equal( "a", offer.CandidateRegionId );
		Assert.Empty( offer.ExclusionConstraints );
	}

	[Fact]
	public void Selection_TieBreaksByOrdinalTargetId()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 75 ) );
		var telemetry = new List<TelemetryEvent>();

		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 0.25 )
			.Monster( 0, 0 )
			.Target( "beta", "rz", 10.0, 0.0 )
			.Target( "alpha", "ry", 10.0, 0.0 ), telemetry );

		Assert.Equal( "ry", director.State.ActiveCandidateId ); // "alpha" < "beta"
	}

	[Fact]
	public void Selection_SkipsInvalidDeadAndIneligibleTargets()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 76 ) );
		var telemetry = new List<TelemetryEvent>();

		var decisions = MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 )
			.Target( "t1", "a", 10.0, 0.0, valid: false )
			.Target( "t2", "b", 10.0, 0.0, alive: false )
			.Target( "t3", "c", 10.0, 0.0, eligible: false ), 0, 5, telemetry );

		Assert.All( decisions, d => Assert.Null( d ) );
		Assert.False( director.State.CandidateLatched );
		Assert.Equal( 0.0, director.State.Progression );
		Assert.Empty( telemetry );
	}

	[Fact]
	public void Hysteresis_KeepsLatchedRegion_WhileViable()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 77 ) );
		var telemetry = new List<TelemetryEvent>();

		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 0.25 ).Monster( 0, 0 ).Target( "ta", "a", 10.0, 0.0 ), telemetry );
		Assert.Equal( "a", director.State.ActiveCandidateId );

		// A nearer target in another region does not steal the latch.
		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 1, 0.25 ).Monster( 0, 0 )
			.Target( "ta", "a", 10.0, 0.0 ).Target( "tb", "b", 1.0, 0.0 ), telemetry );
		Assert.Equal( "a", director.State.ActiveCandidateId );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "candidate_latched" ) );

		// Losing the last viable target clears, then re-latches the new best region same tick.
		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 2, 0.25 ).Monster( 0, 0 ).Target( "tb", "b", 1.0, 0.0 ), telemetry );
		Assert.Equal( "b", director.State.ActiveCandidateId );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "candidate_cleared" ) );
		Assert.Equal( 2, MacroDrive.CountCode( telemetry, "candidate_latched" ) );
	}

	[Fact]
	public void Latch_Persists_WhenEvaluationSkipped()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 78 ) );
		var telemetry = new List<TelemetryEvent>();

		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 0.25 )
			.Monster( 0, 0 ).Target( "ta", "a", 10.0, 0.0 ), telemetry );
		Assert.Equal( "a", director.State.ActiveCandidateId );

		// Monster dead: no evaluation, no clearing (fill keeps running per the gauge rules).
		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 1, 0.25 )
			.Monster( 0, 0, MonsterLifecycle.Dead ).Target( "ta", "a", 10.0, 0.0 ), telemetry );
		Assert.Equal( "a", director.State.ActiveCandidateId );
		Assert.Equal( 0, MacroDrive.CountCode( telemetry, "candidate_cleared" ) );
		Assert.Equal( 0.4375, director.State.Progression );

		// Disabled: still no evaluation; the gauge now decreases.
		director.State.Enabled = false;
		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 2, 0.25 )
			.Monster( 0, 0 ).Target( "ta", "a", 10.0, 0.0 ), telemetry );
		Assert.Equal( "a", director.State.ActiveCandidateId );
		Assert.Equal( 0.1875, director.State.Progression );
	}

	[Fact]
	public void IngressConstraints_FilterBannedCooldownAndUnusable()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 79 ) );
		var telemetry = new List<TelemetryEvent>();
		director.State.IngressBanRemaining["i6"] = 10.0;

		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 0.25 )
			.Monster( 0, 0 )
			.Target( "t1", "atrium", 10.0, 0.0 )
			.Ingress( "i1", "atrium" )
			.Ingress( "i2", "back" )
			.Ingress( "i3", "other" )
			.Ingress( "i4", "atrium", usable: false )
			.Ingress( "i5", "atrium", cooldownUntilTick: 100 )
			.Ingress( "i6", "atrium" )
			.Offstage( "back", new string[] { "i2" }, new string[] { "atrium" } )
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.ForceOpportunity, RegionId = "atrium" } ), telemetry );

		Assert.NotNull( decision );
		Assert.Equal( "atrium", decision.CandidateRegionId );
		Assert.Equal( new string[] { "i1", "i2" }, decision.IngressConstraints );
	}

	[Fact]
	public void IngressConstraints_HostCooldownBoundaryIsInclusive()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 80 ) );
		var telemetry = new List<TelemetryEvent>();

		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 0.25 )
			.Monster( 0, 0 )
			.Target( "t1", "atrium", 10.0, 0.0 )
			.Ingress( "i5", "atrium", cooldownUntilTick: 0 ) // 0 <= tick → usable again
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.ForceOpportunity, RegionId = "atrium" } ), telemetry );

		Assert.Equal( new string[] { "i5" }, decision.IngressConstraints );
	}

	[Fact]
	public void IngressConstraints_Empty_WithoutCandidate()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 81 ) );
		var telemetry = new List<TelemetryEvent>();

		// Reset emits a decision with no candidate latched.
		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 0.25 )
			.Ingress( "i1", "atrium" )
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.ResetPressure, ResetGauge = true } ), telemetry );

		Assert.NotNull( decision );
		Assert.Equal( "", decision.CandidateRegionId );
		Assert.Empty( decision.IngressConstraints );
	}

	[Fact]
	public void IngressBan_AgesOut_AndIsRemoved()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 82 ) );
		var telemetry = new List<TelemetryEvent>();
		director.State.IngressBanRemaining["i1"] = 0.5;

		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 0.25 ), telemetry );
		Assert.True( director.State.IngressBanRemaining.ContainsKey( "i1" ) );
		Assert.Equal( 0.25, director.State.IngressBanRemaining["i1"] );

		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 1, 0.25 ), telemetry );
		Assert.False( director.State.IngressBanRemaining.ContainsKey( "i1" ) );
	}

	[Fact]
	public void Urgency_AggressiveIsOne_NormalHalvedDuringCooldown()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.StartProgression = 0.4;
		cfg.Pressure.CooldownSeconds = 0.5;
		var director = new PressureDirector( new DeterministicRng( 83 ) );
		var telemetry = new List<TelemetryEvent>();

		var decisions = MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ).Target( "t", "a", 10.0, 0.0 ), 0, 4, telemetry );
		Assert.Equal( 1.0, decisions[2].Urgency ); // aggressive
		Assert.Equal( 0.2, decisions[3].Urgency ); // 0.4 * 0.5 during cooldown
	}

	[Fact]
	public void Urgency_NormalWithoutCooldown_EqualsProgression()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.StartProgression = 0.3;
		cfg.Pressure.CooldownSeconds = 0.0;
		var director = new PressureDirector( new DeterministicRng( 84 ) );
		var telemetry = new List<TelemetryEvent>();

		MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ).Target( "t", "a", 10.0, 0.0 ), 0, 3, telemetry );
		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 3, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Ack( "op2-0", ActionStatus.Succeeded ), telemetry );

		// Completion resets to start (0.3); the same tick then refills to 0.3 + 0.7*0.25.
		Assert.Equal( "opportunity_completed", decision.ReasonCode );
		Assert.Equal( 0.0, director.State.CooldownRemaining );
		Assert.Equal( 0.475, decision.Progression );
		Assert.Equal( decision.Progression, decision.Urgency );
	}
}

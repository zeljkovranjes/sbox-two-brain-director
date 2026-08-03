using System.Collections.Generic;
using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Macro;

/// <summary>Script directives: mode/progression overrides, resets, forced opportunities.</summary>
public sealed class DirectiveTests
{
	[Fact]
	public void SetPressureMode_OverridesModeAndProgression_WithoutDecision()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 41 ) );
		var telemetry = new List<TelemetryEvent>();

		// Reach aggressive naturally first (offer op2-0 at tick 2).
		MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ).Target( "t", "a", 10.0, 0.0 ), 0, 3, telemetry );
		Assert.Equal( PressureMode.Aggressive, director.State.Mode );

		// Directive overrides to Normal at 0.3; the tick then fills (0.3 + 0.7*0.25).
		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 3, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.SetPressureMode, Mode = PressureMode.Normal, Progression = 0.3 } ),
			telemetry );

		Assert.Null( decision ); // overrides are telemetry-only, not macro decisions
		Assert.Equal( PressureMode.Normal, director.State.Mode );
		Assert.Equal( 0.475, director.State.Progression );
		Assert.Equal( "op2-0", director.State.PendingOpportunityId ); // directive does not clear the offer
		Assert.Equal( 3, director.State.LastTransitionTick );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "script_set_mode" ) );
	}

	[Fact]
	public void SetPressureMode_WithResetGauge_ResetsAggressive()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.SweepDurationSeconds = 0.5; // directive-set sweep is aged by the same tick
		var director = new PressureDirector( new DeterministicRng( 42 ) );
		var telemetry = new List<TelemetryEvent>();

		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 3, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.SetPressureMode, Mode = PressureMode.Aggressive, Progression = 0.9, ResetGauge = true } ),
			telemetry );

		var s = director.State;
		Assert.Equal( PressureMode.Aggressive, s.Mode );
		Assert.Equal( 1.0, s.Progression ); // reset overrides the directive progression
		Assert.Equal( "op3-0", s.PendingOpportunityId );
		Assert.Equal( 23, s.OpportunityExpiryTick );
		Assert.Equal( 0.25, s.SweepSecondsRemaining ); // 0.5 set by the directive, aged one tick
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "reset" ) );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "script_set_mode" ) );
		Assert.NotNull( decision );
		Assert.Equal( "reset", decision.ReasonCode );
		Assert.Equal( "op3-0", decision.OpportunityId );
	}

	[Fact]
	public void SetProgression_ClampsToUnitRange()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.DecreaseSeconds = 600.0; // isolate the override from decrease
		var director = new PressureDirector( new DeterministicRng( 43 ) );
		var telemetry = new List<TelemetryEvent>();

		// No targets: nothing latches; the gauge only decreases (by 0.25/600 per tick).
		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 0.25 )
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.SetProgression, Progression = 2.5 } ), telemetry );
		Assert.Equal( 1.0 - 0.25 / 600.0, director.State.Progression );

		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 1, 0.25 )
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.SetProgression, Progression = 0.3 } ), telemetry );
		Assert.Equal( 0.3 - 0.25 / 600.0, director.State.Progression );

		MacroDrive.TickOne( director, cfg, WorldBuilder.At( 2, 0.25 )
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.SetProgression, Progression = -0.5 } ), telemetry );
		Assert.Equal( 0.0, director.State.Progression );

		Assert.Equal( 3, MacroDrive.CountCode( telemetry, "script_set_progression" ) );
	}

	[Fact]
	public void ResetPressure_Normal_ClearsCycleState()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 44 ) );
		var telemetry = new List<TelemetryEvent>();

		// Build some state (latch + partial gauge), then reset with no target to avoid re-latch.
		MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ).Target( "t", "a", 10.0, 0.0 ), 0, 3, telemetry );
		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 3, 0.25 )
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.ResetPressure, ResetGauge = false } ), telemetry );

		var s = director.State;
		Assert.Equal( PressureMode.Normal, s.Mode );
		Assert.Equal( 0.0, s.Progression );
		Assert.Equal( 0, s.CompletedOpportunities );
		Assert.False( s.CandidateLatched );
		Assert.Equal( "", s.ActiveCandidateId );
		Assert.Equal( "", s.PendingOpportunityId );
		Assert.Equal( 0.0, s.CooldownRemaining );
		Assert.Equal( 3, s.LastTransitionTick );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "reset" ) );
		Assert.NotNull( decision );
		Assert.Equal( "reset", decision.ReasonCode );
		Assert.Equal( "", decision.OpportunityId );
		Assert.Equal( 0.0, decision.Urgency );
	}

	[Fact]
	public void ResetPressure_Aggressive_StartsFreshOpportunity()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.SweepDurationSeconds = 0.5; // directive-set sweep is aged by the same tick
		var director = new PressureDirector( new DeterministicRng( 45 ) );
		var telemetry = new List<TelemetryEvent>();

		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 3, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.ResetPressure, ResetGauge = true } ), telemetry );

		var s = director.State;
		Assert.Equal( PressureMode.Aggressive, s.Mode );
		Assert.Equal( 1.0, s.Progression );
		Assert.Equal( "op3-0", s.PendingOpportunityId );
		Assert.Equal( 23, s.OpportunityExpiryTick );
		Assert.Equal( 0.25, s.SweepSecondsRemaining ); // 0.5 set by the directive, aged one tick
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "reset" ) );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "mode_aggressive_start" ) );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "opportunity_offered" ) );
		Assert.NotNull( decision );
		Assert.Equal( "reset", decision.ReasonCode );
		Assert.Equal( "op3-0", decision.OpportunityId );
		Assert.Equal( 1.0, decision.Urgency );

		// The fresh aggressive cycle completes when the sweep ends.
		var completion = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 4, 0.25 ), telemetry );
		Assert.Equal( "opportunity_completed", completion.ReasonCode );
		Assert.Equal( 1, s.CompletedOpportunities );
	}

	[Fact]
	public void ResetToStart_DirectCall_RerollsQuotaTarget()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.EventQuotaMin = 2;
		cfg.Pressure.EventQuotaMax = 4;
		var director = new PressureDirector( new DeterministicRng( 46 ) );
		var telemetry = new List<TelemetryEvent>();

		director.State.CompletedOpportunities = 3;
		director.ResetToStart( new TickContext( 10, 0.25 ), cfg, false, telemetry );
		Assert.Equal( 0, director.State.CompletedOpportunities );
		Assert.InRange( director.State.EventQuotaTarget, 2, 4 );
		Assert.Equal( 10, director.State.LastTransitionTick );

		telemetry.Clear();
		director.ResetToStart( new TickContext( 11, 0.25 ), cfg, true, telemetry );
		Assert.Equal( PressureMode.Aggressive, director.State.Mode );
		Assert.Equal( "op11-0", director.State.PendingOpportunityId );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "reset" ) );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "opportunity_offered" ) );
	}

	[Fact]
	public void ForceOpportunity_ForcesTransition_RegardlessOfGauge()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 47 ) );
		var telemetry = new List<TelemetryEvent>();

		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.ForceOpportunity } ), telemetry );

		var s = director.State;
		Assert.Equal( PressureMode.Aggressive, s.Mode );
		Assert.Equal( 0.0, s.Progression ); // gauge untouched by the force
		Assert.Equal( "op0-0", s.PendingOpportunityId );
		Assert.Equal( 20, s.OpportunityExpiryTick );
		Assert.Equal( "a", s.ActiveCandidateId );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "script_forced_opportunity" ) );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "opportunity_offered" ) );
		Assert.NotNull( decision );
		Assert.Equal( "script_forced_opportunity", decision.ReasonCode );
		Assert.Equal( "a", decision.CandidateRegionId );
	}

	[Fact]
	public void ForceOpportunity_UsesPreferredRegion()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 48 ) );
		var telemetry = new List<TelemetryEvent>();

		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 0.25 )
			.Target( "ta", "a", 10.0, 0.0 )
			.Target( "tb", "b", 20.0, 0.0 )
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.ForceOpportunity, RegionId = "b" } ), telemetry );

		Assert.Equal( "b", director.State.ActiveCandidateId );
		Assert.Equal( "b", decision.CandidateRegionId );
	}

	[Fact]
	public void ForceOpportunity_UnknownRegion_IsNoOp()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 49 ) );
		var telemetry = new List<TelemetryEvent>();

		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.ForceOpportunity, RegionId = "nowhere" } ), telemetry );

		Assert.Null( decision );
		Assert.Equal( PressureMode.Normal, director.State.Mode );
		Assert.Equal( 0, MacroDrive.CountCode( telemetry, "script_forced_opportunity" ) );
	}

	[Fact]
	public void ForceOpportunity_RespectsEnabledFalse()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 50 ) );
		var telemetry = new List<TelemetryEvent>();
		director.State.Enabled = false;

		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.ForceOpportunity } ), telemetry );

		Assert.Null( decision );
		Assert.Equal( PressureMode.Normal, director.State.Mode );
		Assert.Equal( "", director.State.PendingOpportunityId );
		Assert.Empty( telemetry );
	}

	[Fact]
	public void ForceOpportunity_IgnoresCooldownAndQuota()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 51 ) );
		var telemetry = new List<TelemetryEvent>();
		director.State.CompletedOpportunities = 4; // == MaxOpportunities
		director.State.CooldownRemaining = 5.0;

		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.ForceOpportunity } ), telemetry );

		Assert.NotNull( decision );
		Assert.Equal( "script_forced_opportunity", decision.ReasonCode );
		Assert.Equal( PressureMode.Aggressive, director.State.Mode );
		Assert.Equal( 4, director.State.CompletedOpportunities ); // count only grows on completion
		Assert.Equal( 0, MacroDrive.CountCode( telemetry, "quota_blocked" ) );
	}

	[Fact]
	public void ForceOpportunity_NoViableCandidate_IsNoOp()
	{
		var cfg = MacroConfigs.Fast();
		var director = new PressureDirector( new DeterministicRng( 52 ) );
		var telemetry = new List<TelemetryEvent>();

		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 0, 0.25 )
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.ForceOpportunity } ), telemetry );

		Assert.Null( decision );
		Assert.Equal( PressureMode.Normal, director.State.Mode );
		Assert.Empty( telemetry );
	}

	[Fact]
	public void ForceOpportunity_WhileAggressive_IsNoOp()
	{
		var cfg = MacroConfigs.Fast();
		cfg.Pressure.SweepDurationSeconds = 50.0; // no natural completion in the window
		var director = new PressureDirector( new DeterministicRng( 53 ) );
		var telemetry = new List<TelemetryEvent>();

		MacroDrive.Run( director, cfg, t => WorldBuilder.At( t, 0.25 ).Target( "t", "a", 10.0, 0.0 ), 0, 3, telemetry );
		Assert.Equal( "op2-0", director.State.PendingOpportunityId );

		var decision = MacroDrive.TickOne( director, cfg, WorldBuilder.At( 3, 0.25 )
			.Target( "t", "a", 10.0, 0.0 )
			.Directive( new ScriptDirective { Kind = ScriptDirectiveKind.ForceOpportunity } ), telemetry );

		Assert.Null( decision );
		Assert.Equal( "op2-0", director.State.PendingOpportunityId );
		Assert.Equal( 1, MacroDrive.CountCode( telemetry, "opportunity_offered" ) );
	}
}

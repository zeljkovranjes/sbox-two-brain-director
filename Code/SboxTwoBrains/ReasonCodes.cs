namespace SboxTwoBrains;

/// <summary>
/// Stable machine-readable telemetry reason codes emitted by the micro agent. Every module
/// transition, sense-gate edge, acknowledgement outcome and feasibility refusal uses one of
/// these codes so hosts, overlays and replay tooling can rely on them.
/// </summary>
internal static class ReasonCodes
{
	// pending-action lifecycle and host acknowledgements (category "action")
	internal const string ActionLapsed = "action_lapsed";
	internal const string AckUnknown = "ack_unknown";
	internal const string ActionPartial = "action_partial";
	internal const string ActionRejected = "action_rejected";
	internal const string ActionFailed = "action_failed";
	internal const string ActionInterrupted = "action_interrupted";
	internal const string Preempt = "preempt";
	internal const string ActionInfeasible = "action_infeasible";

	// perception (category "perception")
	internal const string OmniscienceActive = "omniscience_active";

	// modules and directives (category "micro")
	internal const string LifecycleInactive = "lifecycle_inactive";
	internal const string NavRecovery = "nav_recovery";
	internal const string DespawnRequested = "despawn_requested";
	internal const string ScriptSequence = "script_sequence";
	internal const string ScriptWithdrawal = "script_withdrawal";
	internal const string Stagger = "stagger";
	internal const string RetreatStart = "retreat_start";
	internal const string Hesitate = "hesitate";
	internal const string Flank = "flank";
	internal const string ThreatTimeout = "threat_timeout";
	internal const string AmbushStart = "ambush_start";
	internal const string AmbushTimeout = "ambush_timeout";
	internal const string AttackCommit = "attack_commit";
	internal const string Chase = "chase";
	internal const string ChaseLost = "chase_lost";
	internal const string SuspectResponse = "suspect_response";
	internal const string HidingTarget = "hiding_target";
	internal const string InvestigateReact = "investigate_react";
	internal const string InvestigateApproach = "investigate_approach";
	internal const string InvestigateInspect = "investigate_inspect";
	internal const string InvestigateDone = "investigate_done";
	internal const string InvestigateReset = "investigate_reset";
	internal const string Search = "search";
	internal const string SearchStart = "search_start";
	internal const string SearchEnd = "search_end";
	internal const string Stalk = "stalk";
	internal const string IngressUse = "ingress_use";
	internal const string SweepMove = "sweep_move";
	internal const string SweepDwell = "sweep_dwell";
	internal const string SweepEnd = "sweep_end";
	internal const string Idle = "idle";
	internal const string MicroReset = "micro_reset";
	internal const string ModuleUnknown = "module_unknown";
}

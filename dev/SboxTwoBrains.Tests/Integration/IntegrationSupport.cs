using System.Collections.Generic;
using SboxTwoBrains;
using Host = SboxTwoBrains.Tests.FakeHost.FakeHost;

namespace SboxTwoBrains.Tests.Integration;

/// <summary>
/// Shared builders and probes for the integration lane: a TwoBrainsSystem on the preset
/// catalogue's DEFAULT profile driven through the fake host, planar nav-node
/// helpers, directive shortcuts and telemetry/action scanners used by every scenario.
/// All scenario timing expectations come from the resolved DEFAULT profile at runtime,
/// never from hardcoded duplicates of config values.
/// </summary>
internal static class IntegrationSupport
{
	public const ulong Seed = 42;
	public const double Dt = 1.0 / 20.0;

	/// <summary>Creates a DEFAULT-preset system (fixed seed) plus its fake host.</summary>
	public static Host NewHost( double dt = Dt, ulong seed = Seed )
	{
		var catalogue = AlienIsolationPresets.CreateCatalogue();
		var system = new TwoBrainsSystem( catalogue, "DEFAULT", seed );
		var host = new Host( system ) { DeltaTime = dt };
		host.MonsterPosition = Vec3.Zero;
		host.MonsterRegionId = "atrium";
		return host;
	}

	/// <summary>
	/// Adds a nav node with unknown route distance (&lt; 0), so the core falls back to
	/// planar distances — the fake world has no routing engine to keep RouteDistance fresh.
	/// </summary>
	public static NavCandidate AddPlanarNode( Host host, string nodeId, double x, double z, string regionId, bool reachable = true, NavCandidateKind kind = NavCandidateKind.FrontstageNode )
	{
		var node = new NavCandidate { NodeId = nodeId, Position = new Vec3( x, 0.0, z ), RegionId = regionId, Reachable = reachable, Kind = kind, RouteDistance = -1.0 };
		host.NavCandidates.Add( node );
		return node;
	}

	public static void Direct( Host host, ScriptDirective directive ) => host.PendingDirectives.Add( directive );

	public static ScriptDirective ForceOpportunity( string regionId )
		=> new ScriptDirective { Kind = ScriptDirectiveKind.ForceOpportunity, RegionId = regionId };

	/// <summary>Every telemetry event across the given batches, in emission order.</summary>
	public static List<TelemetryEvent> AllTelemetry( IEnumerable<DecisionBatch> batches )
	{
		var all = new List<TelemetryEvent>();
		foreach ( var batch in batches )
			all.AddRange( batch.Telemetry );
		return all;
	}

	/// <summary>Every action request across the given batches, in emission order.</summary>
	public static List<ActionRequest> AllActions( IEnumerable<DecisionBatch> batches )
	{
		var all = new List<ActionRequest>();
		foreach ( var batch in batches )
			all.AddRange( batch.Actions );
		return all;
	}

	public static bool HasCode( IEnumerable<DecisionBatch> batches, string code )
	{
		foreach ( var batch in batches )
			for ( int i = 0; i < batch.Telemetry.Count; i++ )
				if ( batch.Telemetry[i].Code == code )
					return true;
		return false;
	}

	public static int CountCode( IEnumerable<DecisionBatch> batches, string code )
	{
		int n = 0;
		foreach ( var batch in batches )
			for ( int i = 0; i < batch.Telemetry.Count; i++ )
				if ( batch.Telemetry[i].Code == code )
					n++;
		return n;
	}

	/// <summary>Tick of the first batch carrying the telemetry code; -1 when absent.</summary>
	public static long FirstTickOfCode( IEnumerable<DecisionBatch> batches, string code )
	{
		foreach ( var batch in batches )
			for ( int i = 0; i < batch.Telemetry.Count; i++ )
				if ( batch.Telemetry[i].Code == code )
					return batch.TickIndex;
		return -1;
	}

	/// <summary>Tick of the first batch carrying a macro decision; -1 when none.</summary>
	public static long FirstMacroDecisionTick( List<DecisionBatch> batches )
	{
		for ( int i = 0; i < batches.Count; i++ )
			if ( batches[i].Macro != null )
				return batches[i].TickIndex;
		return -1;
	}

	public static int CountActions( IEnumerable<DecisionBatch> batches, ActionKind kind )
	{
		int n = 0;
		foreach ( var batch in batches )
			for ( int i = 0; i < batch.Actions.Count; i++ )
				if ( batch.Actions[i].Kind == kind )
					n++;
		return n;
	}

	/// <summary>Tick of the first action of a kind; -1 when absent.</summary>
	public static long FirstActionTick( IEnumerable<DecisionBatch> batches, ActionKind kind )
	{
		foreach ( var batch in batches )
			for ( int i = 0; i < batch.Actions.Count; i++ )
				if ( batch.Actions[i].Kind == kind )
					return batch.TickIndex;
		return -1;
	}
}

#nullable enable annotations

// Editor-process compile gate for the two-brain director library.
//
// This only runs when the TB_GATE_RESULT environment variable is set (done by
// dev/editor-rig/run_editor_gate.ps1). Normal users of the library never trigger it.
//
// What it does (inside a real sbox-dev.exe editor session):
//   1. waits for the project + asset system to be ready
//   2. verifies the open project is the tb-editor-rig scratch project (anti-leak safety)
//   3. confirms the library's package assembly compiled and its core type is loadable
//   4. scans the editor log for compile errors / SB500 whitelist violations mentioning the library
//   5. writes a JSON result file to TB_GATE_RESULT and quits the editor
//
// Adapted from humanoid-retargeter's verified M0 gate (same arming-marker pattern).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Editor;
using Sandbox;

namespace SboxTwoBrains.EditorGate;

public static class CompileGate
{
	static bool _started;
	static readonly GateResult Result = new();
	static string _resultPath;

	[EditorEvent.Frame]
	public static void Tick()
	{
		if ( _started )
			return;

		_started = true;

		_resultPath = Environment.GetEnvironmentVariable( "TB_GATE_RESULT" );
		if ( string.IsNullOrWhiteSpace( _resultPath ) )
			return; // not a gate run - do nothing, ever

		// One-shot arming marker, written by the driver immediately before launch and
		// CONSUMED here. The env var alone must never arm the gate: a gate run that boots
		// Steam as its child leaks TB_GATE_RESULT into Steam's environment, and every editor
		// launched through that Steam afterwards inherits it.
		var marker = _resultPath + ".arm";
		try
		{
			if ( !File.Exists( marker ) )
			{
				Log.Info( "[tb-gate] TB_GATE_RESULT is set but there is no arming marker - leaked env var, ignoring" );
				return;
			}
			File.Delete( marker );
		}
		catch
		{
			return; // cannot verify/consume the marker - err on never running
		}

		_ = RunAsync();
	}

	static async Task RunAsync()
	{
		Note( "compile gate starting" );
		Result.engineBooted = true;
		Flush();

		try
		{
			await RunGateAsync();
		}
		catch ( Exception e )
		{
			Note( $"EXCEPTION: {e}" );
		}

		Result.completed = true;
		Result.passed = Result.libraryAssemblyFound && Result.coreTypeFound && Result.compileErrors.Count == 0;
		Flush();
		Note( $"compile gate finished, passed={Result.passed}" );

		if ( Result.refusedWrongProject )
			return;

		await Task.Delay( 1000 );
		try
		{
			EditorUtility.Quit( true );
		}
		catch ( Exception e )
		{
			Note( $"EditorUtility.Quit threw: {e.Message}" );
			Flush();
		}

		await Task.Delay( 10_000 );
		Environment.Exit( Result.passed ? 0 : 1 );
	}

	static async Task RunGateAsync()
	{
		// ---- 1. wait for project + asset system ----------------------------------
		Result.assetSystemReady = await WaitUntil(
			() => Project.Current is not null && AssetSystem.All.Any(),
			timeoutSeconds: 120 );
		Note( $"assetSystemReady={Result.assetSystemReady}" );
		Flush();

		if ( !Result.assetSystemReady )
			return;

		Result.projectPath = Project.Current.GetRootPath();

		// Never touch a real session: only the tb-editor-rig scratch project is fair game.
		if ( (Result.projectPath ?? "").IndexOf( "tb-editor-rig", StringComparison.OrdinalIgnoreCase ) < 0 )
		{
			Result.refusedWrongProject = true;
			Note( $"REFUSING to run: open project '{Result.projectPath}' is not the tb-editor-rig scratch (leaked TB_GATE_RESULT?) - aborting" );
			Flush();
			return;
		}

		// ---- 2. let the managed compiler settle ----------------------------------
		// The editor compiles project + package code on load; give it time to finish.
		await Task.Delay( 8000 );

		// ---- 3. library assembly + core type --------------------------------------
		var assemblies = AppDomain.CurrentDomain.GetAssemblies();
		var libAssembly = assemblies.FirstOrDefault( a =>
		{
			var n = a.GetName().Name ?? "";
			return n.IndexOf( "two_brain_director", StringComparison.OrdinalIgnoreCase ) >= 0;
		} );
		Result.libraryAssemblyFound = libAssembly is not null;
		Result.libraryAssemblyName = libAssembly?.GetName().Name ?? "";
		Note( $"libraryAssemblyFound={Result.libraryAssemblyFound} name={Result.libraryAssemblyName}" );
		Flush();

		if ( libAssembly is not null )
		{
			var coreType = libAssembly.GetTypes().FirstOrDefault( t => t.Name == "TwoBrainsSystem" );
			Result.coreTypeFound = coreType is not null;
			Result.coreTypeName = coreType?.FullName ?? "";
			Note( $"coreTypeFound={Result.coreTypeFound} type={Result.coreTypeName}" );
		}
		else
		{
			Note( "loaded assemblies: " + string.Join( ", ", assemblies.Select( a => a.GetName().Name ).Where( n => n is not null ).OrderBy( n => n ).Take( 60 ) ) );
		}
		Flush();

		// ---- 4. log scan for compile errors / whitelist violations ----------------
		try
		{
			var logPath = Path.Combine( Environment.CurrentDirectory, "logs", "sbox-dev.log" );
			if ( File.Exists( logPath ) )
			{
				using var fs = new FileStream( logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite );
				using var sr = new StreamReader( fs );
				var text = sr.ReadToEnd();
				foreach ( var line in text.Split( '\n' ) )
				{
					var l = line.Trim();
					if ( l.Length == 0 ) continue;
					bool mentionsLibrary = l.IndexOf( "two_brain_director", StringComparison.OrdinalIgnoreCase ) >= 0
						|| l.IndexOf( "twobrains", StringComparison.OrdinalIgnoreCase ) >= 0;
					bool isError = l.IndexOf( "error", StringComparison.OrdinalIgnoreCase ) >= 0;
					bool isWhitelist = l.IndexOf( "SB500", StringComparison.OrdinalIgnoreCase ) >= 0;
					if ( (isError && mentionsLibrary) || isWhitelist )
						Result.compileErrors.Add( l.Length > 300 ? l.Substring( 0, 300 ) : l );
				}
			}
			Result.logScanned = true;
		}
		catch ( Exception e )
		{
			Note( $"log scan failed: {e.Message}" );
		}

		Note( $"compileErrors={Result.compileErrors.Count}" );
		foreach ( var err in Result.compileErrors.Take( 10 ) )
			Note( "ERR: " + err );
		Flush();
	}

	// ---- plumbing ---------------------------------------------------------------

	static async Task<bool> WaitUntil( Func<bool> condition, float timeoutSeconds )
	{
		var sw = Stopwatch.StartNew();
		while ( sw.Elapsed.TotalSeconds < timeoutSeconds )
		{
			bool ok = false;
			try { ok = condition(); }
			catch { /* not ready yet */ }

			if ( ok )
				return true;

			await Task.Delay( 250 );
		}

		return false;
	}

	static void Note( string message )
	{
		Result.log.Add( $"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}" );
		Log.Info( $"[tb-gate] {message}" );
	}

	static void Flush()
	{
		try
		{
			File.WriteAllText( _resultPath, JsonSerializer.Serialize( Result,
				new JsonSerializerOptions { WriteIndented = true } ) );
		}
		catch
		{
			// never let result IO take the editor down
		}
	}

	class GateResult
	{
		public bool engineBooted { get; set; }
		public bool assetSystemReady { get; set; }
		public string projectPath { get; set; }
		public bool libraryAssemblyFound { get; set; }
		public string libraryAssemblyName { get; set; }
		public bool coreTypeFound { get; set; }
		public string coreTypeName { get; set; }
		public bool logScanned { get; set; }
		public List<string> compileErrors { get; set; } = new();
		public bool refusedWrongProject { get; set; }
		public bool completed { get; set; }
		public bool passed { get; set; }
		public List<string> log { get; set; } = new();
	}
}

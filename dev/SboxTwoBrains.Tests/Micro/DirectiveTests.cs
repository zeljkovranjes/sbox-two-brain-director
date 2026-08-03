using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Micro;

/// <summary>Script directives: forced withdrawal, scripted sequences, despawn.</summary>
public sealed class DirectiveTests
{
	[Fact]
	public void ForceWithdrawalTriggersRetreat()
	{
		var d = new AgentDriver();
		d.World.NavCandidates.Add( Snap.Node( "n_far", "R0", 50.0, 0.0 ) );
		d.Direct( new ScriptDirective { DirectiveId = "d1", Kind = ScriptDirectiveKind.ForceWithdrawal } );
		var actions = d.StepOnce();
		Assert.True( d.Tele( "script_withdrawal" ) );
		var action = Assert.Single( actions );
		Assert.Equal( ActionKind.Retreat, action.Kind );
		Assert.True( d.Tele( "retreat_start" ) );
		Assert.Contains( "retreat", d.State.Motivations );
	}

	[Fact]
	public void ScriptedSequenceRunsUntilSucceeded()
	{
		var d = new AgentDriver();
		d.Direct( new ScriptDirective { DirectiveId = "d1", Kind = ScriptDirectiveKind.PlayScriptedSequence, SequenceName = "intro" } );
		var actions = d.StepOnce(); // tick 0
		Assert.True( d.Tele( "script_sequence" ) );
		var action = Assert.Single( actions );
		Assert.Equal( ActionKind.Scripted, action.Kind );
		Assert.Equal( "intro", action.Param );
		Assert.Equal( "intro", d.State.ActiveScriptedSequence );

		d.AckLast( ActionStatus.Succeeded );
		var next = d.StepOnce(); // tick 1: cleared, back to normal
		Assert.Equal( "", d.State.ActiveScriptedSequence );
		Assert.Equal( ActionKind.Wait, Assert.Single( next ).Kind );
	}

	[Fact]
	public void DespawnBlocksAllFurtherActions()
	{
		var d = new AgentDriver();
		d.Direct( new ScriptDirective { DirectiveId = "d1", Kind = ScriptDirectiveKind.Despawn } );
		var first = d.StepOnce();
		Assert.Empty( first );
		Assert.True( d.Tele( "despawn_requested" ) );
		Assert.Contains( "despawn_requested", d.State.Flags );
		Assert.Empty( d.Step( 3 ) ); // stays quiescent until the host despawns it
	}
}

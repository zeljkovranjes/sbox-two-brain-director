using SboxTwoBrains;
using Xunit;

namespace SboxTwoBrains.Tests.Micro;

/// <summary>Investigate module: staged react → approach → inspect → hand-off machine.</summary>
public sealed class InvestigateTests
{
	private static AgentDriver InvestigateDriver( SboxTwoBrains.EffectiveConfig cfg = null )
	{
		var d = new AgentDriver( 1, cfg );
		d.World.CurrentStimuli.Add( Snap.Stim( "st1", SenseChannel.Auditory, 0.9, x: 20.0, region: "R2" ) );
		d.World.NavCandidates.Add( Snap.Node( "n1", "R2", 20.0, 0.0, route: 8.0 ) );
		return d;
	}

	[Fact]
	public void StagesProgressInOrder()
	{
		var d = InvestigateDriver();
		var react = Assert.Single( d.StepOnce() ); // tick 0: react
		Assert.Equal( ActionKind.Wait, react.Kind );
		Assert.Equal( 0.0, react.SpeedScale );
		Assert.Equal( "0.5", react.Param );
		Assert.True( d.Tele( "investigate_react" ) );
		Assert.Equal( 1, d.State.InvestigationStage );
		Assert.Equal( "st1", d.State.InvestigationStimulusId );

		var approach = Assert.Single( d.StepAck() ); // tick 1: approach
		Assert.Equal( ActionKind.MoveTo, approach.Kind );
		Assert.Equal( new Vec3( 20.0, 0.0, 0.0 ), approach.Destination );
		Assert.Equal( d.Cfg.Movement.SpeedSlow, approach.SpeedScale );
		Assert.True( d.Tele( "investigate_approach" ) );
		Assert.Equal( 2, d.State.InvestigationStage );

		var inspect = Assert.Single( d.StepAck() ); // tick 2: inspect
		Assert.Equal( ActionKind.Wait, inspect.Kind );
		Assert.Equal( "2", inspect.Param );
		Assert.True( d.Tele( "investigate_inspect" ) );
		Assert.Equal( 3, d.State.InvestigationStage );

		var handoff = Assert.Single( d.StepAck() ); // tick 3: hand off to search
		Assert.True( d.Tele( "investigate_done" ) );
		Assert.Equal( 0, d.State.InvestigationStage );
		Assert.Equal( "", d.State.InvestigationStimulusId );
		Assert.Equal( ActionKind.Search, handoff.Kind );
	}

	[Fact]
	public void LosingTheStimulusMidStageResetsTheMachine()
	{
		var cfg = new SboxTwoBrains.EffectiveConfig();
		cfg.Perception.Auditory.MaxAgeSeconds = 1.0;
		var d = InvestigateDriver( cfg );
		d.StepOnce(); // tick 0: react
		d.World.CurrentStimuli.Clear();
		d.StepAck(); // tick 1: approach issued (never acked)
		Assert.Equal( 2, d.State.InvestigationStage );
		d.Step( 14 ); // memory ages out at tick 11 (age > 1.0 s)
		Assert.True( d.Hist( "investigate_reset" ) );
		Assert.Equal( 0, d.State.InvestigationStage );
		Assert.Equal( "", d.State.InvestigationStimulusId );
	}

	[Fact]
	public void BelowThresholdStimulusIsIgnored()
	{
		var d = new AgentDriver();
		d.World.CurrentStimuli.Add( Snap.Stim( "st1", SenseChannel.Auditory, 0.2, x: 20.0, region: "R2" ) ); // threshold 0.3
		var action = Assert.Single( d.StepOnce() );
		Assert.Equal( ActionKind.Wait, action.Kind );
		Assert.False( d.Hist( "investigate_react" ) );
	}
}

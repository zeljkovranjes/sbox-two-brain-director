namespace SboxTwoBrains;

/// <summary>
/// Sensory channel a stimulus arrives on. Game-specific senses use
/// <see cref="GameDefined"/> plus <see cref="Stimulus.Subtype"/>.
/// </summary>
public enum SenseChannel
{
	Visual = 0,
	Auditory = 1,
	Touch = 2,
	Damage = 3,
	Light = 4,
	GameDefined = 5,
}

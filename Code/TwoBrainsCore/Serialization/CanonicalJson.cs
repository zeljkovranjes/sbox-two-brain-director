using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TwoBrains.Core.Serialization;

/// <summary>
/// Canonical JSON for decisions and state. Fixed options, no indentation, declaration-order
/// properties, shortest round-trippable doubles (System.Text.Json default on .NET 10).
/// Byte-identical output for identical input is the replay contract; tests enforce it.
/// </summary>
public static class CanonicalJson
{
	public static readonly JsonSerializerOptions Options = CreateOptions();

	private static JsonSerializerOptions CreateOptions()
	{
		var options = new JsonSerializerOptions
		{
			WriteIndented = false,
			DefaultIgnoreCondition = JsonIgnoreCondition.Never,
			NumberHandling = JsonNumberHandling.Strict,
			PropertyNamingPolicy = null,
			Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		};
		options.Converters.Add( new JsonStringEnumConverter() );
		return options;
	}

	public static string ToJson<T>( T value ) => JsonSerializer.Serialize( value, Options );

	public static T FromJson<T>( string json ) => JsonSerializer.Deserialize<T>( json, Options );

	/// <summary>FNV-1a 64-bit over UTF-8 bytes. Used for per-tick state hashes.</summary>
	public static ulong Hash( string text )
	{
		const ulong offset = 14695981039346656037UL;
		const ulong prime = 1099511628211UL;
		ulong hash = offset;
		foreach ( byte b in Encoding.UTF8.GetBytes( text ) )
		{
			hash ^= b;
			hash *= prime;
		}
		return hash;
	}
}

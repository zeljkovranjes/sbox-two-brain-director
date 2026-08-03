using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using SboxTwoBrains;

namespace SboxTwoBrains;

/// <summary>
/// Round-trips <see cref="Vec3"/> through canonical state JSON. The default contract
/// serializer cannot restore Vec3 (a struct with get-only properties is rebuilt through its
/// implicit parameterless constructor, leaving all fields at zero), so micro state would
/// lose remembered/sensed positions across a save. The written shape is byte-identical to
/// the default output ({\"X\":..,\"Y\":..,\"Z\":..} in declaration order), keeping canonical
/// JSON stable; only reading changes.
/// </summary>
internal sealed class Vec3JsonConverter : JsonConverter<Vec3>
{
	public override Vec3 Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		double x = 0.0, y = 0.0, z = 0.0;
		if ( reader.TokenType != JsonTokenType.StartObject )
			throw new JsonException( "Vec3 must be a JSON object." );
		while ( reader.Read() )
		{
			if ( reader.TokenType == JsonTokenType.EndObject )
				return new Vec3( x, y, z );
			if ( reader.TokenType != JsonTokenType.PropertyName )
				throw new JsonException( "Vec3 object expected a property name." );
			string name = reader.GetString();
			if ( !reader.Read() ) throw new JsonException( "Unexpected end of Vec3 object." );
			switch ( name )
			{
				case "X": x = reader.GetDouble(); break;
				case "Y": y = reader.GetDouble(); break;
				case "Z": z = reader.GetDouble(); break;
				default: reader.Skip(); break;
			}
		}
		throw new JsonException( "Unterminated Vec3 object." );
	}

	public override void Write( Utf8JsonWriter writer, Vec3 value, JsonSerializerOptions options )
	{
		writer.WriteStartObject();
		writer.WriteNumber( "X", value.X );
		writer.WriteNumber( "Y", value.Y );
		writer.WriteNumber( "Z", value.Z );
		writer.WriteEndObject();
	}
}

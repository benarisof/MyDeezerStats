using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public class LastFmHistoryResponse
{
    [JsonPropertyName("recenttracks")]
    public RecentTracksWrapper RecentTracks { get; set; } = new RecentTracksWrapper();

}

public class RecentTracksWrapper
{
    [JsonPropertyName("track")]
    public List<LastFmTrack> Tracks { get; set; } = new List<LastFmTrack>();
    [JsonPropertyName("@attr")]
    public RecentTracksAttributes Attributes { get; set; } = new RecentTracksAttributes();
}


public class RecentTracksAttributes
{
    [JsonPropertyName("totalPages")]
    public string TotalPages { get; set; } = string.Empty;
}

public class LastFmTrack
{
    [JsonPropertyName("name")]
    public string Track { get; set; } = string.Empty;

    [JsonPropertyName("artist")]
    [JsonConverter(typeof(ArtistNameConverter))]
    public string Artist { get; set; } = string.Empty;

    [JsonPropertyName("album")]
    [JsonConverter(typeof(AlbumNameConverter))]
    public string Album { get; set; } = String.Empty;

    [JsonPropertyName("date")]
    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime? ListenDate { get; set; }

    [JsonPropertyName("@attr")]
    public NowPlayingAttr? NowPlaying { get; set; }

    [JsonPropertyName("duration")]
    [JsonConverter(typeof(DurationConverter))]
    public string Duration { get; set; } = string.Empty;
}

public class NowPlayingAttr
{
    [JsonPropertyName("nowplaying")]
    public string NowPlaying { get; set; } = string.Empty;
}

// Convertisseur pour la durée
public class DurationConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            // Si la durée est fournie comme nombre (millisecondes)
            if (reader.TryGetInt64(out long milliseconds))
            {
                return milliseconds.ToString();
            }
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            // Si la durée est fournie comme string
            return reader.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

// Convertisseur pour extraire directement le nom de l'artiste
public class ArtistNameConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            if (doc.RootElement.TryGetProperty("#text", out var text))
                return text.GetString() ?? ""; 
            if (doc.RootElement.TryGetProperty("name", out var name))
                return name.GetString() ?? "";
            return string.Empty;
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("#text", value);
        writer.WriteEndObject();
    }
}

// Convertisseur pour extraire directement le titre de l'album
public class AlbumNameConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            if (doc.RootElement.TryGetProperty("#text", out var text))
                return text.GetString() ?? "";
            return string.Empty;
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("#text", value);
        writer.WriteEndObject();
    }
}

// Convertisseur pour les timestamps 
public class UnixTimestampConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            return null;
        }

        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        if (jsonDoc.RootElement.TryGetProperty("uts", out var utsElement))
        {
            if (long.TryParse(utsElement.GetString(), out var unixTime))
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
            }
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStartObject();
            writer.WriteString("uts", ((DateTimeOffset)value.Value).ToUnixTimeSeconds().ToString());
            writer.WriteString("#text", value.Value.ToString("dd MMM yyyy, HH:mm"));
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

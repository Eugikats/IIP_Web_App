using InsuranceIntelligence.Infrastructure.Translation;
using Xunit;

namespace InsuranceIntelligence.Api.Tests;

public sealed class OggOpusVoiceNoteTests
{
    [Fact]
    public void SpeechEngineWav_BecomesAnOggOpusVoiceNote()
    {
        var wav = Wav(sampleRate: 24000, seconds: 1.5);

        var ogg = OggOpusVoiceNote.FromWav(wav);

        Assert.NotNull(ogg);
        Assert.Equal("OggS", System.Text.Encoding.ASCII.GetString(ogg!, 0, 4));
        Assert.Contains("OpusHead", System.Text.Encoding.ASCII.GetString(ogg));
        Assert.True(ogg.Length < wav.Length / 4); // compressed, not a renamed WAV
    }

    [Fact]
    public void AStreamedWav_WithAPlaceholderDataSize_IsStillRead()
    {
        var wav = Wav(sampleRate: 24000, seconds: 0.5);
        BitConverter.GetBytes(-1).CopyTo(wav, 40); // data chunk size unknown while streaming

        Assert.NotNull(OggOpusVoiceNote.FromWav(wav));
    }

    [Fact]
    public void NotAWav_GivesNothing() =>
        Assert.Null(OggOpusVoiceNote.FromWav(System.Text.Encoding.ASCII.GetBytes("<html>not audio</html> padding padding padding")));

    // Run by hand: KATO_WAV=path/to/sunbird.wav dotnet test --filter RealSpeechEngineWav
    [Fact]
    public void RealSpeechEngineWav_WhenProvided_IsWrittenNextToIt()
    {
        var path = Environment.GetEnvironmentVariable("KATO_WAV");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var ogg = OggOpusVoiceNote.FromWav(File.ReadAllBytes(path));
        Assert.NotNull(ogg);
        File.WriteAllBytes(Path.ChangeExtension(path, ".ogg"), ogg!);
    }

    private static byte[] Wav(int sampleRate, double seconds)
    {
        var count = (int)(sampleRate * seconds);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + count * 2);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(count * 2);
        for (var i = 0; i < count; i++)
        {
            writer.Write((short)(Math.Sin(2 * Math.PI * 220 * i / sampleRate) * 8000));
        }

        return stream.ToArray();
    }
}

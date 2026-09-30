using Concentus;
using Concentus.Enums;
using Concentus.Oggfile;

namespace InsuranceIntelligence.Infrastructure.Translation;

/// <summary>
/// Turns the WAV that Sunbird's speech engine returns into an OGG/Opus file, the one format WhatsApp
/// plays as a voice note. Pure managed code, so it runs on the App Service as deployed, with no
/// ffmpeg to install.
/// </summary>
public static class OggOpusVoiceNote
{
    private static readonly int[] OpusSampleRates = [8000, 12000, 16000, 24000, 48000];

    /// <summary>Null when the input is not 16-bit PCM WAV at a rate Opus supports.</summary>
    public static byte[]? FromWav(byte[] wav)
    {
        if (!TryReadPcm(wav, out var samples, out var sampleRate, out var channels)
            || !OpusSampleRates.Contains(sampleRate)
            || channels is < 1 or > 2)
        {
            return null;
        }

        var encoder = OpusCodecFactory.CreateEncoder(sampleRate, channels, OpusApplication.OPUS_APPLICATION_VOIP);
        encoder.Bitrate = 32000;
        using var output = new MemoryStream();
        var ogg = new OpusOggWriteStream(encoder, output);
        ogg.WriteSamples(samples, 0, samples.Length);
        ogg.Finish();
        return output.ToArray();
    }

    /// <summary>Reads the fmt and data chunks of a RIFF/WAVE file, skipping any others.</summary>
    internal static bool TryReadPcm(byte[] wav, out short[] samples, out int sampleRate, out int channels)
    {
        samples = [];
        sampleRate = 0;
        channels = 0;
        if (wav.Length < 44
            || wav[0] != 'R' || wav[1] != 'I' || wav[2] != 'F' || wav[3] != 'F'
            || wav[8] != 'W' || wav[9] != 'A' || wav[10] != 'V' || wav[11] != 'E')
        {
            return false;
        }

        var bitsPerSample = 0;
        var position = 12;
        while (position + 8 <= wav.Length)
        {
            var id = System.Text.Encoding.ASCII.GetString(wav, position, 4);
            var size = BitConverter.ToInt32(wav, position + 4);
            var body = position + 8;
            if (size < 0 && id != "data")
            {
                return false;
            }

            if (id == "fmt " && body + 16 <= wav.Length)
            {
                var format = BitConverter.ToInt16(wav, body);
                channels = BitConverter.ToInt16(wav, body + 2);
                sampleRate = BitConverter.ToInt32(wav, body + 4);
                bitsPerSample = BitConverter.ToInt16(wav, body + 14);
                if (format != 1)
                {
                    return false; // not plain PCM
                }
            }
            else if (id == "data")
            {
                if (bitsPerSample != 16 || channels == 0)
                {
                    return false;
                }

                // Streamed WAVs sometimes carry a placeholder size; read what is actually there.
                var available = Math.Min(size <= 0 ? int.MaxValue : size, wav.Length - body);
                samples = new short[available / 2];
                Buffer.BlockCopy(wav, body, samples, 0, samples.Length * 2);
                return samples.Length > 0;
            }

            position = body + size + (size % 2);
        }

        return false;
    }
}

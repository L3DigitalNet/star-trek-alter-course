using System.Buffers.Binary;
using System.Text;

namespace AlterCourse.AssetCtl.Tests;

internal static class AnimatedPngFixture
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static byte[] Create(byte[] png)
    {
        using var output = new MemoryStream();
        output.Write(Signature);
        int offset = Signature.Length;
        List<byte[]> imageData = [];
        bool animationHeaderWritten = false;
        while (offset < png.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            string type = Encoding.ASCII.GetString(png, offset + 4, 4);
            byte[] data = png.AsSpan(offset + 8, length).ToArray();
            offset += 12 + length;
            if (string.Equals(type, "IDAT", StringComparison.Ordinal))
            {
                imageData.Add(data);
                WriteChunk(output, type, data);
            }
            else if (string.Equals(type, "IEND", StringComparison.Ordinal))
            {
                WriteSecondFrame(output, imageData);
                WriteChunk(output, "IEND", []);
            }
            else
            {
                WriteChunk(output, type, data);
                if (string.Equals(type, "IHDR", StringComparison.Ordinal) && !animationHeaderWritten)
                {
                    WriteAnimationHeader(output, data);
                    animationHeaderWritten = true;
                }
            }
        }

        return output.ToArray();
    }

    private static void WriteAnimationHeader(Stream output, byte[] header)
    {
        Span<byte> control = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(control, 2);
        WriteChunk(output, "acTL", control);
        WriteFrameControl(output, sequence: 0);
    }

    private static void WriteSecondFrame(Stream output, List<byte[]> imageData)
    {
        _ = imageData.Count == 0 ? throw new InvalidOperationException("PNG had no image data.") : imageData[0];
        WriteFrameControl(output, sequence: 1);
        uint sequence = 2;
        foreach (byte[] data in imageData)
        {
            byte[] frameData = new byte[data.Length + 4];
            BinaryPrimitives.WriteUInt32BigEndian(frameData, sequence++);
            data.CopyTo(frameData, 4);
            WriteChunk(output, "fdAT", frameData);
        }
    }

    private static void WriteFrameControl(Stream output, uint sequence)
    {
        Span<byte> control = stackalloc byte[26];
        BinaryPrimitives.WriteUInt32BigEndian(control, sequence);
        BinaryPrimitives.WriteUInt32BigEndian(control[4..], 64);
        BinaryPrimitives.WriteUInt32BigEndian(control[8..], 64);
        BinaryPrimitives.WriteUInt16BigEndian(control[20..], 1);
        BinaryPrimitives.WriteUInt16BigEndian(control[22..], 10);
        WriteChunk(output, "fcTL", control);
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc(typeBytes, data));
        output.Write(crc);
    }

    private static uint Crc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in type)
        {
            crc = UpdateCrc(crc, value);
        }

        foreach (byte value in data)
        {
            crc = UpdateCrc(crc, value);
        }

        return ~crc;
    }

    private static uint UpdateCrc(uint crc, byte value)
    {
        crc ^= value;
        for (int bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) == 0 ? crc >> 1 : 0xedb88320U ^ (crc >> 1);
        }

        return crc;
    }
}

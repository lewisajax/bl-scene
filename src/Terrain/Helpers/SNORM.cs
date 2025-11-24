using System.Buffers.Binary;

namespace Scene.Helpers;

public static class SNORM
{
    public static uint FloatToUint(float num)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(BitConverter.GetBytes(num));
    }

    public static float UintToFloat(uint num)
    {
        return BinaryPrimitives.ReadSingleLittleEndian(BitConverter.GetBytes(num));
    }

    public static NormalizedPair Normalize(uint x)
    {
        NormalizedPair norm = new NormalizedPair();
        norm.Mantissa = x;
        norm.ExpSign = 126;

        while (norm.Mantissa < 0x8000)
        {
            norm.Mantissa <<= 1;
            norm.ExpSign--;
        }

        norm.Mantissa -= 0x8000;
        return norm;
    }

    public static NormalizedPair NormalizeSigned(int x)
    {
        NormalizedPair norm = SNORM.Normalize((uint)Math.Abs(x));
        norm.ExpSign |= (uint)((x < 0) ? 256 : 0);
        return norm;
    }

    public static float Decode8(int x)
    {
        if (x <= -127)
            return -1.0f;
        else if (x >= 127)
            return 1.0f;
        else if (x == 0)
            return 0.0f;

        NormalizedPair norm = SNORM.NormalizeSigned(x << 9);
        uint mant = norm.Mantissa >> 9;

        return SNORM.UintToFloat(
            (norm.ExpSign << 23) +
            (mant << 17) +
            0x10000 +
            (mant << 10) +
            0x200 +
            (mant << 3) +
            4 +
            (mant >> 4) +
            ((mant >> 3) & 1)
        );
    }   
}
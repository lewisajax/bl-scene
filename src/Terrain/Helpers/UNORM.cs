using System.Buffers.Binary;
using System.Text;

// Copied from:
// https://fgiesen.wordpress.com/2024/12/24/unorm-and-snorm-to-float-hardware-edition/
// https://gist.github.com/rygorous/056cb0219e6e65d50457d4b60a33a225?ts=4

namespace Scene.Helpers;

public static class UNORM
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

    public static float Decode16(uint x)
    {
        if (x <= 0)
            return 0f;
        else if (x >= 0xFFFF)
            return 1f;

        NormalizedPair norm = UNORM.Normalize(x);

        return UNORM.UintToFloat(
            (norm.ExpSign << 23) +
            (norm.Mantissa << 8) +
            0x80 +
            (norm.Mantissa >> 8) + 
            ((norm.Mantissa >> 7) & 1)
        );
    }   


}
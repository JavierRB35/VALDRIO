using System.Numerics;

namespace Valdrio.Calculation;

internal static class ExactDecimalProduct
{
    public static decimal Quantize(decimal left, decimal right, int scale)
    {
        var (leftCoefficient, leftScale) = Parts(left);
        var (rightCoefficient, rightScale) = Parts(right);
        var product = leftCoefficient * rightCoefficient;
        var negative = product.Sign < 0;
        var magnitude = BigInteger.Abs(product);
        var productScale = leftScale + rightScale;
        BigInteger units;
        if (productScale <= scale)
            units = magnitude * BigInteger.Pow(10, scale - productScale);
        else
        {
            var divisor = BigInteger.Pow(10, productScale - scale);
            units = BigInteger.DivRem(magnitude, divisor, out var remainder);
            var compared = (remainder * 2).CompareTo(divisor);
            if (compared > 0 || (compared == 0 && !units.IsEven)) units++;
        }

        // Remove insignificant zeros, preserving the exact quantized value.
        while (scale > 0 && units % 10 == 0) { units /= 10; scale--; }
        if (units > (BigInteger.One << 96) - 1) throw new OverflowException();
        var mask = new BigInteger(uint.MaxValue);
        return new decimal(unchecked((int)(uint)(units & mask)),
            unchecked((int)(uint)((units >> 32) & mask)),
            unchecked((int)(uint)((units >> 64) & mask)), negative && units != 0, (byte)scale);
    }

    private static (BigInteger Coefficient, int Scale) Parts(decimal value)
    {
        var bits = decimal.GetBits(value);
        var coefficient = new BigInteger((uint)bits[0]) |
            (new BigInteger((uint)bits[1]) << 32) |
            (new BigInteger((uint)bits[2]) << 64);
        if ((bits[3] & int.MinValue) != 0) coefficient = -coefficient;
        return (coefficient, (bits[3] >> 16) & 0xff);
    }
}

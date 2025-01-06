namespace RiskyCodec.Codec;

using static ArrayUtils;

public class DCTInteger2Transform : ITransform
{
    public int[,] Transform(int[,] input, bool inverse)
    {
        return inverse ? Inverse(input) : Forward(input);
    }

    private int[,] Forward(int[,] input)
    {
        var c0 = GetRow(input, 0);
        var d4 = GetRow(input, 1);
        var c2 = GetRow(input, 2);
        var d6 = GetRow(input, 3);
        var c1 = GetRow(input, 4);
        var d5 = GetRow(input, 5);
        var c3 = GetRow(input, 6);
        var d7 = GetRow(input, 7);
        
        var c4 = d4;
        var c5 = Add(d5, d6);
        var c7 = Sub(d5, d6);
        var c6 = d7;
        
        var b4 = Add(c4, c5);
        var b5 = Sub(c4, c5);
        var b6 = Add(c6, c7);
        var b7 = Sub(c6, c7);
        
        var b0 = Add(c0, c1);
        var b1 = Sub(c0, c1);
        var b2 = Add(Add(c2, Div(c2, 4)), Div(c3, 2));
        var b3 = Sub(Sub(Div(c2, 2), c3), Div(c3, 4));
        
        var a4 = Sub(Add(Add(Div(b7, 4), b4), Div(b4, 4)), Div(b4, 16));
        var a7 = Add(Sub(Sub(Div(b4, 4), b7), Div(b7, 4)), Div(b7, 16));
        var a5 = Add(Add(Sub(b5, b6), Div(b6, 4)), Div(b6, 16));
        var a6 = Sub(Sub(Add(b6, b5), Div(b5, 4)), Div(b5, 16));
        
        var a0 = Add(b0, b2);
        var a1 = Add(b1, b3);
        var a2 = Sub(b1, b3);
        var a3 = Sub(b0, b2);
        
        var o0 = Add(a0, a4);
        var o1 = Add(a1, a5);
        var o2 = Add(a2, a6);
        var o3 = Add(a3, a7);
        var o4 = Sub(a3, a7);
        var o5 = Sub(a2, a6);
        var o6 = Sub(a1, a5);
        var o7 = Sub(a0, a4);
        
        var output = new int[8, 8];
        for (int x = 0; x < 8; x++) output[x, 0] = o0[x];
        for (int x = 0; x < 8; x++) output[x, 1] = o1[x];
        for (int x = 0; x < 8; x++) output[x, 2] = o2[x];
        for (int x = 0; x < 8; x++) output[x, 3] = o3[x];
        for (int x = 0; x < 8; x++) output[x, 4] = o4[x];
        for (int x = 0; x < 8; x++) output[x, 5] = o5[x];
        for (int x = 0; x < 8; x++) output[x, 6] = o6[x];
        for (int x = 0; x < 8; x++) output[x, 7] = o7[x];
        return output;
    }

    private int[,] Inverse(int[,] input)
    {
        var i0 = GetRow(input, 0);
        var i1 = GetRow(input, 1);
        var i2 = GetRow(input, 2);
        var i3 = GetRow(input, 3);
        var i4 = GetRow(input, 4);
        var i5 = GetRow(input, 5);
        var i6 = GetRow(input, 6);
        var i7 = GetRow(input, 7);
        
        var a0 = Add(i0, i7);
        var a1 = Add(i1, i6);
        var a2 = Add(i2, i5);
        var a3 = Add(i3, i4);
        var a4 = Sub(i0, i7);
        var a5 = Sub(i1, i6);
        var a6 = Sub(i2, i5);
        var a7 = Sub(i3, i4);
        
        var b0 = Add(a0, a3);
        var b1 = Add(a1, a2);
        var b2 = Sub(a0, a3);
        var b3 = Sub(a1, a2);
        var b4 = Sub(Add(Add(Div(a7, 4), a4), Div(a4, 4)), Div(a4, 16));
        var b7 = Add(Sub(Sub(Div(a4, 4), a7), Div(a7, 4)), Div(a7, 16));
        var b5 = Sub(Sub(Add(a5, a6), Div(a6, 4)), Div(a6, 16));
        var b6 = Add(Add(Sub(a6, a5), Div(a5, 4)), Div(a5, 16));

        var c0 = Add(b0, b1);
        var c1 = Sub(b0, b1);
        var c2 = Add(Add(b2, Div(b2, 4)), Div(b3, 2));
        var c3 = Sub(Sub(Div(b2, 2), b3), Div(b3, 4));
        var c4 = Add(b4, b5);
        var c5 = Sub(b4, b5);
        var c6 = Add(b6, b7);
        var c7 = Sub(b6, b7);
        
        var d4 = c4;
        var d5 = Add(c5, c7);
        var d6 = Sub(c5, c7);
        var d7 = c6;

        var output = new int[8, 8];
        for (int x = 0; x < 8; x++) output[x, 0] = c0[x];
        for (int x = 0; x < 8; x++) output[x, 1] = d4[x];
        for (int x = 0; x < 8; x++) output[x, 2] = c2[x];
        for (int x = 0; x < 8; x++) output[x, 3] = d6[x];
        for (int x = 0; x < 8; x++) output[x, 4] = c1[x];
        for (int x = 0; x < 8; x++) output[x, 5] = d5[x];
        for (int x = 0; x < 8; x++) output[x, 6] = c3[x];
        for (int x = 0; x < 8; x++) output[x, 7] = d7[x];
        return output;
    }
}
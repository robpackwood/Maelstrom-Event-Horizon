namespace MaelstromEventHorizon.Domain.Math;

internal readonly record struct V2(double X, double Y)
{
    public static readonly V2 Zero = new(0, 0);
    public double Length => System.Math.Sqrt(X * X + Y * Y);
    public double LengthSquared => X * X + Y * Y;
    public V2 Normalized => Length > .0001 ? this / Length : Zero;

    public static V2 FromAngle(double angle)
    {
        return new V2(System.Math.Cos(angle), System.Math.Sin(angle));
    }

    public static double Distance(V2 a, V2 b)
    {
        return (a - b).Length;
    }

    public static V2 operator +(V2 a, V2 b)
    {
        return new V2(a.X + b.X, a.Y + b.Y);
    }

    public static V2 operator -(V2 a, V2 b)
    {
        return new V2(a.X - b.X, a.Y - b.Y);
    }

    public static V2 operator -(V2 a)
    {
        return new V2(-a.X, -a.Y);
    }

    public static V2 operator *(V2 a, double n)
    {
        return new V2(a.X * n, a.Y * n);
    }

    public static V2 operator /(V2 a, double n)
    {
        return new V2(a.X / n, a.Y / n);
    }
}

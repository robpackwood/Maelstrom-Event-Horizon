namespace MaelstromEventHorizon.Domain.Math;

internal readonly record struct V2(double X, double Y)
{
    public static readonly V2 Zero = new(0, 0);
    public double Length => System.Math.Sqrt(X * X + Y * Y);
    public double LengthSquared => X * X + Y * Y;
    public V2 Normalized => Length > .0001 ? this / Length : Zero;
    public static V2 FromAngle(double angle) => new(System.Math.Cos(angle), System.Math.Sin(angle));
    public static double Distance(V2 a, V2 b) => (a - b).Length;
    public static V2 operator +(V2 a, V2 b) => new(a.X + b.X, a.Y + b.Y);
    public static V2 operator -(V2 a, V2 b) => new(a.X - b.X, a.Y - b.Y);
    public static V2 operator -(V2 a) => new(-a.X, -a.Y);
    public static V2 operator *(V2 a, double n) => new(a.X * n, a.Y * n);
    public static V2 operator /(V2 a, double n) => new(a.X / n, a.Y / n);
}

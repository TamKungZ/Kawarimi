namespace Kawarimi.Formats;

public static class ObjSpace
{
    const double MetersPerUnit = 0.01;

    public static float[] PositionToObj(float[] p) =>
        [(float)(p[0] * MetersPerUnit), (float)(p[2] * MetersPerUnit), (float)(-p[1] * MetersPerUnit)];

    public static float[] PositionToGame(float[] p) =>
        [(float)(p[0] / MetersPerUnit), (float)(-p[2] / MetersPerUnit), (float)(p[1] / MetersPerUnit)];

    public static float[] NormalToObj(float[] n) => [n[0], n[2], -n[1]];

    public static float[] NormalToGame(float[] n) => [n[0], -n[2], n[1]];
}

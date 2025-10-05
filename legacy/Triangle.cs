using CamBam.Geom;
using MorphMuse.Services;

public class Triangle
{
    public Point3F A;
    public Point3F B;
    public Point3F C;

    public Triangle(Point3F a, Point3F b, Point3F c)
    {
        this.A = a;
        this.B = b;
        this.C = c;
    }

    public Point3F GetCenter()
    {
        return new Point3F(
            (A.X + B.X + C.X) / 3f,
            (A.Y + B.Y + C.Y) / 3f,
            (A.Z + B.Z + C.Z) / 3f
        );
    }

    public Vector3F GetNormal()
    {
        Vector3F u = new Vector3F(B.X - A.X, B.Y - A.Y, B.Z - A.Z);
        Vector3F v = new Vector3F(C.X - A.X, C.Y - A.Y, C.Z - A.Z);

        Vector3F normal = Geometry3F.Cross(u, v);
        return Geometry3F.Normalize(normal);
    }
}
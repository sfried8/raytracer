using UnityEngine;

public static class CPURayTracer
{
    public static bool hit_aabb(Vector3 boundsMin, Vector3 boundsMax, Ray ray)
    {
        Bounds aabb = new();
        aabb.SetMinMax(boundsMin, boundsMax);
        return aabb.IntersectRay(ray);
    }
    public static bool hit_triangle(TriangleStruct tri, Ray ray)
    {



        float denom = Vector3.Dot(tri.normal, ray.direction);
        if (denom > -0.00001)
        {
            return false;
        }
        float t = (tri.D - Vector3.Dot(tri.normal, ray.origin)) / denom;
        if (t < 0.001)
        {
            return false;
        }
        else
        {
            Vector3 intersection = ray.origin + t * ray.direction;
            Vector3 planar_hitpt_vector = intersection - tri.Q;
            float alpha = Vector3.Dot(tri.w, Vector3.Cross(planar_hitpt_vector, tri.v));
            float beta = Vector3.Dot(tri.w, Vector3.Cross(tri.u, planar_hitpt_vector));
            if (alpha > 0.0 && beta > 0.0 && alpha + beta < 1.0)
            {
                return true;
            }
        }
        return false;
    }

}
using System.Collections.Generic;
using UnityEngine;

public class Triangle
{
    public Vector3 a;
    public Vector3 b;
    public Vector3 c;
    public Vector3 center;
    public Vector3 normal;
    private Vector3 u;
    private Vector3 v;
    private Vector3 n;
    private float D;
    private Vector3 w;

    public Triangle(Vector3 a, Vector3 b, Vector3 c)
    {
        this.a = a;
        this.b = b;
        this.c = c;
        this.center = (a + b + c) / 3.0f;
        u = b - a;
        v = c - a;
        n = Vector3.Cross(u, v);
        normal = Vector3.Normalize(n);
        D = Vector3.Dot(normal, a);
        w = n / Vector3.Dot(n, n);
    }
    public TriangleStruct ToStruct()
    {
        return new TriangleStruct()
        {
            Q = a,
            u = u,
            v = v,
            n = n,
            normal = normal,
            D = D,
            w = w
        };
    }

}
public static class MeshSplitter
{

    private static (MeshChunk meshChunkA, MeshChunk meshChunkB) SplitOnce(MeshChunk startingMesh, int axis)
    {
        float boundsCenterAxis;
        if (axis == 0)
        {
            boundsCenterAxis = startingMesh.bounds.center.x;
        }
        else if (axis == 1)
        {
            boundsCenterAxis = startingMesh.bounds.center.y;
        }
        else
        {
            boundsCenterAxis = startingMesh.bounds.center.z;
        }
        MeshChunk meshChunkA = new()
        {
            bounds = new(),
            triangles = new(),
            name = startingMesh.name
        };
        MeshChunk meshChunkB = new()
        {
            bounds = new(),
            triangles = new(),
            name = startingMesh.name
        };
        foreach (Triangle triangle in startingMesh.triangles)
        {
            float triangleCenterAxis;
            if (axis == 0)
            {
                triangleCenterAxis = triangle.center.x;
            }
            else if (axis == 1)
            {
                triangleCenterAxis = triangle.center.y;
            }
            else
            {
                triangleCenterAxis = triangle.center.z;
            }
            if (triangleCenterAxis < boundsCenterAxis)
            {
                if (meshChunkA.triangles.Count == 0)
                {
                    meshChunkA.bounds = new Bounds(triangle.a, Vector3.one * 0.1f);
                }
                meshChunkA.triangles.Add(triangle);
                meshChunkA.bounds.Encapsulate(triangle.a);
                meshChunkA.bounds.Encapsulate(triangle.b);
                meshChunkA.bounds.Encapsulate(triangle.c);
            }
            else
            {
                if (meshChunkB.triangles.Count == 0)
                {
                    meshChunkB.bounds = new Bounds(triangle.a, Vector3.one * 0.1f);
                }
                meshChunkB.triangles.Add(triangle);
                meshChunkB.bounds.Encapsulate(triangle.a);
                meshChunkB.bounds.Encapsulate(triangle.b);
                meshChunkB.bounds.Encapsulate(triangle.c);
            }
        }
        meshChunkA.bounds = new Bounds(meshChunkA.bounds.center, meshChunkA.bounds.size * 1.05f);
        meshChunkB.bounds = new Bounds(meshChunkB.bounds.center, meshChunkB.bounds.size * 1.05f);

        return (meshChunkA, meshChunkB);
    }


    public static BVHNode MeshChunkToBVHNode(MeshChunk meshChunk, int depth = 0, int limit = 10)
    {
        BVHNode node = new()
        {
            meshChunk = meshChunk,
            childA = null,
            childB = null,
            depth = depth
        };
        if (meshChunk.triangles.Count > 1 && depth < limit)
        {
            (MeshChunk meshChunkA, MeshChunk meshChunkB) = FindBestSplit(meshChunk);
            if (meshChunkA.triangles.Count == 0)
            {
                return MeshChunkToBVHNode(meshChunkB, depth + 1, limit);
            }
            if (meshChunkB.triangles.Count == 0)
            {
                return MeshChunkToBVHNode(meshChunkA, depth + 1, limit);
            }

            node.childA = MeshChunkToBVHNode(meshChunkA, depth + 1, limit);
            node.childB = MeshChunkToBVHNode(meshChunkB, depth + 1, limit);
        }
        return node;
    }


    private static float Cost(MeshChunk meshChunk)
    {
        return meshChunk.bounds.size.x * meshChunk.bounds.size.y * meshChunk.bounds.size.z * meshChunk.triangles.Count;
    }
    private static float Cost(List<MeshChunk> meshChunks)
    {
        float sum = 0f;
        foreach (MeshChunk chunk in meshChunks)
        {
            sum += Cost(chunk);
        }
        return sum;
    }

    public static (MeshChunk meshChunkA, MeshChunk meshChunkB) FindBestSplit(MeshChunk fullMesh)
    {

        int axis = 0;
        if (fullMesh.bounds.size.y > fullMesh.bounds.size.x && fullMesh.bounds.size.y > fullMesh.bounds.size.z)
        {
            axis = 1;
        }
        if (fullMesh.bounds.size.z > fullMesh.bounds.size.x && fullMesh.bounds.size.z > fullMesh.bounds.size.y)
        {
            axis = 2;
        }
        return SplitOnce(fullMesh, axis);
        // float costX = Cost(splitX);
        // float costY = Cost(splitY);
        // float costZ = Cost(splitZ);
        // if (costX <= costY && costX <= costZ)
        // {
        //     return splitX;
        // }
        // if (costY <= costX && costY <= costZ)
        // {
        //     return splitY;
        // }
        // return splitZ;

    }

}
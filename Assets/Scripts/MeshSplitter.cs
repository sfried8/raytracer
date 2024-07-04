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
    public Vector3 min;
    public Vector3 max;

    public Triangle(Vector3 a, Vector3 b, Vector3 c)
    {
        this.a = a;
        this.b = b;
        this.c = c;
        min = Vector3.Min(Vector3.Min(a, b), c);
        max = Vector3.Max(Vector3.Max(a, b), c);
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

    public static int numSplitsToTest = 5;
    private static float EvaluateSplitLite(MeshChunk startingMesh, int axis, float percentage)
    {
        float boundsCenterAxis = startingMesh.bounds.min[axis] + percentage * startingMesh.bounds.size[axis];
        int numTrianglesA = 0;
        RTBounds boundsA = new();
        RTBounds boundsB = new();
        foreach (Triangle triangle in startingMesh.triangles)
        {
            float triangleCenterAxis = triangle.center[axis];
            if (triangleCenterAxis < boundsCenterAxis)
            {
                numTrianglesA++;
                boundsA.Encapsulate(triangle.min, triangle.max);
            }
            else
            {
                boundsB.Encapsulate(triangle.min, triangle.max);
            }
        }
        return Cost(boundsA, numTrianglesA) + Cost(boundsB, startingMesh.triangles.Count - numTrianglesA);

    }
    private static (MeshChunk meshChunkA, MeshChunk meshChunkB) SplitOnce(MeshChunk startingMesh, int axis, float percentage)
    {
        float boundsCenterAxis = startingMesh.bounds.min[axis] + percentage * startingMesh.bounds.size[axis];
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
            float triangleCenterAxis = triangle.center[axis];
            if (triangleCenterAxis < boundsCenterAxis)
            {
                meshChunkA.triangles.Add(triangle);
                meshChunkA.bounds.Encapsulate(triangle.min, triangle.max);
            }
            else
            {
                meshChunkB.triangles.Add(triangle);
                meshChunkB.bounds.Encapsulate(triangle.min, triangle.max);
            }
        }
        // meshChunkA.bounds = new Bounds(meshChunkA.bounds.center, meshChunkA.bounds.size * 1.05f);
        // meshChunkB.bounds = new Bounds(meshChunkB.bounds.center, meshChunkB.bounds.size * 1.05f);

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
            (MeshChunk meshChunkA, MeshChunk meshChunkB, float splitCost) = FindBestSplit(meshChunk);
            float parentCost = Cost(meshChunk);
            if (splitCost >= parentCost)
            {
                // Debug.Log($"stopping here at depth {depth} because cost of children ({splitCost}) is greater than parent ({parentCost})");
                return node;
            }
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
        return Cost(meshChunk.bounds, meshChunk.triangles.Count);
    }
    private static float Cost(RTBounds bounds, int numTriangles)
    {
        float halfSurfaceArea = bounds.size.x * bounds.size.y +
                                bounds.size.y * bounds.size.z +
                                bounds.size.z * bounds.size.x;
        return halfSurfaceArea * numTriangles;
    }

    public static (MeshChunk meshChunkA, MeshChunk meshChunkB, float cost) FindBestSplit(MeshChunk fullMesh)
    {
        float bestCost = 1e30f;
        int bestAxis = 0;
        float bestPercentage = 0;
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < numSplitsToTest; j++)
            {
                var perc = 1.0f / (numSplitsToTest + 1) * (j + 1);
                var splitCost = EvaluateSplitLite(fullMesh, i, perc);
                if (splitCost < bestCost)
                {
                    bestCost = splitCost;
                    bestAxis = i;
                    bestPercentage = perc;
                }
            }
        }
        (var bestChunkA, var bestChunkB) = SplitOnce(fullMesh, bestAxis, bestPercentage);
        return (bestChunkA, bestChunkB, bestCost);

        // int axis = 0;
        // float largestSide = 0;
        // for (int i = 0; i < 3; i++)
        // {
        //     float currentSide = fullMesh.bounds.size[i];
        //     if (currentSide > largestSide)
        //     {
        //         largestSide = currentSide;
        //         axis = i;
        //     }
        // }
        // return SplitOnce(fullMesh, axis);

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
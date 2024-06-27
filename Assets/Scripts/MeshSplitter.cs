using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
public class BVHNode
{
    public MeshChunk meshChunk;
    public int index;
    public BVHNode childA;
    public BVHNode childB;
    public BVHNode parent;
    public int depth;
    public bool isRoot
    {
        get { return depth == 0; }
    }
    public bool isLeaf
    {
        get { return childA == null; }
    }
}
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




        // Vector3 newBoundsSize = startingMesh.bounds.size * 1.1f;//.x, startingMesh.bounds.size.y, startingMesh.bounds.size.z / numSplits.z + 0.1f);
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

    public static List<BVHNode> flattenBVHNode(BVHNode parent)
    {
        List<BVHNode> nodes = new() { parent };
        if (parent.childA != null)
        {
            nodes.AddRange(flattenBVHNode(parent.childA));
        }
        if (parent.childB != null)
        {
            nodes.AddRange(flattenBVHNode(parent.childB));
        }
        return nodes;
    }
    public static (List<BVHNodeStruct>, List<TriangleStruct>) CreateBVHStructs(BVHNode parent, int meshStartIndex, int triangleStartIndex)
    {
        List<BVHNodeStruct> nodeStructs = new();
        List<TriangleStruct> triangles = new();
        List<BVHNode> flattenedBVHNodes = flattenBVHNode(parent);
        for (int i = 0; i < flattenedBVHNodes.Count; i++)
        {
            BVHNode bVHNode = flattenedBVHNodes[i];
            bVHNode.index = i + meshStartIndex;
        }
        foreach (BVHNode node in flattenedBVHNodes)
        {
            BVHNodeStruct nodeStruct = new()
            {
                childA = 0,
                childB = 0,
                depth = node.depth
            };
            if (node.childA != null)
            {
                nodeStruct.childA = node.childA.index;
            }
            if (node.childB != null)
            {
                nodeStruct.childB = node.childB.index;
            }
            if (node.isLeaf)
            {
                nodeStruct.numTriangles = node.meshChunk.triangles.Count;
                nodeStruct.triangleStartIndex = triangles.Count + triangleStartIndex;
                triangles.AddRange(node.meshChunk.triangles.Select((triangle) => triangle.ToStruct()));
            }
            nodeStruct.boundsMin = node.meshChunk.bounds.min;
            nodeStruct.boundsMax = node.meshChunk.bounds.max;
            nodeStructs.Add(nodeStruct);
        }
        return (nodeStructs, triangles);
    }
    public static (List<BVHNodeStruct>, List<TriangleStruct>, BVHNode) CreateBVH(MeshChunk parentMesh, int meshStartIndex, int triangleStartIndex, int limit = 5)
    {
        Debug.Log($"Creating BVH Tree for mesh with {parentMesh.triangles.Count} tris");
        var startTime = Time.realtimeSinceStartup;
        BVHNode tree = MeshChunkToBVHNode(parentMesh, 0, limit);

        (List<BVHNodeStruct> structs, List<TriangleStruct> tris) = CreateBVHStructs(tree, meshStartIndex, triangleStartIndex);
        Debug.Log($"Finished creating BVH tree in {Time.realtimeSinceStartup - startTime} seconds");
        return (structs, tris, tree);
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
    // public static List<MeshChunk> Split(MeshChunk fullMesh, int maxTriangles, int limit = 5)
    // {
    //     List<MeshChunk> subMeshChunks = new List<MeshChunk>();
    //     List<MeshChunk> firstSplit = FindBestSplit(fullMesh);
    //     foreach (MeshChunk chunk in firstSplit)
    //     {
    //         if (chunk.triangles.Count > maxTriangles && limit > 0)
    //         {
    //             subMeshChunks.AddRange(Split(chunk, maxTriangles, limit - 1));
    //         }
    //         else
    //         {
    //             subMeshChunks.Add(chunk);
    //         }
    //     }
    //     return subMeshChunks;
    // }

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
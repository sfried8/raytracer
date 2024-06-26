using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
public class BVHNode
{
    public MeshChunk meshChunk;
    public int index;
    public BVHNode childA;
    public BVHNode childB;
    public int depth;
}
public static class MeshSplitter
{

    private static Vector3 TriangleCenter(Triangle triangle)
    {
        return (3 * triangle.Q + triangle.u + triangle.v) / 3.0f;
    }
    private static (MeshChunk meshChunkA, MeshChunk meshChunkB) SplitOnce(MeshChunk startingMesh, int axis)
    {




        // Vector3 newBoundsSize = startingMesh.bounds.size * 1.1f;//.x, startingMesh.bounds.size.y, startingMesh.bounds.size.z / numSplits.z + 0.1f);
        Vector3 boundsCenterA = startingMesh.bounds.center;
        Vector3 boundsCenterB = startingMesh.bounds.center;
        if (axis == 0)
        {
            boundsCenterA.x -= startingMesh.bounds.size.x / 4.0f;
            boundsCenterB.x += startingMesh.bounds.size.x / 4.0f;
        }
        else if (axis == 1)
        {
            boundsCenterA.y -= startingMesh.bounds.size.y / 4.0f;
            boundsCenterB.y += startingMesh.bounds.size.y / 4.0f;
        }
        else
        {
            boundsCenterA.z -= startingMesh.bounds.size.z / 4.0f;
            boundsCenterB.z += startingMesh.bounds.size.z / 4.0f;
        }
        MeshChunk meshChunkA = new()
        {
            bounds = new Bounds(boundsCenterA, Vector3.one * 0.1f),
            triangles = new List<Triangle>()
        };
        MeshChunk meshChunkB = new()
        {
            bounds = new Bounds(boundsCenterB, Vector3.one * 0.1f),
            triangles = new List<Triangle>()
        };
        System.Random r = new System.Random();
        foreach (Triangle triangle in startingMesh.triangles)
        {
            Vector3 triangleCenter = TriangleCenter(triangle);
            float distanceToA = Vector3.Distance(triangleCenter, boundsCenterA);
            float distanceToB = Vector3.Distance(triangleCenter, boundsCenterB);
            // if (r.NextDouble() > 0.5)
            if (distanceToA < distanceToB)
            {
                meshChunkA.triangles.Add(triangle);
                meshChunkA.bounds.Encapsulate(triangle.Q);
                meshChunkA.bounds.Encapsulate(triangle.Q + triangle.u);
                meshChunkA.bounds.Encapsulate(triangle.Q + triangle.v);
            }
            else
            {
                meshChunkB.triangles.Add(triangle);
                meshChunkB.bounds.Encapsulate(triangle.Q);
                meshChunkB.bounds.Encapsulate(triangle.Q + triangle.u);
                meshChunkB.bounds.Encapsulate(triangle.Q + triangle.v);
            }
        }

        return (meshChunkA, meshChunkB);





    }

    private static List<BVHNode> flattenBVHNode(BVHNode parent, int safetyLimit = 100)
    {
        List<BVHNode> nodes = new() { parent };
        if (parent.childA != null && safetyLimit > 0)
        {
            nodes.AddRange(flattenBVHNode(parent.childA, safetyLimit - 1));
        }
        if (parent.childB != null && safetyLimit > 0)
        {
            nodes.AddRange(flattenBVHNode(parent.childB, safetyLimit - 1));
        }
        return nodes;
    }
    public static (List<BVHNodeStruct>, List<Triangle>) CreateBVHStructs(BVHNode parent, int meshStartIndex, int triangleStartIndex)
    {
        List<BVHNodeStruct> nodeStructs = new();
        List<Triangle> triangles = new();
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
            if (node.childA == null && node.childB == null)
            {
                nodeStruct.numTriangles = node.meshChunk.triangles.Count;
                nodeStruct.triangleStartIndex = triangles.Count + triangleStartIndex;
                triangles.AddRange(node.meshChunk.triangles);
            }
            nodeStruct.boundsMin = node.meshChunk.bounds.min;
            nodeStruct.boundsMax = node.meshChunk.bounds.max;
            nodeStructs.Add(nodeStruct);
        }
        return (nodeStructs, triangles);
    }
    public static (List<BVHNodeStruct>, List<Triangle>) CreateBVH(MeshChunk parentMesh, int meshStartIndex, int triangleStartIndex, int limit = 5)
    {
        BVHNode tree = MeshChunkToBVHNode(parentMesh, 0, limit);
        int numTriangles = 0;
        int minTriangles = parentMesh.triangles.Count;
        int maxTriangles = 0;
        int totalDepth = 0;
        int minDepth = 10;
        int maxDepth = 0;
        int numLeafs = 0;
        var flattened = flattenBVHNode(tree);
        foreach (var node in flattened)
        {
            if (node.childA == null)
            {
                numLeafs++;
                int numTris = node.meshChunk.triangles.Count;
                numTriangles += numTris;
                if (numTris > maxTriangles)
                {
                    maxTriangles = numTris;
                }
                if (numTris < minTriangles)
                {
                    minTriangles = numTris;
                }
                totalDepth += node.depth;
                if (node.depth > maxDepth)
                {
                    maxDepth = node.depth;
                }
                if (node.depth < minDepth)
                {
                    minDepth = node.depth;
                }
            }
        }
        Debug.Log($"Total Nodes: {flattened.Count}, Total Leafs: {numLeafs}");
        Debug.Log($"Total Triangles: {parentMesh.triangles.Count} (from mesh), {numTriangles} (from leafs)");
        Debug.Log($"Triangles: min {minTriangles}, max {maxTriangles}, avg {(float)numTriangles / numLeafs}");
        Debug.Log($"Depth: min {minDepth}, max {maxDepth}, avg {(float)totalDepth / numLeafs}");
        return CreateBVHStructs(tree, meshStartIndex, triangleStartIndex);
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
                return MeshChunkToBVHNode(meshChunkB, depth + 1);
            }
            if (meshChunkB.triangles.Count == 0)
            {
                return MeshChunkToBVHNode(meshChunkA, depth + 1);
            }

            node.childA = MeshChunkToBVHNode(meshChunkA, depth + 1);
            node.childB = MeshChunkToBVHNode(meshChunkB, depth + 1);
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
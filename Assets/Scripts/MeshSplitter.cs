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
}
public static class MeshSplitter
{

    private static List<MeshChunk> SplitOnce(MeshChunk startingMesh, Vector3Int numSplits)
    {
        return SplitOnce(new List<MeshChunk> { startingMesh }, numSplits);
    }
    private static List<MeshChunk> SplitOnce(List<MeshChunk> startingMeshes, Vector3Int numSplits)
    {
        List<MeshChunk> subMeshChunks = new List<MeshChunk>();
        foreach (MeshChunk startingMesh in startingMeshes)
        {


            List<Triangle> fullMeshTrianglesCopy = new List<Triangle>(startingMesh.triangles);

            Vector3 newBoundsSize = new Vector3(startingMesh.bounds.size.x / numSplits.x + 0.1f, startingMesh.bounds.size.y / numSplits.y + 0.1f, startingMesh.bounds.size.z / numSplits.z + 0.1f);
            for (int x = 0; x < numSplits.x; x++)
            {
                for (int y = 0; y < numSplits.y; y++)
                {
                    for (int z = 0; z < numSplits.z; z++)
                    {
                        Vector3 boundsCenter = startingMesh.bounds.min + new Vector3((x + 0.4995f) * newBoundsSize.x, (y + 0.4995f) * newBoundsSize.y, (z + 0.4995f) * newBoundsSize.z);
                        Bounds chunkBounds = new Bounds(boundsCenter, newBoundsSize);
                        List<Triangle> chunkTriangles = new List<Triangle>();
                        List<Vector3> verticesToAdd = new List<Vector3>();
                        List<Triangle> trianglesToTrack = new List<Triangle>(fullMeshTrianglesCopy);
                        foreach (Triangle triangle in trianglesToTrack)
                        {
                            Vector3 a = triangle.Q;
                            Vector3 b = triangle.u + a;
                            Vector3 c = triangle.v + a;
                            if (chunkBounds.Contains(a) || chunkBounds.Contains(b) || chunkBounds.Contains(c))
                            {
                                verticesToAdd.Add(a);
                                verticesToAdd.Add(b);
                                verticesToAdd.Add(c);
                                // chunkBounds.Encapsulate(a);
                                // chunkBounds.Encapsulate(b);
                                // chunkBounds.Encapsulate(c);
                                chunkTriangles.Add(triangle);
                                fullMeshTrianglesCopy.Remove(triangle);
                            }
                        }
                        chunkBounds = new Bounds(verticesToAdd.Count > 0 ? verticesToAdd[0] : Vector3.zero, Vector3.one * 0.1f);
                        foreach (Vector3 vertex in verticesToAdd)
                        {
                            chunkBounds.Encapsulate(vertex);
                        }
                        MeshChunk newSubMeshChunk = new MeshChunk()
                        {
                            triangles = chunkTriangles,
                            bounds = chunkBounds
                        };


                        subMeshChunks.Add(newSubMeshChunk);




                    }
                }
            }
        }
        return subMeshChunks;
    }
    private static List<BVHNode> flattenBVHNode(BVHNode parent, int safetyLimit = 100)
    {
        List<BVHNode> nodes = new List<BVHNode> { parent };
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
        List<BVHNodeStruct> nodeStructs = new List<BVHNodeStruct>();
        List<Triangle> triangles = new List<Triangle>();
        List<BVHNode> flattenedBVHNodes = flattenBVHNode(parent);
        for (int i = 0; i < flattenedBVHNodes.Count; i++)
        {
            BVHNode bVHNode = flattenedBVHNodes[i];
            bVHNode.index = i + meshStartIndex;
        }
        foreach (BVHNode node in flattenedBVHNodes)
        {
            BVHNodeStruct nodeStruct = new BVHNodeStruct()
            {
                childA = 0,
                childB = 0
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
    public static (List<BVHNodeStruct>, List<Triangle>) CreateBVH(MeshChunk parentMesh, int maxTriangles, int meshStartIndex, int triangleStartIndex, int limit = 10)
    {
        BVHNode tree = MeshChunkToBVHNode(parentMesh, maxTriangles, limit);
        return CreateBVHStructs(tree, meshStartIndex, triangleStartIndex);
    }
    public static BVHNode MeshChunkToBVHNode(MeshChunk meshChunk, int maxTriangles, int limit = 10)
    {
        BVHNode node = new BVHNode()
        {
            meshChunk = meshChunk,
            childA = null,
            childB = null
        };
        if (meshChunk.triangles.Count > maxTriangles && limit > 0)
        {
            List<MeshChunk> split = FindBestSplit(meshChunk);
            Debug.Log($"split length: {split.Count}");
            node.childA = MeshChunkToBVHNode(split[0], maxTriangles, limit - 1);
            node.childB = MeshChunkToBVHNode(split[1], maxTriangles, limit - 1);
        }
        return node;
    }
    public static List<MeshChunk> Split(MeshChunk fullMesh, int maxTriangles, int limit = 5)
    {
        List<MeshChunk> subMeshChunks = new List<MeshChunk>();
        List<MeshChunk> firstSplit = FindBestSplit(fullMesh);
        foreach (MeshChunk chunk in firstSplit)
        {
            if (chunk.triangles.Count > maxTriangles && limit > 0)
            {
                subMeshChunks.AddRange(Split(chunk, maxTriangles, limit - 1));
            }
            else
            {
                subMeshChunks.Add(chunk);
            }
        }
        return subMeshChunks;
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

    public static List<MeshChunk> FindBestSplit(MeshChunk fullMesh)
    {
        List<MeshChunk> splitX = SplitOnce(fullMesh, new Vector3Int(2, 1, 1));
        List<MeshChunk> splitY = SplitOnce(fullMesh, new Vector3Int(1, 2, 1));
        List<MeshChunk> splitZ = SplitOnce(fullMesh, new Vector3Int(1, 1, 2));
        float costX = Cost(splitX);
        float costY = Cost(splitY);
        float costZ = Cost(splitZ);
        if (costX <= costY && costX <= costZ)
        {
            return splitX;
        }
        if (costY <= costX && costY <= costZ)
        {
            return splitY;
        }
        return splitZ;

    }

}
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
public static class BVH
{
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
                childAIndex = 0,
            };
            if (node.childA != null)
            {
                nodeStruct.childAIndex = node.childA.index;
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
        // Debug.Log($"Creating BVH Tree for mesh with {parentMesh.triangles.Count} tris");
        var startTime = Time.realtimeSinceStartup;
        BVHNode tree = MeshSplitter.MeshChunkToBVHNode(parentMesh, 0, limit);

        (List<BVHNodeStruct> structs, List<TriangleStruct> tris) = CreateBVHStructs(tree, meshStartIndex, triangleStartIndex);
        // Debug.Log($"Finished creating BVH tree in {Time.realtimeSinceStartup - startTime} seconds");
        return (structs, tris, tree);
    }
    public static List<BVHNode> flattenBVHNode(BVHNode parent, int depth = 0)
    {
        List<BVHNode> nodes = new();
        if (depth == 0)
        {
            nodes.Add(parent);
        }
        if (parent.childA != null)
        {
            nodes.Add(parent.childA);
        }
        if (parent.childB != null)
        {
            nodes.Add(parent.childB);
        }
        if (parent.childA != null)
        {
            nodes.AddRange(flattenBVHNode(parent.childA, depth + 1));
        }
        if (parent.childB != null)
        {
            nodes.AddRange(flattenBVHNode(parent.childB, depth + 1));
        }
        return nodes;
    }
}
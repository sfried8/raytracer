
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RTMesh : MonoBehaviour
{
    public RTMaterial[] materials;

    [SerializeField, HideInInspector] int materialObjectID;
    [SerializeField, HideInInspector] bool materialInitFlag;

    [SerializeField, HideInInspector] BVHNode bVHNode;
    [HideInInspector] List<BVHNode> flattenedBVHNodes;

    [SerializeField] Vector2Int totalParentLeafNodes;
    [SerializeField] Vector4 totalMinMaxMeanTriangle;
    [SerializeField] Vector4 totalMinMaxMeanDepth;
    [SerializeField, Range(1, 10)] int gizmoBBDepth;

    public Mesh mesh;
    Vector3 matchTransform(Vector3 localVec, Transform t)
    {
        return t.position + t.rotation * Vector3.Scale(localVec, t.localScale);
    }
    void Start()
    {
        MeshFilter[] meshFilters = GetComponents<MeshFilter>();
        if (meshFilters.Length > 0)
        {
            mesh = meshFilters[0].sharedMesh;
        }
        else
        {
            mesh = GetComponentInChildren<MeshFilter>().sharedMesh;
        }
    }

    void OnValidate()
    {
        if (!materialInitFlag)
        {
            materialInitFlag = true;
            materials = new RTMaterial[1];
            materials[0] = new RTMaterial();
            materials[0].SetDefaultValues();
        }

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = GetComponentInChildren<MeshRenderer>();
        }
        if (renderer != null)
        {
            if (materialObjectID != gameObject.GetInstanceID())
            {
                Material[] materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    if (renderer.sharedMaterials[i] != null)
                    {
                        materials[i] = new Material(renderer.sharedMaterials[i]);

                    }
                }
                renderer.sharedMaterials = materials;

                materialObjectID = gameObject.GetInstanceID();
            }
            // renderer.sharedMaterial.color = material.color;
        }
    }
    public void SetBVHNode(BVHNode bVHNode)
    {
        this.bVHNode = bVHNode;
        int numTriangles = 0;
        int minTriangles = bVHNode.meshChunk.triangles.Count;
        int maxTriangles = 0;
        int totalDepth = 0;
        int minDepth = 10;
        int maxDepth = 0;
        int numLeafs = 0;
        flattenedBVHNodes = MeshSplitter.flattenBVHNode(bVHNode);
        foreach (var node in flattenedBVHNodes)
        {
            if (node.isLeaf)
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
        totalParentLeafNodes = new Vector2Int(flattenedBVHNodes.Count, numLeafs);
        totalMinMaxMeanDepth = new Vector4(totalDepth, minDepth, maxDepth, (float)totalDepth / numLeafs);
        totalMinMaxMeanTriangle = new Vector4(numTriangles, minTriangles, maxTriangles, (float)numTriangles / numLeafs);

    }

    void OnDrawGizmosSelected()
    {
        if (flattenedBVHNodes == null)
        {
            return;
        }
        foreach (var node in flattenedBVHNodes)
        {
            if (node.isLeaf && Mathf.Abs(node.meshChunk.triangles.Count - totalMinMaxMeanTriangle.z) < 1)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(node.meshChunk.bounds.center, node.meshChunk.bounds.size);
            }
            else if (node.depth == gizmoBBDepth)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireCube(node.meshChunk.bounds.center, node.meshChunk.bounds.size);

            }
            // foreach (var tri in node.meshChunk.triangles)
            // {

            //     Gizmos.DrawLine(tri.a, tri.b);
            //     Gizmos.DrawLine(tri.a, tri.c);
            //     Gizmos.DrawLine(tri.b, tri.c);
            // }
        }
    }
}
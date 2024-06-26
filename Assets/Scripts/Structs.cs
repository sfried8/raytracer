using System.Collections.Generic;
using UnityEngine;


public struct Sphere
{
    public Vector3 origin;
    public float radius;
    public RTMaterial material;
}

public struct TriangleStruct
{
    public Vector3 Q;
    public Vector3 u;
    public Vector3 v;
    public Vector3 n;
    public float D;
    public Vector3 w;
    public Vector3 normal;
}

public struct MeshChunk
{
    public List<Triangle> triangles;
    public RTBounds bounds;
    public string name;
}

public struct MeshInfoStruct
{
    public int bvhNode;
    public Matrix4x4 worldToLocalMatrix;
    public RTMaterial material;
}
public struct BVHNodeStruct
{
    public Vector3 boundsMin;
    public Vector3 boundsMax;
    public int index;
    public int numTriangles;

}
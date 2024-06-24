using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

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
                        if (chunkTriangles.Count == 0)
                        {
                            continue;
                        }
                        chunkBounds = new Bounds(verticesToAdd[0], Vector3.one * 0.1f);
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
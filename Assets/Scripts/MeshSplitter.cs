using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public static class MeshSplitter
{

    public static List<MeshChunk> Split(MeshChunk fullMesh, Vector3Int numSplits, int maxTriangles, int limit = 3)
    {
        List<MeshChunk> subMeshChunks = new List<MeshChunk>();
        List<Triangle> fullMeshTrianglesCopy = new List<Triangle>(fullMesh.triangles);

        Vector3 newBoundsSize = new Vector3(fullMesh.bounds.size.x / numSplits.x, fullMesh.bounds.size.y / numSplits.y, fullMesh.bounds.size.z / numSplits.z);
        for (int x = 0; x < numSplits.x; x++)
        {
            for (int y = 0; y < numSplits.y; y++)
            {
                for (int z = 0; z < numSplits.z; z++)
                {
                    Vector3 boundsCenter = fullMesh.bounds.min + new Vector3((x + 0.5f) * newBoundsSize.x, (y + 0.5f) * newBoundsSize.y, (z + 0.5f) * newBoundsSize.z);
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
                    if (chunkTriangles.Count > maxTriangles && limit > 0)
                    {
                        subMeshChunks.AddRange(FindBestSplit(newSubMeshChunk, maxTriangles, limit - 1));
                    }
                    else
                    {
                        Debug.Log((limit == 0 ? "Hit Limit! Triangles remaining: " : "Stopping because triangles are at ") + chunkTriangles.Count);
                        subMeshChunks.Add(newSubMeshChunk);
                    }



                }
            }
        }
        return subMeshChunks;
    }

    private static float deviation(List<MeshChunk> meshChunks)
    {
        float totalTriangles = 0.0f;
        foreach (MeshChunk meshChunk in meshChunks)
        {
            totalTriangles += (float)meshChunk.triangles.Count;
        }
        float average = (float)totalTriangles / (float)meshChunks.Count;
        float dev = 0;
        foreach (MeshChunk meshChunk in meshChunks)
        {
            Debug.Log("meshChunkTrianglesCount: " + meshChunk.triangles.Count);
            dev += Math.Abs((float)meshChunk.triangles.Count - average);
        }
        Debug.Log("total triangles: " + totalTriangles + ", num mesh chunks: " + meshChunks.Count + ", average: " + average);
        return dev;
    }
    public static List<MeshChunk> FindBestSplit(MeshChunk fullMesh, int maxTriangles, int limit = 5)
    {
        Debug.Log("".PadLeft(20 - limit, ' ') + "Finding Best Split. Current Triangle count is " + fullMesh.triangles.Count);
        List<MeshChunk> splitX = Split(fullMesh, new Vector3Int(2, 1, 1), maxTriangles, limit);
        List<MeshChunk> splitY = Split(fullMesh, new Vector3Int(1, 2, 1), maxTriangles, limit);
        List<MeshChunk> splitZ = Split(fullMesh, new Vector3Int(1, 1, 2), maxTriangles, limit);
        float devX = deviation(splitX);
        float devY = deviation(splitY);
        float devZ = deviation(splitZ);
        Debug.Log(devX + " vs. " + devY + " vs. " + devZ);
        if (devX <= devY && devX <= devZ)
        {
            return splitX;
        }
        if (devY <= devX && devY <= devZ)
        {
            return splitY;
        }
        return splitZ;

    }

}
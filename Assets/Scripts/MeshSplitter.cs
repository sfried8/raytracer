using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public static class MeshSplitter
{

    public static List<MeshChunk> Split(MeshChunk fullMesh, Vector3Int numSplits)
    {
        List<MeshChunk> subMeshChunks = new List<MeshChunk>();
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
                    foreach (Triangle triangle in fullMesh.triangles)
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
                    subMeshChunks.Add(new MeshChunk()
                    {
                        triangles = chunkTriangles,
                        bounds = chunkBounds
                    });

                }
            }
        }
        return subMeshChunks;
    }
}
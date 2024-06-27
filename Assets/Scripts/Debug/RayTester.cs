using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RayTester : MonoBehaviour
{
    RayTracerHelper rayTracerHelper;
    Ray ray;
    // Start is called before the first frame update
    void Start()
    {
        rayTracerHelper = GetComponent<RayTracerHelper>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButton(1))
        {
            ray = GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
        }
    }
    void OnDrawGizmos()
    {
        GetRayTriangle();
    }
    void GetRayTriangle()
    {

        if (rayTracerHelper?.allBVHParentObjects == null) return;
        foreach (var bvhNode in rayTracerHelper.allBVHParentObjects)
        {
            Stack<BVHNode> stack = new();
            stack.Push(bvhNode);
            while (stack.Count > 0)
            {
                var currentNode = stack.Pop();
                if (currentNode.meshChunk.bounds.IntersectRay(ray))
                {
                    if (!currentNode.isLeaf)
                    {
                        stack.Push(currentNode.childA);
                        stack.Push(currentNode.childB);
                        currentNode.childA.parent = currentNode;
                        currentNode.childB.parent = currentNode;
                    }
                    else
                    {
                        foreach (var tri in currentNode.meshChunk.triangles)
                        {
                            if (CPURayTracer.hit_triangle(tri.ToStruct(), ray))
                            {
                                Gizmos.color = Color.green;
                                Gizmos.DrawLine(tri.a, tri.b);
                                Gizmos.DrawLine(tri.a, tri.c);
                                Gizmos.DrawLine(tri.b, tri.c);
                                BVHNode n = currentNode;
                                Gizmos.DrawWireCube(n.meshChunk.bounds.center, n.meshChunk.bounds.size);
                                // while (n != null)
                                // {
                                //     Gizmos.DrawWireCube(n.meshChunk.bounds.center, n.meshChunk.bounds.size);
                                //     n = n.parent;
                                // }
                            }
                            // else
                            // {
                            //     Gizmos.color = Color.red;
                            //     Gizmos.DrawLine(tri.Q, tri.Q + tri.v);
                            //     Gizmos.DrawLine(tri.Q, tri.Q + tri.u);
                            //     Gizmos.DrawLine(tri.Q + tri.v, tri.Q + tri.u);
                            // }
                        }
                    }
                }
            }
        }

        // foreach (int parentIndex in rayTracerHelper.allBvhParents)
        // {
        //     var bvhParent = rayTracerHelper.allBVHInfo[parentIndex];
        // }
        // BVHNodeStruct stack[8];
        // stack[0] = bvhNode;
        // int stackIndex = 1;
        // int safetyLimit = 100;
        // HitInfo closestHit = (HitInfo)0;
        // closestHit.dist = 1.#INF;
        //     while (safetyLimit-- > 0 && stackIndex > 0)
        // {
        //     BVHNodeStruct node = stack[--stackIndex];
        //     stats[0]++;
        //     if (hit_aabb(node.boundsMin, node.boundsMax, r))
        //     {
        //         if (node.childA == 0 && node.childB == 0)
        //         {
        //             HitInfo hit = (HitInfo)0;
        //             for (int i = 0; i < node.numTriangles; i++)
        //             {
        //                 Triangle tri = Triangles[node.triangleStartIndex + i];
        //                 stats[1]++;
        //                 hit = hit_triangle(tri, r);
        //                 if (hit.did_hit && hit.dist < closestHit.dist)
        //                 {
        //                     closestHit = hit;
        //                     closestHit.material = node.material;
        //                 }
        //             }
        //         }
        //         else
        //         {
        //             stack[stackIndex++] = BVHNodes[node.childA];
        //             stack[stackIndex++] = BVHNodes[node.childB];
        //         }
        //     }
        // }
        // return closestHit;
    }

}

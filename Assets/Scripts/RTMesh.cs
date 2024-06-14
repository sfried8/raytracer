
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RTMesh : MonoBehaviour
{
    public RTMaterial[] materials;

    [SerializeField, HideInInspector] int materialObjectID;
    [SerializeField, HideInInspector] bool materialInitFlag;
    [SerializeField] Vector3 boundsMin;
    [SerializeField] Vector3 boundsMax;
    [SerializeField] Vector3 transformedBoundsMin;
    [SerializeField] Vector3 transformedBoundsMax;

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
    void Update()
    {
        boundsMin = mesh.bounds.min;
        boundsMax = mesh.bounds.max;
        transformedBoundsMin = matchTransform(boundsMin, transform);
        transformedBoundsMax = matchTransform(boundsMax, transform);
    }
    void OnValidate()
    {
        if (!materialInitFlag)
        {
            materialInitFlag = true;
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
                    materials[i] = new Material(renderer.sharedMaterials[i]);
                }
                renderer.sharedMaterials = materials;

                materialObjectID = gameObject.GetInstanceID();
            }
            // renderer.sharedMaterial.color = material.color;
        }
    }
}
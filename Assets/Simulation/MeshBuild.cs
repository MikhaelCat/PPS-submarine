using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(int.MinValue)]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshBuild : MonoBehaviour
{
    [Header("Build")]
    [SerializeField] private bool buildOnAwake = true;
    [SerializeField] private bool includeInactiveChildren = true;
    [SerializeField] private bool deactivateSourceObjects = true;

    [Header("Collider")]
    [SerializeField] private bool rebuildMeshCollider = true;
    [SerializeField] private bool convexForDynamicRigidbody = true;
    [SerializeField] private bool disableOtherRootColliders = true;

    private Mesh generatedMesh;
    private bool isBuilt;

    private void Awake()
    {
        if (buildOnAwake)
        {
            BuildCombinedMesh();
        }
    }

    [ContextMenu("Build Combined Mesh")]
    public void BuildCombinedMesh()
    {
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(includeInactiveChildren);
        List<CombineInstance> combineInstances = new List<CombineInstance>();
        List<Material> materials = new List<Material>();
        HashSet<GameObject> sourceObjects = new HashSet<GameObject>();
        Matrix4x4 rootWorldToLocal = transform.worldToLocalMatrix;
        int totalVertexCount = 0;

        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter == null || meshFilter.transform == transform)
            {
                continue;
            }

            Mesh sourceMesh = meshFilter.sharedMesh;
            if (sourceMesh == null)
            {
                continue;
            }

            MeshRenderer sourceRenderer = meshFilter.GetComponent<MeshRenderer>();
            if (sourceRenderer == null)
            {
                continue;
            }

            totalVertexCount += sourceMesh.vertexCount;

            Matrix4x4 localMatrix = rootWorldToLocal * meshFilter.transform.localToWorldMatrix;
            int subMeshCount = Mathf.Max(1, sourceMesh.subMeshCount);
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                combineInstances.Add(new CombineInstance
                {
                    mesh = sourceMesh,
                    subMeshIndex = subMeshIndex,
                    transform = localMatrix
                });

                materials.Add(GetMaterialForSubMesh(sourceRenderer, subMeshIndex));
            }

            sourceObjects.Add(meshFilter.gameObject);
        }

        if (combineInstances.Count == 0)
        {
            Debug.LogWarning($"MeshBuild on {name} did not find child meshes to combine.", this);
            return;
        }

        ClearGeneratedMesh();

        generatedMesh = new Mesh
        {
            name = $"{name}_CombinedMesh"
        };

        if (totalVertexCount > 65535)
        {
            generatedMesh.indexFormat = IndexFormat.UInt32;
        }

        generatedMesh.CombineMeshes(combineInstances.ToArray(), false, true, false);
        generatedMesh.RecalculateBounds();

        MeshFilter targetMeshFilter = GetComponent<MeshFilter>();
        targetMeshFilter.sharedMesh = generatedMesh;

        MeshRenderer targetRenderer = GetComponent<MeshRenderer>();
        targetRenderer.sharedMaterials = materials.ToArray();

        if (rebuildMeshCollider)
        {
            RebuildCollider(generatedMesh);
        }

        if (deactivateSourceObjects)
        {
            foreach (GameObject sourceObject in sourceObjects)
            {
                sourceObject.SetActive(false);
            }
        }

        isBuilt = true;
    }

    private Material GetMaterialForSubMesh(MeshRenderer renderer, int subMeshIndex)
    {
        Material[] sharedMaterials = renderer.sharedMaterials;
        if (sharedMaterials == null || sharedMaterials.Length == 0)
        {
            return renderer.sharedMaterial;
        }

        if (subMeshIndex < sharedMaterials.Length)
        {
            return sharedMaterials[subMeshIndex];
        }

        return sharedMaterials[sharedMaterials.Length - 1];
    }

    private void RebuildCollider(Mesh combinedMesh)
    {
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }

        Rigidbody attachedRigidbody = GetComponent<Rigidbody>();
        if (attachedRigidbody != null && !attachedRigidbody.isKinematic)
        {
            meshCollider.convex = convexForDynamicRigidbody;
        }

        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = combinedMesh;

        if (disableOtherRootColliders)
        {
            Collider[] colliders = GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i] != meshCollider)
                {
                    colliders[i].enabled = false;
                }
            }
        }
    }

    private void OnDestroy()
    {
        ClearGeneratedMesh();
    }

    private void ClearGeneratedMesh()
    {
        if (!isBuilt || generatedMesh == null)
        {
            return;
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh == generatedMesh)
        {
            meshFilter.sharedMesh = null;
        }

        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider != null && meshCollider.sharedMesh == generatedMesh)
        {
            meshCollider.sharedMesh = null;
        }

        if (Application.isPlaying)
        {
            Destroy(generatedMesh);
        }
        else
        {
            DestroyImmediate(generatedMesh);
        }

        generatedMesh = null;
        isBuilt = false;
    }
}

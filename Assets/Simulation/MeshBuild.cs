using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(int.MinValue)]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshBuild : MonoBehaviour
{
    private const string GeneratedColliderObjectPrefix = "__GeneratedConvexCollider_";

    [Header("Build")]
    [SerializeField] private bool buildOnAwake = true;
    [SerializeField] private bool includeInactiveChildren = true;
    [SerializeField] private bool deactivateSourceObjects = true;

    [Header("Collider")]
    [SerializeField] private bool rebuildMeshCollider = true;
    [SerializeField] private bool convexForDynamicRigidbody = true;
    [SerializeField] private bool buildCompoundConvexForDynamic = true;
    [SerializeField] private int maxTrianglesPerConvexPart = 192;
    [SerializeField] private bool disableRootMeshColliderWhenCompound = true;
    [SerializeField] private bool disableOtherRootColliders = true;

    private Mesh generatedMesh;
    private readonly List<Mesh> generatedColliderMeshes = new List<Mesh>();
    private readonly List<GameObject> generatedColliderObjects = new List<GameObject>();
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
        bool requiresConvexCollider = attachedRigidbody != null && !attachedRigidbody.isKinematic && convexForDynamicRigidbody;

        if (requiresConvexCollider && buildCompoundConvexForDynamic)
        {
            BuildCompoundConvexColliders(combinedMesh, meshCollider);
        }
        else
        {
            ClearGeneratedCompoundColliders();

            meshCollider.enabled = true;
            meshCollider.convex = requiresConvexCollider;
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = combinedMesh;
        }

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
        ClearGeneratedCompoundColliders();

        if (generatedMesh == null)
        {
            isBuilt = false;
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

    private void BuildCompoundConvexColliders(Mesh combinedMesh, MeshCollider rootMeshCollider)
    {
        ClearGeneratedCompoundColliders();

        List<Mesh> colliderParts = BuildConvexColliderParts(combinedMesh);
        if (colliderParts.Count == 0)
        {
            rootMeshCollider.enabled = true;
            rootMeshCollider.convex = true;
            rootMeshCollider.sharedMesh = null;
            rootMeshCollider.sharedMesh = combinedMesh;
            Debug.LogWarning(
                $"MeshBuild on {name}: failed to build compound convex parts, fallback to single convex mesh.",
                this);
            return;
        }

        for (int i = 0; i < colliderParts.Count; i++)
        {
            Mesh partMesh = colliderParts[i];
            GameObject colliderObject = new GameObject($"{GeneratedColliderObjectPrefix}{i:D3}");
            colliderObject.transform.SetParent(transform, false);

            MeshCollider partCollider = colliderObject.AddComponent<MeshCollider>();
            partCollider.convex = true;
            partCollider.sharedMesh = partMesh;

            generatedColliderMeshes.Add(partMesh);
            generatedColliderObjects.Add(colliderObject);
        }

        rootMeshCollider.sharedMesh = null;
        rootMeshCollider.convex = true;

        if (disableRootMeshColliderWhenCompound)
        {
            rootMeshCollider.enabled = false;
        }
        else
        {
            rootMeshCollider.enabled = true;
            rootMeshCollider.sharedMesh = colliderParts[0];
        }
    }

    private List<Mesh> BuildConvexColliderParts(Mesh sourceMesh)
    {
        List<Mesh> result = new List<Mesh>();
        if (sourceMesh == null)
        {
            return result;
        }

        Vector3[] sourceVertices = sourceMesh.vertices;
        int[] sourceTriangles = sourceMesh.triangles;
        int triangleCount = sourceTriangles.Length / 3;
        if (triangleCount <= 0 || sourceVertices.Length == 0)
        {
            return result;
        }

        int trianglesPerPart = Mathf.Clamp(maxTrianglesPerConvexPart, 8, 255);
        int[] orderedTriangleIndices = GetTrianglesOrderedByLongestAxis(sourceVertices, sourceTriangles, sourceMesh.bounds);

        for (int startTriangle = 0; startTriangle < triangleCount; startTriangle += trianglesPerPart)
        {
            int count = Mathf.Min(trianglesPerPart, triangleCount - startTriangle);
            Mesh partMesh = BuildMeshPart(sourceVertices, sourceTriangles, orderedTriangleIndices, startTriangle, count);
            if (partMesh != null)
            {
                result.Add(partMesh);
            }
        }

        return result;
    }

    private int[] GetTrianglesOrderedByLongestAxis(Vector3[] vertices, int[] triangles, Bounds bounds)
    {
        int triangleCount = triangles.Length / 3;
        int[] ordered = new int[triangleCount];
        float[] keys = new float[triangleCount];

        int axis = GetLongestAxis(bounds.size);

        for (int triangle = 0; triangle < triangleCount; triangle++)
        {
            int triangleStart = triangle * 3;
            Vector3 center =
                (vertices[triangles[triangleStart]]
                + vertices[triangles[triangleStart + 1]]
                + vertices[triangles[triangleStart + 2]]) / 3f;

            keys[triangle] = axis == 0 ? center.x : axis == 1 ? center.y : center.z;
            ordered[triangle] = triangle;
        }

        System.Array.Sort(keys, ordered);
        return ordered;
    }

    private int GetLongestAxis(Vector3 size)
    {
        if (size.x >= size.y && size.x >= size.z)
        {
            return 0;
        }

        if (size.y >= size.z)
        {
            return 1;
        }

        return 2;
    }

    private Mesh BuildMeshPart(
        Vector3[] sourceVertices,
        int[] sourceTriangles,
        int[] orderedTriangleIndices,
        int startTriangle,
        int triangleCount)
    {
        Dictionary<int, int> remap = new Dictionary<int, int>(triangleCount * 3);
        List<Vector3> vertices = new List<Vector3>(triangleCount * 3);
        List<int> triangles = new List<int>(triangleCount * 3);

        for (int i = 0; i < triangleCount; i++)
        {
            int tri = orderedTriangleIndices[startTriangle + i];
            int triStart = tri * 3;

            int i0 = sourceTriangles[triStart];
            int i1 = sourceTriangles[triStart + 1];
            int i2 = sourceTriangles[triStart + 2];

            if (i0 == i1 || i1 == i2 || i0 == i2)
            {
                continue;
            }

            Vector3 v0 = sourceVertices[i0];
            Vector3 v1 = sourceVertices[i1];
            Vector3 v2 = sourceVertices[i2];
            if (Vector3.Cross(v1 - v0, v2 - v0).sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            int r0 = RemapIndex(i0, sourceVertices, remap, vertices);
            int r1 = RemapIndex(i1, sourceVertices, remap, vertices);
            int r2 = RemapIndex(i2, sourceVertices, remap, vertices);

            triangles.Add(r0);
            triangles.Add(r1);
            triangles.Add(r2);
        }

        if (triangles.Count < 3)
        {
            return null;
        }

        Mesh partMesh = new Mesh
        {
            name = $"{name}_ConvexPart"
        };

        if (vertices.Count > 65535)
        {
            partMesh.indexFormat = IndexFormat.UInt32;
        }

        partMesh.SetVertices(vertices);
        partMesh.SetTriangles(triangles, 0);
        partMesh.RecalculateBounds();
        partMesh.RecalculateNormals();

        return partMesh;
    }

    private int RemapIndex(
        int sourceIndex,
        Vector3[] sourceVertices,
        Dictionary<int, int> remap,
        List<Vector3> targetVertices)
    {
        if (remap.TryGetValue(sourceIndex, out int index))
        {
            return index;
        }

        int newIndex = targetVertices.Count;
        targetVertices.Add(sourceVertices[sourceIndex]);
        remap[sourceIndex] = newIndex;
        return newIndex;
    }

    private void ClearGeneratedCompoundColliders()
    {
        for (int i = 0; i < generatedColliderObjects.Count; i++)
        {
            GameObject colliderObject = generatedColliderObjects[i];
            if (colliderObject == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(colliderObject);
            }
            else
            {
                DestroyImmediate(colliderObject);
            }
        }
        generatedColliderObjects.Clear();

        for (int i = 0; i < generatedColliderMeshes.Count; i++)
        {
            Mesh colliderMesh = generatedColliderMeshes[i];
            if (colliderMesh == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(colliderMesh);
            }
            else
            {
                DestroyImmediate(colliderMesh);
            }
        }
        generatedColliderMeshes.Clear();

        // Чистим старые объекты после domain reload, когда списки могли обнулиться.
        List<Transform> staleChildren = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name.StartsWith(GeneratedColliderObjectPrefix))
            {
                staleChildren.Add(child);
            }
        }

        for (int i = 0; i < staleChildren.Count; i++)
        {
            if (Application.isPlaying)
            {
                Destroy(staleChildren[i].gameObject);
            }
            else
            {
                DestroyImmediate(staleChildren[i].gameObject);
            }
        }
    }
}

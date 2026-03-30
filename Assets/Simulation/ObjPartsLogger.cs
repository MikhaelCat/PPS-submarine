using System.Text;
using UnityEngine;

[ExecuteAlways]
public class ObjPartsLogger : MonoBehaviour
{
    public enum CoordinateMode
    {
        MeshVerticesAverage,
        MeshBoundsCenter,
        RendererBoundsCenter,
    }

    [Header("Logging")]
    [SerializeField] private bool includeInactive = true;
    [SerializeField] private CoordinateMode coordinateMode = CoordinateMode.MeshVerticesAverage;
    [SerializeField] private bool logOnStart = true;
    [SerializeField] private bool logEachPartSeparately = true;

    private void Start()
    {
        if (logOnStart)
        {
            LogPartsCoordinates();
        }
    }

    [ContextMenu("Log OBJ Parts Coordinates")]
    public void LogPartsCoordinates()
    {
        Transform[] childTransforms = GetComponentsInChildren<Transform>(includeInactive);
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(includeInactive);
        Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactive);
        StringBuilder builder = new StringBuilder();
        int loggedParts = 0;

        builder.AppendLine(
            $"OBJ parts for scene object \"{gameObject.name}\": transforms={childTransforms.Length}, meshFilters={meshFilters.Length}, renderers={renderers.Length}, mode={coordinateMode}");

        for (int i = 0; i < meshFilters.Length; i++)
        {
            Transform part = meshFilters[i].transform;
            Vector3 localPoint = GetLocalPoint(part, meshFilters[i], part.GetComponent<Renderer>());
            string line = $"sceneObject=\"{gameObject.name}\", part=\"{part.name}\", local=({localPoint.x:F4}, {localPoint.y:F4}, {localPoint.z:F4})";
            builder.AppendLine(line);
            if (logEachPartSeparately)
            {
                Debug.Log(line, part);
            }
            loggedParts++;
        }

        if (loggedParts == 0)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Transform part = renderers[i].transform;
                Vector3 localPoint = GetLocalPoint(part, null, renderers[i]);
                string line = $"sceneObject=\"{gameObject.name}\", part=\"{part.name}\", local=({localPoint.x:F4}, {localPoint.y:F4}, {localPoint.z:F4})";
                builder.AppendLine(line);
                if (logEachPartSeparately)
                {
                    Debug.Log(line, part);
                }
                loggedParts++;
            }
        }

        if (loggedParts == 0)
        {
            Debug.LogWarning(
                $"ObjPartsLogger on \"{gameObject.name}\" did not find any MeshFilter or Renderer in children or on the root object. transforms={childTransforms.Length}, meshFilters={meshFilters.Length}, renderers={renderers.Length}",
                this);
            return;
        }

        Debug.Log(builder.ToString(), this);
    }

    private Vector3 GetLocalPoint(Transform part, MeshFilter meshFilter, Renderer rendererComponent)
    {
        switch (coordinateMode)
        {
            case CoordinateMode.RendererBoundsCenter:
            {
                if (rendererComponent != null)
                {
                    return transform.InverseTransformPoint(rendererComponent.bounds.center);
                }
                break;
            }

            case CoordinateMode.MeshBoundsCenter:
            {
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    Vector3 worldCenter = part.TransformPoint(meshFilter.sharedMesh.bounds.center);
                    return transform.InverseTransformPoint(worldCenter);
                }

                if (rendererComponent != null)
                {
                    return transform.InverseTransformPoint(rendererComponent.bounds.center);
                }
                break;
            }

            case CoordinateMode.MeshVerticesAverage:
            default:
            {
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    Vector3[] vertices = meshFilter.sharedMesh.vertices;
                    if (vertices != null && vertices.Length > 0)
                    {
                        Vector3 localAverage = Vector3.zero;
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            localAverage += vertices[i];
                        }

                        localAverage /= vertices.Length;
                        Vector3 worldAverage = part.TransformPoint(localAverage);
                        return transform.InverseTransformPoint(worldAverage);
                    }
                }

                if (rendererComponent != null)
                {
                    return transform.InverseTransformPoint(rendererComponent.bounds.center);
                }
                break;
            }
        }

        return transform.InverseTransformPoint(part.position);
    }
}

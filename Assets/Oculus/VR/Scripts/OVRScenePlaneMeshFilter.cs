using Unity.Collections;
using UnityEngine;

/// <summary>
/// Generates a mesh that represents a plane's boundary.
/// </summary>
/// <remarks>
/// When added to a GameObject that represents a scene entity, such as a floor, ceiling, or desk, this component
/// generates a mesh from its boundary vertices.
/// </remarks>
[RequireComponent(typeof(MeshFilter))]
public class OVRScenePlaneMeshFilter : MonoBehaviour
{
	private MeshFilter _meshFilter;
	private Mesh _mesh;

	private void Start()
	{
		_mesh = new Mesh();
		_meshFilter = GetComponent<MeshFilter>();
		_meshFilter.sharedMesh = _mesh;

		CreateMeshFromBoundary();
	}

	private void CreateMeshFromBoundary()
	{
		var sceneAnchor = GetComponent<OVRSceneAnchor>();
		if (sceneAnchor == null) return;

		_mesh.name = $"OVRPlaneMeshFilter {sceneAnchor.Uuid}";

		var boundary = OVRPlugin.GetSpaceBoundary2D(sceneAnchor.Space, Allocator.Temp);
		if (!boundary.IsCreated) return;

		using (boundary)
		{
			OVRMeshGenerator.GenerateMesh(boundary, _mesh);
		}
	}
}

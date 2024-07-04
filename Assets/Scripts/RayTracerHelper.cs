using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Mathf;


public enum DebugDisplayMode
{
	NONE = 0,
	NORMALS = 1,
	BOXES = 2,
	TRIANGLES = 4,
	BOXES_AND_TRIANGLES = 8,

}
public enum AccumulateSetting
{
	WhileStatic,
	Always,
	Never
}
[Serializable]
public class RTDebugSettings
{
	public DebugDisplayMode debugDisplayMode;
	[SerializeField, Range(0, 1000)] public int boxTestCap;
	[SerializeField, Range(0, 1000)] public int triangleTestCap;
	public bool enableCPURayTest;


}
[Serializable]
public class RTObjectCounts
{
	public int triangles;
	public int bvhNodes;
	public int meshes;
}
[ExecuteAlways, ImageEffectAllowedInSceneView]
public class RayTracerHelper : MonoBehaviour
{
	// Raytracer is currently *very* slow, so limit the number of triangles allowed per mesh
	public const int TriangleLimit = 1500;

	[Header("Ray Tracing Settings")]
	[SerializeField, Range(0, 32)] int maxBounceCount = 4;
	[SerializeField, Range(0, 64)] int numRaysPerPixel = 2;
	[SerializeField, Min(0)] float defocusStrength = 0;
	[SerializeField, Min(0)] float divergeStrength = 0.3f;
	[SerializeField, Min(0)] float focusDistance = 1;
	[SerializeField] bool shouldReinitialize = false;
	[SerializeField, Range(1, 32)] int bvhDepthLimit;
	[SerializeField, Range(1, 32)] int bvhNumSplits = 5;
	// [SerializeField] EnvironmentSettings environmentSettings;

	[Header("View Settings")]
	[SerializeField] bool useShaderInSceneView;
	public AccumulateSetting accumulateSetting;


	public RTDebugSettings debugSettings = new();
	[Header("Info")]
	[SerializeField] int numRenderedFrames;
	public RTObjectCounts objectCounts = new();
	[SerializeField] float framesPerSecond = 0;

	[HideInInspector] public bool shouldAccumulate;


	// Materials and render textures
	Material rayTracingMaterial;
	Material accumulateMaterial;
	RenderTexture resultTexture;

	// Buffers
	ComputeBuffer sphereBuffer;
	ComputeBuffer triangleBuffer;
	ComputeBuffer bvhNodeBuffer;
	ComputeBuffer meshInfoBuffer;

	public List<TriangleStruct> allTriangles;


	bool clearAccumulate = false;

	bool initialized = false;
	private bool shouldSaveScreenshot = false;
	public List<BVHNodeStruct> allBVHInfo;
	public List<BVHNode> allBVHParentObjects;
	public List<MeshInfoStruct> allMeshes;
	public Dictionary<(Mesh, int), int> meshToBVHNodeIndex;
	string snapshotDirectory;
	int snapshotFrame;
	[Header("References")]
	[SerializeField] Shader rayTracingShader;
	[SerializeField] Shader accumulateShader;
	public RTAnimation rtAnimation;

	void Start()
	{
		numRenderedFrames = 0;
		shouldAccumulate = true;
		snapshotDirectory = "C://Users/Sam/Documents/bananas/" + DateTime.UtcNow.ToFileTimeUtc();
		snapshotFrame = 0;

		meshToBVHNodeIndex ??= new();
	}

	void TakeSnapshot()
	{
		if (!System.IO.Directory.Exists(snapshotDirectory))
		{
			System.IO.Directory.CreateDirectory(snapshotDirectory);
		}
		shouldSaveScreenshot = false;
		Texture2D renderedTexture = new Texture2D(Screen.width, Screen.height);
		renderedTexture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
		RenderTexture.active = null;
		byte[] byteArray = renderedTexture.EncodeToPNG();
		System.IO.File.WriteAllBytes(snapshotDirectory + "/" + (++snapshotFrame).ToString("D4") + ".png", byteArray);
	}
	bool ShouldTakeScreenshot()
	{
		if (shouldSaveScreenshot)
		{
			return true;
		}
		if (rtAnimation != null && rtAnimation.OnFrameComplete())
		{
			return true;
		}
		return false;
	}
	// Called after any camera (e.g. game or scene camera) has finished rendering into the src texture
	void OnRenderImage(RenderTexture src, RenderTexture target)
	{
		bool isSceneCam = Camera.current.name == "SceneCamera";

		if (isSceneCam)
		{
			if (useShaderInSceneView)
			{
				InitFrame(false);
				Graphics.Blit(null, target, rayTracingMaterial);
			}
			else
			{
				Graphics.Blit(src, target); // Draw the unaltered camera render to the screen
			}
		}
		else
		{
			InitFrame(true);

			RenderTexture prevFrameCopy = RenderTexture.GetTemporary(src.width, src.height, 0, ShaderHelper.RGBA_SFloat);

			// Run the ray tracing shader and draw the result to a temp texture
			rayTracingMaterial.SetInt("Frame", numRenderedFrames);
			RenderTexture currentFrame = RenderTexture.GetTemporary(src.width, src.height, 0, ShaderHelper.RGBA_SFloat);
			if (!clearAccumulate && (accumulateSetting == AccumulateSetting.Always || (shouldAccumulate && accumulateSetting == AccumulateSetting.WhileStatic)))
			{
				// Create copy of prev frame
				Graphics.Blit(resultTexture, prevFrameCopy);
				Graphics.Blit(null, currentFrame, rayTracingMaterial);

				// Accumulate
				accumulateMaterial.SetInt("_Frame", numRenderedFrames);
				accumulateMaterial.SetTexture("_PrevFrame", prevFrameCopy);
				Graphics.Blit(currentFrame, resultTexture, accumulateMaterial);
			}
			else
			{
				clearAccumulate = false;
				numRenderedFrames = 0;
				Graphics.Blit(null, resultTexture, rayTracingMaterial);
			}

			// Draw result to screen
			Graphics.Blit(resultTexture, target);

			if (ShouldTakeScreenshot())
			{
				TakeSnapshot();
				if (rtAnimation != null)
				{
					rtAnimation.NextStep();
					clearAccumulate = true;
				}
			}

			// Release temps
			RenderTexture.ReleaseTemporary(currentFrame);
			RenderTexture.ReleaseTemporary(prevFrameCopy);
			RenderTexture.ReleaseTemporary(currentFrame);

			numRenderedFrames += Application.isPlaying && shouldAccumulate ? 1 : 0;
			framesPerSecond = numRenderedFrames / Time.timeSinceLevelLoad;
		}
	}

	public void SaveScreenShot()
	{
		shouldSaveScreenshot = true;
	}
	void InitFrame(bool isGameCam)
	{

		// Create materials used in blits
		ShaderHelper.InitMaterial(rayTracingShader, ref rayTracingMaterial);
		ShaderHelper.InitMaterial(accumulateShader, ref accumulateMaterial);
		// Create result render texture
		ShaderHelper.CreateRenderTexture(ref resultTexture, Screen.width, Screen.height, FilterMode.Bilinear, ShaderHelper.RGBA_SFloat, "Result");

		// Update data
		UpdateCameraParams(Camera.current);
		allTriangles ??= new List<TriangleStruct>();
		allBVHInfo ??= new List<BVHNodeStruct>();
		allBVHParentObjects ??= new();
		allMeshes ??= new List<MeshInfoStruct>();
		CreateMeshes();
		if (!initialized)
		{
			createBuffersAndSend();
			CreateSpheres();
			initialized = true;
			shouldReinitialize = false;
		}
		SetShaderParams();

	}

	void createBuffersAndSend()
	{
		ShaderHelper.CreateStructuredBuffer(ref triangleBuffer, allTriangles);
		ShaderHelper.CreateStructuredBuffer(ref bvhNodeBuffer, allBVHInfo);
		rayTracingMaterial.SetBuffer("Triangles", triangleBuffer);
		rayTracingMaterial.SetInt("NumTriangles", allTriangles.Count);

		rayTracingMaterial.SetBuffer("BVHNodes", bvhNodeBuffer);
		rayTracingMaterial.SetInt("NumBVHNodes", allBVHInfo.Count);


	}
	void SetShaderParams()
	{
		rayTracingMaterial.SetInt("MaxBounces", maxBounceCount);
		rayTracingMaterial.SetInt("RaysPerPixel", numRaysPerPixel);
		rayTracingMaterial.SetFloat("DefocusStrength", defocusStrength);
		rayTracingMaterial.SetFloat("DivergeStrength", divergeStrength);
		rayTracingMaterial.SetInt("DebugDisplayMode", (int)debugSettings.debugDisplayMode);
		rayTracingMaterial.SetInt("BoxTestCap", debugSettings.boxTestCap);
		rayTracingMaterial.SetInt("TriangleTestCap", debugSettings.triangleTestCap);

		// rayTracingMaterial.SetInteger("EnvironmentEnabled", environmentSettings.enabled ? 1 : 0);
		// rayTracingMaterial.SetColor("GroundColour", environmentSettings.groundColour);
		// rayTracingMaterial.SetColor("SkyColourHorizon", environmentSettings.skyColourHorizon);
		// rayTracingMaterial.SetColor("SkyColourZenith", environmentSettings.skyColourZenith);
		// rayTracingMaterial.SetFloat("SunFocus", environmentSettings.sunFocus);
		// rayTracingMaterial.SetFloat("SunIntensity", environmentSettings.sunIntensity);
	}

	void UpdateCameraParams(Camera cam)
	{
		float planeHeight = focusDistance * Tan(cam.fieldOfView * 0.5f * Deg2Rad) * 2;
		float planeWidth = planeHeight * cam.aspect;
		// Send data to shader
		rayTracingMaterial.SetVector("ViewParams", new Vector3(planeWidth, planeHeight, focusDistance));
		rayTracingMaterial.SetMatrix("CamLocalToWorldMatrix", cam.transform.localToWorldMatrix);
	}

	Vector3 matchTransform(Vector3 localVec, Transform t)
	{
		return matchTransform(localVec, t.position, t.rotation, t.localScale);
	}
	Vector3 matchTransform(Vector3 localVec, Vector3 position, UnityEngine.Quaternion rotation, Vector3 localScale)
	{
		return position + rotation * Vector3.Scale(localVec, localScale);
	}
	public int CreateBVHForMesh(Mesh mesh, int subMeshIndex)
	{
		MeshChunk meshChunk = new MeshChunk()
		{
			triangles = new(),
			bounds = new(),
		};

		// ...
		SubMeshDescriptor subMeshDescriptor = mesh.GetSubMesh(subMeshIndex);

		int numTriangles = subMeshDescriptor.indexCount / 3;
		int subMeshDescriptorIndexStart = subMeshDescriptor.indexStart;
		Vector3[] meshVerts = mesh.vertices;
		int[] meshTris = mesh.triangles;
		var sw = System.Diagnostics.Stopwatch.StartNew();
		for (int triangleVertex = 0; triangleVertex < numTriangles; triangleVertex++)
		{

			Vector3 a = meshVerts[meshTris[subMeshDescriptorIndexStart + 3 * triangleVertex + 0]];
			Vector3 b = meshVerts[meshTris[subMeshDescriptorIndexStart + 3 * triangleVertex + 1]];
			Vector3 c = meshVerts[meshTris[subMeshDescriptorIndexStart + 3 * triangleVertex + 2]];
			Triangle t = new Triangle(a, b, c);
			meshChunk.bounds.Encapsulate(t.min, t.max);

			meshChunk.triangles.Add(t);
			// allTriangles.Add(triangle);
		}
		sw.Stop();
		Debug.Log($"Populating mesh chunk took {sw.Elapsed}");
		sw.Restart();
		// int depthLimit = (int)Clamp(Log(Pow(meshChunk.triangles.Count / 0.3f, 1.9f)) - 6.4f, 1, 20);
		// Debug.Log($"{mo.gameObject.name}: {meshChunk.triangles.Count} triangles, depth {depthLimit}");
		MeshSplitter.numSplitsToTest = bvhNumSplits;
		(List<BVHNodeStruct> bvhNodes, List<TriangleStruct> triangles, BVHNode parent) = BVH.CreateBVH(meshChunk, allBVHInfo.Count, allTriangles.Count, bvhDepthLimit);
		allBVHParentObjects.Add(parent);
		int bvhNodeIndex = allBVHInfo.Count;
		allBVHInfo.AddRange(bvhNodes);
		sw.Stop();
		Debug.Log($"creating bvh took {sw.Elapsed}");
		allTriangles.AddRange(triangles);
		initialized = false;
		return bvhNodeIndex;
	}
	public int GetBVHNodeIndexForMesh(Mesh mesh, int subMeshIndex)
	{
		if (!meshToBVHNodeIndex.ContainsKey((mesh, subMeshIndex)))
		{
			int newMeshBVHIndex = CreateBVHForMesh(mesh, subMeshIndex);
			meshToBVHNodeIndex[(mesh, subMeshIndex)] = newMeshBVHIndex;
			return newMeshBVHIndex;
		}
		return meshToBVHNodeIndex[(mesh, subMeshIndex)];
	}
	public void CreateMeshes()
	{
		RTMesh[] meshObjects = FindObjectsOfType<RTMesh>();

		allMeshes.Clear();

		for (int i = 0; i < meshObjects.Length; i++)
		{
			RTMesh mo = meshObjects[i];
			if (!mo.gameObject.activeInHierarchy)
			{
				continue;
			}
			Mesh mesh = mo.GetComponent<MeshFilter>().sharedMesh;
			for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
			{


				MeshInfoStruct meshInfo = new()
				{
					bvhNode = GetBVHNodeIndexForMesh(mesh, subMeshIndex),
					material = mo.materials[subMeshIndex],
					worldToLocalMatrix = mo.transform.worldToLocalMatrix,
					localToWorldMatrix = mo.transform.localToWorldMatrix
				};
				allMeshes.Add(meshInfo);
			}

		}
		ShaderHelper.CreateStructuredBuffer(ref meshInfoBuffer, allMeshes);

		// numMeshChunks = allBVHInfo.Count;
		rayTracingMaterial.SetBuffer("Meshes", meshInfoBuffer);
		rayTracingMaterial.SetInt("NumMeshes", allMeshes.Count);

		objectCounts.triangles = allTriangles.Count;
		objectCounts.bvhNodes = allBVHInfo.Count;
		objectCounts.meshes = allMeshes.Count;
	}


	void CreateSpheres()
	{
		// Create sphere data from the sphere objects in the scene
		RTSphere[] sphereObjects = FindObjectsOfType<RTSphere>();
		Sphere[] spheres = new Sphere[sphereObjects.Length];

		for (int i = 0; i < sphereObjects.Length; i++)
		{
			spheres[i] = new Sphere()
			{
				origin = sphereObjects[i].transform.position,
				radius = sphereObjects[i].transform.localScale.x * 0.5f,
				material = sphereObjects[i].material,
			};

		}


		// Create buffer containing all sphere data, and send it to the shader
		ShaderHelper.CreateStructuredBuffer(ref sphereBuffer, spheres);
		rayTracingMaterial.SetBuffer("Spheres", sphereBuffer);
		rayTracingMaterial.SetInt("NumSpheres", spheres.Length);
		// RTObjectCounts["Spheres"] = spheres.Length;
	}


	void OnDisable()
	{
		ShaderHelper.Release(sphereBuffer, triangleBuffer, bvhNodeBuffer, meshInfoBuffer);
		ShaderHelper.Release(resultTexture);
	}

	void OnValidate()
	{
		maxBounceCount = Mathf.Max(0, maxBounceCount);
		numRaysPerPixel = Mathf.Max(1, numRaysPerPixel);
		if (shouldReinitialize)
		{
			initialized = false;
		}
		// environmentSettings.sunFocus = Mathf.Max(1, environmentSettings.sunFocus);
		// environmentSettings.sunIntensity = Mathf.Max(0, environmentSettings.sunIntensity);

	}
	void OnDrawGizmos()
	{
		// Ray ray = GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
		// Debug.Log(ray.ToString());
		// Gizmos.DrawRay(ray.origin, ray.direction);
	}
}
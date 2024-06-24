using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.Mathf;

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
	[SerializeField] bool shouldSplitMeshes;
	[SerializeField] Vector3Int numSplits = Vector3Int.one;
	[SerializeField] int maxTrianglesPerChunk = 96;
	// [SerializeField] EnvironmentSettings environmentSettings;

	[Header("View Settings")]
	[SerializeField] bool useShaderInSceneView;
	[Header("Debug")]
	[SerializeField] bool displaySurfaceNormals;
	[Header("References")]
	[SerializeField] Shader rayTracingShader;
	[SerializeField] Shader accumulateShader;

	[Header("Info")]
	[SerializeField] int numRenderedFrames;
	[SerializeField] int numMeshChunks;
	[SerializeField] int numMeshParents;
	[SerializeField] int numTriangles;
	[SerializeField] int numSpheres;
	public enum AccumulateSetting
	{
		WhileStatic,
		Always,
		Never
	}
	public AccumulateSetting accumulateSetting;
	public bool shouldAccumulate;
	public bool showBoundingCorners;
	// Materials and render textures
	Material rayTracingMaterial;
	Material accumulateMaterial;
	RenderTexture resultTexture;

	// Buffers
	ComputeBuffer sphereBuffer;
	ComputeBuffer triangleBuffer;
	ComputeBuffer meshInfoBuffer;
	ComputeBuffer meshParentInfoBuffer;

	List<Triangle> allTriangles;


	bool clearAccumulate = false;

	bool initialized = false;
	private bool shouldSaveScreenshot = false;
	List<MeshInfo> allMeshInfo;
	List<MeshParent> allMeshParentInfo;
	string snapshotDirectory;
	int snapshotFrame;
	public RTAnimation rtAnimation;

	void Start()
	{
		numRenderedFrames = 0;
		shouldAccumulate = true;
		snapshotDirectory = "C://Users/Sam/Documents/bananas/" + DateTime.UtcNow.ToFileTimeUtc();
		snapshotFrame = 0;
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
		if (!initialized)
		{
			CreateMeshes();
			CreateSpheres();
			initialized = true;
			shouldReinitialize = false;
		}
		SetShaderParams(isGameCam);

	}

	void SetShaderParams(bool isGameCam)
	{
		rayTracingMaterial.SetInt("MaxBounces", maxBounceCount);
		rayTracingMaterial.SetInt("RaysPerPixel", isGameCam ? numRaysPerPixel : Min(numRaysPerPixel, 2));
		rayTracingMaterial.SetFloat("DefocusStrength", defocusStrength);
		rayTracingMaterial.SetFloat("DivergeStrength", divergeStrength);
		rayTracingMaterial.SetInt("DisplayNormals", displaySurfaceNormals ? 1 : 0);

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
		return t.position + t.rotation * Vector3.Scale(localVec, t.localScale);
	}
	void CreateMeshes()
	{
		RTMesh[] meshObjects = FindObjectsOfType<RTMesh>();

		allTriangles ??= new List<Triangle>();
		allMeshInfo ??= new List<MeshInfo>();
		allMeshParentInfo ??= new List<MeshParent>();
		allTriangles.Clear();
		allMeshInfo.Clear();
		allMeshParentInfo.Clear();

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

				SubMeshDescriptor subMeshDescriptor = mesh.GetSubMesh(subMeshIndex);
				MeshChunk meshChunk = new MeshChunk()
				{
					triangles = new List<Triangle>(),
					bounds = new Bounds(matchTransform(mesh.vertices[mesh.triangles[subMeshDescriptor.indexStart]], mo.transform), Vector3.one * 0.1f)
				};
				// MeshInfo meshInfo = new MeshInfo()
				// {
				// numTriangles = subMeshDescriptor.indexCount / 3,
				// triangleStartIndex = allTriangles.Count,
				// material = mo.materials[subMeshIndex],
				// };
				for (int triangleVertex = 0; triangleVertex < subMeshDescriptor.indexCount / 3; triangleVertex += 1)
				{
					Vector3 a = matchTransform(mesh.vertices[mesh.triangles[subMeshDescriptor.indexStart + 3 * triangleVertex + 0]], mo.transform);
					Vector3 b = matchTransform(mesh.vertices[mesh.triangles[subMeshDescriptor.indexStart + 3 * triangleVertex + 1]], mo.transform);
					Vector3 c = matchTransform(mesh.vertices[mesh.triangles[subMeshDescriptor.indexStart + 3 * triangleVertex + 2]], mo.transform);
					meshChunk.bounds.Encapsulate(a);
					meshChunk.bounds.Encapsulate(b);
					meshChunk.bounds.Encapsulate(c);

					Triangle triangle = new Triangle()
					{
						Q = a,
						u = b - a,
						v = c - a,
					};
					meshChunk.triangles.Add(triangle);
					// allTriangles.Add(triangle);
				}
				List<MeshChunk> subMeshChunks = shouldSplitMeshes ? MeshSplitter.FindBestSplit(meshChunk, maxTrianglesPerChunk) : new List<MeshChunk>(new MeshChunk[] { meshChunk });
				MeshParent meshParent = new MeshParent()
				{
					meshStartIndex = allMeshInfo.Count,
					numMeshes = subMeshChunks.Count,
					boundsMin = meshChunk.bounds.min,
					boundsMax = meshChunk.bounds.max
				};
				foreach (MeshChunk subMeshChunk in subMeshChunks)
				{
					MeshInfo meshInfo = new MeshInfo()
					{
						numTriangles = subMeshChunk.triangles.Count,
						triangleStartIndex = allTriangles.Count,
						material = mo.materials[subMeshIndex],
						boundsMin = subMeshChunk.bounds.min,
						boundsMax = subMeshChunk.bounds.max
					};
					allTriangles.AddRange(subMeshChunk.triangles);
					meshInfo.material.SetInverseCheckerScale();
					allMeshInfo.Add(meshInfo);
				}
				allMeshParentInfo.Add(meshParent);
			}

		}

		// numMeshChunks = allMeshInfo.Count;
		numTriangles = allTriangles.Count;
		ShaderHelper.CreateStructuredBuffer(ref triangleBuffer, allTriangles);
		ShaderHelper.CreateStructuredBuffer(ref meshInfoBuffer, allMeshInfo);
		ShaderHelper.CreateStructuredBuffer(ref meshParentInfoBuffer, allMeshParentInfo);
		rayTracingMaterial.SetBuffer("Triangles", triangleBuffer);
		rayTracingMaterial.SetInt("NumTriangles", numTriangles);
		// numSpheres = numTriangles * 3;
		// Sphere[] spheres = new Sphere[numSpheres];

		// for (int i = 0; i < allTriangles.Count; i++)
		// {
		// 	Triangle t = allTriangles[i];
		// 	spheres[3 * i] = new Sphere()
		// 	{
		// 		origin = t.x,
		// 		radius = 0.05f,
		// 		material = t.material
		// 	};
		// 	spheres[3 * i + 1] = new Sphere()
		// 	{
		// 		origin = t.y,
		// 		radius = 0.05f,
		// 		material = t.material
		// 	};
		// 	spheres[3 * i + 2] = new Sphere()
		// 	{
		// 		origin = t.z,
		// 		radius = 0.05f,
		// 		material = t.material
		// 	};
		// }
		// for (int i = 0; i < spheres.Length; i++)
		// {
		// 	Sphere s = spheres[i];
		// 	Debug.Log(i.ToString() + s.origin);
		// }
		// ShaderHelper.CreateStructuredBuffer(ref sphereBuffer, spheres);
		// rayTracingMaterial.SetBuffer("Spheres", sphereBuffer);
		// rayTracingMaterial.SetInt("NumSpheres", numSpheres);
		numMeshChunks = allMeshInfo.Count;
		numMeshParents = allMeshParentInfo.Count;
		rayTracingMaterial.SetBuffer("Meshes", meshInfoBuffer);
		rayTracingMaterial.SetBuffer("MeshParents", meshParentInfoBuffer);
		rayTracingMaterial.SetInt("NumMeshParents", allMeshInfo.Count);
	}


	void CreateSpheres()
	{
		// Create sphere data from the sphere objects in the scene
		RTSphere[] sphereObjects = FindObjectsOfType<RTSphere>();
		Sphere[] spheres = new Sphere[sphereObjects.Length + (showBoundingCorners ? 8 * allMeshInfo.Count : 0)];

		for (int i = 0; i < sphereObjects.Length; i++)
		{
			spheres[i] = new Sphere()
			{
				origin = sphereObjects[i].transform.position,
				radius = sphereObjects[i].transform.localScale.x * 0.5f,
				material = sphereObjects[i].material,
			};
			spheres[i].material.SetInverseCheckerScale();

		}

		if (showBoundingCorners)
		{
			float baseRadius = 0.1f;
			for (int i = 0; i < allMeshInfo.Count; i++)
			{
				float boundingCornerRadius = baseRadius + (allMeshInfo[i].numTriangles / 300.0f);
				Vector3 mi = allMeshInfo[i].boundsMin;
				Vector3 ma = allMeshInfo[i].boundsMax;
				RTMaterial material = new RTMaterial();
				material.SetDefaultValues();
				material.specularProbability = 0;
				material.color = Color.HSVToRGB((i * 3.1415926f / allMeshInfo.Count) % 1.0f, 1, 0.8f);
				spheres[8 * i + sphereObjects.Length] = new Sphere()
				{
					origin = mi,
					radius = boundingCornerRadius,
					material = material
				};
				spheres[8 * i + sphereObjects.Length + 1] = new Sphere()
				{
					origin = new Vector3(mi.x, mi.y, ma.z),
					radius = boundingCornerRadius,
					material = material
				};
				spheres[8 * i + sphereObjects.Length + 2] = new Sphere()
				{
					origin = new Vector3(mi.x, ma.y, mi.z),
					radius = boundingCornerRadius,
					material = material
				};
				spheres[8 * i + sphereObjects.Length + 3] = new Sphere()
				{
					origin = new Vector3(ma.x, mi.y, mi.z),
					radius = boundingCornerRadius,
					material = material
				};
				spheres[8 * i + sphereObjects.Length + 4] = new Sphere()
				{
					origin = new Vector3(mi.x, ma.y, ma.z),
					radius = boundingCornerRadius,
					material = material
				};
				spheres[8 * i + sphereObjects.Length + 5] = new Sphere()
				{
					origin = new Vector3(ma.x, mi.y, ma.z),
					radius = boundingCornerRadius,
					material = material
				};
				spheres[8 * i + sphereObjects.Length + 6] = new Sphere()
				{
					origin = new Vector3(ma.x, ma.y, mi.z),
					radius = boundingCornerRadius,
					material = material
				};
				spheres[8 * i + sphereObjects.Length + 7] = new Sphere()
				{
					origin = ma,
					radius = boundingCornerRadius,
					material = material
				};
			}
		}
		// Create buffer containing all sphere data, and send it to the shader
		ShaderHelper.CreateStructuredBuffer(ref sphereBuffer, spheres);
		rayTracingMaterial.SetBuffer("Spheres", sphereBuffer);
		numSpheres = spheres.Length;
		rayTracingMaterial.SetInt("NumSpheres", spheres.Length);
	}


	void OnDisable()
	{
		ShaderHelper.Release(sphereBuffer, triangleBuffer, meshInfoBuffer, meshParentInfoBuffer);
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
}
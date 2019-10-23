using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine.Rendering;
using Random = Unity.Mathematics.Random;

[RequireComponent(typeof(ConvertToEntity))]
[RequiresEntityConversion]
public class RoadGenerator : MonoBehaviour, IConvertGameObjectToEntity
{
	public int voxelCount=60;
	public float voxelSize = 1f;
	public int trisPerMesh = 4000;
	public Material roadMaterial;
	public Material carMaterial;
	public Mesh intersectionMesh;
	public Mesh carMesh;
	public float carSpeed=2f;
	public int numCars;

	bool[,,] trackVoxels;
	List<Intersection> intersections;
	List<TrackSpline> trackSplines;
	Intersection[,,] intersectionsGrid;

	Vector3Int[] dirs;
	Vector3Int[] fullDirs;

	public const float intersectionSize = .5f;
	public const float trackRadius = .2f;
	public const float trackThickness = .05f;
	public const int splineResolution=20;
	public const float carSpacing = .13f;

	const int instancesPerBatch=1023;


	// intersection pair:  two 32-bit IDs, packed together
	HashSet<long> intersectionPairs;

	List<Mesh> roadMeshes;
	List<List<Matrix4x4>> intersectionMatrices;

	void IConvertGameObjectToEntity.Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
		SpawnRoads();

		var intersectionStateBuffer = dstManager.AddBuffer<IntersectionStateElementData>(entity);
		intersectionStateBuffer.Reserve(intersections.Count);

		foreach (var intersection in intersections)
			intersectionStateBuffer.Add(new IntersectionStateElementData());

		var trackSplineStateBuffer = dstManager.AddBuffer<TrackSplineStateElementData>(entity);
		trackSplineStateBuffer.Reserve(trackSplines.Count);

		foreach (var trackSpline in trackSplines)
			trackSplineStateBuffer.Add(new TrackSplineStateElementData());

		var intersectionBuffer = dstManager.AddBuffer<IntersectionElementData>(entity);
		intersectionBuffer.Reserve(intersections.Count);

		foreach (var intersection in intersections)
		{
			var intersectionElement = new IntersectionElementData()
			{
				position = intersection.position,
				neighbouringSpline0 = -1,
				neighbouringSpline1 = -1,
				neighbouringSpline2 = -1,
			};

			var v = intersection.normal;
			intersectionElement.normal = new int3(v.x, v.y, v.z);

			for (var j = 0; j < intersection.neighborSplines.Count; ++j)
				intersectionElement[j] = trackSplines.IndexOf(intersection.neighborSplines[j]);

			intersectionBuffer.Add(intersectionElement);
		}

	    
	    // Etienne: add rendering entities for graph elements
	    var renderable = dstManager.CreateArchetype(typeof(RenderMesh), typeof(LocalToWorld));
	    SpawnRoadRenderables(dstManager, renderable);
	    SpawnIntersectionRenderables(dstManager, renderable);

	    var trackSplineBuffer = dstManager.AddBuffer<TrackSplineElementData>(entity);
	    trackSplineBuffer.Reserve(trackSplines.Count);

	    foreach (var trackSpline in trackSplines)
	    {
		    var trackSplineElement = new TrackSplineElementData()
		    {
			    startIntersection = intersections.IndexOf(trackSpline.startIntersection),
			    curve = new BezierData()
			    {
				    startPoint = trackSpline.startPoint,
				    endPoint = trackSpline.endPoint,
				    anchor1 = trackSpline.anchor1,
				    anchor2 = trackSpline.anchor2,
				    startNormal = new int3(trackSpline.startNormal.x, trackSpline.startNormal.y,
					    trackSpline.startNormal.z),
				    endNormal = new int3(trackSpline.endNormal.x, trackSpline.endNormal.y, trackSpline.endNormal.z),
				    startTangent = new int3(trackSpline.startTangent.x, trackSpline.startTangent.y,
					    trackSpline.startTangent.z),
				    endTangent = new int3(trackSpline.endTangent.x, trackSpline.endTangent.y, trackSpline.endTangent.z),
			    },
			    measuredLength = trackSpline.measuredLength,
			    maxCarCount = trackSpline.maxCarCount,
			    carQueueSize = trackSpline.carQueueSize
		    };

		    trackSplineBuffer.Add(trackSplineElement);
	    }
	    
	    SpawnCars(dstManager, entity, numCars, trackSplines.Count);
    }

	void SpawnRoadRenderables(EntityManager dstManager, EntityArchetype renderable)
	{
		List<Vector3> splineVertices = new List<Vector3>();
		List<Vector2> splineUvs = new List<Vector2>();
		List<int> splineTriangles = new List<int>();
	    
		List<Mesh> roadGeometries = new List<Mesh>();
		var index = 0;
		foreach (var trackSpline in trackSplines)
		{
			++index;
		   
			trackSpline.GenerateMesh(splineVertices, splineUvs, splineTriangles);
		   
			// start a new mesh
			if (splineTriangles.Count / 3 > trisPerMesh || index == trackSplines.Count)
			{
				// instanciate merged mesh
				var mesh = new Mesh();
				mesh.SetVertices(splineVertices);
				mesh.SetUVs(0, splineUvs);
				mesh.SetTriangles(splineTriangles, 0);
				mesh.RecalculateNormals();
				mesh.RecalculateBounds();
				roadGeometries.Add(mesh);
			    
				splineVertices.Clear();
				splineUvs.Clear();
				splineTriangles.Clear();
			}
		}
	    
		foreach (var mesh in roadGeometries)
		{
			var e = dstManager.CreateEntity(renderable);
			dstManager.SetComponentData(e, new LocalToWorld
			{
				Value = float4x4.identity
			});
			dstManager.SetSharedComponentData(e, new RenderMesh
			{
				mesh = mesh,
				castShadows = ShadowCastingMode.On,
				layer = 0,
				material = roadMaterial,
				receiveShadows = true,
				subMesh = 0
			});
		}
	}

	void SpawnIntersectionRenderables(EntityManager dstManager, EntityArchetype renderable)
	{
		var intersectionRenderMesh = new RenderMesh
		{
			mesh = intersectionMesh,
			castShadows = ShadowCastingMode.On,
			layer = 0,
			material = roadMaterial,
			receiveShadows = true,
			subMesh = 0
		};
		foreach (var intersection in intersections)
		{
			var e = dstManager.CreateEntity(renderable);
			dstManager.SetComponentData(e, new LocalToWorld
			{
				Value = intersection.GetMatrix()
			});
			dstManager.SetSharedComponentData(e, intersectionRenderMesh);
		}
	}

	void SpawnCars(EntityManager dstManager, Entity entity, int carCout, int roadCount)
	{

		var carArchetype = dstManager.CreateArchetype(
			typeof(RenderMesh),
			typeof(LocalToWorld),
			typeof(SplineT), 
			typeof(SplineSideDirection), 
			typeof(BezierData),
			typeof(Translation),
			typeof(Scale),
			typeof(Rotation));

		
		var materials = new List<Material>();
		for (int i = 0; i != 64; ++i)
		{
			var m = new Material(carMaterial);
			m.color = UnityEngine.Random.ColorHSV();
			materials.Add(m);
		}

		// car initialization data
		var colors = new List<float3>(); 
		var roadIndices = new List<int>();
		var lanes = new List<byte>();
		var velocities = new List<float>();
		var times = new List<float>();

		InitializeCars(carCout, roadCount, colors, roadIndices, lanes, velocities, times);

		int count = 4000;
		var random = new Random(0x6E624EB7u);
		
		var maxCarsPerRoad = (int)math.ceil(count / (float)roadCount);
		var createdCarCount = 0;

		int currentCarsOnRoad = 0;
		int currentRoadIndex = 0;
		
		var roads = dstManager.GetBuffer<TrackSplineElementData>(entity);
		var trackSpline = roads[currentRoadIndex];

		for (int i = 0; i < count; ++i)
		{
			var e = dstManager.CreateEntity(carArchetype);
			
			// TODO set component data based on init data
			// SplineT, BezierData, SplineSideDirection, Translation, Rotation
			dstManager.SetComponentData(e, new SplineT(){Value = random.NextFloat(.0f, .8f)});
			dstManager.SetComponentData(e, new SplineSideDirection()
			{
				DirectionValue = (byte)((i%2)*2),
				SideValue = (byte)(((i>>1)%2)*2)
			});

			dstManager.SetComponentData(e, new Scale { Value = 0.1f });
			dstManager.SetComponentData(e, trackSpline.curve);
			dstManager.SetSharedComponentData(e, new RenderMesh
				{
					mesh = carMesh,
					castShadows = ShadowCastingMode.On,
					layer = 0,
					material = materials[random.NextInt(materials.Count)],
					receiveShadows = true,
					subMesh = 0
				});

			currentCarsOnRoad++;

			if (currentCarsOnRoad >= maxCarsPerRoad)
			{
				currentCarsOnRoad = 0;
				currentRoadIndex++;
				roads = dstManager.GetBuffer<TrackSplineElementData>(entity);
				trackSpline = roads[currentRoadIndex];
			}
		}
	}

	// computes needed initial state for cars,
	// computed data is meant to be copied to relevant car archetype components
	void InitializeCars(
		int count, 
		int roadCount, // we assume road indices are in [0, roadCount - 1]
		List<float3> colors, 
		List<int> roadIndices, 
		List<byte> lanes, // bitmask 
		List<float> velocities, 
		List<float> times) // splineT
	{
		var random = new Random(0x6E624EB7u);
		var maxCarsPerRoad = (int)math.ceil(count / roadCount);
		var createdCarCount = 0;
			
		for (int roadIndex = 0; roadIndex < roadCount; ++roadIndex)
		{
			var carsOnRoad = math.min(maxCarsPerRoad, count - createdCarCount);
			createdCarCount += carsOnRoad;
			for (int i = 0; i < carsOnRoad; ++i)
			{
				colors.Add(random.NextFloat3());
				roadIndices.Add(roadIndex);
				lanes.Add((byte)(i % 4));
				velocities.Add(random.NextFloat(.4f, .8f));
				// we assume systems will "solve inconsistencies", if not we'll go for a more deterministic approach
				times.Add(random.NextFloat());
			}
		}
	}

	long HashIntersectionPair(Intersection a, Intersection b) {
		// pack two intersections' IDs into one int64
		int id1 = a.id;
		int id2 = b.id;

		return ((long)Mathf.Min(id1,id2) << 32) + Mathf.Max(id1,id2);
	}

	int Dot(Vector3Int a,Vector3Int b) {
		return a.x * b.x + a.y * b.y + a.z * b.z;
	}

	bool GetVoxel(Vector3Int pos,bool outOfBoundsValue=true) {
		return GetVoxel(pos.x,pos.y,pos.z,outOfBoundsValue);
	}
	bool GetVoxel(int x,int y,int z,bool outOfBoundsValue=true) {
		if (x >= 0 && x < voxelCount && y >= 0 && y < voxelCount && z >= 0 && z < voxelCount) {
			return trackVoxels[x,y,z];
		} else {
			return outOfBoundsValue;
		}
	}

	int CountNeighbors(Vector3Int pos,bool includeDiagonal = false) {
		return CountNeighbors(pos.x,pos.y,pos.z,includeDiagonal);
	}
	int CountNeighbors(int x, int y, int z, bool includeDiagonal = false) {
		int neighborCount = 0;

		Vector3Int[] dirList = dirs;
		if (includeDiagonal) {
			dirList = fullDirs;
		}

		for (int k = 0; k < dirList.Length; k++) {
			Vector3Int dir = dirList[k];
			if (GetVoxel(x + dir.x,y + dir.y,z + dir.z)) {
				neighborCount++;
			}
		}
		return neighborCount;
	}

	Intersection FindFirstIntersection(Vector3Int pos, Vector3Int dir, out Vector3Int otherDirection) {
		// step along our voxel paths (before splines have been spawned),
		// starting at one intersection, and stopping when we reach another intersection
		while (true) {
			pos += dir;
			if (intersectionsGrid[pos.x,pos.y,pos.z]!=null) {
				otherDirection = dir*-1;
				return intersectionsGrid[pos.x,pos.y,pos.z];
			}
			if (GetVoxel(pos+dir,false)==false) {
				bool foundTurn = false;
				for (int i=0;i<dirs.Length;i++) {
					if (dirs[i]!=dir && dirs[i]!=(dir*-1)) {
						if (GetVoxel(pos+dirs[i],false)==true) {
							dir = dirs[i];
							foundTurn = true;
							break;
						}
					}
				}
				if (foundTurn==false) {
					// dead end
					otherDirection = Vector3Int.zero;
					return null;
				}
			}
		}
	}

	void SpawnRoads() {

		// cardinal directions:
		dirs = new Vector3Int[] { new Vector3Int(1,0,0),new Vector3Int(-1,0,0),new Vector3Int(0,1,0),new Vector3Int(0,-1,0),new Vector3Int(0,0,1),new Vector3Int(0,0,-1) };

		// cardinal directions + diagonals in 3D:
		fullDirs = new Vector3Int[26];
		int dirIndex = 0;
		for (int x=-1;x<=1;x++) {
			for (int y=-1;y<=1;y++) {
				for (int z=-1;z<=1;z++) {
					if (x!=0 || y!=0 || z!=0) {
						fullDirs[dirIndex] = new Vector3Int(x,y,z);
						dirIndex++;
					}
				}
			}
		}

		// first generation pass: plan roads as basic voxel data only
		trackVoxels = new bool[voxelCount,voxelCount,voxelCount];
		List<Vector3Int> activeVoxels = new List<Vector3Int>();
		trackVoxels[voxelCount / 2,voxelCount / 2,voxelCount / 2] = true;
		activeVoxels.Add(new Vector3Int(voxelCount / 2,voxelCount / 2,voxelCount / 2));

		// after voxel generation, we'll convert our network into non-voxels
		intersections = new List<Intersection>();
		intersectionsGrid = new Intersection[voxelCount,voxelCount,voxelCount];
		intersectionPairs = new HashSet<long>();
		trackSplines = new List<TrackSpline>();
		roadMeshes = new List<Mesh>();
		intersectionMatrices = new List<List<Matrix4x4>>();

		// plan roads broadly: first, as a grid of true/false voxels
		int ticker = 0;
		while (activeVoxels.Count>0 && ticker<50000) {
			ticker++;
			int index = UnityEngine.Random.Range(0,activeVoxels.Count);
			Vector3Int pos = activeVoxels[index];
			Vector3Int dir = dirs[UnityEngine.Random.Range(0,dirs.Length)];
			Vector3Int pos2 = new Vector3Int(pos.x + dir.x,pos.y + dir.y,pos.z + dir.z);
			if (GetVoxel(pos2) == false) {
				// when placing a new voxel, it must have fewer than three
				// diagonal-or-cardinal neighbors.
				// (this blocks nonplanar intersections from forming)
				if (CountNeighbors(pos2,true) < 3) {
					activeVoxels.Add(pos2);
					trackVoxels[pos2.x,pos2.y,pos2.z] = true;
				}

			}

			int neighborCount = CountNeighbors(pos);
			if (neighborCount >= 3) {
				// no more than three cardinal neighbors for any voxel (no 4-way intersections allowed)
				// (really, this is to avoid nonplanar intersections)
				Intersection intersection = new Intersection(pos,(Vector3)pos*voxelSize,Vector3Int.zero);
				intersection.id = intersections.Count;
				intersections.Add(intersection);
				intersectionsGrid[pos.x,pos.y,pos.z] = intersection;
				activeVoxels.RemoveAt(index);
			}
		}

		Debug.Log(intersections.Count + " intersections");

		// at this point, we've generated our full layout, but everything
		// is voxels, and we've identified which voxels are intersections.
		// next, we'll reinterpret our voxels as a network of intersections:
		// we'll find all "neighboring intersections" in our voxel map
		// (neighboring intersections are connected by a chain of voxels,
		// which we'll replace with splines)

		for (int i=0;i<intersections.Count;i++) {
			Intersection intersection = intersections[i];
			Vector3Int axesWithNeighbors = Vector3Int.zero;
			for (int j=0;j<dirs.Length;j++) {
				if (GetVoxel(intersection.index+dirs[j],false)) {
					axesWithNeighbors.x += Mathf.Abs(dirs[j].x);
					axesWithNeighbors.y += Mathf.Abs(dirs[j].y);
					axesWithNeighbors.z += Mathf.Abs(dirs[j].z);

					Vector3Int connectDir;
					Intersection neighbor = FindFirstIntersection(intersection.index,dirs[j],out connectDir);
					if (neighbor!=null && neighbor!=intersection) {
						// make sure we haven't already added the reverse-version of this spline
						long hash = HashIntersectionPair(intersection,neighbor);
						if (intersectionPairs.Contains(hash)==false) {
							intersectionPairs.Add(hash);

							TrackSpline spline = new TrackSpline(intersection,dirs[j],neighbor,connectDir);
							trackSplines.Add(spline);

							intersection.neighbors.Add(neighbor);
							intersection.neighborSplines.Add(spline);
							neighbor.neighbors.Add(intersection);
							neighbor.neighborSplines.Add(spline);
						}
					}
				}
			}

			// find this intersection's normal - it's the one axis
			// along which we have no neighbors
			for (int j=0;j<3;j++) {
				if (axesWithNeighbors[j]==0) {
					if (intersection.normal == Vector3Int.zero) {
						intersection.normal = new Vector3Int();
						intersection.normal[j] = -1+UnityEngine.Random.Range(0,2)*2;
						//Debug.DrawRay(intersection.position,(Vector3)intersection.normal * .5f,Color.red,1000f);
					} else {
						Debug.LogError("a straight line has been marked as an intersection!");
					}
				}
			}
			if (intersection.normal==Vector3Int.zero) {
				Debug.LogError("nonplanar intersections are not allowed!");
			}

			// NOTE - if you investigate the above logic, you might be confused about how
			// dead-ends are given normals, since we're assuming that all intersections
			// have two axes with neighbors and only one axis without. dead-ends only have
			// one neighbor-axis...and somehow they still get a normal without a special case.
			//
			// the "gotcha" is that the visible dead-ends in the demo have three
			// neighbors during the voxel phase, with two of their neighbor chains leading
			// to nothing. these "hanging chains" are not included as splines, so the
			// dead-ends that we see are actually "T" shapes with the top two segments hidden.
		}

		Debug.Log(trackSplines.Count + " road splines");
	}

	void GenerateRenderingData()
	{
		// generate road meshes

		List<Vector3> vertices = new List<Vector3>();
		List<Vector2> uvs = new List<Vector2>();
		List<int> triangles = new List<int>();

		int triCount = 0;

		for (int i=0;i<trackSplines.Count;i++) {
			trackSplines[i].GenerateMesh(vertices,uvs,triangles);	

			if (triangles.Count/3>trisPerMesh || i==trackSplines.Count-1) {
				// our current mesh data is ready to go!
				if (triangles.Count > 0) {
					Mesh mesh = new Mesh();
					mesh.name = "Generated Road Mesh";
					mesh.SetVertices(vertices);
					mesh.SetUVs(0,uvs);
					mesh.SetTriangles(triangles,0);
					mesh.RecalculateNormals();
					mesh.RecalculateBounds();
					roadMeshes.Add(mesh);
					triCount += triangles.Count / 3;
				}

				vertices.Clear();
				uvs.Clear();
				triangles.Clear();
			}
		}

		// generate intersection matrices for batch-rendering
		int batch = 0;
		intersectionMatrices.Add(new List<Matrix4x4>());
		for (int i=0;i<intersections.Count;i++) {
			intersectionMatrices[batch].Add(intersections[i].GetMatrix());
			if (intersectionMatrices[batch].Count==instancesPerBatch) {
				batch++;
				intersectionMatrices.Add(new List<Matrix4x4>());
			}
		}

		Debug.Log(triCount + " road triangles ("+roadMeshes.Count+" meshes)");
	}
}

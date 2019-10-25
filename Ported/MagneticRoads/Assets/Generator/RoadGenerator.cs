using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using JetBrains.Annotations;
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
	public int voxelCount = 60;
	public float voxelSize = 1f;
	public int trisPerMesh = 4000;
	public Material roadMaterial;
	public Mesh intersectionMesh;
	public float carSpeed = 2f;
	public int numCars;
	public int maxGenerationTicks = 500000;

	bool[,,] trackVoxels;
	List<Intersection> intersections;
	List<TrackSpline> trackSplines;
	Intersection[,,] intersectionsGrid;

	Vector3Int[] dirs;
	Vector3Int[] fullDirs;

	public const float intersectionSize = .5f;
	public const float trackRadius = .2f;
	public const float trackThickness = .05f;
	public const int splineResolution = 20;
	public const float carSpacing = .13f;

	const int instancesPerBatch = 1023;


	// intersection pair:  two 32-bit IDs, packed together
	HashSet<long> intersectionPairs;

	List<Mesh> roadMeshes;
	List<List<Matrix4x4>> intersectionMatrices;

	private List<Entity> intersectionEntities;
	private List<Entity> roadEntities;

	void IConvertGameObjectToEntity.Convert(Entity entity, EntityManager dstManager,
		GameObjectConversionSystem conversionSystem)
	{
		intersectionEntities = new List<Entity>();
		roadEntities = new List<Entity>();

		SpawnRoads();

//		var intersectionStateBuffer = dstManager.AddBuffer<IntersectionStateElementData>(entity);
//		intersectionStateBuffer.Reserve(intersections.Count);
//
//		foreach (var intersection in intersections)
//			intersectionStateBuffer.Add(new IntersectionStateElementData());
//
//		var trackSplineStateBuffer = dstManager.AddBuffer<TrackSplineStateElementData>(entity);
//		trackSplineStateBuffer.Reserve(trackSplines.Count);
//
//		foreach (var trackSpline in trackSplines)
//			trackSplineStateBuffer.Add(new TrackSplineStateElementData());
//
//		var intersectionBuffer = dstManager.AddBuffer<IntersectionElementData>(entity);
//		intersectionBuffer.Reserve(intersections.Count);

		foreach (var intersection in intersections)
		{
			var neighbourCount = intersection.neighborSplines.Count;

//			var intersectionElement = new IntersectionElementData()
//			{
//				position = intersection.position,
//				normal = (Vector3) intersection.normal,
//				neighbouringSpline0 = -1,
//				neighbouringSpline1 = -1,
//				neighbouringSpline2 = -1,
//				neighbourCount = neighbourCount
//			};
//
//			for (var j = 0; j < neighbourCount; ++j)
//				intersectionElement[j] = trackSplines.IndexOf(intersection.neighborSplines[j]);
//
//			intersectionBuffer = dstManager.GetBuffer<IntersectionElementData>(entity);
//			intersectionBuffer.Add(intersectionElement);

			var interSectionEntity = dstManager.CreateEntity();
			dstManager.AddComponentData(interSectionEntity, new NeighborSpline());
			dstManager.AddComponentData(interSectionEntity, new IntersectionData());

			intersectionEntities.Add(interSectionEntity);
		}

		// Etienne: add rendering entities for graph elements
		var renderable = dstManager.CreateArchetype(typeof(RenderMesh), typeof(LocalToWorld));
		SpawnRoadRenderables(dstManager, renderable);
		SpawnIntersectionRenderables(dstManager, renderable);

//		var trackSplineBuffer = dstManager.AddBuffer<TrackSplineElementData>(entity);
//		trackSplineBuffer.Reserve(trackSplines.Count);

		foreach (var trackSpline in trackSplines)
		{
			var curve = new BezierData()
				{
					startPoint = trackSpline.startPoint,
					startNormal = (Vector3) trackSpline.startNormal,
					startTangent = (Vector3) trackSpline.startTangent,
					endPoint = trackSpline.endPoint,
					endNormal = (Vector3) trackSpline.endNormal,
					endTangent = (Vector3) trackSpline.endTangent,
					anchor1 = trackSpline.anchor1,
					anchor2 = trackSpline.anchor2,
					twistMode = trackSpline.twistMode
				};
//			var trackSplineElement = new TrackSplineElementData()
//			{
//				startIntersection = intersections.IndexOf(trackSpline.startIntersection),
//				endIntersection = intersections.IndexOf(trackSpline.endIntersection),
//              curve = curve,
//				measuredLength = trackSpline.measuredLength,
//				maxCarCount = trackSpline.maxCarCount,
//				carQueueSize = trackSpline.carQueueSize,
//				twistMode = trackSpline.twistMode
//			};
//			trackSplineBuffer = dstManager.GetBuffer<TrackSplineElementData>(entity);
//			trackSplineBuffer.Add(trackSplineElement);

			var roadEntity = dstManager.CreateEntity();
			roadEntities.Add(roadEntity);

			dstManager.AddComponentData(roadEntity, curve);
			dstManager.AddComponentData(roadEntity, new SplineLength() {Value = trackSpline.measuredLength});
			var rd = new RoadData()
			{
				startIntersection = intersectionEntities[intersections.IndexOf(trackSpline.startIntersection)],
				endIntersection = intersectionEntities[intersections.IndexOf(trackSpline.endIntersection)],
				capacity = trackSpline.maxCarCount,
			};
			dstManager.AddComponentData(roadEntity, rd);

			dstManager.AddBuffer<QueueData0>(roadEntity);
			dstManager.AddBuffer<QueueData1>(roadEntity);
			dstManager.AddBuffer<QueueData2>(roadEntity);
			dstManager.AddBuffer<QueueData3>(roadEntity);

			//we link the nodes

			var node = dstManager.GetComponentData<NeighborSpline>(rd.startIntersection);

			int index = node.IndexOf(Entity.Null);
			node[index] = roadEntity;
			dstManager.SetComponentData(rd.startIntersection, node);


			node = dstManager.GetComponentData<NeighborSpline>(rd.endIntersection);
			index = node.IndexOf(Entity.Null);
			node[index] = roadEntity;
			dstManager.SetComponentData(rd.endIntersection, node);
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

	Entity GetNextRoadEntity(List<Entity> freeRoads, ref Random random)
	{
		if (freeRoads.Count == 0)
		{
			return Entity.Null;
		}
		
		var lastIndex = freeRoads.Count - 1;
		
		int secondIndex = random.NextInt(0, lastIndex);

		var swap = freeRoads[secondIndex];
		freeRoads[secondIndex] = freeRoads[lastIndex] ;
		freeRoads[lastIndex] = swap;
		return swap;
	}

	public Entity CreateLane(EntityManager dstManager, Entity road,ref  RoadData roadData, ref  SplineSideDirection sideDirection)
	{
		BezierData laneCurve = dstManager.GetComponentData<BezierData>(road);

		var lane = dstManager.CreateEntity();
		
		dstManager.AddComponentData(lane,laneCurve);
		dstManager.AddComponentData(lane,sideDirection);
		dstManager.AddComponentData(lane,dstManager.GetComponentData<SplineLength>(road));
		dstManager.AddComponentData(lane,new RoadReference(){Value = road});
		dstManager.AddComponentData(lane, roadData);
		
		dstManager.AddBuffer<QueueData>(lane);
		return lane;
	}
	
	void SpawnCars(EntityManager dstManager, Entity entity, int carCount, int roadCount)
	{

		var carArchetype = dstManager.CreateArchetype(
			typeof(ColorData),
			typeof(LocalToWorld),
			typeof(SplineT), 
			typeof(SplineSideDirection), 
			typeof(BezierData),
			typeof(Translation),
			typeof(NonUniformScale),
			typeof(Next),
			typeof(Lane),
			typeof(NormalizedSpeed),
			typeof(Rotation));

		int count = carCount;
		var random = new Random(0x6E624EB7u);

		int currentCarsOnRoad = 0;
		int currentRoadIndex = 0;
		
//		var roads = dstManager.GetBuffer<TrackSplineElementData>(entity);
//		var roadStates = dstManager.GetBuffer<TrackSplineStateElementData>(entity);
//		var trackSpline = roads[currentRoadIndex];
//		var trackSplineState = roadStates[currentRoadIndex];

		List<Entity> freeRoads = new List<Entity>();
		freeRoads.AddRange(roadEntities);

		int freeRoadsCount = freeRoads.Count;
		for (int i = 0; i < freeRoadsCount; ++i)
		{
			int firstIndex = i;
			int secondIndex = random.NextInt(0, freeRoadsCount-1);

			var swap = freeRoads[firstIndex];
			freeRoads[firstIndex] = freeRoads[secondIndex];
			freeRoads[secondIndex] = swap;
		}

		var currentRoadEntity = GetNextRoadEntity(freeRoads, ref random);
		
		for (int i = 0; i < count; ++i)
		{
			if(freeRoads.Count == 0 || currentRoadEntity == Entity.Null)
				return;

			var rd = dstManager.GetComponentData<RoadData>(currentRoadEntity);
			
			var e = dstManager.CreateEntity(carArchetype);

			// TODO set component data based on init data
			// SplineT, BezierData, SplineSideDirection, Translation, Rotation

			bool added = false;

			SplineSideDirection laneDir= new SplineSideDirection();

			for (int tries = 0; tries < 4 && !added; ++tries)
			{
				laneDir = SplineSideDirection.GetDirectionForIndex(i + tries);

				switch (laneDir.QueueIndex())
				{
					case 0:
						added = TryAddCarToLane<Lane0>(dstManager, currentRoadEntity, ref rd, e, ref laneDir);
						break;
					case 1:
						added = TryAddCarToLane<Lane1>(dstManager, currentRoadEntity, ref rd, e, ref laneDir);
						break;
					case 2:
						added = TryAddCarToLane<Lane2>(dstManager, currentRoadEntity, ref rd, e, ref laneDir);
						break;
					case 3:
						added = TryAddCarToLane<Lane3>(dstManager, currentRoadEntity, ref rd, e, ref laneDir);
						break;
					default:
						break;
				}
			}

			if (!added)
			{
				freeRoads.RemoveAt(freeRoads.Count -1);
				i--;
				dstManager.DestroyEntity(e);
				currentRoadEntity = GetNextRoadEntity(freeRoads, ref random);
				continue;
			}

			dstManager.SetComponentData(e, laneDir);

			//we then assign it to a road
//			var lane = new Lane
//			{
//				splineSide = random.NextFloat() > 0.5 ? 1 : -1,
//				splineDirection = random.NextFloat() > 0.5 ? 1 : -1,
//				trackSplineIndex = currentRoadIndex
//			};

//			var next = new Next()
//			{
//				Value = trackSplineState.GetLastEntityIn(lane.splineSide, lane.splineDirection)
//			};
//
//			dstManager.SetComponentData(e, lane);
//			dstManager.SetComponentData(e, next);
			dstManager.SetComponentData(e, new NonUniformScale { Value = new float3(0.1f, 0.08f,0.12f) });
			dstManager.SetComponentData(e, dstManager.GetComponentData<BezierData>(currentRoadEntity));
			dstManager.AddComponentData(e, dstManager.GetComponentData<SplineLength>(currentRoadEntity));

			var color = UnityEngine.Random.ColorHSV();
			dstManager.SetComponentData(e, new ColorData
			{
				value = new float4(color.r, color.g, color.b, color.a)
			});
			

			currentCarsOnRoad++;

			currentRoadEntity = GetNextRoadEntity(freeRoads, ref random);

//			roadStates = dstManager.GetBuffer<TrackSplineStateElementData>(entity);
//			trackSplineState.SetLastEntityIn(lane.splineSide, lane.splineDirection, e);
//			var c = trackSplineState.GetCarCount(lane.splineSide, lane.splineDirection);
//			trackSplineState.SetCarCount(lane.splineSide, lane.splineDirection, c + 1);
//			roadStates[currentRoadIndex] = trackSplineState;
//
//			if (currentCarsOnRoad >= maxCarsPerRoad)
//			{
//				currentCarsOnRoad = 0;
//				currentRoadIndex++;
//				roads = dstManager.GetBuffer<TrackSplineElementData>(entity);
//				roadStates = dstManager.GetBuffer<TrackSplineStateElementData>(entity);
//				trackSpline = roads[currentRoadIndex];
//				trackSplineState = roadStates[currentRoadIndex];
//			}
		}
	}

	private bool TryAddCarToLane<LaneType>(EntityManager dstManager, Entity currentRoadEntity, ref RoadData rd, Entity e,
		ref SplineSideDirection laneDir) where LaneType:struct, IComponentData, ILaneRef
	{
		bool added = false;
		
		LaneType laneRef = new LaneType();

		if (dstManager.HasComponent<LaneType>(currentRoadEntity))
		{
			laneRef = dstManager.GetComponentData<LaneType>(currentRoadEntity);
		}
		else
		{
			laneRef.laneEntity = CreateLane(dstManager, currentRoadEntity, ref rd, ref laneDir);
			dstManager.AddComponentData<LaneType>(currentRoadEntity, laneRef);
		}

		var b = dstManager.GetBuffer<QueueData>(laneRef.laneEntity);
		if (b.Length < rd.capacity)
		{
			b.Add(new QueueData() {CarEntity = e});
			dstManager.AddComponentData(e, new RoadReference() {Value = laneRef.laneEntity});
			added = true;
		}

		return added;
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
		while (activeVoxels.Count>0 && ticker<maxGenerationTicks) {
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

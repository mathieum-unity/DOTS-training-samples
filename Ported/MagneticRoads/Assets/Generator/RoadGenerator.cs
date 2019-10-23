using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine.Rendering;

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
	public float carSpeed=2f;

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

		var trackSplineBuffer = dstManager.AddBuffer<TrackSplineElementData>(entity);
		trackSplineBuffer.Reserve(trackSplines.Count);

		foreach (var trackSpline in trackSplines)
		{
			var trackSplineElement = new TrackSplineElementData()
			{
				startIntersection = intersections.IndexOf(trackSpline.startIntersection),
				startPoint = trackSpline.startPoint,
				endPoint = trackSpline.endPoint,
				anchor1 = trackSpline.anchor1,
				anchor2 = trackSpline.anchor2,
				measuredLength = trackSpline.measuredLength,
				maxCarCount = trackSpline.maxCarCount,
				carQueueSize = trackSpline.carQueueSize
			};

			var v = trackSpline.startNormal;
			trackSplineElement.startNormal = new int3(v.x, v.y, v.z);
			v = trackSpline.startTangent;
			trackSplineElement.startTangent = new int3(v.x, v.y, v.z);
			v = trackSpline.endNormal;
			trackSplineElement.endNormal = new int3(v.x, v.y, v.z);
			v = trackSpline.endTangent;
			trackSplineElement.endTangent = new int3(v.x, v.y, v.z);

			trackSplineBuffer.Add(trackSplineElement);
		}
	    
	    // Etienne: add rendering entities for graph elements

	    List<Vector3> mergedVertices = new List<Vector3>();
	    List<Vector2> mergedUvs = new List<Vector2>();
	    List<int> mergedTriangles = new List<int>();
	    
	    List<Vector3> splineVertices = new List<Vector3>();
	    List<Vector2> splineUvs = new List<Vector2>();
	    List<int> splineTriangles = new List<int>();
	    
	    List<Mesh> mergedRoadGeometries = new List<Mesh>();
	    var triCount = 0;

	    foreach (var trackSpline in trackSplines)
	    {
		    splineVertices.Clear();
		    splineUvs.Clear();
		    splineTriangles.Clear();
		    trackSpline.GenerateMesh(splineVertices, splineUvs, splineTriangles);

			// start a new mesh
		    if (triCount + splineTriangles.Count > trisPerMesh)
		    {
			    var mesh = new Mesh();
			    mesh.vertices = mergedVertices.ToArray();
			    mesh.uv = mergedUvs.ToArray();
			    mesh.triangles = mergedTriangles.ToArray();
			    mergedRoadGeometries.Add(mesh);
			    
			    mergedVertices.Clear();
			    mergedUvs.Clear();
			    mergedTriangles.Clear();
			    triCount = 0;
		    }
		    else
		    {
			    triCount += splineTriangles.Count;
			    mergedVertices.AddRange(splineVertices);
			    mergedUvs.AddRange(splineUvs);
			    mergedTriangles.AddRange(splineTriangles);
		    }
	    }
	    
	    var lastMesh = new Mesh();
	    lastMesh.vertices = mergedVertices.ToArray();
	    lastMesh.uv = mergedUvs.ToArray();
	    lastMesh.triangles = mergedTriangles.ToArray();
	    mergedRoadGeometries.Add(lastMesh);
	    
	    var renderable = dstManager.CreateArchetype(typeof(MeshRenderer), typeof(MeshFilter), typeof(LocalToWorld));
	    foreach (var mesh in mergedRoadGeometries)
	    {
		    var e = dstManager.CreateEntity(renderable);
		    dstManager.AddComponentData(e, new LocalToWorld
		    {
			    Value = float4x4.identity
		    });
		    dstManager.SetComponentData(e, new RenderMesh
		    {
			    mesh = mesh,
			    castShadows = ShadowCastingMode.On,
			    layer = 0,
			    material = roadMaterial,
			    receiveShadows = true,
			    subMesh = 0
		    });
	    }

	    var index = 0;
	    foreach (var intersection in intersections)
	    {
		    var e = dstManager.CreateEntity(renderable);
		    dstManager.AddComponentData(e, new LocalToWorld
		    {
			    Value = intersection.GetMatrix()
		    });
		    dstManager.SetComponentData(e, new RenderMesh
		    {
			    mesh = intersectionMesh,
			    castShadows = ShadowCastingMode.On,
			    layer = 0,
			    material = roadMaterial,
			    receiveShadows = true,
			    subMesh = 0
		    });
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

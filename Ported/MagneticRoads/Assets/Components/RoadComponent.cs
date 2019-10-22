using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct IntersectionElementData : IBufferElementData
{
	public float3 position;
	public int3 normal;
	public int neighbouringSpline0;
	public int neighbouringSpline1;
	public int neighbouringSpline2;

	public int this[int index]
	{
		get
		{
			switch (index)
			{
				case 0: return neighbouringSpline0;
				case 1: return neighbouringSpline1;
				case 2: return neighbouringSpline2;
			};

			throw new System.IndexOutOfRangeException();
		}
		set
		{
			switch (index)
			{
				case 0: neighbouringSpline0 = value; return;
				case 1: neighbouringSpline1 = value; return;
				case 2: neighbouringSpline2 = value; return;
			};

			throw new System.IndexOutOfRangeException();
		}
	}
}

public struct TrackSplineElementData : IBufferElementData
{
	public int startIntersection;
	public float3 startPoint;
	public int3 startNormal;
	public int3 startTangent;
	public int endIntersection;
	public float3 endPoint;
	public int3 endNormal;
	public int3 endTangent;
	public float3 anchor1;
	public float3 anchor2;
	public int maxCarCount;
	public float measuredLength;
	public float carQueueSize;
}

public struct RoadComponent : IComponentData
{
    public Entity edge; // a road corresponds to a graph edge
    
    public NativeList<Entity> topLeftLane;
    public NativeList<Entity> topRightLane;
    public NativeList<Entity> bottomLeftLane;
    public NativeList<Entity> bottomRightLane;

    public const int k_LanesPerRoad = 4;
    
    // lets us iterate over lanes
    static int GetLaneIndex(bool isLeft, bool isTop)
    {
        return (isLeft ? 0 : 1) + (isTop ? 0 : 2);
    }

    public NativeList<Entity> GetLane(bool isLeft, bool isTop)
    {
        switch (GetLaneIndex(isLeft, isTop))
        {
            case 0: return topLeftLane;
            case 1: return topRightLane;
            case 2: return bottomLeftLane;
            default: return bottomRightLane;
        }
    }
}

public struct IntersectionHandle : IComponentData
{
    public Entity Value;
}

public struct Edge : IComponentData
{
    public int start, end;
}

public struct Node : IComponentData
{
    public int start, end;
}




public struct Velocity : IComponentData
{
    public float Value;
}

public struct LaneIndex : IComponentData
{
    public byte Value;

    public bool IsLeft => (Value & 1) == 0;
}

// purpose: group car entities in chunks according to their lanes / roads
// cars that belong to the same lane should be nearby in memory
public struct Lane : ISharedComponentData
{
    public Entity road;
    public int laneIndex;
}

public class Somewhere
{

    
}

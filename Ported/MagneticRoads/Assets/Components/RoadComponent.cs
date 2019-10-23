using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct Lane : IComponentData
{
    public int splineDirection;
    public int splineSide;
    public int trackSplineIndex;
}

public struct NormalizedSpeed : IComponentData
{
    public float Value;
}

public struct InIntersection : IComponentData
{

}

public struct Next : IComponentData
{
    public Entity value;
}

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

public struct IntersectionStateElementData : IBufferElementData
{
    public bool occupied0;
    public bool occupied1;

    public bool this[int index]
    {
        get
        {
            switch (index)
            {
                case 0: return occupied0;
                case 1: return occupied1;
            }

            throw new System.IndexOutOfRangeException();
        }
        set
        {
            switch (index)
            {
                case 0: occupied0 = value; return;
                case 1: occupied1 = value; return;
            }

            throw new System.IndexOutOfRangeException();
        }
    }
}

public struct TrackSplineElementData : IBufferElementData
{
	public int startIntersection;
    public int endIntersection;
	public BezierData curve;
	public int maxCarCount;
	public float measuredLength;
	public float carQueueSize;
}

/*public struct RoadComponent : IComponentData
{
    public int trackSplineIndex;
    
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
}*/

public struct TrackSplineStateElementData : IBufferElementData
{
    public int carCount0;
    public int carCount1;
    public int carCount2;
    public int carCount3;
    public Entity lastEntity0;
    public Entity lastEntity1;
    public Entity lastEntity2;
    public Entity lastEntity3;
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

public class Somewhere
{

    
}

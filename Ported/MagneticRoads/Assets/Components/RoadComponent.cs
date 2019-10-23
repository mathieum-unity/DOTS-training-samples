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
    public Entity Value;
}

public struct IntersectionElementData : IBufferElementData
{
	public float3 position;
	public float3 normal;
	public int neighbouringSpline0;
	public int neighbouringSpline1;
	public int neighbouringSpline2;
    public int neighbourCount;

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

    public int IndexOf(int value)
    {
        for (var i = 0; i < neighbourCount; ++i)
            if (this[i] == value) return i;

        return -1;
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
	public float3 startPoint;
	public float3 startNormal;
	public float3 startTangent;
	public int endIntersection;
	public float3 endPoint;
	public float3 endNormal;
	public float3 endTangent;
	public float3 anchor1;
	public float3 anchor2;
	public int maxCarCount;
	public float measuredLength;
	public float carQueueSize;
    public int twistMode;
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

    int GetLaneIndex(int splineSide, int splineDirection)
    {
        return (splineDirection > 0 ? 0 : 1) + (splineSide > 0 ? 0 : 2);
    }
    
    public int GetCarCount(int splineSide, int splineDirection)
    {
        var index = GetLaneIndex(splineSide, splineDirection);

        switch (index)
        {
            case 0: return carCount0;
            case 1: return carCount1;
            case 2: return carCount2;
            case 3: return carCount3;
        }

        throw new System.IndexOutOfRangeException();
    }

    public void SetCarCount(int splineSide, int splineDirection, int value)
    {
        var index = GetLaneIndex(splineSide, splineDirection);

        switch (index)
        {
            case 0: carCount0 = value; return;
            case 1: carCount1 = value; return;
            case 2: carCount2 = value; return;
            case 3: carCount3 = value; return;
        }

        throw new System.IndexOutOfRangeException();
    }

    public Entity GetLastEntity(int splineSide, int splineDirection)
    {
        var index = GetLaneIndex(splineSide, splineDirection);

        switch (index)
        {
            case 0: return lastEntity0;
            case 1: return lastEntity1;
            case 2: return lastEntity2;
            case 3: return lastEntity3;
        }

        throw new System.IndexOutOfRangeException();
    }

    public void SetLastEntity(int splineSide, int splineDirection, Entity value)
    {
        var index = GetLaneIndex(splineSide, splineDirection);

        switch (index)
        {
            case 0: lastEntity0 = value; return;
            case 1: lastEntity1 = value; return;
            case 2: lastEntity2 = value; return;
            case 3: lastEntity3 = value; return;
        }

        throw new System.IndexOutOfRangeException();
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

public class Somewhere
{

    
}

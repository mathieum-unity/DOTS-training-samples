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
    public int endIntersection;
	public BezierData curve;
	public int maxCarCount;
	public float measuredLength;
	public float carQueueSize;
    public int twistMode;
}

public struct TrackSplineStateElementData : IBufferElementData
{
    public int carCount0;
    public int carCount1;
    public int carCount2;
    public int carCount3;
    public Entity lastEntityIn0;
    public Entity lastEntityIn1;
    public Entity lastEntityIn2;
    public Entity lastEntityIn3;
    public Entity lastEntityOut0;
    public Entity lastEntityOut1;
    public Entity lastEntityOut2;
    public Entity lastEntityOut3;

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

    public Entity GetLastEntityIn(int splineSide, int splineDirection)
    {
        var index = GetLaneIndex(splineSide, splineDirection);

        switch (index)
        {
            case 0: return lastEntityIn0;
            case 1: return lastEntityIn1;
            case 2: return lastEntityIn2;
            case 3: return lastEntityIn3;
        }

        throw new System.IndexOutOfRangeException();
    }

    public void SetLastEntityIn(int splineSide, int splineDirection, Entity value)
    {
        var index = GetLaneIndex(splineSide, splineDirection);

        switch (index)
        {
            case 0: lastEntityIn0 = value; return;
            case 1: lastEntityIn1 = value; return;
            case 2: lastEntityIn2 = value; return;
            case 3: lastEntityIn3 = value; return;
        }

        throw new System.IndexOutOfRangeException();
    }

    public Entity GetLastEntityOut(int splineSide, int splineDirection)
    {
        var index = GetLaneIndex(splineSide, splineDirection);

        switch (index)
        {
            case 0: return lastEntityOut0;
            case 1: return lastEntityOut1;
            case 2: return lastEntityOut2;
            case 3: return lastEntityOut3;
        }

        throw new System.IndexOutOfRangeException();
    }

    public void SetLastEntityOut(int splineSide, int splineDirection, Entity value)
    {
        var index = GetLaneIndex(splineSide, splineDirection);

        switch (index)
        {
            case 0: lastEntityOut0 = value; return;
            case 1: lastEntityOut1 = value; return;
            case 2: lastEntityOut2 = value; return;
            case 3: lastEntityOut3 = value; return;
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

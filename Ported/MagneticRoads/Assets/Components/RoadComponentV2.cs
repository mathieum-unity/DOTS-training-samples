using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public interface IQueueEntry
{
    Entity carId { get; }
    float NormalizedSpeed { get; set; }
    float SplineTimer { get; set; }
}

public struct QueueData : IBufferElementData
{
    public Entity CarEntity;
    public SplineTConstraints constraints;
    public float NormalizedSpeed;
    public float SplineTimer;
    public Entity carId => CarEntity;
}
public struct QueueData0 : IBufferElementData, IQueueEntry
{
    public Entity Value;
    public SplineTConstraints c;
    public float m_NormalizedSpeed;
    public float m_SplineTimer;
    public Entity carId => Value;
    public SplineTConstraints constraints
    {
        get => c;
        set => c = value;
    }

    public float NormalizedSpeed
    {
        get => m_NormalizedSpeed;
        set => m_NormalizedSpeed = value;
    }

    public float SplineTimer
    {
        get => m_SplineTimer;
        set => m_SplineTimer = value;
    }
}

public struct QueueData1 : IBufferElementData, IQueueEntry
{
    public Entity Value;
    public SplineTConstraints c;
    public float m_NormalizedSpeed;
    public float m_SplineTimer;
    public Entity carId => Value;
    public SplineTConstraints constraints
    {
        get => c;
        set => c = value;
    }

    public float NormalizedSpeed
    {
        get => m_NormalizedSpeed;
        set => m_NormalizedSpeed = value;
    }

    public float SplineTimer
    {
        get => m_SplineTimer;
        set => m_SplineTimer = value;
    }}

public struct QueueData2 : IBufferElementData, IQueueEntry
{
    public Entity Value;
    public SplineTConstraints c;
    public float m_NormalizedSpeed;
    public float m_SplineTimer;
    public Entity carId => Value;
    public SplineTConstraints constraints
    {
        get => c;
        set => c = value;
    }

    public float NormalizedSpeed
    {
        get => m_NormalizedSpeed;
        set => m_NormalizedSpeed = value;
    }

    public float SplineTimer
    {
        get => m_SplineTimer;
        set => m_SplineTimer = value;
    }
}

public struct QueueData3 : IBufferElementData, IQueueEntry
{
    public Entity Value;
    public SplineTConstraints c;
    public float m_NormalizedSpeed;
    public float m_SplineTimer;
    public Entity carId => Value;
    public SplineTConstraints constraints
    {
        get => c;
        set => c = value;
    }

    public float NormalizedSpeed
    {
        get => m_NormalizedSpeed;
        set => m_NormalizedSpeed = value;
    }

    public float SplineTimer
    {
        get => m_SplineTimer;
        set => m_SplineTimer = value;
    }
}



public struct SplineTConstraints: IComponentData
{
    public float MaxTValue;
    public int flags;

    public bool isFirst
    {
        get => (flags & 1) == 1;
        set
        {
            if (value)
            {
                flags = flags | 1;
            }
            else
            {
                flags = flags & ~1;
            }
        }
    }

    public bool needsSlowDown
    {
        get => (flags & 2) == 2;
        set
        {
            if (value)
            {
                flags = flags | 2;
            }
            else
            {
                flags = flags & ~2;
            }
        }
    }
}

public struct RoadReference : IComponentData
{
    public Entity Value;
}

public struct Capacity: IComponentData
{
    public int Value;
}

public struct RoadData : IComponentData
{
    public Entity startIntersection;
    public Entity endIntersection;
    public int capacity;
}

public interface ILaneRef
{
    Entity laneEntity { get; set; }
}

public struct Lane0 :IComponentData,ILaneRef
{
    public Entity Value;
    public Entity laneEntity { get => Value; set => Value = value; }
}

public struct Lane1:IComponentData,ILaneRef
{
    public Entity Value;
    public Entity laneEntity { get => Value; set => Value = value; }
}

public struct Lane2:IComponentData,ILaneRef
{
    public Entity Value;
    public Entity laneEntity { get => Value; set => Value = value; }
}
public struct Lane3:IComponentData,ILaneRef
{
    public Entity Value;
    public Entity laneEntity { get => Value; set => Value = value; }
}
public struct NeighborSpline : IComponentData
{
    public Entity Value0;
    public Entity Value1;
    public Entity Value2;
    
    public Entity this[int index]
    {
        get
        {
            switch (index)
            {
                case 0: return Value0;
                case 1: return Value1;
                case 2: return Value2;
            };

            return Entity.Null;
        }
        set
        {
            switch (index)
            {
                case 0: Value0 = value; return;
                case 1: Value1 = value; return;
                case 2: Value2 = value; return;
            };
        }
    }

    public int IndexOf(Entity value)
    {
        if (Value0 == value)
        {
            return 0;
        }

        if (Value1 == value)
        {
            return 1;
        }

        if (Value2 == value)
        {
            return 2;
        }

        return -1;
    } 
}


public struct IntersectionData: IComponentData
{
    public bool occupied0;
    public bool occupied1;

    bool IsOccupied(int side)
    {
        return side > 0? occupied1:
        occupied0;
    }
    
    void SetOccupied(int side, bool v)
    {
        if (side > 0)
            occupied1 = v;
        else
        {
            occupied0 = v;
        }
    }

}

/*
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
*/
using Unity.Collections;
using Unity.Entities;

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

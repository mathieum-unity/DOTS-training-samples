using Unity.Entities;
using Unity.Mathematics;

public struct TransformData : IComponentData
{
    public float4x4 localToWorld;
}

public struct Color : IComponentData
{
    public float4 Value;
}



 

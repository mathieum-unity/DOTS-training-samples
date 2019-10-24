using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Jobs;
using UnityEngine;

public class CarRenderingSystem : JobComponentSystem
{
    EntityQuery m_Group;

    protected override void OnCreate()
    {
        m_Group = GetEntityQuery(ComponentType.ReadOnly<ColorData>(), ComponentType.ReadOnly<LocalToWorld>());
    }

    [BurstCompile]
    struct CollectRenderingDataJob : IJobForEachWithEntity<LocalToWorld, ColorData>
    {
        public NativeArray<float4x4> Transforms;
        public NativeArray<float4> Colors;

        public void Execute(Entity entity, int index,
            [ReadOnly] ref LocalToWorld localToWorld,
            [ReadOnly] ref ColorData color)
        {
            Transforms[index] = localToWorld.Value;
            Colors[index] = color.value;
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var renderer = CarRenderer.GetInstance();
        if (renderer == null)
        {
            Debug.LogError("Looks like CarRenderer was not instanciated.");
            return inputDeps;
        }

        renderer.Resize(m_Group.CalculateEntityCount());
        
        var job = new CollectRenderingDataJob
        {
            Transforms = renderer.transforms, Colors = renderer.colors
        }.Schedule(this, inputDeps);

        return job;
    }
}


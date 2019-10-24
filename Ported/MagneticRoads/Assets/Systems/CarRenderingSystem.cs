using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class CarRenderingSystem : ComponentSystem
{
    EntityQuery m_Group;

    protected override void OnCreate()
    {
        m_Group = GetEntityQuery(ComponentType.ReadOnly<ColorData>(), ComponentType.ReadOnly<LocalToWorld>());
    }

    protected override void OnUpdate()
    {
        var renderer = CarRenderer.GetInstance();
        if (renderer == null)
        {
            Debug.LogError("Looks like CarRenderer was not instanciated.");
            return;
        }

        var chunks = m_Group.CreateArchetypeChunkArray(Allocator.TempJob);
        var localToWorldType = GetArchetypeChunkComponentType<LocalToWorld>(true);
        var colorDataType = GetArchetypeChunkComponentType<ColorData>(true);

        var access = renderer.GetWriteAccess();
        access.Reset();
        foreach (var chunk in chunks)
        {
            var transforms = chunk.GetNativeArray(localToWorldType);
            var colors = chunk.GetNativeArray(colorDataType);
            access.AddRange(transforms, colors);
        }
        chunks.Dispose();
        access.Apply();
    }
}


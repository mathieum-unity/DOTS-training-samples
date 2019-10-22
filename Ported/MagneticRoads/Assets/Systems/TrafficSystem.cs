using System.ComponentModel;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;

// TODO execute before spline evaluation system
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public class TrafficSystem : JobComponentSystem
{
    [BurstCompile]
    struct UpdateIntersection : IJobForEach<IntersectionHandle, SplineT, Velocity>
    {
        const float k_IntersectionVelocityCoefficient = 0.7f;
        
        public void Execute([Unity.Collections.ReadOnly] ref IntersectionHandle intersectionHandle, ref SplineT t, ref Velocity velocity)
        {

            velocity.Value = velocity.Value * k_IntersectionVelocityCoefficient;

        }
    }

    [BurstCompile]
    struct ApplyVelocity : IJobForEachWithEntity<Edge, LaneIndex, SplineLength, Velocity, SplineT>
    {
        const float k_MaxSpeed = 2f;
        
        [WriteOnly]
        public EntityCommandBuffer.Concurrent CommandBuffer;
        public EntityManager manager;
        
        public void Execute(
            Entity entity, int index,
            [Unity.Collections.ReadOnly] ref Edge edge, 
            [Unity.Collections.ReadOnly] ref LaneIndex laneIndex, 
            [Unity.Collections.ReadOnly] ref SplineLength splineLength, 
            [Unity.Collections.ReadOnly] ref Velocity velocity, 
            ref SplineT t)
        {
            t.Value += velocity.Value * k_MaxSpeed / splineLength.Value * Time.deltaTime;
            if (t.Value > 1)
            {
                t.Value = 1;
                
                // select node we are arriving to
                var isStart = true;

                var targetNode = laneIndex.IsLeft ? edge.start : edge.end;
                
                
                
                // when in an intersection, we still on a road?
                
                
                //CommandBuffer.AddComponent(entity, new IntersectionHandle{ Value = ? });
                
                
                //CommandBuffer.RemoveComponent(entity, new Edge{ Value = ? });

            }
        }
    }

    protected override JobHandle OnUpdate(JobHandle handle)
    {
        return handle;
    }


}


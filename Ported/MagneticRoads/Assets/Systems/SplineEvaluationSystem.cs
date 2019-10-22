using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public class SplineEvaluationSystem : JobComponentSystem
{
    public static float3 EvaluateBezier(float tValue, [ReadOnly] ref BezierData curve)
    {
        var oneMinusT = (1 - tValue);
        var pos = curve.startPoint * oneMinusT * oneMinusT * oneMinusT + 
              3f * curve.anchor1 * oneMinusT * oneMinusT * tValue + 
              3f * curve.anchor2 * oneMinusT * tValue * tValue + 
              curve.endPoint * tValue * tValue * tValue;
        return pos;
    }
    [BurstCompile]
    struct EvaluateSplinePosition : IJobForEach<SplineT, BezierData, SplineDirection, UpVector, Translation>
    {
        public void Execute([ReadOnly] ref SplineT t, 
            [ReadOnly] ref BezierData curve, 
            [ReadOnly] ref SplineDirection dir, 
            [ReadOnly] ref UpVector up,
            ref Translation position)
        {
            var tValue = t.Value > 1.0f ? 1.0f : t.Value;
            
            if (dir.Value < 0)
            {
                tValue = 1 - tValue;
            }

            position.Value = EvaluateBezier(tValue, ref curve);
        }
    }
    
    [BurstCompile]
    struct EvaluateSplineRotation : IJobForEach<UpVector, ForwardVector, Rotation>
    {
        public void Execute([ReadOnly] ref UpVector up, 
            [ReadOnly] ref ForwardVector forward, 
            ref Rotation rot)
        {
            rot.Value = Unity.Mathematics.quaternion.LookRotation(forward.Value, up.Value);
        }
    }  
    
    [BurstCompile]
    struct EvaluateSplineUpForward : IJobForEach<SplineT, BezierData, NormalData, SplineSideDirection, UpVector, ForwardVector>
    {
        public void Execute([ReadOnly] ref SplineT t, 
            [ReadOnly] ref BezierData curve, 
            [ReadOnly] ref NormalData normals,
            [ReadOnly] ref SplineSideDirection dir,
            ref UpVector up,
            ref ForwardVector forward)
        {
            
        }
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        var upForward = new EvaluateSplineUpForward();
        var upForwardHandle = upForward.Schedule(this, inputDependencies);

        var rotJob = new EvaluateSplineRotation();
        var rotHandle = rotJob.Schedule(this, upForwardHandle);
        
        var posJob = new EvaluateSplinePosition();
        var posHandle =  posJob.Schedule(this, upForwardHandle);

        return JobHandle.CombineDependencies(rotHandle, posHandle);
    }
}
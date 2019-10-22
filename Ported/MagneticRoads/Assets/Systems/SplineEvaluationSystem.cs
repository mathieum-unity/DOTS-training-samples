using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;

[UpdateInGroup(typeof(LateSimulationSystemGroup))]
public class SplineEvaluationSystem : JobComponentSystem
{
    [BurstCompile]
    struct EvaluateSpline : IJobForEach<SplineT, BezierData, Translation>
    {
        public void Execute([ReadOnly] ref SplineT t, [ReadOnly] ref BezierData curve, ref Translation postion)
        {
        }
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        var job = new EvaluateSpline();

        return job.Schedule(this, inputDependencies);
    }
}
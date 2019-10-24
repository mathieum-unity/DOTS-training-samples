
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public class RoadSystemV2 : JobComponentSystem
{
    [BurstCompile]
    struct UpdateSpline<QueueType> : IJobForEach_BCCC<QueueType, BezierData, RoadData, SplineLength> where QueueType: struct, IBufferElementData, IQueueEntry
    {
        [ReadOnly]public ComponentDataFromEntity<SplineT> splineAccess;
        [ReadOnly]public ComponentDataFromEntity<IntersectionData> intersectionAccess;
        static readonly float maxSpeed = 2f;

        public float m_DeltaTime;
        public int direction;
        public int side;
 
        public float UpdateSpeed(ref QueueType queueEntry, float measureLength, float maxTValue, bool first, bool slowDown)
        {
            var splineConstraints = new SplineTConstraints() {MaxTValue = maxTValue, isFirst = first, needsSlowDown = slowDown};
            
            queueEntry.NormalizedSpeed += m_DeltaTime * 2f;

            if (queueEntry.NormalizedSpeed > 1f)
            {
                queueEntry.NormalizedSpeed = 1f;
            }

            queueEntry.SplineTimer += queueEntry.NormalizedSpeed * maxSpeed / measureLength * m_DeltaTime;

            var approachSpeed = 1f;

            if (!splineConstraints.isFirst)
            {
                // someone's ahead of us - don't clip through them
                var maxT = splineConstraints.MaxTValue;

                if (queueEntry.SplineTimer > maxT)
                {
                    queueEntry.SplineTimer = maxT;
                    queueEntry.NormalizedSpeed = 0f;
                }
                else
                {
                    // slow down when approaching another car
                    approachSpeed = (maxT - queueEntry.SplineTimer) * 5f;
                }
            }
            else
            {
                // we're "first in line" in our lane, but we still might need
                // to slow down if our next intersection is occupied
                if (splineConstraints.needsSlowDown)
                {
                    approachSpeed = (1f - queueEntry.SplineTimer) * .8f + .2f;
                }
            }

            if (queueEntry.NormalizedSpeed > approachSpeed)
            {
                queueEntry.NormalizedSpeed = approachSpeed;
            }

            if (queueEntry.SplineTimer > 1)
            {
                queueEntry.SplineTimer = 0;
                
                //switch direction
//                int direction = ((int) dir.DirectionValue) - 1;
//                direction *= -1;
//                dir.DirectionValue = (byte)(direction + 1);
            }
            
            return queueEntry.SplineTimer;
        }
        
        public void Execute(DynamicBuffer<QueueType> queue,
            [ReadOnly] ref BezierData curve,
            [ReadOnly] ref RoadData rd,
            [ReadOnly] ref SplineLength splineLength)
        {
            if (queue.Length <= 1)
                return;

            QueueType first = queue[0];

            float spacing = 1.0f / rd.capacity;

            Entity intersectionEntity;

            if (direction > 0)
            {
                intersectionEntity = rd.endIntersection;
            }
            else
            {
                intersectionEntity = rd.startIntersection;
            }
            
            var intersection = intersectionAccess[intersectionEntity];

            bool occupied = side > 0 ? intersection.occupied1 : intersection.occupied0;
            
            var maxT = UpdateSpeed(ref first, splineLength.Value, 1000, true, occupied);
            queue[0] = first;
            for (int i = 1; i < queue.Length; ++i)
            {
                QueueType second = queue[i];
                
                maxT = UpdateSpeed(ref second,splineLength.Value,maxT - spacing, false, false);
                queue[i] = second;
            }
        }
    }
    

    protected override void OnCreate()
    {
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        float dt = Time.deltaTime;

        var updateSplineT0 = new UpdateSpline<QueueData0>()
        {
            splineAccess = GetComponentDataFromEntity<SplineT>(),
            intersectionAccess = GetComponentDataFromEntity<IntersectionData>(),
            direction=0,
            side = 0,
            m_DeltaTime = dt,
        };


        var updateSplineT1 = new UpdateSpline<QueueData1>()
        {
            splineAccess = GetComponentDataFromEntity<SplineT>(),
            intersectionAccess = GetComponentDataFromEntity<IntersectionData>(),
            direction=1,
            side = 0,
            m_DeltaTime = dt,
        };

        var updateSplineT2 = new UpdateSpline<QueueData2>()
        {
            splineAccess = GetComponentDataFromEntity<SplineT>(),
            intersectionAccess = GetComponentDataFromEntity<IntersectionData>(),
            direction=0,
            side = 1,
            m_DeltaTime = dt,
        };

        var updateSplineT3 = new UpdateSpline<QueueData3>()
        {
            splineAccess = GetComponentDataFromEntity<SplineT>(),
            intersectionAccess = GetComponentDataFromEntity<IntersectionData>(),
            direction=1,
            side = 1,
            m_DeltaTime = dt,
        };
        
//        var updateSplineAll = new UpdateSplineAll()
//        {
//            splineAccess = GetComponentDataFromEntity<SplineT>(),
//            intersectionAccess = GetComponentDataFromEntity<IntersectionData>(),
//            CommandBuffer = m_Barrier.CreateCommandBuffer().ToConcurrent(),
//        };


        NativeArray<JobHandle> updateJobs = new NativeArray<JobHandle>(4, Allocator.Temp);
        
        updateJobs[0] = updateSplineT0.Schedule(this, inputDeps);
        updateJobs[1] = updateSplineT1.Schedule(this, inputDeps);
        updateJobs[2] = updateSplineT2.Schedule(this, inputDeps);
        updateJobs[3] = updateSplineT3.Schedule(this, inputDeps);
        
        var updateJobDeps = JobHandle.CombineDependencies(updateJobs);

        updateJobs.Dispose();

        return updateJobDeps;
    }
}
///*
//[UpdateInGroup(typeof(SimulationSystemGroup))]
//[UpdateAfter(typeof(RoadSystemV2))]
//public class UpdateSpeedSystem  : JobComponentSystem
//{
//    /*  struct UpdateSplineT : IJobForEachWithEntity<RoadReference, SplineLength, SplineSideDirection, SplineTConstraints, NormalizedSpeed, SplineT>
//    {
//        public float m_DeltaTime;
//        static readonly float maxSpeed = 2f;
//        [ReadOnly]public BufferFromEntity<QueueData0> queue0Access;
//        [ReadOnly]public BufferFromEntity<QueueData1> queue1Access;
//        [ReadOnly]public BufferFromEntity<QueueData2> queue2Access;
//        [ReadOnly]public BufferFromEntity<QueueData3> queue3Access;
//
//        void AssignConstraints<QueueType>(Entity carEntity, 
//            DynamicBuffer<QueueType> buffer, ref SplineTConstraints c) where QueueType: struct, IQueueEntry
//        {
//            for (int i = 0; i < buffer.Length; ++i)
//            {
//                if (buffer[i].carId == carEntity)
//                {
//                    c = buffer[i].constraints;
//                    return;
//                }
//            }
//        }
//        
//        public void Execute(Entity carEntity, int jobIndex, [ReadOnly] ref RoadReference currentRoad,
//                            [ReadOnly] ref SplineLength measuredLength, 
//                            [ReadOnly] ref SplineSideDirection sideDirection, 
//                            ref SplineTConstraints splineConstraints, 
//                            ref NormalizedSpeed normalizedSpeed, 
//                            ref SplineT splineTimer)
//        {
//            int QueueIndex = sideDirection.QueueIndex();
//
//            switch (QueueIndex)
//            {
//                case 0:
//                    AssignConstraints(carEntity, queue0Access[currentRoad.Value], ref splineConstraints); 
//                    break;
//                case 1:
//                    AssignConstraints(carEntity, queue1Access[currentRoad.Value], ref splineConstraints); 
//                    break;
//                case 2:
//                    AssignConstraints(carEntity, queue2Access[currentRoad.Value], ref splineConstraints); 
//                    break;
//                case 3:
//                    AssignConstraints(carEntity, queue3Access[currentRoad.Value], ref splineConstraints); 
//                    break;
//                default:
//                    break;
//            }
//            
//           
//        }
//    }*/
//
//    struct SelectNextRoad : IJobForEach<SplineT, SplineSideDirection>
//    {
//        public void Execute(ref SplineT t,
//            ref SplineSideDirection dir
//        )
//        {
//            if (t.Value > 1)
//            {
//                t.Value = 0;
//                
//                int direction = ((int) dir.DirectionValue) - 1;
//                direction *= -1;
//                dir.DirectionValue = (byte)(direction + 1);
//            }
//        }
//    }
//    
//    protected override JobHandle OnUpdate(JobHandle inputDeps)
//    {
//        var jobHandle = inputDeps;
//
//        var updateT = new UpdateSplineT()
//        {
//            m_DeltaTime = Time.deltaTime,
//            queue0Access = GetBufferFromEntity<QueueData0>(),
//            queue1Access = GetBufferFromEntity<QueueData1>(),
//            queue2Access = GetBufferFromEntity<QueueData2>(),
//            queue3Access = GetBufferFromEntity<QueueData3>()
//        };
//        jobHandle = updateT.Schedule(this, jobHandle);
//        
//        var nextRoad = new SelectNextRoad() {};
//        jobHandle = nextRoad.Schedule(this, jobHandle);
//        return jobHandle;
//    }
//}

//// TODO execute before spline evaluation system
//[UpdateInGroup(typeof(SimulationSystemGroup))]
//public class RoadSystemV2 : ComponentSystem
//{
//    static readonly float maxSpeed = 2f;
//    EntityQuery m_Query;
//    DynamicBuffer<TrackSplineElementData> m_TrackSplineBuffer;
//    DynamicBuffer<TrackSplineStateElementData> m_TrackSplineStateBuffer;
//    DynamicBuffer<IntersectionElementData> m_IntersectionBuffer;
//    DynamicBuffer<IntersectionStateElementData> m_IntersectionStateBuffer;
//    float m_DeltaTime = 0f;
//    Random m_Random = new Random(1);
//
//    protected override void OnCreate()
//    {
//        m_Query = GetEntityQuery(typeof(TrackSplineElementData), typeof(IntersectionStateElementData));
//    }
//
//    protected override void OnUpdate()
//    {
//        var entities = m_Query.ToEntityArray(Allocator.TempJob);
//
//        Debug.Assert(entities.Length == 1);
//
//        m_TrackSplineBuffer = EntityManager.GetBuffer<TrackSplineElementData>(entities[0]);
//        m_TrackSplineStateBuffer = EntityManager.GetBuffer<TrackSplineStateElementData>(entities[0]);
//        m_IntersectionBuffer = EntityManager.GetBuffer<IntersectionElementData>(entities[0]);
//        m_IntersectionStateBuffer = EntityManager.GetBuffer<IntersectionStateElementData>(entities[0]);
//        m_DeltaTime = Time.deltaTime;
//
//        Entities.ForEach((Entity e, ref Lane lane, ref SplineT splineTimer, ref Next next, ref NormalizedSpeed normalizedSpeed) =>
//        {
//            Step1(ref lane, ref splineTimer, ref next, ref normalizedSpeed);
//            Step2(e, ref splineTimer, ref next, ref normalizedSpeed, ref lane);
//        });
//
//        entities.Dispose();
//    }
//
//    void Step1(ref Lane lane, ref SplineT splineTimer, ref Next next, ref NormalizedSpeed normalizedSpeed)
//    {
//        normalizedSpeed.Value += m_DeltaTime * 2f;
//
//        if (normalizedSpeed.Value > 1f)
//        {
//            normalizedSpeed.Value = 1f;
//        }
//
//        var roadSpline = m_TrackSplineBuffer[lane.trackSplineIndex];
//        splineTimer.Value += normalizedSpeed.Value * maxSpeed / roadSpline.measuredLength * m_DeltaTime;
//
//        var approachSpeed = 1f;
//
//        var trackSplineState = m_TrackSplineStateBuffer[lane.trackSplineIndex];
//        var lastOut = trackSplineState.GetLastEntityOut(lane.splineSide, lane.splineDirection);
//
//        if (next.Value == lastOut)
//            next.Value = Entity.Null;
//
//        if (next.Value != Entity.Null)
//        {
//            // someone's ahead of us - don't clip through them
//            var nextT = EntityManager.GetComponentData<SplineT>(next.Value);
//            var maxT = nextT.Value - roadSpline.carQueueSize;
//            if (splineTimer.Value > maxT)
//            {
//                splineTimer.Value = maxT;
//                normalizedSpeed.Value = 0f;
//            }
//            else
//            {
//                // slow down when approaching another car
//                approachSpeed = (maxT - splineTimer.Value) * 5f;
//            }
//        }
//        else
//        {
//            // we're "first in line" in our lane, but we still might need
//            // to slow down if our next intersection is occupied
//            var intersectionIndex = lane.splineDirection == 1 ? roadSpline.endIntersection : roadSpline.startIntersection;
//            var occupied = m_IntersectionStateBuffer[intersectionIndex];
//            if (occupied[(lane.splineSide + 1) / 2])
//            {
//                approachSpeed = (1f - splineTimer.Value) * .8f + .2f;
//            }
//        }
//
//        if (normalizedSpeed.Value > approachSpeed)
//        {
//            normalizedSpeed.Value = approachSpeed;
//        }
//    }
//
//    void Step2(Entity e, ref SplineT splineTimer, ref Next next, ref NormalizedSpeed normalizedSpeed, ref Lane lane)
//    {
//        if (splineTimer.Value >= 1f)
//        {
//            // we've reached the end of our current segment
//
//            /*
//            if (isInsideIntersection)
//            {
//                // we're exiting an intersection - make sure the next road
//                // segment has room for us before we proceed
//                if (roadSpline.GetQueue(splineDirection, splineSide).Count <= roadSpline.maxCarCount)
//                {
//                    intersectionSpline.startIntersection.occupied[(intersectionSide + 1) / 2] = false;
//                    isInsideIntersection = false;
//                    splineTimer = 0f;
//                }
//                else
//                {
//                    splineTimer = 1f;
//                    normalizedSpeed = 0f;
//                }
//            }
//            else
//            */
//            {
//                var prevLane = lane;
//                var roadSpline = m_TrackSplineBuffer[lane.trackSplineIndex];
//
//                // we're exiting a road segment - first, we need to know
//                // which intersection we're entering
//                var intersectionIndex = lane.splineDirection == 1 ? roadSpline.endIntersection : roadSpline.startIntersection;
//                var intersection = m_IntersectionBuffer[intersectionIndex];
//
//                if (lane.splineDirection == 1)
//                {
//                    //intersectionSpline.startPoint = roadSpline.endPoint;
//                }
//                else
//                {
//                    //intersectionSpline.startPoint = roadSpline.startPoint;
//                }
//
//                // now we need to know which road segment we'll move into
//                // (dead-ends force u-turns, but otherwise, u-turns are not allowed)
//                int newNeighbourIndex = 0;
//                int myNeighbourIndex = intersection.IndexOf(lane.trackSplineIndex);
//                if (intersection.neighbourCount > 1)
//                {
//                    newNeighbourIndex = m_Random.NextInt(intersection.neighbourCount-1);
//                    if (newNeighbourIndex >= myNeighbourIndex)
//                    {
//                        newNeighbourIndex++;
//                    }
//                }
//                var nextTrackSplineIndex = intersection[newNeighbourIndex];
//                var nextTrackSpline = m_TrackSplineBuffer[nextTrackSplineIndex];
//                //TrackSpline newSpline = intersection.neighborSplines[newSplineIndex];
//
//                // make sure that our side of the intersection (top/bottom)
//                // is empty before we enter
//                var intersectionSide = lane.splineSide;
//                var occupied = m_IntersectionStateBuffer[intersectionIndex];
//                if (occupied[(intersectionSide + 1) / 2])
//                {
//                    splineTimer.Value = 1f;
//                    normalizedSpeed.Value = 0f;
//                }
//                else
//                {
//                    // to avoid flipping between top/bottom of our roads,
//                    // we need to know our new spline's normal at our entrance point
//                    Vector3 newNormal;
//                    if (nextTrackSpline.startIntersection == intersectionIndex)
//                    {
//                        lane.splineDirection = 1;
//                        newNormal = nextTrackSpline.curve.startNormal;
//                        //intersectionSpline.endPoint = newSpline.startPoint;
//                    }
//                    else
//                    {
//                        lane.splineDirection = -1;
//                        newNormal = nextTrackSpline.curve.endNormal;
//                        //intersectionSpline.endPoint = newSpline.endPoint;
//                    }
//
//                    // now we'll prepare our intersection spline - this lets us
//                    // create a "temporary lane" inside the current intersection
//                    //intersectionSpline.anchor1 = (intersection.position + intersectionSpline.startPoint) * .5f;
//                    //intersectionSpline.anchor2 = (intersection.position + intersectionSpline.endPoint) * .5f;
//                    //intersectionSpline.startTangent = Vector3Int.RoundToInt((intersection.position - intersectionSpline.startPoint).normalized);
//                    //intersectionSpline.endTangent = Vector3Int.RoundToInt((intersection.position - intersectionSpline.endPoint).normalized);
//                    //intersectionSpline.startNormal = intersection.normal;
//                    //intersectionSpline.endNormal = intersection.normal;
//
//                    if (lane.trackSplineIndex == nextTrackSplineIndex)
//                    {
//                        // u-turn - make our intersection spline more rounded than usual
//                        //Vector3 perp = Vector3.Cross(intersectionSpline.startTangent, intersectionSpline.startNormal);
//                        //intersectionSpline.anchor1 += (Vector3)intersectionSpline.startTangent * RoadGenerator.intersectionSize * .5f;
//                        //intersectionSpline.anchor2 += (Vector3)intersectionSpline.startTangent * RoadGenerator.intersectionSize * .5f;
//
//                        //intersectionSpline.anchor1 -= perp * RoadGenerator.trackRadius * .5f * intersectionSide;
//                        //intersectionSpline.anchor2 += perp * RoadGenerator.trackRadius * .5f * intersectionSide;
//                    }
//
//                    //intersectionSpline.startIntersection = intersection;
//                    //intersectionSpline.endIntersection = intersection;
//                    //intersectionSpline.MeasureLength();
//
//                    //isInsideIntersection = true;
//
//                    // to maintain our current orientation, should we be
//                    // on top of or underneath our next road segment?
//                    // (each road segment has its own "up" direction, at each end)
//                    /*
//                    if (Vector3.Dot(newNormal, up) > 0f)
//                    {
//                        splineSide = 1;
//                    }
//                    else
//                    {
//                        splineSide = -1;
//                    }
//
//                    // should we be on top of or underneath the intersection?
//					if (Vector3.Dot(intersectionSpline.startNormal,up) > 0f) {
//						intersectionSide = 1;
//					} else {
//						intersectionSide = -1;
//					}
//                    */
//
//                    // block other cars from entering this intersection
//                    occupied[(intersectionSide + 1) / 2] = true;
//                    m_IntersectionStateBuffer[intersectionIndex] = occupied;
//                    
//                    // add "leftover" spline timer value to our new spline timer
//                    // (avoids a stutter when changing between splines)
//                    //splineTimer.Value = (splineTimer.Value - 1f) * roadSpline.measuredLength / intersectionSpline.measuredLength;
//                    splineTimer.Value -= 1f;
//                    lane.trackSplineIndex = nextTrackSplineIndex;
//
//                    //Update next trackState
//                    {
//                        var trackSplineState = m_TrackSplineStateBuffer[nextTrackSplineIndex];
//                        var carCount = trackSplineState.GetCarCount(lane.splineSide, lane.splineDirection);
//                        trackSplineState.SetCarCount(lane.splineSide, lane.splineDirection, carCount + 1);
//
//                        var lastIn = trackSplineState.GetLastEntityIn(lane.splineSide, lane.splineDirection);
//                        var lastOut = trackSplineState.GetLastEntityOut(lane.splineSide, lane.splineDirection);
//
//                        if (lastIn == lastOut)
//                            next.Value = Entity.Null;
//                        else
//                            next.Value = lastIn;
//
//                        trackSplineState.SetLastEntityIn(lane.splineSide, lane.splineDirection, e);
//                        m_TrackSplineStateBuffer[nextTrackSplineIndex] = trackSplineState;
//                    }
//
//                    //Update prev trackState
//                    {
//                        var trackSplineState = m_TrackSplineStateBuffer[prevLane.trackSplineIndex];
//                        var carCount = trackSplineState.GetCarCount(prevLane.splineSide, prevLane.splineDirection);
//                        trackSplineState.SetCarCount(prevLane.splineSide, prevLane.splineDirection, carCount - 1);
//                        trackSplineState.SetLastEntityOut(prevLane.splineSide, prevLane.splineDirection, e);
//                        m_TrackSplineStateBuffer[prevLane.trackSplineIndex] = trackSplineState;
//                    }
//
//                    //TODO: this should happen in another system that updates cars in intersections
//                    {
//                        occupied[(intersectionSide + 1) / 2] = false;
//                        m_IntersectionStateBuffer[intersectionIndex] = occupied;
//                    }
//
//                    //TODO: Do properly
//                    PostUpdateCommands.SetComponent<BezierData>(e, nextTrackSpline.curve);
//                    PostUpdateCommands.SetComponent<SplineSideDirection>(e, new SplineSideDirection()
//                    {
//                        DirectionValue = (byte)(lane.splineDirection > 0 ? 1 : 0),
//                        SideValue = (byte)(lane.splineSide > 0 ? 1 : 0)
//                    });
//                }
//            }
//        }
//    }
//}
//

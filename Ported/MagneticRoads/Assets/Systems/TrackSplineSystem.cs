using System.ComponentModel;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

// TODO execute before spline evaluation system
[UpdateInGroup(typeof(SimulationSystemGroup))]
public class TrackSplineSystem : ComponentSystem
{
    static readonly float maxSpeed = 2f;
    EntityQuery m_Query;
    DynamicBuffer<TrackSplineElementData> m_TrackSplineBuffer;
    DynamicBuffer<TrackSplineStateElementData> m_TrackSplineStateBuffer;
    DynamicBuffer<IntersectionElementData> m_IntersectionBuffer;
    DynamicBuffer<IntersectionStateElementData> m_IntersectionStateBuffer;
    float m_DeltaTime = 0f;
    Random m_Random = new Random(1);

    protected override void OnCreate()
    {
        m_Query = GetEntityQuery(typeof(TrackSplineElementData), typeof(IntersectionStateElementData));
    }

    protected override void OnUpdate()
    {
        var entities = m_Query.ToEntityArray(Allocator.TempJob);

        Debug.Assert(entities.Length == 1);

        m_TrackSplineBuffer = EntityManager.GetBuffer<TrackSplineElementData>(entities[0]);
        m_TrackSplineStateBuffer = EntityManager.GetBuffer<TrackSplineStateElementData>(entities[0]);
        m_IntersectionBuffer = EntityManager.GetBuffer<IntersectionElementData>(entities[0]);
        m_IntersectionStateBuffer = EntityManager.GetBuffer<IntersectionStateElementData>(entities[0]);
        m_DeltaTime = Time.deltaTime;

        Entities.ForEach((Entity e, ref Lane lane, ref SplineT splineTimer, ref Next next, ref NormalizedSpeed normalizedSpeed) =>
        {
            Step1(ref lane, ref splineTimer, ref next, ref normalizedSpeed);
            Step2(e, ref splineTimer, ref normalizedSpeed, ref lane);
        });

        entities.Dispose();
    }

    void Step1(ref Lane lane, ref SplineT splineTimer, ref Next next, ref NormalizedSpeed normalizedSpeed)
    {
        normalizedSpeed.Value += m_DeltaTime * 2f;

        if (normalizedSpeed.Value > 1f)
        {
            normalizedSpeed.Value = 1f;
        }

        var roadSpline = m_TrackSplineBuffer[lane.trackSplineIndex];
        splineTimer.Value += normalizedSpeed.Value * maxSpeed / roadSpline.measuredLength * m_DeltaTime;

        var approachSpeed = 1f;

        if (next.Value != Entity.Null)
        {
            // someone's ahead of us - don't clip through them
            var nextT = EntityManager.GetComponentData<SplineT>(next.Value);
            var maxT = nextT.Value - roadSpline.carQueueSize;
            if (splineTimer.Value > maxT)
            {
                splineTimer.Value = maxT;
                normalizedSpeed.Value = 0f;
            }
            else
            {
                // slow down when approaching another car
                approachSpeed = (maxT - splineTimer.Value) * 5f;
            }
        }
        else
        {
            // we're "first in line" in our lane, but we still might need
            // to slow down if our next intersection is occupied
            var intersectionIndex = lane.splineDirection == 1 ? roadSpline.endIntersection : roadSpline.startIntersection;
            var occupied = m_IntersectionStateBuffer[intersectionIndex];
            if (occupied[(lane.splineSide + 1) / 2])
            {
                approachSpeed = (1f - splineTimer.Value) * .8f + .2f;
            }
        }

        if (normalizedSpeed.Value > approachSpeed)
        {
            normalizedSpeed.Value = approachSpeed;
        }
    }

    void Step2(Entity e, ref SplineT splineTimer, ref NormalizedSpeed normalizedSpeed, ref Lane lane)
    {
        if (splineTimer.Value >= 1f)
        {
            // we've reached the end of our current segment

            /*
            if (isInsideIntersection)
            {
                // we're exiting an intersection - make sure the next road
                // segment has room for us before we proceed
                if (roadSpline.GetQueue(splineDirection, splineSide).Count <= roadSpline.maxCarCount)
                {
                    intersectionSpline.startIntersection.occupied[(intersectionSide + 1) / 2] = false;
                    isInsideIntersection = false;
                    splineTimer = 0f;
                }
                else
                {
                    splineTimer = 1f;
                    normalizedSpeed = 0f;
                }
            }
            else
            */
            {
                var currentLane = lane;
                var currentTrackIndex = lane.trackSplineIndex;
                var roadSpline = m_TrackSplineBuffer[currentTrackIndex];

                // we're exiting a road segment - first, we need to know
                // which intersection we're entering
                var intersectionIndex = lane.splineDirection == 1 ? roadSpline.endIntersection : roadSpline.startIntersection;
                var intersection = m_IntersectionBuffer[intersectionIndex];

                if (lane.splineDirection == 1)
                {
                    //intersectionSpline.startPoint = roadSpline.endPoint;
                }
                else
                {
                    //intersectionSpline.startPoint = roadSpline.startPoint;
                }

                // now we need to know which road segment we'll move into
                // (dead-ends force u-turns, but otherwise, u-turns are not allowed)
                int newNeighbourIndex = 0;
                int myNeighbourIndex = intersection.IndexOf(currentTrackIndex);
                if (intersection.neighbourCount > 1)
                {
                    newNeighbourIndex = m_Random.NextInt(intersection.neighbourCount-1);
                    if (newNeighbourIndex >= myNeighbourIndex)
                    {
                        newNeighbourIndex++;
                    }
                }
                var nextTrackSplineIndex = intersection[newNeighbourIndex];
                var nextTrackSpline = m_TrackSplineBuffer[nextTrackSplineIndex];
                //TrackSpline newSpline = intersection.neighborSplines[newSplineIndex];

                // make sure that our side of the intersection (top/bottom)
                // is empty before we enter
                var intersectionSide = lane.splineSide;
                var occupied = m_IntersectionStateBuffer[intersectionIndex];
                if (occupied[(intersectionSide + 1) / 2])
                {
                    splineTimer.Value = 1f;
                    normalizedSpeed.Value = 0f;
                }
                else
                {
                    // to avoid flipping between top/bottom of our roads,
                    // we need to know our new spline's normal at our entrance point
                    Vector3 newNormal;
                    if (nextTrackSpline.startIntersection == intersectionIndex)
                    {
                        lane.splineDirection = 1;
                        newNormal = nextTrackSpline.curve.startNormal;
                        //intersectionSpline.endPoint = newSpline.startPoint;
                    }
                    else
                    {
                        lane.splineDirection = -1;
                        newNormal = nextTrackSpline.curve.endNormal;
                        //intersectionSpline.endPoint = newSpline.endPoint;
                    }

                    // now we'll prepare our intersection spline - this lets us
                    // create a "temporary lane" inside the current intersection
                    //intersectionSpline.anchor1 = (intersection.position + intersectionSpline.startPoint) * .5f;
                    //intersectionSpline.anchor2 = (intersection.position + intersectionSpline.endPoint) * .5f;
                    //intersectionSpline.startTangent = Vector3Int.RoundToInt((intersection.position - intersectionSpline.startPoint).normalized);
                    //intersectionSpline.endTangent = Vector3Int.RoundToInt((intersection.position - intersectionSpline.endPoint).normalized);
                    //intersectionSpline.startNormal = intersection.normal;
                    //intersectionSpline.endNormal = intersection.normal;

                    if (currentTrackIndex == nextTrackSplineIndex)
                    {
                        // u-turn - make our intersection spline more rounded than usual
                        //Vector3 perp = Vector3.Cross(intersectionSpline.startTangent, intersectionSpline.startNormal);
                        //intersectionSpline.anchor1 += (Vector3)intersectionSpline.startTangent * RoadGenerator.intersectionSize * .5f;
                        //intersectionSpline.anchor2 += (Vector3)intersectionSpline.startTangent * RoadGenerator.intersectionSize * .5f;

                        //intersectionSpline.anchor1 -= perp * RoadGenerator.trackRadius * .5f * intersectionSide;
                        //intersectionSpline.anchor2 += perp * RoadGenerator.trackRadius * .5f * intersectionSide;
                    }

                    //intersectionSpline.startIntersection = intersection;
                    //intersectionSpline.endIntersection = intersection;
                    //intersectionSpline.MeasureLength();

                    //isInsideIntersection = true;

                    // to maintain our current orientation, should we be
                    // on top of or underneath our next road segment?
                    // (each road segment has its own "up" direction, at each end)
                    /*
                    if (Vector3.Dot(newNormal, up) > 0f)
                    {
                        splineSide = 1;
                    }
                    else
                    {
                        splineSide = -1;
                    }

                    // should we be on top of or underneath the intersection?
					if (Vector3.Dot(intersectionSpline.startNormal,up) > 0f) {
						intersectionSide = 1;
					} else {
						intersectionSide = -1;
					}
                    */

                    // block other cars from entering this intersection
                    occupied[(intersectionSide + 1) / 2] = true;
                    m_IntersectionStateBuffer[intersectionIndex] = occupied;
                    
                    

                    // add "leftover" spline timer value to our new spline timer
                    // (avoids a stutter when changing between splines)
                    //splineTimer.Value = (splineTimer.Value - 1f) * roadSpline.measuredLength / intersectionSpline.measuredLength;
                    //roadSpline = newSpline;
                    splineTimer.Value -= 1f;
                    lane.trackSplineIndex = nextTrackSplineIndex;

                    var trackSplineState = m_TrackSplineStateBuffer[nextTrackSplineIndex];
                    var nextEntity = new Next()
                    {
                        Value = trackSplineState.GetLastEntity(lane.splineSide, lane.splineDirection)
                    };

                    //Update trackState
                    var carCount = trackSplineState.GetCarCount(lane.splineSide, lane.splineDirection);
                    trackSplineState.SetCarCount(lane.splineSide, lane.splineDirection, carCount + 1);
                    trackSplineState.SetLastEntity(lane.splineSide, lane.splineDirection, e);
                    m_TrackSplineStateBuffer[nextTrackSplineIndex] = trackSplineState;

                    //Update prev trackState
                    trackSplineState = m_TrackSplineStateBuffer[currentTrackIndex];
                    trackSplineState.SetCarCount(currentLane.splineSide, currentLane.splineDirection, carCount - 1);

                    if (carCount == 1)
                        trackSplineState.SetLastEntity(currentLane.splineSide, currentLane.splineDirection, Entity.Null);

                    m_TrackSplineStateBuffer[currentTrackIndex] = trackSplineState;

                    //TODO: this should happen in another system that updates cars in intersections
                    occupied[(intersectionSide + 1) / 2] = false;
                    m_IntersectionStateBuffer[intersectionIndex] = occupied;

                    PostUpdateCommands.SetComponent<BezierData>(e, nextTrackSpline.curve);
                    PostUpdateCommands.SetComponent<SplineSideDirection>(e, new SplineSideDirection()
                    {
                        DirectionValue = (byte)(lane.splineDirection > 0 ? 1 : 0),
                        SideValue = (byte)(lane.splineSide > 0 ? 1 : 0)

                    });
                }
            }
        }
    }
}


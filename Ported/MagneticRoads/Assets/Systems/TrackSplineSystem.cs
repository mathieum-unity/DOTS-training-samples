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
public class TrackSplineSystem : ComponentSystem
{
    static readonly float maxSpeed = 2f;
    EntityQuery m_Query;
    DynamicBuffer<TrackSplineElementData> m_TrackSplineBuffer;
    DynamicBuffer<TrackSplineStateElementData> m_TrackSplineStateBuffer;
    DynamicBuffer<IntersectionElementData> m_IntersectionBuffer;
    DynamicBuffer<IntersectionStateElementData> m_IntersectionStateBuffer;
    float m_DeltaTime = 0f;

    protected override void OnCreate()
    {
        m_Query = GetEntityQuery(typeof(TrackSplineElementData), typeof(IntersectionStateElementData));
    }

    protected override void OnUpdate()
    {
        var entities = m_Query.ToEntityArray(Allocator.Temp);

        Debug.Assert(entities.Length == 1);

        m_TrackSplineBuffer = EntityManager.GetBuffer<TrackSplineElementData>(entities[0]);
        m_TrackSplineStateBuffer = EntityManager.GetBuffer<TrackSplineStateElementData>(entities[0]);
        m_IntersectionBuffer = EntityManager.GetBuffer<IntersectionElementData>(entities[0]);
        m_IntersectionStateBuffer = EntityManager.GetBuffer<IntersectionStateElementData>(entities[0]);
        m_DeltaTime = Time.deltaTime;

        Entities.WithNone<InIntersection>().ForEach((ref Lane lane, ref SplineT splineTimer, ref Next next, ref NormalizedSpeed normalizedSpeed) =>
        {
            Step1(ref lane, ref splineTimer, ref next, ref normalizedSpeed);
            Step2(ref splineTimer);

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

        if (next.value != Entity.Null)
        {
            // someone's ahead of us - don't clip through them
            var nextT = EntityManager.GetComponentData<SplineT>(next.value);
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
            if (lane.splineDirection == 1)
            {
                var occupied = m_IntersectionStateBuffer[roadSpline.endIntersection];
                if (occupied[(lane.splineSide + 1) / 2])
                {
                    approachSpeed = (1f - splineTimer.Value) * .8f + .2f;
                }
            }
            else
            {
                var occupied = m_IntersectionStateBuffer[roadSpline.startIntersection];
                if (occupied[(lane.splineSide + 1) / 2])
                {
                    approachSpeed = (1f - splineTimer.Value) * .8f + .2f;
                }
            }
        }

        if (normalizedSpeed.Value > approachSpeed)
        {
            normalizedSpeed.Value = approachSpeed;
        }
    }

    void Step2(ref SplineT splineTimer)
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
            {
                // we're exiting a road segment - first, we need to know
                // which intersection we're entering
                IntersectionElementData intersection;
                if (lane.splineDirection == 1)
                {
                    intersection = m_IntersectionBuffer[roadSpline.endIntersection];
                    intersectionSpline.startPoint = roadSpline.endPoint;
                }
                else
                {
                    intersection = m_IntersectionBuffer[roadSpline.startIntersection];
                    intersectionSpline.startPoint = roadSpline.startPoint;
                }

                // now we need to know which road segment we'll move into
                // (dead-ends force u-turns, but otherwise, u-turns are not allowed)
                int newSplineIndex = 0;
                if (intersection.neighbors.Count > 1)
                {
                    int mySplineIndex = intersection.neighborSplines.IndexOf(roadSpline);
                    newSplineIndex = Random.Range(0, intersection.neighborSplines.Count - 1);
                    if (newSplineIndex >= mySplineIndex)
                    {
                        newSplineIndex++;
                    }
                }
                TrackSpline newSpline = intersection.neighborSplines[newSplineIndex];

                // make sure that our side of the intersection (top/bottom)
                // is empty before we enter
                if (intersection.occupied[(intersectionSide + 1) / 2])
                {
                    splineTimer = 1f;
                    normalizedSpeed = 0f;
                }
                else
                {
                    // to avoid flipping between top/bottom of our roads,
                    // we need to know our new spline's normal at our entrance point
                    Vector3 newNormal;
                    if (newSpline.startIntersection == intersection)
                    {
                        splineDirection = 1;
                        newNormal = newSpline.startNormal;
                        intersectionSpline.endPoint = newSpline.startPoint;
                    }
                    else
                    {
                        splineDirection = -1;
                        newNormal = newSpline.endNormal;
                        intersectionSpline.endPoint = newSpline.endPoint;
                    }

                    // now we'll prepare our intersection spline - this lets us
                    // create a "temporary lane" inside the current intersection
                    intersectionSpline.anchor1 = (intersection.position + intersectionSpline.startPoint) * .5f;
                    intersectionSpline.anchor2 = (intersection.position + intersectionSpline.endPoint) * .5f;
                    intersectionSpline.startTangent = Vector3Int.RoundToInt((intersection.position - intersectionSpline.startPoint).normalized);
                    intersectionSpline.endTangent = Vector3Int.RoundToInt((intersection.position - intersectionSpline.endPoint).normalized);
                    intersectionSpline.startNormal = intersection.normal;
                    intersectionSpline.endNormal = intersection.normal;

                    if (roadSpline == newSpline)
                    {
                        // u-turn - make our intersection spline more rounded than usual
                        Vector3 perp = Vector3.Cross(intersectionSpline.startTangent, intersectionSpline.startNormal);
                        intersectionSpline.anchor1 += (Vector3)intersectionSpline.startTangent * RoadGenerator.intersectionSize * .5f;
                        intersectionSpline.anchor2 += (Vector3)intersectionSpline.startTangent * RoadGenerator.intersectionSize * .5f;

                        intersectionSpline.anchor1 -= perp * RoadGenerator.trackRadius * .5f * intersectionSide;
                        intersectionSpline.anchor2 += perp * RoadGenerator.trackRadius * .5f * intersectionSide;
                    }

                    intersectionSpline.startIntersection = intersection;
                    intersectionSpline.endIntersection = intersection;
                    intersectionSpline.MeasureLength();

                    isInsideIntersection = true;

                    // to maintain our current orientation, should we be
                    // on top of or underneath our next road segment?
                    // (each road segment has its own "up" direction, at each end)
                    if (Vector3.Dot(newNormal, up) > 0f)
                    {
                        splineSide = 1;
                    }
                    else
                    {
                        splineSide = -1;
                    }

                    // should we be on top of or underneath the intersection?
                    if (Vector3.Dot(intersectionSpline.startNormal, up) > 0f)
                    {
                        intersectionSide = 1;
                    }
                    else
                    {
                        intersectionSide = -1;
                    }

                    // block other cars from entering this intersection
                    intersection.occupied[(intersectionSide + 1) / 2] = true;

                    // remove ourselves from our previous lane's list of cars
                    if (queue != null)
                    {
                        queue.Remove(this);
                    }

                    // add "leftover" spline timer value to our new spline timer
                    // (avoids a stutter when changing between splines)
                    splineTimer = (splineTimer - 1f) * roadSpline.measuredLength / intersectionSpline.measuredLength;
                    roadSpline = newSpline;

                    newSpline.GetQueue(splineDirection, splineSide).Add(this);
                }
            }
            */
        }
    }
}


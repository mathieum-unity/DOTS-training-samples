using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TrackSplineSystem))]
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

    public static float3 EvaluateTangent(float t, [ReadOnly] ref BezierData curve)
    {
        var ti = (1 - t);
        var tp0 = 3 * ti * ti;
        var tp1 = 6 * t * ti;
        var tp2 = 3 * t * t;

        return (tp0 * (curve.anchor1 - curve.startPoint)) + (tp1 * (curve.anchor2 - curve.anchor1)) +
               (tp2 * (curve.endPoint - curve.anchor2));
    }

    /// <summary>Returns the angle in degrees from 0 to 180 between two float3s.</summary>
//    public static float angle(float3 from, float3 to)
//    {
//        return math.degrees(math.acos(math.dot(math.normalize(from), math.normalize(to))));
//    }
//        
//    /// <summary>Returns the signed angle in degrees from 180 to -180 between two float3s.</summary>
//    public static float anglesigned(float3 from, float3 to)
//    {
//        float angle = math.acos(math.dot(math.normalize(from), math.normalize(to)));
//        float3 cross = math.cross(from, to);
//        angle *= math.sign(math.dot(math.up(), cross));
//        return math.degrees(angle);
//    }
    public static float Angle(float3 from, float3 to)
    {
        float num = (float) math.sqrt((double) math.lengthsq(from) * (double) math.lengthsq(to));
        if ((double) num < 1.00000000362749E-15)
            return 0.0f;
        return (float) math.acos((double) math.clamp(math.dot(from, to) / num, -1f, 1f)) * 57.29578f;
    }

    public static float SignedAngle(float3 from, float3 to, float3 axis)
    {
        float num1 = Angle(from, to);
        float num2 = (float) ((double) from.y * (double) to.z - (double) from.z * (double) to.y);
        float num3 = (float) ((double) from.z * (double) to.x - (double) from.x * (double) to.z);
        float num4 = (float) ((double) from.x * (double) to.y - (double) from.y * (double) to.x);
        float num5 = Mathf.Sign((float) ((double) axis.x * (double) num2 + (double) axis.y * (double) num3 +
                                         (double) axis.z * (double) num4));
        return num1 * num5;
    }

    public static float3x3 fromToMatrix(float3 from, float3 to)
    {
        float3 v = math.cross(from, to);
        float e = math.dot(from, to);
        const float kEpsilon = 0.000001f;
        if (e > 1.0 - kEpsilon) /* "from" almost or equal to "to"-vector? */
        {
            /* return identity */
            return float3x3.identity;
            ;
        }
        else if (e < -1.0 + kEpsilon) /* "from" almost or equal to negated "to"? */
        {
            float3x3 result = float3x3.identity;
            float3 left = new float3(0.0f, from[2], -from[1]);

            float invlen;
            float fxx, fyy, fzz, fxy, fxz, fyz;
            float uxx, uyy, uzz, uxy, uxz, uyz;
            float lxx, lyy, lzz, lxy, lxz, lyz;
            /* left=CROSS(from, (1,0,0)) */

            if (math.dot(left, left) < kEpsilon) /* was left=CROSS(from,(1,0,0)) a good choice? */
            {
                /* here we now that left = CROSS(from, (1,0,0)) will be a good choice */
                left[0] = -from[2];
                left[1] = 0.0f;
                left[2] = from[0];
            }

            /* normalize "left" */
            invlen = 1.0f / math.sqrt(math.dot(left, left));
            left[0] *= invlen;
            left[1] *= invlen;
            left[2] *= invlen;
            var up = math.cross(left, from);
            /* now we have a coordinate system, i.e., a basis;    */
            /* M=(from, up, left), and we want to rotate to:      */
            /* N=(-from, up, -left). This is done with the matrix:*/
            /* N*M^T where M^T is the transpose of M              */
            fxx = -from[0] * from[0];
            fyy = -from[1] * from[1];
            fzz = -from[2] * from[2];
            fxy = -from[0] * from[1];
            fxz = -from[0] * from[2];
            fyz = -from[1] * from[2];

            uxx = up[0] * up[0];
            uyy = up[1] * up[1];
            uzz = up[2] * up[2];
            uxy = up[0] * up[1];
            uxz = up[0] * up[2];
            uyz = up[1] * up[2];

            lxx = -left[0] * left[0];
            lyy = -left[1] * left[1];
            lzz = -left[2] * left[2];
            lxy = -left[0] * left[1];
            lxz = -left[0] * left[2];
            lyz = -left[1] * left[2];
            /* symmetric matrix */
            /*Get(0, 0)*/
            result[0][0] = fxx + uxx + lxx;
            /*Get(0, 1)*/
            result[0][1] = fxy + uxy + lxy;
            /*Get(0, 2)*/
            result[0][2] = fxz + uxz + lxz;
            /*Get(1, 0)*/
            result[1][0] = result[0][1]; //Get(0, 1);
            /*Get(1, 1)*/
            result[1][1] = fyy + uyy + lyy;
            /*Get(1, 2)*/
            result[1][2] = fyz + uyz + lyz;
            /*Get(2, 0)*/
            result[2][0] = result[0][2]; //Get(0, 2);
            /*Get(2, 1)*/
            result[2][1] = result[1][2]; //Get(1, 2);
            /*Get(2, 2)*/
            result[2][2] = fzz + uzz + lzz;
            return result;
        }
        else /* the most common case, unless "from"="to", or "from"=-"to" */
        {
            /* ...otherwise use this hand optimized version (9 mults less) */
            float3x3 result = float3x3.identity;

            float hvx, hvz, hvxy, hvxz, hvyz;
            float h = (1.0f - e) / math.dot(v, v);
            hvx = h * v[0];
            hvz = h * v[2];
            hvxy = hvx * v[1];
            hvxz = hvx * v[2];
            hvyz = hvz * v[1];
            /*Get(0, 0)*/
            result[0][0] = e + hvx * v[0];
            /*Get(0, 1)*/
            result[0][1] = hvxy - v[2];
            /*Get(0, 2)*/
            result[0][2] = hvxz + v[1];
            /*Get(1, 0)*/
            result[1][0] = hvxy + v[2];
            /*Get(1, 1)*/
            result[1][1] = e + h * v[1] * v[1];
            /*Get(1, 2)*/
            result[1][2] = hvyz - v[0];
            /*Get(2, 0)*/
            result[2][0] = hvxz - v[1];
            /*Get(2, 1)*/
            result[2][1] = hvyz + v[0];
            /*Get(2, 2)*/
            result[2][2] = e + hvz * v[2];
            return result;
        }
    }

    public static quaternion fromToQuaternion(float3 from, float3 to)
    {
        var m = fromToMatrix(from, to);

        return new quaternion(m);
    }

    public static float3 Extrude(float2 point, float t, ref BezierData curve, out float3 tangent, out float3 up,
        out quaternion rot)
    {
        float3 sample1 = EvaluateBezier(t, ref curve);

        tangent = EvaluateTangent(t, ref curve);
        tangent = math.normalize(tangent);

        // each spline uses one out of three possible twisting methods:
        quaternion fromTo = quaternion.identity;


        if (curve.twistMode == 0)
        {
            // method 1 - rotate startNormal around our current tangent
            float angle = SignedAngle(curve.startNormal, curve.endNormal, tangent);
            fromTo = quaternion.AxisAngle(tangent, angle);
        }
        else if (curve.twistMode == 1)
        {
            // method 2 - rotate startNormal toward endNormal
            fromTo = fromToQuaternion(curve.startNormal, curve.endNormal);
        }
        else if (curve.twistMode == 2)
        {
            // method 3 - rotate startNormal by "startOrientation-to-endOrientation" rotation
            quaternion startRotation = quaternion.LookRotation(curve.startTangent, curve.startNormal);
            quaternion endRotation = quaternion.LookRotation(curve.endTangent * -1, curve.endNormal);
            fromTo = math.mul(endRotation, math.inverse(startRotation));
        }

        // other twisting methods can be added, but they need to
        // respect the relationship between startNormal and endNormal.
        // for example: if startNormal and endNormal are equal, the road
        // can twist 0 or 360 degrees, but NOT 180.

        float smoothT = math.smoothstep(0f, 1f, t * 1.02f - .01f);

        rot = math.slerp(quaternion.identity, fromTo, smoothT);

        up = math.mul(rot, curve.startNormal);
        float3 right = math.cross(tangent, up);

//            // measure twisting errors:
//            // we have three possible spline-twisting methods, and
//            // we test each spline with all three to find the best pick
//            if (up.magnitude < .5f || right.magnitude < .5f) {
//                errorCount++;
//            }

        return sample1 + right * point.x + up * point.y;
    }


   // [BurstCompile]
    struct EvaluateSplineUpForward : IJobForEach<SplineT, BezierData, SplineSideDirection, Translation, Rotation>
    {
        public void Execute([ReadOnly] ref SplineT t,
            [ReadOnly] ref BezierData curve,
            [ReadOnly] ref SplineSideDirection dir,
            ref Translation position, ref Rotation rotation)
        {
            t.Value += 0.001f;

            if (t.Value > 1.0f)
                t.Value = 0;
            
            int direction = ((int) dir.DirectionValue) - 1;
            int side = ((int) dir.SideValue) - 1;

            float tValue = t.Value;
            if (direction == -1)
            {
                tValue = 1f - tValue;
            }

            float2 extrudePoint = new Vector2(-RoadGenerator.trackRadius * .5f * direction*side,
                RoadGenerator.trackThickness * .5f * side);

            // find our position and orientation
            float3 forward, up;
            quaternion rot;
            float3 splinePoint = Extrude(extrudePoint, tValue, ref curve, out forward, out up, out rot);

            up *= side;
            position.Value = splinePoint + math.normalize(up) * .06f;
            rotation.Value = rot;
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDependencies)
    {
        var upForward = new EvaluateSplineUpForward();
        var upForwardHandle = upForward.Schedule(this, inputDependencies);

        return upForwardHandle;
//        var posHandle = posJob.Schedule(this, upForwardHandle);
//        return JobHandle.CombineDependencies(rotHandle, posHandle);
    }
}
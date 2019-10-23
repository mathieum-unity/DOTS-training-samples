using Unity.Entities;
using Unity.Mathematics;

/*
 *
public void MeasureLength() {
		measuredLength = 0f;
		Vector3 point = Evaluate(0f);
		for (int i = 1; i <= RoadGenerator.splineResolution; i++) {
			Vector3 newPoint = Evaluate((float)i / RoadGenerator.splineResolution);
			measuredLength += (newPoint - point).magnitude;
			point = newPoint;
		}

		maxCarCount = Mathf.CeilToInt(measuredLength / RoadGenerator.carSpacing);
		carQueueSize = 1f / maxCarCount;
	}

	public Vector3 Evaluate(float t) {
		// cubic bezier

		t = Mathf.Clamp01(t);
		return startPoint * (1f - t) * (1f - t) * (1f - t) + 3f * anchor1 * (1f - t) * (1f - t) * t + 3f * anchor2 * (1f - t) * t * t + endPoint * t * t * t;
	}

	public Vector3 Extrude(Vector2 point, float t) {
		Vector3 tangent,up;
		return Extrude(point,t,out tangent,out up);
	}

	public Vector3 Extrude(Vector2 point, float t, out Vector3 tangent, out Vector3 up) {
		Vector3 sample1 = Evaluate(t);
		Vector3 sample2;

		float flipper = 1f;
		if (t+.01f<1f) {
			sample2 = Evaluate(t + .01f);
		} else {
			sample2 = Evaluate(t - .01f);
			flipper = -1f;
		}
		
		tangent = (sample2 - sample1).normalized * flipper;
		tangent.Normalize();

		// each spline uses one out of three possible twisting methods:
		Quaternion fromTo=Quaternion.identity;
		if (twistMode==0) {
			// method 1 - rotate startNormal around our current tangent
			float angle = Vector3.SignedAngle(startNormal,endNormal,tangent);
			fromTo = Quaternion.AngleAxis(angle,tangent);
		} else if (twistMode==1) {
			// method 2 - rotate startNormal toward endNormal
			fromTo = Quaternion.FromToRotation(startNormal,endNormal);
		} else if (twistMode==2) {
			// method 3 - rotate startNormal by "startOrientation-to-endOrientation" rotation
			Quaternion startRotation = Quaternion.LookRotation(startTangent,startNormal);
			Quaternion endRotation = Quaternion.LookRotation(endTangent * -1,endNormal);
			fromTo = endRotation* Quaternion.Inverse(startRotation);
		}
		// other twisting methods can be added, but they need to
		// respect the relationship between startNormal and endNormal.
		// for example: if startNormal and endNormal are equal, the road
		// can twist 0 or 360 degrees, but NOT 180.

		float smoothT = Mathf.SmoothStep(0f,1f,t * 1.02f - .01f);
		
		up = Quaternion.Slerp(Quaternion.identity,fromTo,smoothT) * startNormal;
		Vector3 right = Vector3.Cross(tangent,up);

		// measure twisting errors:
		// we have three possible spline-twisting methods, and
		// we test each spline with all three to find the best pick
		if (up.magnitude < .5f || right.magnitude < .5f) {
			errorCount++;
		}

		return sample1 + right * point.x + up * point.y;
	}
	 */

/*
 *
 * 	public Vector3 startPoint;
	public Vector3 anchor1;
	public Vector3 anchor2;
	public Vector3 endPoint;

	public float measuredLength;

	public Vector3Int startNormal;
	public Vector3Int endNormal;
	public Vector3Int startTangent;
	public Vector3Int endTangent;

 */

public struct BezierData : IComponentData
{
	public float3 startPoint;
	public float3 anchor1;
	public float3 anchor2;
	public float3 endPoint;   
	public int3 startNormal;
	public int3 endNormal;
	public int3 startTangent;
	public int3 endTangent;
	public int twistMode;
}

public struct SplineLength : IComponentData
{
	public float Value;
}

public struct SplineT : IComponentData
{
	public float Value;
}

public struct UpForwardVector : IComponentData
{
	public float UpValue;
	public float ForwardValue;
}

public struct SplineSideDirection : IComponentData
{
	public byte DirectionValue;
	public byte SideValue;
}

public struct Disabled:IComponentData{}
using UnityEngine;

/// <summary>
/// 쿄다이 팔 IK 계산 (2026-10-29). 게임(KyodaiLimb)과 모션 창의 편집 미리보기가 같은 계산을 쓰려고 뺐다.
/// </summary>
public static class KyodaiIK
{
    /// <summary>
    /// 팔만 커지며 뻗기: 목표까지 모자란 만큼 위팔(과 그 아래 전부)을 키운다. 어깨는 그대로라 몸은 뒤에 남는다.
    /// baseLength = 커지기 전 위팔+아래팔 길이(월드)
    /// </summary>
    public static void Grow(Transform upper, Vector3 restScale, float baseLength, Vector3 target, float maxGrow)
    {
        if (baseLength < 1e-4f) return;
        float need = Vector3.Distance(upper.position, target) / (baseLength * 0.98f);
        upper.localScale = restScale * Mathf.Clamp(need, 1f, Mathf.Max(1f, maxGrow));
    }

    /// <summary>
    /// 해석적 2관절 IK(코사인 법칙). 위팔·아래팔 회전만 바꿔 손목(end)이 target에 닿게 한다. 안 닿으면 쭉 편다.
    /// 매 프레임 기본 자세에서 출발해야 비틀림이 쌓이지 않는다(호출 전에 회전을 되돌릴 것)
    /// </summary>
    public static void SolveTwoBone(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 elbowHint)
    {
        Vector3 a = upper.position, b = lower.position, c = end.position, t = target;
        float lab = Vector3.Distance(a, b);
        float lcb = Vector3.Distance(b, c);
        if (lab < 1e-4f || lcb < 1e-4f) return;
        float lat = Mathf.Clamp(Vector3.Distance(a, t), 1e-4f, lab + lcb - 1e-4f);

        Vector3 ac = (c - a).normalized, ab = (b - a).normalized, at = (t - a).normalized;
        Vector3 ba = (a - b).normalized, bc = (c - b).normalized;

        float acAb0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ac, ab), -1f, 1f));
        float baBc0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ba, bc), -1f, 1f));
        float acAt0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ac, at), -1f, 1f));
        float acAb1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
        float baBc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));

        // 굽히는 축: 지금 팔꿈치 방향. 팔이 일자면(축이 0) 힌트 방향으로 굽힌다
        Vector3 axis0 = Vector3.Cross(c - a, b - a);
        if (axis0.sqrMagnitude < 1e-6f) axis0 = Vector3.Cross(c - a, elbowHint);
        if (axis0.sqrMagnitude < 1e-6f) axis0 = Vector3.Cross(c - a, Vector3.forward);
        axis0.Normalize();
        Vector3 axis1 = Vector3.Cross(c - a, t - a);
        bool aim = axis1.sqrMagnitude > 1e-8f;
        if (aim) axis1.Normalize();

        Quaternion upperWorld = upper.rotation;
        Quaternion lowerWorld = lower.rotation;
        Quaternion upperDelta = Quaternion.AngleAxis((acAb1 - acAb0) * Mathf.Rad2Deg, Quaternion.Inverse(upperWorld) * axis0);
        if (aim) upperDelta *= Quaternion.AngleAxis(acAt0 * Mathf.Rad2Deg, Quaternion.Inverse(upperWorld) * axis1);
        upper.localRotation *= upperDelta;
        lower.localRotation *= Quaternion.AngleAxis((baBc1 - baBc0) * Mathf.Rad2Deg, Quaternion.Inverse(lowerWorld) * axis0);
    }

    /// <summary>임시 상자 모델: 어깨에서 손까지 팔 막대를 늘려 잇는다</summary>
    public static void Stretch(Transform segment, Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        if (d.sqrMagnitude < 0.0001f) return;
        segment.position = (from + to) * 0.5f;
        segment.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        Vector3 s = segment.localScale;
        segment.localScale = new Vector3(s.x, d.magnitude, s.z);
    }
}

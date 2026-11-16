using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 파츠 이미지에 등급별 바깥 외곽선(UI/Outline)을 입힌다.
// 등급마다 머티리얼을 하나만 만들어 같이 써서, 같은 등급끼리는 UI 배칭이 깨지지 않는다
public static class PartOutline
{
    // 빌드에 포함되도록 셰이더는 YJW/Resources에 둔다
    private const string ShaderName = "UI/Outline";
    private const float OutlineSize = 2f;
    private const float OuterMode = 0f;

    private static readonly Dictionary<PartGrade, Material> Materials = new Dictionary<PartGrade, Material>();
    private static Shader _shader;
    private static bool _warned;

    // 파츠가 없으면 외곽선을 지운다
    public static void Apply(Image image, PartsDefinition part)
    {
        if (image == null)
            return;

        image.material = part != null ? GetMaterial(part.Grade) : null;
    }

    public static Color GradeColor(PartGrade grade)
    {
        switch (grade)
        {
            case PartGrade.Common: return new Color(0.62f, 0.62f, 0.62f, 1f);   // 회색
            case PartGrade.Rare: return new Color(0.1f, 0.52f, 0.95f, 1f);      // 하늘색 쪽 파란색 #1A85F2
            case PartGrade.Epic: return new Color(0.6f, 0.18f, 0.88f, 1f);      // 진한 보라색 #992EE0
            case PartGrade.Legendary: return new Color(1f, 0.85f, 0.2f, 1f);    // 노란색
            case PartGrade.Prototype: return new Color(1f, 0.25f, 0.25f, 1f);   // 빨간색
            default: return Color.clear;
        }
    }

    // 기본 등급은 외곽선 없이 둔다
    private static Material GetMaterial(PartGrade grade)
    {
        if (grade == PartGrade.Basic)
            return null;

        if (Materials.TryGetValue(grade, out Material material) && material != null)
            return material;

        if (_shader == null)
            _shader = Shader.Find(ShaderName);

        if (_shader == null)
        {
            if (!_warned)
            {
                _warned = true;
                Debug.LogWarning($"[PartOutline] '{ShaderName}' 셰이더를 찾지 못해 외곽선 없이 그립니다.");
            }
            return null;
        }

        material = new Material(_shader) { name = $"PartOutline_{grade}" };
        material.SetFloat("_Outline", 1f);
        material.SetColor("_OutlineColor", GradeColor(grade));
        material.SetFloat("_OutlineSize", OutlineSize);
        material.SetFloat("_OutlineMode", OuterMode);

        Materials[grade] = material;
        return material;
    }
}

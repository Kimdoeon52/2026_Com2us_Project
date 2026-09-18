# Unity JSON 데이터 관리 기초 가이드 (for KKH - 전투 데이터 매니지먼트)

이 문서는 Unity(C#) 환경에서 JSON 데이터를 설계하고 로드/관리하는 데 필요한 핵심 개념과 실무 팁을 정리한 가이드입니다.

---

## 1. JSON 기본 개념

JSON(JavaScript Object Notation)은 사람이 읽고 쓰기 쉽고, 기계가 파싱(해석)하기 쉬운 경량 텍스트 데이터 포맷입니다.

### 기본 문법 4가지
1. **객체 (Object) -> `{ }`**: C#의 `class` 또는 `struct` 1개 인스턴스에 대응
2. **배열 (Array) -> `[ ]`**: C#의 `List<T>` 또는 `T[]`에 대응
3. **Key-Value 쌍 -> `"key": value`**: 키 이름은 항상 큰따옴표(`""`)로 감쌈
4. **지원하는 값(Value) 타입**:
   - 문자열: `"Knight"`, `"HeavyScrap"`
   - 숫자: `100`, `25.5` (정수, 실수 모두 지원)
   - 불리언: `true`, `false`
   - 객체 및 배열: `{ ... }`, `[ ... ]`
   - 빈 값: `null`

> [!WARNING]
> **자주 발생하는 문법 에러 (Trailing Comma)**
> JSON은 마지막 요소 뒤에 쉼표(`,`)가 오면 파싱 에러가 발생합니다.
> - ❌ `{"id": 1, "hp": 100,}`
> - ⭕ `{"id": 1, "hp": 100}`

---

## 2. Unity 내장 `JsonUtility` 규칙

Unity는 자체적으로 고성능 내장 유틸리티인 `JsonUtility`를 제공합니다.

### 필수 준수 규칙
1. **`[System.Serializable]` 필수**: JSON으로 변환하거나 읽어올 모든 C# 클래스/구조체 위에 선언해야 합니다.
2. **필드명 일치**: C# 클래스의 변수명과 JSON의 `"key"` 이름이 대소문자까지 정확히 일치해야 합니다.
3. **루트 배열 불가 (가장 중요)**:
   - Unity `JsonUtility`는 최상위 루트가 `[ ... ]` (배열) 형태인 JSON을 직접 읽지 못합니다.
   - 따라서 반드시 루트를 `{ "dataList": [ ... ] }` 형태의 래퍼(Wrapper) 클래스로 감싸야 합니다.
4. **지원하지 않는 타입**: `Dictionary<TKey, TValue>`는 `JsonUtility`로 직접 직렬화/역직렬화할 수 없습니다. (리스트로 읽은 후 런타임에 딕셔너리로 변환 권장)

---

## 3. 전투 데이터 예시 실습

### (1) JSON 파일 (`EnemyData.json`)
```json
{
  "enemies": [
    {
      "id": 1001,
      "name": "ScrapScout",
      "maxHp": 250,
      "stamina": 100,
      "baseAttack": 30,
      "baseDefense": 10,
      "rewardGold": 120,
      "dropTable": [
        { "grade": 1, "weight": 70 },
        { "grade": 2, "weight": 30 }
      ]
    },
    {
      "id": 1002,
      "name": "HeavyScrapper",
      "maxHp": 600,
      "stamina": 80,
      "baseAttack": 65,
      "baseDefense": 35,
      "rewardGold": 300,
      "dropTable": [
        { "grade": 2, "weight": 60 },
        { "grade": 3, "weight": 40 }
      ]
    }
  ]
}
```

### (2) 매핑할 C# 데이터 모델 (`EnemyBattleData.cs`)
```csharp
using System;
using System.Collections.Generic;

// 드랍 보상 가중치 항목
[Serializable]
public struct DropRewardEntry
{
    public int grade;
    public int weight;
}

// 개별 적의 기본 정적 데이터
[Serializable]
public class EnemyBattleData
{
    public int id;
    public string name;
    public int maxHp;
    public int stamina;
    public int baseAttack;
    public int baseDefense;
    public int rewardGold;
    public List<DropRewardEntry> dropTable;
}

// JsonUtility 루트 래퍼 클래스
[Serializable]
public class EnemyDatabaseWrapper
{
    public List<EnemyBattleData> enemies;
}
```

---

## 4. Unity에서 JSON 읽어오기 (Load)

### 방법 A: TextAsset 방식 (가장 간편하고 빌드 시 안전)
JSON 파일을 `Assets/.../Resources/` 폴더에 넣거나 인스펙터의 `TextAsset` 필드에 드래그 앤 드롭하는 방식입니다.

```csharp
using UnityEngine;

public class BattleDataLoader : MonoBehaviour
{
    [SerializeField] private TextAsset enemyDataJsonAsset;

    public EnemyDatabaseWrapper LoadFromTextAsset()
    {
        if (enemyDataJsonAsset == null)
        {
            Debug.LogError("JSON 에셋이 연결되지 않았습니다.");
            return null;
        }

        // 텍스트를 바로 C# 클래스로 역직렬화
        return JsonUtility.FromJson<EnemyDatabaseWrapper>(enemyDataJsonAsset.text);
    }
}
```

### 방법 B: `System.IO.File` 파일 경로 방식 (외부 파일 로드)
```csharp
using System.IO;
using UnityEngine;

public class BattleDataLoader
{
    public EnemyDatabaseWrapper LoadFromFile(string relativePath)
    {
        string fullPath = Path.Combine(Application.dataPath, relativePath);
        
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"파일을 찾을 수 없습니다: {fullPath}");
            return null;
        }

        string jsonText = File.ReadAllText(fullPath);
        return JsonUtility.FromJson<EnemyDatabaseWrapper>(jsonText);
    }
}
```

---

## 5. 실무 데이터 매니지먼트 팁 (다른 파트원 지원용)

### 팁 1: 로드 후 `Dictionary`로 인덱싱하기
JSON에서 읽어온 데이터는 리스트(`List<T>`)입니다. 
다른 파트원(전투 액션, 연출 등)이 특정 ID의 몬스터 정보를 빠르게 찾을 수 있도록 매니저가 로드 시점에 `Dictionary<int, EnemyBattleData>`로 변환해 두는 것이 좋습니다.

```csharp
using System.Collections.Generic;
using UnityEngine;

public class BattleDataManager : MonoBehaviour
{
    private Dictionary<int, EnemyBattleData> _enemyDataMap = new Dictionary<int, EnemyBattleData>();

    public void Initialize(EnemyDatabaseWrapper database)
    {
        _enemyDataMap.Clear();
        foreach (var enemy in database.enemies)
        {
            _enemyDataMap[enemy.id] = enemy;
        }
    }

    // 다른 파트원이 손쉽게 호출하는 조회 함수
    public EnemyBattleData GetEnemyData(int id)
    {
        if (_enemyDataMap.TryGetValue(id, out var data))
            return data;

        Debug.LogWarning($"Enemy ID {id}를 찾을 수 없습니다.");
        return null;
    }
}
```

### 팁 2: 정적 데이터(JSON)와 런타임 상태(전투 중)의 분리
* **JSON 데이터**: 몬스터의 `maxHp = 250`, `baseAttack = 30` 같은 **원형(Prototype) 읽기 전용 데이터**입니다.
* **전투 런타임 상태**: 전투 중 공격을 받아 깎이는 `currentHp`, 버프 상태 등은 JSON 클래스 원본 값을 직접 깎지 말고, 별도의 런타임 객체(`BattleEntityState`)를 생성해 관리해야 데이터 오염이 발생하지 않습니다.


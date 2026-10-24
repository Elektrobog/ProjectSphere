using UnityEngine;
using System.Collections.Generic;

public class SpiralStepSpawner : MonoBehaviour
{
    [System.Serializable]
    public struct StepData
    {
        public float height;
        public float angleOffset;
        [Tooltip("Если > 0, переопределяет общий радиус для этой ступени")]
        public float customRadius;
    }

    [Header("Префаб ступени")]
    public GameObject stepPrefab;

    [Header("Параметры расположения")]
    public float radius = 1.5f;
    public float startAngle = 0f;

    [Header("Список ступеней")]
    public List<StepData> steps = new List<StepData>();

    [Header("Опции")]
    public bool destroyPreviousOnGenerate = true;

    private List<GameObject> spawnedSteps = new List<GameObject>();

    private void Start()
    {
        GenerateStepsRuntime();
    }

    public void GenerateStepsRuntime()
    {
        GenerateStepsInternal(destroyImmediate: false);
    }

    [ContextMenu("Generate Steps")]
    public void GenerateSteps()
    {
        GenerateStepsInternal(destroyImmediate: true);
    }

    private void GenerateStepsInternal(bool destroyImmediate)
    {
        if (stepPrefab == null)
        {
            Debug.LogError("Step Prefab не назначен!", this);
            return;
        }

        if (destroyPreviousOnGenerate)
            ClearSteps(destroyImmediate);

        foreach (var stepData in steps)
        {
            float effectiveRadius = stepData.customRadius > 0 ? stepData.customRadius : radius;
            float angle = startAngle + stepData.angleOffset;

            Vector3 pos = transform.position + new Vector3(
                effectiveRadius * Mathf.Cos(angle * Mathf.Deg2Rad),
                stepData.height,
                effectiveRadius * Mathf.Sin(angle * Mathf.Deg2Rad)
            );

            Quaternion rot = Quaternion.Euler(0f, angle, 0f);

            GameObject step = Instantiate(stepPrefab, pos, rot, transform);
            spawnedSteps.Add(step);

            // === АВТОМАТИЧЕСКАЯ ИНИЦИАЛИЗАЦИЯ ПЛАТФОРМЫ ===
            if (step.TryGetComponent<RotatingPlatform>(out var rotatingPlatform))
            {
                rotatingPlatform.Initialize(this.transform);   // ← КЛЮЧЕВАЯ СТРОКА
            }
        }

        Debug.Log($"Сгенерировано {spawnedSteps.Count} ступеней.");
    }

    [ContextMenu("Clear Steps")]
    public void ClearSteps()
    {
        ClearSteps(destroyImmediate: true);
    }

    private void ClearSteps(bool destroyImmediate)
    {
        foreach (var step in spawnedSteps)
        {
            if (step != null)
            {
                if (destroyImmediate)
                    DestroyImmediate(step);
                else
                    Destroy(step);
            }
        }
        spawnedSteps.Clear();
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(SpiralStepSpawner))]
public class SpiralStepSpawnerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        SpiralStepSpawner spawner = (SpiralStepSpawner)target;

        GUILayout.Space(10);
        GUILayout.Label("Управление", UnityEditor.EditorStyles.boldLabel);

        if (GUILayout.Button("Generate Steps"))
        {
            if (Application.isPlaying)
                spawner.GenerateStepsRuntime();
            else
                spawner.GenerateSteps();
        }

        if (GUILayout.Button("Clear Steps"))
        {
            spawner.ClearSteps();
        }
    }
}
#endif
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class CameraObstacleFade : MonoBehaviour
{
    [Header("Цели и слои")]
    [Tooltip("Трансформ шара, за которым следит камера.")]
    public Transform playerTarget;

    [Tooltip("Слои объектов, которые становятся полупрозрачными при перекрытии шара (колонна, платформы).")]
    public LayerMask fadeLayers = -1;

    [Header("Параметры прозрачности")]
    [Tooltip("Радиус коридора проверки от камеры до шара (в метрах).")]
    [Min(0.05f)] public float checkRadius = 0.6f;

    [Tooltip("Не проверять последние N метров перед шаром, чтобы платформа прямо под ним не мигала.")]
    [Min(0f)] public float endMargin = 0.4f;

    [Tooltip("Смещение точки прицеливания относительно центра шара.")]
    public Vector3 targetOffset = new Vector3(0f, 0.25f, 0f);

    [Tooltip("Скорость плавного перехода в полупрозрачность и обратно.")]
    [Min(0.5f)] public float fadeSpeed = 6f;

    [Tooltip("Небольшая задержка (сек) перед возвратом непрозрачности, чтобы убрать дребезг на краю платформы.")]
    [Min(0f)] public float unfadeDelay = 0.08f;

    [Header("Производительность")]
    [Tooltip("Частота физических проверок препятствий. Прозрачность продолжает обновляться каждый кадр.")]
    [Min(0.01f)] public float scanInterval = 0.033f;

    private static readonly int ObjectFadeAmountId = Shader.PropertyToID("_ObjectFadeAmount");
    private static readonly int CamCutoutOriginId = Shader.PropertyToID("_CamCutoutOrigin");

    private MaterialPropertyBlock mpb;
    private float nextScanTime;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[32];
    private readonly Collider[] overlapBuffer = new Collider[16];

    private class FadedGroup
    {
        public Renderer[] renderers;
        public float currentFade;
        public float lastAppliedFade = -1f;
        public float lastBlockedTime;
        public bool isActive;
    }

    private readonly Dictionary<Collider, FadedGroup> colliderCache = new Dictionary<Collider, FadedGroup>(64);
    private readonly Dictionary<Transform, FadedGroup> rootCache = new Dictionary<Transform, FadedGroup>(32);
    private readonly List<FadedGroup> activeGroups = new List<FadedGroup>(16);

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
    }

    void OnDisable()
    {
        for (int i = 0; i < activeGroups.Count; i++)
        {
            ApplyFadeToRenderers(activeGroups[i], 0f);
            activeGroups[i].isActive = false;
        }
        activeGroups.Clear();
        Shader.SetGlobalVector(CamCutoutOriginId, Vector4.zero);
    }

    void LateUpdate()
    {
        if (playerTarget == null)
            return;

        float now = Time.time;
        Vector3 camPos = transform.position;
        Vector3 targetPos = playerTarget.position + targetOffset;
        Shader.SetGlobalVector(CamCutoutOriginId, new Vector4(camPos.x, camPos.y, camPos.z, 1f));

        if (now >= nextScanTime)
        {
            nextScanTime = now + Mathf.Max(0.01f, scanInterval);

            // 1. Проверка зоны вокруг самой камеры без слепой зоны
            int overlaps = Physics.OverlapSphereNonAlloc(camPos, checkRadius, overlapBuffer, fadeLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps; i++)
            {
                MarkColliderBlocked(overlapBuffer[i], now);
            }

            // 2. Проверка коридора от камеры к шару
            Vector3 toTarget = targetPos - camPos;
            float dist = toTarget.magnitude;
            if (dist > 0.1f)
            {
                Vector3 dir = toTarget / dist;
                float checkDist = Mathf.Max(0.05f, dist - endMargin);

                int hits = Physics.SphereCastNonAlloc(camPos, checkRadius, dir, hitBuffer, checkDist, fadeLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits; i++)
                {
                    MarkColliderBlocked(hitBuffer[i].collider, now);
                }
            }
        }

        // 3. Плавное обновление только активных групп
        float step = fadeSpeed * Time.deltaTime;
        for (int i = activeGroups.Count - 1; i >= 0; i--)
        {
            FadedGroup group = activeGroups[i];
            bool blocked = (now - group.lastBlockedTime) <= unfadeDelay;
            float targetFade = blocked ? 1f : 0f;

            group.currentFade = Mathf.MoveTowards(group.currentFade, targetFade, step);

            if (Mathf.Abs(group.currentFade - group.lastAppliedFade) > 0.002f)
            {
                ApplyFadeToRenderers(group, group.currentFade);
            }

            if (!blocked && group.currentFade <= 0.001f)
            {
                ApplyFadeToRenderers(group, 0f);
                group.isActive = false;
                activeGroups.RemoveAt(i);
            }
        }
    }

    private void MarkColliderBlocked(Collider col, float now)
    {
        if (col == null) return;
        if (col.transform == playerTarget || col.transform.IsChildOf(playerTarget)) return;

        if (!colliderCache.TryGetValue(col, out FadedGroup group))
        {
            Transform groupRoot = ResolveObjectRoot(col.transform);
            if (groupRoot == null) return;

            if (!rootCache.TryGetValue(groupRoot, out group))
            {
                Renderer[] allRenderers = groupRoot.GetComponentsInChildren<Renderer>(true);
                var fadeable = new List<Renderer>(allRenderers.Length);

                for (int i = 0; i < allRenderers.Length; i++)
                {
                    Renderer r = allRenderers[i];
                    if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(ObjectFadeAmountId))
                        fadeable.Add(r);
                }

                group = new FadedGroup
                {
                    renderers = fadeable.ToArray(),
                    currentFade = 0f,
                    lastAppliedFade = -1f
                };
                rootCache[groupRoot] = group;
            }
            colliderCache[col] = group;
        }

        if (group.renderers == null || group.renderers.Length == 0)
            return;

        group.lastBlockedTime = now;
        if (!group.isActive)
        {
            group.isActive = true;
            activeGroups.Add(group);
        }
    }

    private Transform ResolveObjectRoot(Transform t)
    {
        var platform = t.GetComponentInParent<IPlatformVelocityProvider>();
        if (platform is Component comp)
            return comp.transform;

        Transform current = t;

        // Если коллайдер висит на отдельном дочернем объекте без своих Renderer (например, ~gen_ColumnCollider),
        // поднимаемся до родителя, которому принадлежат визуальные меши объекта.
        while (current.parent != null && current.GetComponentInChildren<Renderer>(true) == null)
        {
            current = current.parent;
        }

        // Поднимаемся выше только пока родитель является частью той же составной модели (имеет Renderer или LODGroup),
        // не захватывая корневые папки-контейнеры сцены.
        while (current.parent != null && ((1 << current.parent.gameObject.layer) & fadeLayers.value) != 0)
        {
            Transform parent = current.parent;
            if (parent.GetComponent<Renderer>() == null && parent.GetComponent<LODGroup>() == null)
                break;

            current = parent;
        }

        return current;
    }

    private void ApplyFadeToRenderers(FadedGroup group, float fadeAmount)
    {
        group.lastAppliedFade = fadeAmount;
        Renderer[] renderers = group.renderers;
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(mpb);
            mpb.SetFloat(ObjectFadeAmountId, fadeAmount);
            r.SetPropertyBlock(mpb);
        }
    }
}
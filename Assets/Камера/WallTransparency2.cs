using UnityEngine;
using System.Collections.Generic;

public class WallTransparency2 : MonoBehaviour
{
    [SerializeField] private Transform _playerTarget;
    [SerializeField] private LayerMask _wallLayer;

    [Header("Настройки дырки")]
    [SerializeField] private float _holeSize = 0.5f; // Размер дырки
    [SerializeField] private float _holeSharpness = 2.0f; // Резкость границы (1=линейно, >1=резче)
    [SerializeField] private float _fadeSpeed = 8f; // Скорость появления/исчезновения

    [Header("Смещение дырки")]
    [SerializeField] private Vector3 _offset = new Vector3(0f, -0.3f, 0f);

    [Header("Оптимизация")]
    [SerializeField] private int _maxSimultaneousWalls = 5; // Макс. стен одновременно
    

    private MaterialPropertyBlock _mpb;
    private Camera _camera;

    private class WallData
    {
        public float currentHoleSize = 0f;
        public float targetHoleSize = 0f;
        public Vector3 fadeCenterWorld;
        public bool isActive = false;
    }

    // Используем Dictionary для быстрого доступа O(1)
    private readonly Dictionary<Renderer, WallData> _wallData = new();
    private readonly HashSet<Renderer> _currentFrameWalls = new();
    private readonly Dictionary<Renderer, WallData> _currentCenters = new();

    void Start()
    {
        _mpb = new MaterialPropertyBlock();
        _camera = GetComponent<Camera>();
        if (_camera == null) _camera = Camera.main;
    }

    void LateUpdate()
    {
        Process();
    }

    void Process()
    {
        FindWalls();
        UpdateWalls();
        Cleanup();
    }

    void FindWalls()
    {
        _currentFrameWalls.Clear();
        _currentCenters.Clear();

        if (_playerTarget == null) return;

        Vector3 camPos = _camera.transform.position;
        Vector3 playerPos = _playerTarget.position;
        Vector3 dir = (playerPos - camPos).normalized;
        float distToPlayer = Vector3.Distance(camPos, playerPos);

        // Используем RaycastAll для всех стен на пути
        RaycastHit[] hits = Physics.RaycastAll(camPos, dir, distToPlayer, _wallLayer);

        // Сортируем по расстоянию (от ближних к дальним)
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        // Ограничиваем количество одновременно обрабатываемых стен
        int wallCount = Mathf.Min(hits.Length, _maxSimultaneousWalls);

        for (int i = 0; i < wallCount; i++)
        {
            RaycastHit hit = hits[i];
            Renderer wallRenderer = hit.collider.GetComponent<Renderer>();

            if (wallRenderer == null) continue;

            // Проверяем, поддерживает ли материал эффект дырки
            if (!HasHoleEffectShader(wallRenderer)) continue;

            // Центр дырки = точка попадания + смещение
            Vector3 fadeCenterWorld = hit.point + _offset;

            WallData data = new WallData
            {
                fadeCenterWorld = fadeCenterWorld,
                isActive = true
            };

            _currentFrameWalls.Add(wallRenderer);
            _currentCenters[wallRenderer] = data;
        }
    }

    bool HasHoleEffectShader(Renderer renderer)
    {
        // Проверяем все материалы рендерера
        foreach (var material in renderer.sharedMaterials)
        {
            if (material == null) continue;

            // Проверяем наличие нужных свойств в шейдере
            if (material.HasProperty("_FadeCenter") &&
                material.HasProperty("_FadeRadius"))
            {
                return true;
            }
        }
        return false;
    }

    void UpdateWalls()
    {
        // Обновляем существующие стены
        foreach (var kvp in _wallData)
        {
            Renderer wall = kvp.Key;
            if (wall == null) continue;

            WallData data = kvp.Value;

            if (_currentFrameWalls.Contains(wall))
            {
                // Стена всё ещё перекрывает игрока
                data.targetHoleSize = _holeSize;
                data.fadeCenterWorld = _currentCenters[wall].fadeCenterWorld;
                data.isActive = true;
            }
            else
            {
                // Стена больше не перекрывает - убираем дырку
                data.targetHoleSize = 0f;
                data.isActive = false;
            }

            // Плавное изменение размера дырки
            data.currentHoleSize = Mathf.Lerp(
                data.currentHoleSize,
                data.targetHoleSize,
                Time.deltaTime * _fadeSpeed
            );

            ApplyToWall(wall, data);
        }

        // Добавляем новые стены
        foreach (Renderer wall in _currentFrameWalls)
        {
            if (!_wallData.ContainsKey(wall))
            {
                WallData data = _currentCenters[wall];
                data.currentHoleSize = 0f; // Начинаем с нуля
                data.targetHoleSize = _holeSize;
                data.isActive = true;

                _wallData[wall] = data;
                ApplyToWall(wall, data);
            }
        }
    }

    void ApplyToWall(Renderer wall, WallData data)
    {
        // Применяем ко всем сабмешам
        for (int i = 0; i < wall.sharedMaterials.Length; i++)
        {
            var material = wall.sharedMaterials[i];
            if (material == null) continue;

            // Проверяем, поддерживает ли этот материал эффект
            if (!material.HasProperty("_FadeCenter") ||
                !material.HasProperty("_FadeRadius"))
                continue;

            wall.GetPropertyBlock(_mpb, i);

            // Основные параметры для дырки
            _mpb.SetVector("_FadeCenter", data.fadeCenterWorld);
            _mpb.SetFloat("_FadeRadius", data.currentHoleSize);

            // Опциональные параметры (если есть в шейдере)
            if (material.HasProperty("_HoleSharpness"))
                _mpb.SetFloat("_HoleSharpness", _holeSharpness);

            if (material.HasProperty("_Cutoff"))
                _mpb.SetFloat("_Cutoff", 0.5f); // Стандартный порог для alpha clipping

            wall.SetPropertyBlock(_mpb, i);
        }
    }

    void Cleanup()
    {
        List<Renderer> toRemove = new();

        foreach (var kvp in _wallData)
        {
            if (kvp.Key == null)
            {
                toRemove.Add(kvp.Key);
                continue;
            }

            // Если дырка почти исчезла и стена неактивна - очищаем
            if (!kvp.Value.isActive && kvp.Value.currentHoleSize < 0.01f)
            {
                ClearWallProperties(kvp.Key);
                toRemove.Add(kvp.Key);
            }
        }

        // Удаляем из словаря
        foreach (var renderer in toRemove)
            _wallData.Remove(renderer);
    }

    void ClearWallProperties(Renderer wall)
    {
        if (wall == null) return;

        for (int i = 0; i < wall.sharedMaterials.Length; i++)
        {
            wall.GetPropertyBlock(_mpb, i);
            _mpb.Clear();
            wall.SetPropertyBlock(_mpb, i);
        }
    }

    // Визуализация для отладки в редакторе
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.red;

        foreach (var kvp in _wallData)
        {
            if (kvp.Value.isActive)
            {
                // Рисуем сферу в центре дырки
                Gizmos.DrawWireSphere(kvp.Value.fadeCenterWorld, kvp.Value.currentHoleSize);

                // Линия от камеры к центру дырки
                if (_camera != null)
                {
                    Gizmos.DrawLine(_camera.transform.position, kvp.Value.fadeCenterWorld);
                }
            }
        }
    }
}
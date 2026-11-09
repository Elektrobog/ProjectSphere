using UnityEngine;

/// <summary>
/// Метка "эта платформа создана спавнером". Кладётся на КАЖДУЮ сгенерированную
/// платформу (и на вращающиеся, и на стоящие) и сохраняется в сцене — по ней
/// спавнер находит свои платформы после Play и переоткрытия сцены.
///
/// ВАЖНО: класс лежит в ОТДЕЛЬНОМ файле с тем же именем. Unity не может сохранить
/// MonoBehaviour, объявленный внутри чужого файла, — компонент превращается в
/// "The referenced script (Unknown) on this Behaviour is missing!".
/// </summary>
public class SpiralPlatformMarker : MonoBehaviour
{
    [HideInInspector] public string spawnerId;
    [HideInInspector] public string platformId;
}

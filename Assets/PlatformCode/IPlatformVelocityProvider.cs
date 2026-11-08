using UnityEngine;

/// <summary>
/// Платформа сообщает свою скорость объектам, стоящим на ней.
/// Используется движущимися платформами и персонажем.
/// </summary>
public interface IPlatformVelocityProvider
{
    /// Линейная скорость платформы в заданной точке (мировая).
    Vector3 GetVelocityAtPoint(Vector3 point);

    /// Угловая скорость платформы (рад/сек).
    Vector3 GetAngularVelocity();
}
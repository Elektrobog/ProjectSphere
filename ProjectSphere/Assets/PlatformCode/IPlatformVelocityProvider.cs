using UnityEngine;

public interface IPlatformVelocityProvider
{
    Vector3 GetVelocityAtPoint(Vector3 worldPoint);
    Vector3 GetAngularVelocity(); // Метод, который мы добавили
}
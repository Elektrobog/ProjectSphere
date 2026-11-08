using TMPro;
using UnityEngine;

/// Показывает версию сборки на тексте меню.
public class BuildVersionDisplay : MonoBehaviour
{
    [Tooltip("Текст, на который выводится версия (пусто — TMP_Text на этом объекте)")]
    public TMP_Text versionText;
    [Tooltip("Префикс перед номером версии")]
    public string prefix = "v";

    private void Awake()
    {
        if (versionText == null)
            versionText = GetComponent<TMP_Text>();
        if (versionText != null)
            versionText.text = prefix + Application.version;
        else
            Debug.LogWarning("[BuildVersionDisplay] Не найден TMP_Text для вывода версии.");
    }
}
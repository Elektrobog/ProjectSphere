using UnityEngine;
using TMPro; // если используешь TextMeshPro

public class BuildVersionDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI versionText;

    void Start()
    {
        versionText.text = "v" + Application.version;
    }
}

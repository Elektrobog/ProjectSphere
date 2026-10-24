using UnityEngine;

public class TileController : MonoBehaviour
{
    public bool openForward;
    public bool openBack;
    public bool openLeft;
    public bool openRight;

    // —сылки на колонны (перетащить в инспекторе)
    public Column columnForward;
    public Column columnBack;
    public Column columnLeft;
    public Column columnRight;

    private MazeGenerator generator;

    private void Start()
    {
        generator = FindAnyObjectByType<MazeGenerator>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball")) return;
        generator.ActivateTile(this);
    }

    public void SetAllColumns(bool state)
    {
        if (columnForward) columnForward.SetUp(state);
        if (columnBack) columnBack.SetUp(state);
        if (columnLeft) columnLeft.SetUp(state);
        if (columnRight) columnRight.SetUp(state);
    }
}
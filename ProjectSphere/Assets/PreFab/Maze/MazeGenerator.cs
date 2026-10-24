using UnityEngine;
using System.Collections.Generic;

public class MazeGenerator : MonoBehaviour
{
    public enum SpawnMode
    {
        FromCorner,
        Centered,
        CustomPosition
    }

    [Header("Spawn Settings")]
    public SpawnMode spawnMode = SpawnMode.FromCorner;
    public Vector3 customSpawnPosition;

    [Header("Maze Settings")]
    public int width = 20;
    public int height = 20;
    public float tileSize = 8f;

    [Header("Entrance / Exit")]
    public Vector2Int entranceCoords = new Vector2Int(0, 0);
    public Vector2Int exitCoords = new Vector2Int(19, 19);

    [Header("Prefabs")]
    public GameObject tilePrefab;

    [Header("Debug")]
    public bool debugMode = true;
    public Color openColor = Color.green;
    public Color entranceColor = Color.blue;
    public Color exitColor = Color.yellow;

    private TileController[,] tiles;

    [HideInInspector] public TileController entranceTile;
    [HideInInspector] public TileController exitTile;

    private void Start()
    {
        Generate();
    }

    void Generate()
    {
        tiles = new TileController[width, height];

        Vector3 origin = CalculateOrigin();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 pos = origin + new Vector3(x * tileSize, 0, y * tileSize);
                GameObject obj = Instantiate(tilePrefab, pos, Quaternion.identity, transform);
                tiles[x, y] = obj.GetComponent<TileController>();
            }
        }

        bool[,] visited = new bool[width, height];
        GenerateDFS(entranceCoords.x, entranceCoords.y, visited);

        entranceTile = tiles[entranceCoords.x, entranceCoords.y];
        exitTile = tiles[exitCoords.x, exitCoords.y];

        OpenExternalSides(entranceTile, entranceCoords);
        OpenExternalSides(exitTile, exitCoords);
    }

    Vector3 CalculateOrigin()
    {
        switch (spawnMode)
        {
            case SpawnMode.FromCorner:
                return transform.position;
            case SpawnMode.Centered:
                return transform.position - new Vector3((width * tileSize) / 2f, 0, (height * tileSize) / 2f);
            case SpawnMode.CustomPosition:
                return customSpawnPosition;
            default:
                return transform.position;
        }
    }

    void GenerateDFS(int x, int y, bool[,] visited)
    {
        visited[x, y] = true;

        Vector2Int[] dirs = {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(1, 0)
        };

        for (int i = 0; i < dirs.Length; i++)
        {
            int r = Random.Range(i, dirs.Length);
            (dirs[i], dirs[r]) = (dirs[r], dirs[i]);
        }

        foreach (var d in dirs)
        {
            int nx = x + d.x;
            int ny = y + d.y;

            if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
            if (visited[nx, ny]) continue;

            if (d.x == 1) { tiles[x, y].openRight = true; tiles[nx, ny].openLeft = true; }
            if (d.x == -1) { tiles[x, y].openLeft = true; tiles[nx, ny].openRight = true; }
            if (d.y == 1) { tiles[x, y].openForward = true; tiles[nx, ny].openBack = true; }
            if (d.y == -1) { tiles[x, y].openBack = true; tiles[nx, ny].openForward = true; }

            GenerateDFS(nx, ny, visited);
        }
    }

    void OpenExternalSides(TileController tile, Vector2Int coords)
    {
        if (coords.x == 0) tile.openLeft = true;
        if (coords.x == width - 1) tile.openRight = true;
        if (coords.y == 0) tile.openBack = true;
        if (coords.y == height - 1) tile.openForward = true;
    }

    public void ActivateTile(TileController tile)
    {
        foreach (var t in tiles)
            t.SetAllColumns(false);

        Vector2Int pos = FindTile(tile);

        // Вперёд (+Y)
        if (pos.y + 1 < height)
        {
            if (!tile.openForward)
            {
                TileController neighbor = tiles[pos.x, pos.y + 1];
                if (neighbor.columnBack != null)
                    neighbor.columnBack.SetUp(true);
            }
        }
        else
        {
            if (!(tile == exitTile && tile.openForward) && !(tile == entranceTile && tile.openForward))
                if (tile.columnForward != null)
                    tile.columnForward.SetUp(true);
        }

        // Назад (-Y)
        if (pos.y - 1 >= 0)
        {
            if (!tile.openBack)
            {
                TileController neighbor = tiles[pos.x, pos.y - 1];
                if (neighbor.columnForward != null)
                    neighbor.columnForward.SetUp(true);
            }
        }
        else
        {
            if (!(tile == entranceTile && tile.openBack))
                if (tile.columnBack != null)
                    tile.columnBack.SetUp(true);
        }

        // Влево (-X)
        if (pos.x - 1 >= 0)
        {
            if (!tile.openLeft)
            {
                TileController neighbor = tiles[pos.x - 1, pos.y];
                if (neighbor.columnRight != null)
                    neighbor.columnRight.SetUp(true);
            }
        }
        else
        {
            if (!(tile == entranceTile && tile.openLeft) && !(tile == exitTile && tile.openLeft))
                if (tile.columnLeft != null)
                    tile.columnLeft.SetUp(true);
        }

        // Вправо (+X)
        if (pos.x + 1 < width)
        {
            if (!tile.openRight)
            {
                TileController neighbor = tiles[pos.x + 1, pos.y];
                if (neighbor.columnLeft != null)
                    neighbor.columnLeft.SetUp(true);
            }
        }
        else
        {
            if (!(tile == entranceTile && tile.openRight) && !(tile == exitTile && tile.openRight))
                if (tile.columnRight != null)
                    tile.columnRight.SetUp(true);
        }
    }

    Vector2Int FindTile(TileController tile)
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (tiles[x, y] == tile)
                    return new Vector2Int(x, y);
        return Vector2Int.zero;
    }

    private void OnDrawGizmos()
    {
        if (!debugMode || tiles == null) return;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                TileController tile = tiles[x, y];
                if (tile == null) continue;

                Vector3 pos = tile.transform.position;

                Gizmos.color = Color.gray;
                Gizmos.DrawWireCube(pos, new Vector3(tileSize, 0.1f, tileSize));

                Gizmos.color = openColor;

                if (tile.openForward)
                    Gizmos.DrawLine(pos, pos + Vector3.forward * tileSize);
                if (tile.openBack)
                    Gizmos.DrawLine(pos, pos + Vector3.back * tileSize);
                if (tile.openLeft)
                    Gizmos.DrawLine(pos, pos + Vector3.left * tileSize);
                if (tile.openRight)
                    Gizmos.DrawLine(pos, pos + Vector3.right * tileSize);
            }
        }

        if (entranceTile != null)
        {
            Gizmos.color = entranceColor;
            Gizmos.DrawSphere(entranceTile.transform.position + Vector3.up * 2, 1f);
        }

        if (exitTile != null)
        {
            Gizmos.color = exitColor;
            Gizmos.DrawSphere(exitTile.transform.position + Vector3.up * 2, 1f);
        }
    }
}
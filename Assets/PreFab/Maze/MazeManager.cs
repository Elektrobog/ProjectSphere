using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // обязательно для новой системы ввода
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    [Header("Grid Settings")]
    public int width = 10;
    public int depth = 10;
    public float tileSize = 8f;
    public float loweredHeight = 0.1f;
    public float raisedHeight = 20f;
    public float animationDuration = 0.3f;

    [Header("Grid Offset")]
    public Vector3 gridOffset = Vector3.zero;

    [Header("Start and End Positions")]
    public int startX = 0;
    public int startZ = 0;
    public int endX = 9;
    public int endZ = 9;

    [Header("Maze Generation")]
    public int randomSeed = 0;
    public int extraPassages = 10;

    [Header("Start Behavior")]
    public bool raiseWallsAtStart = true;

    [Header("Debug Visualization")]
    public bool showDebugPath = true;
    public Color pathColor = Color.blue;
    public bool showWrongPaths = true;
    public Color wrongPathColor = Color.red;

    [Header("Prefabs")]
    public GameObject columnPrefab;

    // --- Секретная комбинация ---
    [Header("Secret Cheat")]
    public KeyCode[] secretKeySequence;          // последовательность клавиш
    public float secretTimeout = 2f;              // время на ввод всей комбинации
    public bool showPathOnSecret = true;          // показывать путь при активации

    private int secretSequenceIndex = 0;
    private float lastSecretKeyTime = 0f;
    private bool secretActive = false;             // текущее состояние видимости пути
    private Dictionary<KeyCode, Key> keyCodeToKey; // словарь для преобразования KeyCode в Key новой системы

    // --- Остальные поля ---
    private struct CellData { public bool passageNorth, passageSouth, passageWest, passageEast; }
    private CellData[,] mazeData;
    private Transform[,] columns;
    private StepDetector[,] detectors;
    private bool[,] isRaised;
    private int previousX = -1, previousZ = -1;
    private List<Vector2Int> raisedNeighbors = new List<Vector2Int>();
    private LineRenderer pathLine;
    private List<LineRenderer> wrongPathLines = new List<LineRenderer>();
    private bool isMazeSpawned = false;

    void Awake()
    {
        if (Instance == null) Instance = this; else Destroy(gameObject);
        InitializeKeyMap(); // инициализация словаря для клавиш
    }

    void Start() { }

    // --- Инициализация словаря для преобразования KeyCode в Key новой системы ---
    void InitializeKeyMap()
    {
        keyCodeToKey = new Dictionary<KeyCode, Key>
        {
            { KeyCode.A, Key.A },
            { KeyCode.B, Key.B },
            { KeyCode.C, Key.C },
            { KeyCode.D, Key.D },
            { KeyCode.E, Key.E },
            { KeyCode.F, Key.F },
            { KeyCode.G, Key.G },
            { KeyCode.H, Key.H },
            { KeyCode.I, Key.I },
            { KeyCode.J, Key.J },
            { KeyCode.K, Key.K },
            { KeyCode.L, Key.L },
            { KeyCode.M, Key.M },
            { KeyCode.N, Key.N },
            { KeyCode.O, Key.O },
            { KeyCode.P, Key.P },
            { KeyCode.Q, Key.Q },
            { KeyCode.R, Key.R },
            { KeyCode.S, Key.S },
            { KeyCode.T, Key.T },
            { KeyCode.U, Key.U },
            { KeyCode.V, Key.V },
            { KeyCode.W, Key.W },
            { KeyCode.X, Key.X },
            { KeyCode.Y, Key.Y },
            { KeyCode.Z, Key.Z },
            { KeyCode.Alpha0, Key.Digit0 },
            { KeyCode.Alpha1, Key.Digit1 },
            { KeyCode.Alpha2, Key.Digit2 },
            { KeyCode.Alpha3, Key.Digit3 },
            { KeyCode.Alpha4, Key.Digit4 },
            { KeyCode.Alpha5, Key.Digit5 },
            { KeyCode.Alpha6, Key.Digit6 },
            { KeyCode.Alpha7, Key.Digit7 },
            { KeyCode.Alpha8, Key.Digit8 },
            { KeyCode.Alpha9, Key.Digit9 },
            { KeyCode.Space, Key.Space },
            { KeyCode.Return, Key.Enter },
            { KeyCode.Escape, Key.Escape },
            { KeyCode.UpArrow, Key.UpArrow },
            { KeyCode.DownArrow, Key.DownArrow },
            { KeyCode.LeftArrow, Key.LeftArrow },
            { KeyCode.RightArrow, Key.RightArrow },
            { KeyCode.F1, Key.F1 },
            { KeyCode.F2, Key.F2 },
            { KeyCode.F3, Key.F3 },
            { KeyCode.F4, Key.F4 },
            { KeyCode.F5, Key.F5 },
            { KeyCode.F6, Key.F6 },
            { KeyCode.F7, Key.F7 },
            { KeyCode.F8, Key.F8 },
            { KeyCode.F9, Key.F9 },
            { KeyCode.F10, Key.F10 },
            { KeyCode.F11, Key.F11 },
            { KeyCode.F12, Key.F12 }
        };
    }

    void Update()
    {
        // Проверка секретной комбинации
        CheckSecretInput();
    }

    // --- Проверка секретной комбинации с использованием новой Input System ---
    void CheckSecretInput()
    {
        if (secretKeySequence == null || secretKeySequence.Length == 0 || keyCodeToKey == null)
            return;

        // Если прошло слишком много времени с последнего нажатия – сброс
        if (secretSequenceIndex > 0 && Time.time - lastSecretKeyTime > secretTimeout)
        {
            secretSequenceIndex = 0;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Проверяем, нажата ли какая-либо клавиша в этом кадре
        if (keyboard.anyKey.wasPressedThisFrame)
        {
            KeyCode? pressedKey = null;
            foreach (var kv in keyCodeToKey)
            {
                if (keyboard[kv.Value].wasPressedThisFrame)
                {
                    pressedKey = kv.Key;
                    break;
                }
            }

            if (pressedKey.HasValue)
            {
                if (pressedKey.Value == secretKeySequence[secretSequenceIndex])
                {
                    // Правильная клавиша
                    lastSecretKeyTime = Time.time;
                    secretSequenceIndex++;
                    if (secretSequenceIndex >= secretKeySequence.Length)
                    {
                        // Комбинация полностью введена
                        secretSequenceIndex = 0;
                        ActivateSecret();
                    }
                }
                else
                {
                    // Неправильная клавиша – сброс
                    secretSequenceIndex = 0;
                }
            }
        }
    }

    void ActivateSecret()
    {
        if (showPathOnSecret)
        {
            secretActive = !secretActive;
            // Включаем/выключаем видимость отладочных линий
            if (pathLine != null) pathLine.gameObject.SetActive(secretActive);
            foreach (var line in wrongPathLines)
                if (line != null) line.gameObject.SetActive(secretActive);
            Debug.Log($"Secret cheat: path visibility = {secretActive}");
        }
        else
        {
            Debug.Log("Secret cheat activated, but no action assigned.");
        }
    }

    // --- Остальные методы без изменений (SpawnMaze, ClearMaze, GenerateMaze и т.д.) ---
    public void SpawnMaze()
    {
        if (isMazeSpawned) ClearMaze();
        columns = new Transform[width, depth];
        detectors = new StepDetector[width, depth];
        isRaised = new bool[width, depth];
        for (int x = 0; x < width; x++)
            for (int z = 0; z < depth; z++)
            {
                Vector3 pos = transform.position + new Vector3(x * tileSize, 0, z * tileSize) + gridOffset;
                GameObject col = Instantiate(columnPrefab, pos, Quaternion.identity, transform);
                col.name = $"Column_{x}_{z}";
                columns[x, z] = col.transform;
                SetColumnHeight(x, z, loweredHeight, true);
                StepDetector det = col.GetComponentInChildren<StepDetector>();
                if (det != null) { det.gridX = x; det.gridZ = z; detectors[x, z] = det; }
                isRaised[x, z] = false;
            }
        GenerateMaze();
        if (showDebugPath) DrawDebugPath();
        if (raiseWallsAtStart) PlayerSteppedOnTile(startX, startZ);
        isMazeSpawned = true;
        Debug.Log("Maze spawned.");
    }

    public void ClearMaze()
    {
        if (!isMazeSpawned) return;
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
        columns = null; detectors = null; isRaised = null; mazeData = null;
        previousX = -1; previousZ = -1; raisedNeighbors.Clear();
        if (pathLine != null) DestroyImmediate(pathLine.gameObject);
        foreach (var l in wrongPathLines) if (l != null) DestroyImmediate(l.gameObject);
        wrongPathLines.Clear();
        isMazeSpawned = false;
        Debug.Log("Maze cleared.");
    }

    void GenerateMaze()
    {
        mazeData = new CellData[width, depth];
        System.Random rand = (randomSeed != 0) ? new System.Random(randomSeed) : new System.Random();
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        bool[,] visited = new bool[width, depth];
        Vector2Int start = new Vector2Int(startX, startZ);
        stack.Push(start); visited[start.x, start.y] = true;
        Vector2Int[] dirs = { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(1, 0) };
        while (stack.Count > 0)
        {
            Vector2Int cur = stack.Peek();
            List<Vector2Int> neighbors = new List<Vector2Int>();
            foreach (var d in dirs)
            {
                int nx = cur.x + d.x, nz = cur.y + d.y;
                if (nx >= 0 && nx < width && nz >= 0 && nz < depth && !visited[nx, nz]) neighbors.Add(new Vector2Int(nx, nz));
            }
            if (neighbors.Count > 0)
            {
                Vector2Int next = neighbors[rand.Next(neighbors.Count)];
                visited[next.x, next.y] = true;
                int dx = next.x - cur.x, dz = next.y - cur.y;
                if (dx == 1) { mazeData[cur.x, cur.y].passageEast = true; mazeData[next.x, next.y].passageWest = true; }
                else if (dx == -1) { mazeData[cur.x, cur.y].passageWest = true; mazeData[next.x, next.y].passageEast = true; }
                else if (dz == 1) { mazeData[cur.x, cur.y].passageNorth = true; mazeData[next.x, next.y].passageSouth = true; }
                else if (dz == -1) { mazeData[cur.x, cur.y].passageSouth = true; mazeData[next.x, next.y].passageNorth = true; }
                stack.Push(next);
            }
            else stack.Pop();
        }
        if (extraPassages > 0)
        {
            List<System.Tuple<Vector2Int, Vector2Int>> walls = new List<System.Tuple<Vector2Int, Vector2Int>>();
            for (int x = 0; x < width; x++) for (int z = 0; z < depth; z++)
                {
                    if (x + 1 < width && !mazeData[x, z].passageEast && !mazeData[x + 1, z].passageWest)
                        walls.Add(new System.Tuple<Vector2Int, Vector2Int>(new Vector2Int(x, z), new Vector2Int(x + 1, z)));
                    if (z + 1 < depth && !mazeData[x, z].passageNorth && !mazeData[x, z + 1].passageSouth)
                        walls.Add(new System.Tuple<Vector2Int, Vector2Int>(new Vector2Int(x, z), new Vector2Int(x, z + 1)));
                }
            for (int i = 0; i < walls.Count; i++) { int r = i + rand.Next(walls.Count - i); var tmp = walls[r]; walls[r] = walls[i]; walls[i] = tmp; }
            int toOpen = Mathf.Min(extraPassages, walls.Count);
            for (int i = 0; i < toOpen; i++)
            {
                var w = walls[i]; var a = w.Item1; var b = w.Item2;
                if (b.x == a.x + 1) { mazeData[a.x, a.y].passageEast = true; mazeData[b.x, b.y].passageWest = true; }
                else if (b.x == a.x - 1) { mazeData[a.x, a.y].passageWest = true; mazeData[b.x, b.y].passageEast = true; }
                else if (b.y == a.y + 1) { mazeData[a.x, a.y].passageNorth = true; mazeData[b.x, b.y].passageSouth = true; }
                else if (b.y == a.y - 1) { mazeData[a.x, a.y].passageSouth = true; mazeData[b.x, b.y].passageNorth = true; }
            }
        }
    }

    List<Vector2Int> FindShortestPath()
    {
        if (mazeData == null) return null;
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Vector2Int start = new Vector2Int(startX, startZ), goal = new Vector2Int(endX, endZ);
        q.Enqueue(start); visited.Add(start); cameFrom[start] = start;
        Vector2Int[] dirs = { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(1, 0) };
        while (q.Count > 0)
        {
            Vector2Int cur = q.Dequeue();
            if (cur == goal)
            {
                List<Vector2Int> path = new List<Vector2Int>();
                Vector2Int step = goal;
                while (step != start) { path.Add(step); step = cameFrom[step]; }
                path.Add(start); path.Reverse(); return path;
            }
            CellData cell = mazeData[cur.x, cur.y];
            if (cell.passageNorth) { var n = new Vector2Int(cur.x, cur.y + 1); if (!visited.Contains(n)) { visited.Add(n); cameFrom[n] = cur; q.Enqueue(n); } }
            if (cell.passageSouth) { var n = new Vector2Int(cur.x, cur.y - 1); if (!visited.Contains(n)) { visited.Add(n); cameFrom[n] = cur; q.Enqueue(n); } }
            if (cell.passageWest) { var n = new Vector2Int(cur.x - 1, cur.y); if (!visited.Contains(n)) { visited.Add(n); cameFrom[n] = cur; q.Enqueue(n); } }
            if (cell.passageEast) { var n = new Vector2Int(cur.x + 1, cur.y); if (!visited.Contains(n)) { visited.Add(n); cameFrom[n] = cur; q.Enqueue(n); } }
        }
        return null;
    }

    List<List<Vector2Int>> FindAllDeadEndPaths(List<Vector2Int> mainPath)
    {
        if (mainPath == null || mainPath.Count == 0) return new List<List<Vector2Int>>();
        HashSet<Vector2Int> mainSet = new HashSet<Vector2Int>(mainPath);
        Dictionary<Vector2Int, List<Vector2Int>> graph = new Dictionary<Vector2Int, List<Vector2Int>>();
        for (int x = 0; x < width; x++) for (int z = 0; z < depth; z++)
            {
                Vector2Int cell = new Vector2Int(x, z);
                List<Vector2Int> neigh = new List<Vector2Int>();
                CellData d = mazeData[x, z];
                if (d.passageNorth && z + 1 < depth) neigh.Add(new Vector2Int(x, z + 1));
                if (d.passageSouth && z - 1 >= 0) neigh.Add(new Vector2Int(x, z - 1));
                if (d.passageWest && x - 1 >= 0) neigh.Add(new Vector2Int(x - 1, z));
                if (d.passageEast && x + 1 < width) neigh.Add(new Vector2Int(x + 1, z));
                graph[cell] = neigh;
            }
        List<Vector2Int> deadEnds = new List<Vector2Int>();
        foreach (var kv in graph) if (kv.Value.Count == 1 && !mainSet.Contains(kv.Key)) deadEnds.Add(kv.Key);
        if (deadEnds.Count == 0) return new List<List<Vector2Int>>();
        List<List<Vector2Int>> allBranches = new List<List<Vector2Int>>();
        foreach (var deadEnd in deadEnds)
        {
            Queue<Vector2Int> q = new Queue<Vector2Int>();
            Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            q.Enqueue(deadEnd); visited.Add(deadEnd);
            Vector2Int foundMain = Vector2Int.zero; bool found = false; int safety = 0;
            while (q.Count > 0 && safety++ < 10000)
            {
                Vector2Int cur = q.Dequeue();
                if (mainSet.Contains(cur)) { foundMain = cur; found = true; break; }
                foreach (var n in graph[cur]) if (!visited.Contains(n)) { visited.Add(n); cameFrom[n] = cur; q.Enqueue(n); }
            }
            if (found)
            {
                List<Vector2Int> branch = new List<Vector2Int>();
                Vector2Int step = foundMain; int pathSafety = 0;
                while (step != deadEnd && pathSafety++ < 1000) { branch.Add(step); step = cameFrom[step]; }
                if (step == deadEnd)
                {
                    branch.Reverse(); if (branch.Count > 0) branch.RemoveAt(branch.Count - 1);
                    branch.Insert(0, deadEnd); allBranches.Add(branch);
                }
                else Debug.LogError($"Path reconstruction failed for deadEnd {deadEnd}");
            }
        }
        return allBranches;
    }

    void DrawDebugPath()
    {
        if (columns == null) return;
        if (pathLine != null) DestroyImmediate(pathLine.gameObject);
        foreach (var l in wrongPathLines) if (l != null) DestroyImmediate(l.gameObject);
        wrongPathLines.Clear();

        List<Vector2Int> mainPath = FindShortestPath();
        if (mainPath != null && mainPath.Count > 0)
        {
            GameObject mainObj = new GameObject("MainPath");
            mainObj.transform.SetParent(transform, false);
            pathLine = mainObj.AddComponent<LineRenderer>();
            pathLine.startWidth = pathLine.endWidth = 0.5f;
            pathLine.material = new Material(Shader.Find("Sprites/Default"));
            pathLine.startColor = pathLine.endColor = pathColor;
            pathLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pathLine.receiveShadows = false;
            pathLine.positionCount = mainPath.Count;
            for (int i = 0; i < mainPath.Count; i++)
            {
                Vector3 pos = columns[mainPath[i].x, mainPath[i].y].position;
                pos.y += 1f;
                pathLine.SetPosition(i, pos);
            }
            // Скрываем по умолчанию, если секретный режим не активен
            pathLine.gameObject.SetActive(secretActive);
        }

        if (showWrongPaths && mainPath != null)
        {
            var wrongPaths = FindAllDeadEndPaths(mainPath);
            int idx = 0;
            foreach (var p in wrongPaths)
            {
                if (p.Count == 0) continue;
                GameObject wrongObj = new GameObject($"WrongPath_{idx++}");
                wrongObj.transform.SetParent(transform, false);
                LineRenderer wr = wrongObj.AddComponent<LineRenderer>();
                wr.startWidth = wr.endWidth = 0.5f;
                wr.material = new Material(Shader.Find("Sprites/Default"));
                wr.startColor = wr.endColor = wrongPathColor;
                wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                wr.receiveShadows = false;
                wr.positionCount = p.Count;
                for (int i = 0; i < p.Count; i++)
                {
                    Vector3 pos = columns[p[i].x, p[i].y].position;
                    pos.y += 1.1f;
                    wr.SetPosition(i, pos);
                }
                wr.gameObject.SetActive(secretActive);
                wrongPathLines.Add(wr);
            }
        }
    }

    public Vector3 GetStartWorldPosition() => columns[startX, startZ].position;
    public Vector3 GetEndWorldPosition() => columns[endX, endZ].position;

    void SetColumnHeight(int x, int z, float targetHeight, bool instant = false)
    {
        if (instant)
        {
            Vector3 s = columns[x, z].localScale; s.y = targetHeight; columns[x, z].localScale = s;
            if (detectors[x, z] != null) detectors[x, z].gameObject.SetActive(targetHeight == loweredHeight);
            isRaised[x, z] = (targetHeight != loweredHeight);
        }
        else
        {
            if (detectors[x, z] != null) detectors[x, z].gameObject.SetActive(targetHeight == loweredHeight);
            StartCoroutine(AnimateColumn(x, z, targetHeight));
        }
    }

    IEnumerator AnimateColumn(int x, int z, float targetHeight)
    {
        Transform col = columns[x, z];
        float start = col.localScale.y, t = 0f;
        while (t < animationDuration)
        {
            float ny = Mathf.Lerp(start, targetHeight, t / animationDuration);
            Vector3 s = col.localScale; s.y = ny; col.localScale = s;
            t += Time.deltaTime; yield return null;
        }
        Vector3 fs = col.localScale; fs.y = targetHeight; col.localScale = fs;
        isRaised[x, z] = (targetHeight != loweredHeight);
    }

    public void PlayerSteppedOnTile(int x, int z)
    {
        if (x == previousX && z == previousZ) return;
        foreach (var c in raisedNeighbors) SetColumnHeight(c.x, c.y, loweredHeight);
        raisedNeighbors.Clear();
        CellData cell = mazeData[x, z];
        if (!cell.passageNorth) { int nx = x, nz = z + 1; if (nx >= 0 && nx < width && nz >= 0 && nz < depth && !isRaised[nx, nz]) { SetColumnHeight(nx, nz, raisedHeight); raisedNeighbors.Add(new Vector2Int(nx, nz)); } }
        if (!cell.passageSouth) { int nx = x, nz = z - 1; if (nx >= 0 && nx < width && nz >= 0 && nz < depth && !isRaised[nx, nz]) { SetColumnHeight(nx, nz, raisedHeight); raisedNeighbors.Add(new Vector2Int(nx, nz)); } }
        if (!cell.passageWest) { int nx = x - 1, nz = z; if (nx >= 0 && nx < width && nz >= 0 && nz < depth && !isRaised[nx, nz]) { SetColumnHeight(nx, nz, raisedHeight); raisedNeighbors.Add(new Vector2Int(nx, nz)); } }
        if (!cell.passageEast) { int nx = x + 1, nz = z; if (nx >= 0 && nx < width && nz >= 0 && nz < depth && !isRaised[nx, nz]) { SetColumnHeight(nx, nz, raisedHeight); raisedNeighbors.Add(new Vector2Int(nx, nz)); } }
        previousX = x; previousZ = z;
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(GridManager))]
    public class GridManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            GridManager gm = (GridManager)target;
            if (GUILayout.Button("Refresh Debug Path")) { if (gm.pathLine) DestroyImmediate(gm.pathLine.gameObject); foreach (var l in gm.wrongPathLines) if (l) DestroyImmediate(l.gameObject); gm.wrongPathLines.Clear(); gm.DrawDebugPath(); }
            if (GUILayout.Button("Spawn Maze")) gm.SpawnMaze();
            if (GUILayout.Button("Clear Maze")) gm.ClearMaze();
        }
    }
#endif
}
using UnityEngine;

public class GridGenerator : MonoBehaviour
{
    [SerializeField] private GameObject gridPrefab;

    [Header("Grid Settings")]
    [SerializeField] private int columns = 100;
    [SerializeField] private int rows = 100;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private bool generateOnStart = true;

    private Transform gridParent;

    void Start()
    {
        if (generateOnStart)
        {
            GenerateGrid();
        }
    }

    public void GenerateGrid()
    {
        if (gridPrefab == null)
        {
            Debug.LogError("GridGenerator: gridPrefab is not assigned.", this);
            return;
        }

        ClearGrid();

        gridParent = new GameObject("Grid").transform;
        gridParent.SetParent(transform, false);

        Vector3 origin = transform.position;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                Vector3 position = origin + new Vector3(col * cellSize, row * cellSize, 0f);
                GameObject cell = Instantiate(gridPrefab, position, Quaternion.identity, gridParent);
                cell.name = $"Cell_{col}_{row}";
            }
        }
    }

    public void ClearGrid()
    {
        if (gridParent != null)
        {
            if (Application.isPlaying)
            {
                Destroy(gridParent.gameObject);
            }
            else
            {
                DestroyImmediate(gridParent.gameObject);
            }
        }
    }
}

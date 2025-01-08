using System.Collections;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MainController : MonoBehaviour
{
    [SerializeField] private MapSO mapData;
    [SerializeField] private Map map;
    [SerializeField] private RectView rectView;
    [SerializeField] private TMP_Dropdown modelDropDown;
    [SerializeField] private Button modelCreateButton;

    [SerializeField] private State state;
    [SerializeField] private Vector2 worldPosition;
    [SerializeField] private Vector2Int tilePosition;

    private int currentModelSelectedId;

    private Ray ray;
    private Plane mapPlane;
    private Vector2 INVALID_FLOAT2;
    private Vector2Int INVALID_INT2;
    private Vector2Int startDragTilePosition;
    private RectInt rect;
    private bool isDragging;
    private bool isModelEditValid;

    private EntityManager entityManager;
    //private EntityArchetype meshChunkArchetype;
    private Entity controllerEntity;

    private void Awake ()
    {
        state = State.None;
        currentModelSelectedId = -1;
        rectView.Show(false);

        mapPlane = new Plane(Vector3.up, Vector3.zero);
        worldPosition = Vector2.zero;
        tilePosition = Vector2Int.zero;
        startDragTilePosition = Vector2Int.zero;
        rect = new RectInt(Vector2Int.zero, Vector2Int.zero);
        isDragging = false;
        isModelEditValid = false;

        INVALID_FLOAT2 = new Vector2(-1f, -1f);
        INVALID_INT2 = new Vector2Int(-1, -1);

        modelCreateButton.onClick.AddListener(() => OnModelCreateButtonClicked());
        modelDropDown.onValueChanged.AddListener((int v) => OnModelSelectionChanged(v));
    }

    private void Start ()
    {
        map.Initialize(mapData);

        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        //meshChunkArchetype = entityManager.CreateArchetype(typeof(MeshChunkData));

        CreateControllerEntity();
        UI_LoadModels();
    }

    private void OnDestroy ()
    {
        modelCreateButton.onClick.RemoveAllListeners();
        modelDropDown.onValueChanged.RemoveAllListeners();
    }

    private void Update ()
    {
        GetWorldPositions();
        CheckInputs();
    }

    private void CreateControllerEntity ()
    {
        controllerEntity = entityManager.CreateEntity(typeof(ControllerComponent), typeof(MapComponent));
        entityManager.SetComponentData(controllerEntity, new ControllerComponent
        {
            State = ControllerState.None,
            Rect = int4.zero,
            ModelSelectedId = 0,
            FloorSelectedTextureId = 0
        });
        entityManager.SetComponentData(controllerEntity, new MapComponent
        {
            TileDimension = mapData.MapDimension,
            TileWidth = mapData.TileWidth,
            ChunkDimension = mapData.ChunkDimension,
            ChunkWidth = mapData.ChunkWidth
        });
    }

    private void GetWorldPositions ()
    {
        ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        var intersectPoint = math.up();

        if (mapPlane.Raycast(ray, out float distance))
        {
            intersectPoint = ray.GetPoint(distance);
        }

        var lastTilePosition = tilePosition;

        worldPosition = new Vector2(intersectPoint.x, intersectPoint.z);

        if (intersectPoint.y < 1f && IsWorldPositionValid(worldPosition))
        {
            tilePosition = new Vector2Int((int)(worldPosition.x / mapData.TileWidth), (int)(worldPosition.y / mapData.TileWidth));

            if (lastTilePosition.Equals(tilePosition))
            {
                OnTilePositionChanged(true);
            }
        }
        else
        {
            worldPosition = INVALID_FLOAT2;
            tilePosition = INVALID_INT2;

            if (lastTilePosition.Equals(tilePosition))
            {
                OnTilePositionChanged(false);
            }
        }
    }

    private bool IsWorldPositionValid (Vector2 worldPosition)
    {
        return worldPosition.x >= 0f && worldPosition.x < mapData.MapUnitDimension.x &&
            worldPosition.y >= 0f && worldPosition.y < mapData.MapUnitDimension.y;
    }

    private void CheckInputs ()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelCreation();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                if (state == State.CreatingModel)
                {
                    isDragging = true;
                    startDragTilePosition = tilePosition;
                }
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;

            if (!EventSystem.current.IsPointerOverGameObject())
            {
                if (state == State.CreatingModel)
                {
                    entityManager.SetComponentData(controllerEntity, new ControllerComponent
                    {
                        State = ControllerState.CreateModel,
                        Rect = new int4(rect.position.x, rect.position.y, rect.size.x, rect.size.y),
                        ModelSelectedId = currentModelSelectedId,
                        FloorSelectedTextureId = 0
                    });
                }
            }
        }
    }

    private void OnTilePositionChanged (bool tilePositionValid)
    {
        if (state == State.CreatingModel)
        {
            if (isDragging)
            {
                GetRectFromTwoPoints(startDragTilePosition, tilePosition, out var rectPosition, out var rectSize);
                rect = new RectInt(rectPosition, rectSize);
                rectView.SetRect(rect, mapData.TileWidth);
                isModelEditValid = tilePositionValid && IsWorldPositionValid(startDragTilePosition);
                rectView.Show(isModelEditValid);
            }
            else
            {
                isModelEditValid = tilePositionValid;
                rect = new RectInt(tilePosition, Vector2Int.one);
                rectView.SetRect(rect, mapData.TileWidth);
                rectView.Show(isModelEditValid);
            }
        }
    }

    public void GetRectFromTwoPoints (Vector2Int p1, Vector2Int p2, out Vector2Int init, out Vector2Int size)
    {
        if (p1.x <= p2.x && p1.y <= p2.y)
        {
            init = p1;
            size = p2 - p1;
        }
        else if (p1.x <= p2.x && p1.y >= p2.y)
        {
            init = new Vector2Int(p1.x, p2.y);
            size = new Vector2Int(p2.x - p1.x, p1.y - p2.y);
        }
        else if (p1.x >= p2.x && p1.y <= p2.y)
        {
            init = new Vector2Int(p2.x, p1.y);
            size = new Vector2Int(p1.x - p2.x, p2.y - p1.y);
        }
        else
        {
            init = p2;
            size = p1 - p2;
        }

        size += new Vector2Int(1, 1);
    }

    private void OnModelCreateButtonClicked ()
    {
        CancelCreation();

        state = State.CreatingModel;
        rectView.Show(true);
    }

    private void OnModelSelectionChanged (int modelSelected)
    {
        CancelCreation();
        currentModelSelectedId = modelSelected;
    }

    private void CancelCreation ()
    {
        state = State.None;
        isModelEditValid = false;
        rectView.Show(false);
    }

    private void UI_LoadModels ()
    {
        var modelDataEntityQuery = entityManager.CreateEntityQuery(typeof(ModelDataEntityBuffer));

        var modelDataEntityBuffer = modelDataEntityQuery.GetSingletonBuffer<ModelDataEntityBuffer>();

        var dropDownOptions = new System.Collections.Generic.List<string>();

        for (int i = 0; i < modelDataEntityBuffer.Length; i++)
        {
            var modelEntity = modelDataEntityBuffer[i].Value;

            var modelBlobDataComponent = entityManager.GetComponentData<MeshDataComponent>(modelEntity);
            var modelName = modelBlobDataComponent.meshDataBlob.Value.meshName.BlobCharToString();

            dropDownOptions.Add(modelName);
        }

        modelDropDown.AddOptions(dropDownOptions);
        
        if (dropDownOptions.Count > 0 )
        {
            currentModelSelectedId = 0;
        }

        modelDataEntityQuery.Dispose();
    }
}

public enum State
{
    None, CreatingModel
}
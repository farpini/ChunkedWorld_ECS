using System.Collections;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
using TMPro;
using UnityEngine.UI;

public class MainController : MonoBehaviour
{
    [SerializeField] private MapSO mapData;
    [SerializeField] private Map map;
    [SerializeField] private RectView rectView;
    [SerializeField] private TMP_Dropdown modelDropDown;
    [SerializeField] private Button modelCreateButton;
    [SerializeField] private Button modelRemoveButton;
    [SerializeField] private Button generateTerrainButton;
    [SerializeField] private Button lowerTerrainButton;
    [SerializeField] private float mapMagnification;
    [SerializeField] private float2 mapOffset;

    [SerializeField] private ControllerState state;

    private int modelCount;
    private int currentModelSelectedId;

    private EntityManager entityManager;
    //private EntityArchetype meshChunkArchetype;
    private Entity controllerEntity;

    private void Awake ()
    {
        state = ControllerState.None;
        modelCount = 0;
        currentModelSelectedId = -1;
        rectView.Show(false);

        modelCreateButton.onClick.AddListener(() => OnModelCreateButtonClicked());
        modelRemoveButton.onClick.AddListener(() => OnModelRemoveButtonClicked());
        generateTerrainButton.onClick.AddListener(() => OnGenerateTerrainButtonClicked());
        lowerTerrainButton.onClick.AddListener(() => OnTerrainLowerButtonClicked());
        modelDropDown.onValueChanged.AddListener((int v) => OnModelSelectionChanged(v));
    }

    private void Start ()
    {
        map.Initialize(mapData);

        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        //meshChunkArchetype = entityManager.CreateArchetype(typeof(MeshChunkData));

        var modelDataEntityQuery = entityManager.CreateEntityQuery(typeof(ModelDataEntityBuffer));
        var modelDataEntityBuffer = modelDataEntityQuery.GetSingletonBuffer<ModelDataEntityBuffer>();
        modelCount = modelDataEntityBuffer.Length;

        CreateControllerEntity();
        UI_LoadModels(modelDataEntityBuffer);

        modelDataEntityQuery.Dispose();
    }

    private void OnDestroy ()
    {
        modelCreateButton.onClick.RemoveAllListeners();
        modelRemoveButton.onClick.RemoveAllListeners();
        generateTerrainButton.onClick.RemoveAllListeners();
        lowerTerrainButton.onClick.RemoveAllListeners();
        modelDropDown.onValueChanged.RemoveAllListeners();
    }

    private void Update ()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelCreation();
        }
    }

    private void CreateControllerEntity ()
    {
        controllerEntity = entityManager.CreateEntity(
            typeof(ControllerComponent), 
            typeof(MapComponent), 
            typeof(MapTileComponent), 
            typeof(RectChunkEntityBuffer),
            typeof(RectGameObject));

        entityManager.SetComponentData(controllerEntity, new ControllerComponent
        {
            State = ControllerState.None,
            OnRectSelecting = false,
            Rect = int4.zero,
            ModelCount = modelCount,
            ModelSelectedId = 0,
            FloorSelectedTextureId = 0,
            StartTile = int2.zero,
            MapMagnification = mapMagnification,
            MapOffset = mapOffset
        });

        entityManager.SetComponentData(controllerEntity, new MapComponent
        {
            TileDimension = mapData.MapDimension,
            TileWidth = mapData.TileWidth,
            ChunkDimension = mapData.ChunkDimension,
            ChunkWidth = mapData.ChunkWidth,
            MaxHeight = mapData.MaxHeight
        });

        entityManager.SetComponentData(controllerEntity, new MapTileComponent
        {
            TileData = new NativeArray<TileData>(mapData.MapDimension.x * mapData.MapDimension.y, Allocator.Persistent),
            TileHeightMap = new NativeArray<int>((mapData.MapDimension.x + 1) * (mapData.MapDimension.y + 1), Allocator.Persistent)
        });

        entityManager.SetComponentData(controllerEntity, new RectGameObject
        {
            RectView = rectView
        });

        entityManager.SetName(controllerEntity, "ControllerEntity");
    }

    private void OnModelCreateButtonClicked ()
    {
        CancelCreation();

        state = ControllerState.CreateModel;

        entityManager.SetComponentData(controllerEntity, new ControllerComponent
        {
            State = state,
            OnRectSelecting = true,
            Rect = int4.zero,
            ModelCount = modelCount,
            ModelSelectedId = currentModelSelectedId,
            FloorSelectedTextureId = 0,
            StartTile = int2.zero,
            MapMagnification = mapMagnification,
            MapOffset = mapOffset
        });

        rectView.Show(true);
    }

    private void OnModelRemoveButtonClicked ()
    {
        CancelCreation();

        state = ControllerState.RemoveModel;

        entityManager.SetComponentData(controllerEntity, new ControllerComponent
        {
            State = state,
            OnRectSelecting = true,
            Rect = int4.zero,
            ModelCount = modelCount,
            ModelSelectedId = currentModelSelectedId,
            FloorSelectedTextureId = 0,
            StartTile = int2.zero,
            MapMagnification = mapMagnification,
            MapOffset = mapOffset
        });

        rectView.Show(true);
    }

    private void OnTerrainLowerButtonClicked ()
    {
        CancelCreation();

        state = ControllerState.LowerTerrain;

        entityManager.SetComponentData(controllerEntity, new ControllerComponent
        {
            State = state,
            OnRectSelecting = true,
            Rect = int4.zero,
            ModelCount = modelCount,
            ModelSelectedId = currentModelSelectedId,
            FloorSelectedTextureId = 0,
            StartTile = int2.zero,
            MapMagnification = mapMagnification,
            MapOffset = mapOffset
        });

        rectView.Show(true);
    }

    private void OnGenerateTerrainButtonClicked ()
    {
        CancelCreation();

        state = ControllerState.GenerateTerrain;

        entityManager.SetComponentData(controllerEntity, new ControllerComponent
        {
            State = state,
            OnRectSelecting = false,
            Rect = int4.zero,
            ModelCount = modelCount,
            ModelSelectedId = currentModelSelectedId,
            FloorSelectedTextureId = 0,
            StartTile = int2.zero,
            MapMagnification = mapMagnification,
            MapOffset = mapOffset
        });
    }

    private void OnModelSelectionChanged (int modelSelected)
    {
        CancelCreation();
        currentModelSelectedId = modelSelected;
    }

    private void CancelCreation ()
    {
        state = ControllerState.None;

        entityManager.SetComponentData(controllerEntity, new ControllerComponent
        {
            State = state,
            OnRectSelecting = false,
            Rect = int4.zero,
            ModelCount = modelCount,
            ModelSelectedId = currentModelSelectedId,
            FloorSelectedTextureId = 0,
            StartTile = int2.zero
        });

        rectView.Show(false);
    }

    private void UI_LoadModels (DynamicBuffer<ModelDataEntityBuffer> modelDataEntityBuffer)
    {
        var dropDownOptions = new System.Collections.Generic.List<string>();

        for (int i = 0; i < modelDataEntityBuffer.Length; i++)
        {
            var modelEntity = modelDataEntityBuffer[i].Value;

            var modelBlobInfoComponent = entityManager.GetSharedComponentManaged<MeshBlobInfoComponent>(modelEntity);
            var modelName = modelBlobInfoComponent.meshInfoBlob.Value.meshName.BlobCharToString();

            dropDownOptions.Add(modelName);
        }

        modelDropDown.AddOptions(dropDownOptions);
        
        if (dropDownOptions.Count > 0 )
        {
            currentModelSelectedId = 0;
        }
    }
}
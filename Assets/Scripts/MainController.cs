using System.Collections;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
using TMPro;
using UnityEngine.UI;

public class MainController : MonoBehaviour
{
    [SerializeField] private Map map;
    [SerializeField] private RectView rectView;
    [SerializeField] private TerrainGeneratorPanel terrainGeneratorPanel;
    [SerializeField] private Button terrainGeneratorPanelButton;

    [SerializeField] private ControllerState state;

    private MapComponent2 mapComponent;

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

        //modelCreateButton.onClick.AddListener(() => OnModelCreateButtonClicked());
        //modelRemoveButton.onClick.AddListener(() => OnModelRemoveButtonClicked());
        //generateTerrainButton.onClick.AddListener(() => OnGenerateTerrainButtonClicked());
        //modelDropDown.onValueChanged.AddListener((int v) => OnModelSelectionChanged(v));
        terrainGeneratorPanelButton.onClick.AddListener(() => OnTerrainGeneratorPanelButtonClicked());

        terrainGeneratorPanel.OnTerrainGeneratorButtonClicked += OnTerrainGeneratorButtonClicked;
    }

    private void Start ()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        //meshChunkArchetype = entityManager.CreateArchetype(typeof(MeshChunkData));

        var modelDataEntityQuery = entityManager.CreateEntityQuery(typeof(ModelDataEntityBuffer));
        var modelDataEntityBuffer = modelDataEntityQuery.GetSingletonBuffer<ModelDataEntityBuffer>();
        modelCount = modelDataEntityBuffer.Length;

        CreateControllerEntity();
        UI_LoadModels(modelDataEntityBuffer);

        map.gameObject.SetActive(true);

        modelDataEntityQuery.Dispose();
    }

    private void OnDestroy ()
    {
        //modelCreateButton.onClick.RemoveAllListeners();
        //modelRemoveButton.onClick.RemoveAllListeners();
        //generateTerrainButton.onClick.RemoveAllListeners();
        //modelDropDown.onValueChanged.RemoveAllListeners();
        terrainGeneratorPanelButton.onClick.RemoveAllListeners();

        terrainGeneratorPanel.OnTerrainGeneratorButtonClicked -= OnTerrainGeneratorButtonClicked;
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
            typeof(MapComponent2),
            typeof(MapTileComponent), 
            typeof(RectChunkEntityBuffer),
            typeof(RefGameObject));

        entityManager.SetComponentData(controllerEntity, new ControllerComponent
        {
            State = ControllerState.None,
            OnRectSelecting = false,
            Rect = int4.zero,
            ModelCount = modelCount,
            ModelSelectedId = 0,
            FloorSelectedTextureId = 0,
            StartTile = int2.zero
        });

        mapComponent = new MapComponent2
        {
            TileDimension = new int2(32, 32),
            TileWidth = 4,
            ChunkWidth = 32,
            MaxHeight = 4,
            MaxDepth = 1,
            Roughness = 0.25f
        };

        entityManager.SetComponentData(controllerEntity, mapComponent);

        map.Initialize(mapComponent);

        entityManager.SetComponentData(controllerEntity, new MapTileComponent
        {
            TileData = new NativeArray<TileData>(mapComponent.TileDimension.x * mapComponent.TileDimension.y, Allocator.Persistent),
            TileHeightMap = new NativeArray<int>((mapComponent.TileDimension.x + 1) * (mapComponent.TileDimension.y + 1), Allocator.Persistent)
        });

        entityManager.SetComponentData(controllerEntity, new RefGameObject
        {
            Map = map,
            RectView = rectView
        });

        entityManager.SetName(controllerEntity, "ControllerEntity");
    }

    private void OnTerrainGeneratorPanelButtonClicked ()
    {
        var isPanelActive = terrainGeneratorPanel.gameObject.activeSelf;
        terrainGeneratorPanel.gameObject.SetActive(!isPanelActive);
        if (!isPanelActive)
        {
            Debug.Log("UPDATE");
            terrainGeneratorPanel.UpdatePanel(mapComponent);
        }
    }

    private void OnTerrainGeneratorButtonClicked (int mapSize, int maxHeight, float roughness, int maxDepth)
    {
        Debug.LogWarning("OnTerrainGeneratorButtonClicked");




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
            StartTile = int2.zero
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
            StartTile = int2.zero
        });

        rectView.Show(true);
    }

    private void OnGenerateTerrainButtonClicked ()
    {
        CancelCreation();

        /*
        state = ControllerState.GenerateTerrain;

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

        var mapComponent = entityManager.GetComponentData<MapComponent>(controllerEntity);
        mapComponent.Roughness = mapRoughness;
        mapComponent.Smoothness = mapSmoothness;
        mapComponent.MaxHeight = mapHeight;
        mapComponent.MaxDepth = mapDepth;
        entityManager.SetComponentData<MapComponent>(controllerEntity, mapComponent);
        */
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

        //modelDropDown.AddOptions(dropDownOptions);
        
        if (dropDownOptions.Count > 0 )
        {
            currentModelSelectedId = 0;
        }
    }
}
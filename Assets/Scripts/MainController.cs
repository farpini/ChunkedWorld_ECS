using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.UI;

public class MainController : MonoBehaviour
{
    [SerializeField] private Map map;
    [SerializeField] private RectView rectView;
    [SerializeField] private TerrainGeneratorPanel terrainGeneratorPanel;
    [SerializeField] private ChunkedModelPanel chunkedModelPanel;
    [SerializeField] private Button terrainGeneratorPanelButton;
    [SerializeField] private Button chunkedModelPanelButton;

    [SerializeField] private ControllerState state;

    private MapComponent mapComponent;

    private int modelCount;

    private EntityManager entityManager;
    //private EntityArchetype meshChunkArchetype;
    private Entity controllerEntity;

    private void Awake ()
    {
        state = ControllerState.None;
        modelCount = 0;
        rectView.Show(false);

        terrainGeneratorPanelButton.onClick.AddListener(() => OnTerrainGeneratorPanelButtonClicked());
        chunkedModelPanelButton.onClick.AddListener(() => OnChunkedModelPanelButtonClicked());

        terrainGeneratorPanel.OnAnyUIEvent += CancelAction;
        terrainGeneratorPanel.OnTerrainGeneratorButtonClicked += OnTerrainGeneratorButtonClicked;

        chunkedModelPanel.OnAnyUIEvent += CancelAction;
        chunkedModelPanel.OnSelectPlacementClicked += OnChunkedModelSelectPlacementButtonClicked;
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
        chunkedModelPanelButton.onClick.RemoveAllListeners();

        terrainGeneratorPanel.OnAnyUIEvent -= CancelAction;
        terrainGeneratorPanel.OnTerrainGeneratorButtonClicked -= OnTerrainGeneratorButtonClicked;

        chunkedModelPanel.OnAnyUIEvent -= CancelAction;
        chunkedModelPanel.OnSelectPlacementClicked -= OnChunkedModelSelectPlacementButtonClicked;
    }

    private void Update ()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelAction();
        }
    }

    private void CreateControllerEntity ()
    {
        controllerEntity = entityManager.CreateEntity(
            typeof(ControllerComponent),
            typeof(MapComponent),
            typeof(MapTileComponent), 
            typeof(RectChunkEntityBuffer),
            typeof(RefGameObject));

        entityManager.SetComponentData(controllerEntity, new ControllerComponent
        {
            State = ControllerState.GenerateTerrain,
            OnRectSelecting = false,
            Rect = int4.zero,
            ModelCount = modelCount,
            ModelSelectedId = 0,
            FloorSelectedTextureId = 0,
            StartTile = int2.zero
        });

        mapComponent = new MapComponent
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

        entityManager.SetComponentData(controllerEntity, new RefGameObject
        {
            Map = map,
            RectView = rectView
        });

        entityManager.SetName(controllerEntity, "ControllerEntity");
    }

    private void OnTerrainGeneratorPanelButtonClicked ()
    {
        CancelAction();
        var isPanelActive = terrainGeneratorPanel.gameObject.activeSelf;
        terrainGeneratorPanel.gameObject.SetActive(!isPanelActive);
        if (!isPanelActive)
        {
            terrainGeneratorPanel.UpdatePanel(mapComponent);
        }
        chunkedModelPanel.gameObject.SetActive(false);
    }

    private void OnChunkedModelPanelButtonClicked ()
    {
        CancelAction();
        var isPanelActive = chunkedModelPanel.gameObject.activeSelf;
        chunkedModelPanel.gameObject.SetActive(!isPanelActive);
        if (!isPanelActive)
        {
            chunkedModelPanel.UpdatePanel();
        }
        terrainGeneratorPanel.gameObject.SetActive(false);
    }

    private void OnTerrainGeneratorButtonClicked (int mapSize, int maxHeight, float roughness, int maxDepth)
    {
        CancelAction();

        state = ControllerState.GenerateTerrain;

        entityManager.SetComponentData(controllerEntity, new MapComponent
        {
            TileDimension = new int2(mapSize, mapSize),
            TileWidth = 4,
            ChunkWidth = 32,
            MaxHeight = maxHeight,
            MaxDepth = maxDepth,
            Roughness = roughness
        });

        var controllerData = entityManager.GetComponentData<ControllerComponent>(controllerEntity);
        controllerData.State = state;
        entityManager.SetComponentData(controllerEntity, controllerData);
    }

    private void OnChunkedModelSelectPlacementButtonClicked (int modelSelectedId)
    {
        CancelAction();

        state = ControllerState.ChunkedModelSelectPlacement;

        var controllerData = entityManager.GetComponentData<ControllerComponent>(controllerEntity);
        controllerData.State = state;
        controllerData.ModelSelectedId = modelSelectedId;
        controllerData.OnRectSelecting = true;
        entityManager.SetComponentData(controllerEntity, controllerData);

        rectView.Show(true);
    }


    /*
    private void OnModelCreateButtonClicked ()
    {
        CancelAction();

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
        CancelAction();

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
    */

    private void OnGenerateTerrainButtonClicked ()
    {
        CancelAction();

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

        var currentMapComponent = entityManager.GetComponentData<MapComponent>(controllerEntity);
        currentMapComponent.Roughness = mapRoughness;
        currentMapComponent.Smoothness = mapSmoothness;
        currentMapComponent.MaxHeight = mapHeight;
        currentMapComponent.MaxDepth = mapDepth;
        entityManager.SetComponentData<MapComponent>(controllerEntity, currentMapComponent);
        */
    }

    private void CancelAction ()
    {
        state = ControllerState.None;

        entityManager.SetComponentData(controllerEntity, new ControllerComponent
        {
            State = state,
            OnRectSelecting = false,
            Rect = int4.zero,
            ModelCount = modelCount,
            ModelSelectedId = -1,
            FloorSelectedTextureId = 0,
            StartTile = int2.zero
        });

        rectView.Show(false);
    }

    private void UI_LoadModels (DynamicBuffer<ModelDataEntityBuffer> modelDataEntityBuffer)
    {
        for (int i = 0; i < modelDataEntityBuffer.Length; i++)
        {
            var modelEntity = modelDataEntityBuffer[i].Value;

            var modelBlobInfoComponent = entityManager.GetSharedComponentManaged<MeshBlobInfoComponent>(modelEntity);

            chunkedModelPanel.LoadModel(ref modelBlobInfoComponent.meshInfoBlob.Value.meshName, ref modelBlobInfoComponent.meshInfoBlob.Value.meshIcon);
        }

        chunkedModelPanel.UpdatePanel();
    }
}
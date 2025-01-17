using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TerrainGeneratorPanel : MonoBehaviour
{
    [SerializeField] private Button generateButton;
    [SerializeField] private TMP_Dropdown mapSizeDropdown;
    [SerializeField] private Slider landHeight;
    [SerializeField] private Slider landRoughness;
    [SerializeField] private Slider waterDepth;
    [SerializeField] private MapSettings[] mapSettingOptions;

    private int currentMapSize;
    private int currentMapHeight;
    private float currentRoughness;
    private int currentWaterDepth;

    public Action OnAnyUIEvent;
    public Action<int, int, float, int> OnTerrainGeneratorButtonClicked;

    private void Awake ()
    {
        if (mapSettingOptions != null)
        {
            var optionsStr = new List<string>();
            for (int i = 0; i < mapSettingOptions.Length; i++)
            {
                optionsStr.Add(mapSettingOptions[i].size.ToString() + "x" + mapSettingOptions[i].size.ToString());
            }
            mapSizeDropdown.AddOptions(optionsStr);
        }

        OnMapSizeChanged(0);

        mapSizeDropdown.onValueChanged.AddListener((int v) => OnMapSizeChanged(v));
        generateButton.onClick.AddListener(() => OnMapGeneratorButtonClicked());
        landHeight.onValueChanged.AddListener((float v) => OnLandHeightChanged(v));
        landRoughness.onValueChanged.AddListener((float v) => OnLandRoughnessChanged(v));
        waterDepth.onValueChanged.AddListener((float v) => OnWaterDepthChanged(v));
    }

    private void OnDestroy ()
    {
        mapSizeDropdown.onValueChanged.RemoveAllListeners();
        generateButton.onClick.RemoveAllListeners();
        landHeight.onValueChanged.RemoveAllListeners();
        landRoughness.onValueChanged.RemoveAllListeners();
        waterDepth.onValueChanged.RemoveAllListeners();
    }

    public void UpdatePanel (MapComponent mapData)
    {
        for (int i = 0; i < mapSettingOptions.Length; i++)
        {
            if (mapData.TileDimension.x == mapSettingOptions[i].size)
            {
                currentMapSize = mapSettingOptions[i].size;
                mapSizeDropdown.value = i;
                break;
            }
        }

        landHeight.value = mapData.MaxHeight;
        landRoughness.value = mapData.Roughness;
        waterDepth.value = mapData.MaxDepth;
    }

    private void OnMapSizeChanged (int value)
    {
        currentMapSize = mapSettingOptions[value].size;

        landHeight.minValue = mapSettingOptions[value].heightInterval.x;
        landHeight.maxValue = mapSettingOptions[value].heightInterval.y;
        landHeight.value = mapSettingOptions[value].startHeight;

        currentMapHeight = (int)landHeight.value;

        landRoughness.minValue = mapSettingOptions[value].roughnessInterval.x;
        landRoughness.maxValue = mapSettingOptions[value].roughnessInterval.y;
        landRoughness.value = mapSettingOptions[value].startRoughness;

        currentRoughness = (int)landRoughness.value;

        waterDepth.minValue = mapSettingOptions[value].depthInterval.x;
        waterDepth.maxValue = mapSettingOptions[value].depthInterval.y;
        waterDepth.value = mapSettingOptions[value].startDepth;

        currentWaterDepth = (int)waterDepth.value;

        OnAnyUIEvent?.Invoke();
    }

    private void OnLandHeightChanged (float value)
    {
        currentMapHeight = (int)value;

        OnAnyUIEvent?.Invoke();
    }

    private void OnLandRoughnessChanged (float value)
    {
        currentRoughness = value;

        OnAnyUIEvent?.Invoke();
    }

    private void OnWaterDepthChanged (float value)
    {
        currentWaterDepth = (int)value;

        OnAnyUIEvent?.Invoke();
    }

    private void OnMapGeneratorButtonClicked ()
    {
        OnAnyUIEvent?.Invoke();
        OnTerrainGeneratorButtonClicked?.Invoke(currentMapSize, currentMapHeight, currentRoughness, currentWaterDepth);
    }
}

[Serializable]
public class MapSettings
{
    public int size;
    public Vector2Int heightInterval;
    public Vector2 roughnessInterval;
    public Vector2Int depthInterval;
    public int startHeight;
    public float startRoughness;
    public int startDepth;
}
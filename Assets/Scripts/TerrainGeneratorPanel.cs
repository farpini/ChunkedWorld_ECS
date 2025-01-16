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
    [SerializeField] private int[] mapSizeOptions;

    private int currentMapSize;
    private int currentMapHeight;
    private float currentRoughness;
    private int currentWaterDepth;

    public Action<int, int, float, int> OnTerrainGeneratorButtonClicked;

    private void Awake ()
    {
        if (mapSizeOptions != null)
        {
            var optionsStr = new List<string>();
            for (int i = 0; i < mapSizeOptions.Length; i++)
            {
                optionsStr.Add(mapSizeOptions[i].ToString() + "x" + mapSizeOptions[i].ToString());
            }
            mapSizeDropdown.AddOptions(optionsStr);
        }

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

    public void UpdatePanel (MapComponent2 mapData)
    {
        for (int i = 0; i < mapSizeOptions.Length; i++)
        {
            if (mapData.TileDimension.x == mapSizeOptions[i])
            {
                currentMapSize = mapSizeOptions[i];
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
        currentMapSize = mapSizeOptions[value];

        landHeight.minValue = 1;
        landHeight.maxValue = 4;
        landHeight.value = 3;

        landRoughness.minValue = 0;
        landRoughness.maxValue = 4;
        landRoughness.value = 0.25f;

        waterDepth.minValue = 0;
        waterDepth.maxValue = 3;
        waterDepth.value = 1;
    }

    private void OnLandHeightChanged (float value)
    {
        currentMapHeight = (int)value;
    }

    private void OnLandRoughnessChanged (float value)
    {
        currentRoughness = value;
    }

    private void OnWaterDepthChanged (float value)
    {
        currentWaterDepth = (int)value;
    }

    private void OnMapGeneratorButtonClicked ()
    {
        OnTerrainGeneratorButtonClicked?.Invoke(currentMapSize, currentMapHeight, currentRoughness, currentWaterDepth);
    }
}
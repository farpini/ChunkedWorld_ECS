/*
 * Written by Fernando Arpini Ferretto
 * https://github.com/farpini/ProceduralTileChunkedMap_ECS
 */

using System;
using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class ChunkedModelPanel : MonoBehaviour
{
    [SerializeField] private Image modelImage;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Slider randomSpawnSlider;
    [SerializeField] private Button randomPlacementButton;
    [SerializeField] private Button selectPlacementButton;
    [SerializeField] private Button removeModelButton;
    [SerializeField] private TMP_Text modelNameText;
    [SerializeField] private TMP_Text spawnRandomText;

    private List<string> modelNameList = new();
    private List<Sprite> modelSpriteList = new();

    private int currentModelSelected;
    private int currentPercentageSpawnModel;

    public Action OnAnyUIEvent;
    public Action<int, int> OnRandomPlacementClicked;
    public Action<int> OnSelectPlacementClicked;
    public Action OnRemoveModelClicked;


    private void Awake ()
    {
        currentModelSelected = -1;
        currentPercentageSpawnModel = (int)randomSpawnSlider.value;

        nextButton.onClick.AddListener(() => OnNextButtonClicked());
        previousButton.onClick.AddListener(() => OnPreviousButtonClicked());
        randomPlacementButton.onClick.AddListener(() => OnRandomPlacementButtonClicked());
        selectPlacementButton.onClick.AddListener(() => OnSelectPlacementButtonClicked());
        removeModelButton.onClick.AddListener(() => OnRemoveModelButtonClicked());
        randomSpawnSlider.onValueChanged.AddListener((float v) => OnSpawnRandomPercentageValueChanged(v));
    }

    private void OnDestroy ()
    {
        nextButton.onClick.RemoveAllListeners();
        previousButton.onClick.RemoveAllListeners();
        randomPlacementButton.onClick.RemoveAllListeners();
        selectPlacementButton.onClick.RemoveAllListeners();
        removeModelButton.onClick.RemoveAllListeners();
        randomSpawnSlider.onValueChanged.RemoveAllListeners();
    }

    public void LoadModel (ref BlobArray<char> nameArray, ref BlobArray<byte> textureByteArray)
    {
        modelNameList.Add(nameArray.BlobCharToString());

        var iconModelTexture = new Texture2D(128, 128, TextureFormat.RGB24, false);
        iconModelTexture.LoadRawTextureData(textureByteArray.ToArray());
        iconModelTexture.Apply();

        var sprite = Sprite.Create(iconModelTexture, new Rect(0.0f, 0.0f, iconModelTexture.width, iconModelTexture.height), new Vector2(0f, 0f));

        modelSpriteList.Add(sprite);
    }

    public void UpdatePanel ()
    {
        if (modelNameList.Count > 0)
        {
            SelectModel(0);
        }

        SetRandomPercentageText();
    }

    private void SelectModel (int modelId)
    {
        currentModelSelected = modelId;

        if (currentModelSelected < modelSpriteList.Count)
        {
            modelImage.sprite = modelSpriteList[currentModelSelected];
            modelNameText.text = modelNameList[currentModelSelected];
        }
    }

    private void SetRandomPercentageText ()
    {
        spawnRandomText.text = "Spawn Random: " + currentPercentageSpawnModel.ToString() + "%";
    }

    private void OnSpawnRandomPercentageValueChanged (float value)
    {
        currentPercentageSpawnModel = (int)value;
        SetRandomPercentageText();
        OnAnyUIEvent?.Invoke();
    }

    private void OnNextButtonClicked ()
    {
        var nextModelId = currentModelSelected;
        nextModelId++;
        if (nextModelId >= modelSpriteList.Count)
        {
            nextModelId = 0;
        }
        SelectModel(nextModelId);
        OnAnyUIEvent?.Invoke();
    }

    private void OnPreviousButtonClicked ()
    {
        var previousModelId = currentModelSelected;
        previousModelId--;
        if (previousModelId < 0)
        {
            previousModelId = modelSpriteList.Count - 1;
        }
        SelectModel(previousModelId);
        OnAnyUIEvent?.Invoke();
    }

    private void OnRandomPlacementButtonClicked ()
    {
        OnAnyUIEvent?.Invoke();
        OnRandomPlacementClicked?.Invoke(currentModelSelected, currentPercentageSpawnModel);
    }

    private void OnSelectPlacementButtonClicked ()
    {
        OnAnyUIEvent?.Invoke();
        OnSelectPlacementClicked?.Invoke(currentModelSelected);
    }

    private void OnRemoveModelButtonClicked ()
    {
        OnAnyUIEvent?.Invoke();
        OnRemoveModelClicked?.Invoke();
    }
}
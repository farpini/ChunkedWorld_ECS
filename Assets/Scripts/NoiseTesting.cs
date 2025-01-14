using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEngine;

public static class NoiseTesting
{
    private static float OctavedRidgeNoise (float2 pos, TerrainHeightMapSettings heightMaptSettings, MapComponent mapComponent)
    {
        float noiseVal = 0, amplitude = 1, freq = heightMaptSettings.noiseScale, weight = 1;

        for (int o = 0; o < heightMaptSettings.octaves; o++)
        {
            float v = 1 - Mathf.Abs(noise.snoise(pos / freq / mapComponent.TileDimension.x));
            v *= v;
            v *= weight;
            weight = Mathf.Clamp01(v * heightMaptSettings.weight);
            noiseVal += v * amplitude;

            freq /= heightMaptSettings.frequency;
            amplitude /= heightMaptSettings.lacunarity;
        }

        return noiseVal;
    }

    private static float OctavedSimplexNoise (float2 pos, TerrainHeightMapSettings heightMaptSettings, MapComponent mapComponent)
    {
        float noiseVal = 0, amplitude = 1, freq = heightMaptSettings.noiseScale;

        for (int o = 0; o < heightMaptSettings.octaves; o++)
        {
            float v = (noise.snoise(pos / freq / mapComponent.TileDimension.x) + 1) / 2f;
            noiseVal += v * amplitude;

            freq /= heightMaptSettings.frequency;
            amplitude /= heightMaptSettings.lacunarity;
        }

        return noiseVal;
    }

    private static float FalloffMap (float2 pos, TerrainHeightMapSettings heightMaptSettings, MapComponent mapComponent)
    {
        float x = (pos.x / (mapComponent.TileDimension.x + 1)) * 2 - 1;
        float y = (pos.y / (mapComponent.TileDimension.y + 1)) * 2 - 1;

        float value = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));

        float a = heightMaptSettings.falloffSteepness;
        float b = heightMaptSettings.falloffOffset;

        return 1 - (Mathf.Pow(value, a) / (Mathf.Pow(value, a) + Mathf.Pow((b - b * value), a)));
    }

    /*
    int maxHeight = 4;

        for (int i = 0; i<mapComponent.TileDimension.x; i++)
        {
            for (int j = 0; j<mapComponent.TileDimension.y; j++)
            {
                var tilePosition = new int2(i, j);
    var tileIndex = mapComponent.GetTileIndexFromTilePosition(tilePosition);
    var noise = (OctavedSimplexNoise(tilePosition, terrainHeightMapSettings) + OctavedRidgeNoise(tilePosition, terrainHeightMapSettings))
        / 2f * FalloffMap(tilePosition, terrainHeightMapSettings) * maxHeight;
    var h = math.clamp(noise, 0, maxHeight);
    var tileData = mapTiles[tileIndex];
    tileData.tileHeight = (int) math.round (h);
    mapTiles[tileIndex] = tileData;
                var dPos = new Vector3(tilePosition.x, 0f, tilePosition.y) * mapComponent.TileWidth;
    var dFPos = new Vector3(tilePosition.x, 20f, tilePosition.y) * mapComponent.TileWidth;
    Debug.DrawLine(dPos, dFPos, GetColor(tileData.tileHeight), 20f);
            }
        }
    */
}

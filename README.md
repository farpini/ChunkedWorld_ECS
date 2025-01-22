# PROCEDURAL TILE CHUNKED MAP - ECS
----------------------------------------------
![](https://github.com/farpini/ProceduralTileChunkedMap_ECS/blob/main/TitleImage.JPG)<br />
This is a tile map procedural generator. It was developed using the ECS architecture and use of burst compiler for high-performance.
Models can be placed on the map and are rendered by chunks with a custom method of combining meshes (like what GPU instancing does).
It uses a diamond-square algorithm to generate terrains.
<br /><br />Features:
<br />-> Map size of 2^n (can be modified for larger sizes);
<br />-> Map input settings: land height, land roughness and water depth;
<br />-> Place/remove models with area selection;
<br />-> Generation with start seed based on the current epoch time;
<br />-> Random placement (not yet implemented);
<br /><br /><p align="center">
![](https://github.com/farpini/ProceduralTileChunkedMap_ECS/blob/main/Features.gif)<br /><br /><p align="center">
![](https://github.com/farpini/ProceduralTileChunkedMap_ECS/blob/main/Performance.gif)<br /><br /><p align="center">
![](https://github.com/farpini/ProceduralTileChunkedMap_ECS/blob/main/Chunk.gif)<br /><br /></p>
Adding new models:<br />
New models can be added to the project. You just need to create a prefab with Model Component Script and add it to the MeshDataAuthoring model array.
Make sure that the model mesh index format is set to 32 bits and Read/Write option is enabled. The model should appear in the Chunked Model UI Selection.<br /><br />

![](https://github.com/farpini/ProceduralTileChunkedMap_ECS/blob/main/MeshSettings.JPG)<br /><br />
----------------------------------------------
Assets: (thanks to)
- Water shader - Bitgem (StylisedWater).
- 3D Models templates: Low-Poly Park - Thunderent

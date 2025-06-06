using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine.Profiling;
using UnityEngine.Experimental.Rendering;
using Cell = UnityEngine.Rendering.ProbeReferenceVolume.Cell;
using CellStreamingScratchBuffer = UnityEngine.Rendering.ProbeReferenceVolume.CellStreamingScratchBuffer;
using CellStreamingScratchBufferLayout = UnityEngine.Rendering.ProbeReferenceVolume.CellStreamingScratchBufferLayout;

namespace UnityEngine.Rendering
{
    internal class ProbeBrickPool
    {
        internal static readonly int _Out_L0_L1Rx = Shader.PropertyToID("_Out_L0_L1Rx");
        internal static readonly int _Out_L1G_L1Ry = Shader.PropertyToID("_Out_L1G_L1Ry");
        internal static readonly int _Out_L1B_L1Rz = Shader.PropertyToID("_Out_L1B_L1Rz");
        internal static readonly int _Out_Shared = Shader.PropertyToID("_Out_Shared");
        internal static readonly int _Out_ProbeOcclusion = Shader.PropertyToID("_Out_ProbeOcclusion");
        internal static readonly int _Out_SkyOcclusionL0L1 = Shader.PropertyToID("_Out_SkyOcclusionL0L1");
        internal static readonly int _Out_SkyShadingDirectionIndices = Shader.PropertyToID("_Out_SkyShadingDirectionIndices");
        internal static readonly int _Out_L2_0 = Shader.PropertyToID("_Out_L2_0");
        internal static readonly int _Out_L2_1 = Shader.PropertyToID("_Out_L2_1");
        internal static readonly int _Out_L2_2 = Shader.PropertyToID("_Out_L2_2");
        internal static readonly int _Out_L2_3 = Shader.PropertyToID("_Out_L2_3");
        internal static readonly int _ProbeVolumeScratchBufferLayout = Shader.PropertyToID(nameof(ProbeReferenceVolume.CellStreamingScratchBufferLayout));
        internal static readonly int _ProbeVolumeScratchBuffer= Shader.PropertyToID("_ScratchBuffer");

        internal static int DivRoundUp(int x, int y) => (x + y - 1) / y;

        const int kChunkSizeInBricks = 128;

        [DebuggerDisplay("Chunk ({x}, {y}, {z})")]
        public struct BrickChunkAlloc
        {
            public int x, y, z;

            internal int flattenIndex(int sx, int sy) { return z * (sx * sy) + y * sx + x; }
        }

        public struct DataLocation
        {
            internal Texture TexL0_L1rx;

            internal Texture TexL1_G_ry;
            internal Texture TexL1_B_rz;

            internal Texture TexL2_0;
            internal Texture TexL2_1;
            internal Texture TexL2_2;
            internal Texture TexL2_3;

            internal Texture TexProbeOcclusion;

            internal Texture TexValidity;
            internal Texture TexSkyOcclusion;
            internal Texture TexSkyShadingDirectionIndices;

            internal int width;
            internal int height;
            internal int depth;

            internal void Cleanup()
            {
                CoreUtils.Destroy(TexL0_L1rx);

                CoreUtils.Destroy(TexL1_G_ry);
                CoreUtils.Destroy(TexL1_B_rz);

                CoreUtils.Destroy(TexL2_0);
                CoreUtils.Destroy(TexL2_1);
                CoreUtils.Destroy(TexL2_2);
                CoreUtils.Destroy(TexL2_3);

                CoreUtils.Destroy(TexProbeOcclusion);

                CoreUtils.Destroy(TexValidity);
                CoreUtils.Destroy(TexSkyOcclusion);
                CoreUtils.Destroy(TexSkyShadingDirectionIndices);

                TexL0_L1rx = null;

                TexL1_G_ry = null;
                TexL1_B_rz = null;

                TexL2_0 = null;
                TexL2_1 = null;
                TexL2_2 = null;
                TexL2_3 = null;
                TexProbeOcclusion = null;
                TexValidity = null;
                TexSkyOcclusion = null;
                TexSkyShadingDirectionIndices = null;
            }
        }

        internal const int kBrickCellCount = 3;
        internal const int kBrickProbeCountPerDim = kBrickCellCount + 1;
        internal const int kBrickProbeCountTotal = kBrickProbeCountPerDim * kBrickProbeCountPerDim * kBrickProbeCountPerDim;
        internal const int kChunkProbeCountPerDim = kChunkSizeInBricks * kBrickProbeCountPerDim;

        internal int estimatedVMemCost { get; private set; }

        const int kMaxPoolWidth = 1 << 11; // 2048 texels is a d3d11 limit for tex3d in all dimensions

        internal DataLocation m_Pool; // internal to access it from blending pool only
        BrickChunkAlloc m_NextFreeChunk;
        Stack<BrickChunkAlloc> m_FreeList;
        int m_AvailableChunkCount;

        ProbeVolumeSHBands m_SHBands;
        bool m_ContainsValidity;
        bool m_ContainsProbeOcclusion;
        bool m_ContainsRenderingLayers;
        bool m_ContainsSkyOcclusion;
        bool m_ContainsSkyShadingDirection;

        static ComputeShader s_DataUploadCS;
        static int s_DataUploadKernel;
        static ComputeShader s_DataUploadL2CS;
        static int s_DataUploadL2Kernel;
        static LocalKeyword s_DataUpload_Shared;
        static LocalKeyword s_DataUpload_ProbeOcclusion;
        static LocalKeyword s_DataUpload_SkyOcclusion;
        static LocalKeyword s_DataUpload_SkyShadingDirection;

        internal static void Initialize()
        {
            if (!SystemInfo.supportsComputeShaders)
                return;

            s_DataUploadCS = GraphicsSettings.GetRenderPipelineSettings<ProbeVolumeRuntimeResources>()?.probeVolumeUploadDataCS;
            s_DataUploadL2CS = GraphicsSettings.GetRenderPipelineSettings<ProbeVolumeRuntimeResources>()?.probeVolumeUploadDataL2CS;

            if (s_DataUploadCS != null)
            {
                s_DataUploadKernel = s_DataUploadCS ? s_DataUploadCS.FindKernel("UploadData") : -1;
                s_DataUpload_Shared = new LocalKeyword(s_DataUploadCS, "PROBE_VOLUMES_SHARED_DATA");
                s_DataUpload_ProbeOcclusion = new LocalKeyword(s_DataUploadCS, "PROBE_VOLUMES_PROBE_OCCLUSION");
                s_DataUpload_SkyOcclusion = new LocalKeyword(s_DataUploadCS, "PROBE_VOLUMES_SKY_OCCLUSION");
                s_DataUpload_SkyShadingDirection = new LocalKeyword(s_DataUploadCS, "PROBE_VOLUMES_SKY_SHADING_DIRECTION");
            }

            if (s_DataUploadL2CS != null)
            {
                s_DataUploadL2Kernel = s_DataUploadL2CS ? s_DataUploadL2CS.FindKernel("UploadDataL2") : -1;
            }
        }

        internal Texture GetValidityTexture()
        {
            return m_Pool.TexValidity;
        }

        internal Texture GetSkyOcclusionTexture()
        {
            return m_Pool.TexSkyOcclusion;
        }

        internal Texture GetSkyShadingDirectionIndicesTexture()
        {
            return m_Pool.TexSkyShadingDirectionIndices;
        }

        internal Texture GetProbeOcclusionTexture()
        {
            return m_Pool.TexProbeOcclusion;
        }

        internal ProbeBrickPool(ProbeVolumeTextureMemoryBudget memoryBudget, ProbeVolumeSHBands shBands, bool allocateValidityData = false, bool allocateRenderingLayerData = false, bool allocateSkyOcclusion = false, bool allocateSkyShadingData = false, bool allocateProbeOcclusionData = false)
        {
            Profiler.BeginSample("Create ProbeBrickPool");
            m_NextFreeChunk.x = m_NextFreeChunk.y = m_NextFreeChunk.z = 0;

            m_SHBands = shBands;
            m_ContainsValidity = allocateValidityData;
            m_ContainsProbeOcclusion = allocateProbeOcclusionData;
            m_ContainsRenderingLayers = allocateRenderingLayerData;
            m_ContainsSkyOcclusion = allocateSkyOcclusion;
            m_ContainsSkyShadingDirection = allocateSkyShadingData;

            m_FreeList = new Stack<BrickChunkAlloc>(256);

            DerivePoolSizeFromBudget(memoryBudget, out int width, out int height, out int depth);
            AllocatePool(width, height, depth);

            m_AvailableChunkCount = (m_Pool.width / (kChunkSizeInBricks * kBrickProbeCountPerDim)) * (m_Pool.height / kBrickProbeCountPerDim) * (m_Pool.depth / kBrickProbeCountPerDim);

            Profiler.EndSample();
        }

        internal void AllocatePool(int width, int height, int depth)
        {
            m_Pool = CreateDataLocation(width * height * depth, false, m_SHBands, "APV", true,
                m_ContainsValidity, m_ContainsRenderingLayers, m_ContainsSkyOcclusion, m_ContainsSkyShadingDirection, m_ContainsProbeOcclusion, out int estimatedCost);
            estimatedVMemCost = estimatedCost;
        }

        public int GetRemainingChunkCount()
        {
            return m_AvailableChunkCount;
        }

        internal void EnsureTextureValidity()
        {
            // We assume that if a texture is null, all of them are. In any case we reboot them altogether.
            if (m_Pool.TexL0_L1rx == null)
            {
                m_Pool.Cleanup();
                AllocatePool(m_Pool.width, m_Pool.height, m_Pool.depth);
            }
        }

        internal bool EnsureTextureValidity(bool renderingLayers, bool skyOcclusion, bool skyDirection, bool probeOcclusion)
        {
            if (m_ContainsRenderingLayers != renderingLayers || m_ContainsSkyOcclusion != skyOcclusion || m_ContainsSkyShadingDirection != skyDirection || m_ContainsProbeOcclusion != probeOcclusion)
            {
                m_Pool.Cleanup();

                m_ContainsRenderingLayers = renderingLayers;
                m_ContainsSkyOcclusion = skyOcclusion;
                m_ContainsSkyShadingDirection = skyDirection;
                m_ContainsProbeOcclusion = probeOcclusion;
                AllocatePool(m_Pool.width, m_Pool.height, m_Pool.depth);
                return false;
            }
            return true;
        }

        internal static int GetChunkSizeInBrickCount() { return kChunkSizeInBricks; }
        internal static int GetChunkSizeInProbeCount() { return kChunkSizeInBricks * kBrickProbeCountTotal; }

        internal int GetPoolWidth() { return m_Pool.width; }
        internal int GetPoolHeight() { return m_Pool.height; }
        internal Vector3Int GetPoolDimensions() { return new Vector3Int(m_Pool.width, m_Pool.height, m_Pool.depth); }
        internal void GetRuntimeResources(ref ProbeReferenceVolume.RuntimeResources rr)
        {
            rr.L0_L1rx = m_Pool.TexL0_L1rx as RenderTexture;

            rr.L1_G_ry = m_Pool.TexL1_G_ry as RenderTexture;
            rr.L1_B_rz = m_Pool.TexL1_B_rz as RenderTexture;

            rr.L2_0 = m_Pool.TexL2_0 as RenderTexture;
            rr.L2_1 = m_Pool.TexL2_1 as RenderTexture;
            rr.L2_2 = m_Pool.TexL2_2 as RenderTexture;
            rr.L2_3 = m_Pool.TexL2_3 as RenderTexture;

            rr.ProbeOcclusion = m_Pool.TexProbeOcclusion as RenderTexture;

            rr.Validity = m_Pool.TexValidity as RenderTexture;
            rr.SkyOcclusionL0L1 = m_Pool.TexSkyOcclusion as RenderTexture;
            rr.SkyShadingDirectionIndices = m_Pool.TexSkyShadingDirectionIndices as RenderTexture;
        }

        internal void Clear()
        {
            m_FreeList.Clear();
            m_NextFreeChunk.x = m_NextFreeChunk.y = m_NextFreeChunk.z = 0;
        }

        internal static int GetChunkCount(int brickCount)
        {
            int chunkSize = kChunkSizeInBricks;
            return (brickCount + chunkSize - 1) / chunkSize;
        }

        internal bool Allocate(int numberOfBrickChunks, List<BrickChunkAlloc> outAllocations, bool ignoreErrorLog)
        {
            while (m_FreeList.Count > 0 && numberOfBrickChunks > 0)
            {
                outAllocations.Add(m_FreeList.Pop());
                numberOfBrickChunks--;
                m_AvailableChunkCount--;
            }

            for (uint i = 0; i < numberOfBrickChunks; i++)
            {
                if (m_NextFreeChunk.z >= m_Pool.depth)
                {
                    // During baking we know we can hit this when trying to do dilation of all cells at the same time.
                    // We don't want controlled error message spam during baking so we ignore it.
                    // In theory this should never happen with proper streaming/defrag but we keep the message just in case otherwise.
                    if (!ignoreErrorLog)
                        Debug.LogError("Cannot allocate more brick chunks, probe volume brick pool is full.");

                    Deallocate(outAllocations);
                    outAllocations.Clear();
                    return false; // failure case, pool is full
                }

                outAllocations.Add(m_NextFreeChunk);
                m_AvailableChunkCount--;

                m_NextFreeChunk.x += kChunkSizeInBricks * kBrickProbeCountPerDim;
                if (m_NextFreeChunk.x >= m_Pool.width)
                {
                    m_NextFreeChunk.x = 0;
                    m_NextFreeChunk.y += kBrickProbeCountPerDim;
                    if (m_NextFreeChunk.y >= m_Pool.height)
                    {
                        m_NextFreeChunk.y = 0;
                        m_NextFreeChunk.z += kBrickProbeCountPerDim;
                    }
                }
            }

            return true;
        }

        internal void Deallocate(List<BrickChunkAlloc> allocations)
        {
            m_AvailableChunkCount += allocations.Count;

            foreach (var brick in allocations)
                m_FreeList.Push(brick);
        }

        internal void Update(DataLocation source, List<BrickChunkAlloc> srcLocations, List<BrickChunkAlloc> dstLocations, int destStartIndex, ProbeVolumeSHBands bands)
        {
            for (int i = 0; i < srcLocations.Count; i++)
            {
                BrickChunkAlloc src = srcLocations[i];
                BrickChunkAlloc dst = dstLocations[destStartIndex + i];

                for (int j = 0; j < kBrickProbeCountPerDim; j++)
                {
                    int width = Mathf.Min(kChunkSizeInBricks * kBrickProbeCountPerDim, source.width - src.x);
                    Graphics.CopyTexture(source.TexL0_L1rx, src.z + j, 0, src.x, src.y, width, kBrickProbeCountPerDim, m_Pool.TexL0_L1rx, dst.z + j, 0, dst.x, dst.y);

                    Graphics.CopyTexture(source.TexL1_G_ry, src.z + j, 0, src.x, src.y, width, kBrickProbeCountPerDim, m_Pool.TexL1_G_ry, dst.z + j, 0, dst.x, dst.y);
                    Graphics.CopyTexture(source.TexL1_B_rz, src.z + j, 0, src.x, src.y, width, kBrickProbeCountPerDim, m_Pool.TexL1_B_rz, dst.z + j, 0, dst.x, dst.y);

                    if (m_ContainsValidity)
                        Graphics.CopyTexture(source.TexValidity, src.z + j, 0, src.x, src.y, width, kBrickProbeCountPerDim, m_Pool.TexValidity, dst.z + j, 0, dst.x, dst.y);

                    if (m_ContainsSkyOcclusion)
                    {
                        Graphics.CopyTexture(source.TexSkyOcclusion, src.z + j, 0, src.x, src.y, width, kBrickProbeCountPerDim, m_Pool.TexSkyOcclusion, dst.z + j, 0, dst.x, dst.y);
                        if (m_ContainsSkyShadingDirection)
                        {
                            Graphics.CopyTexture(source.TexSkyShadingDirectionIndices, src.z + j, 0, src.x, src.y, width, kBrickProbeCountPerDim, m_Pool.TexSkyShadingDirectionIndices, dst.z + j, 0, dst.x, dst.y);
                        }
                    }

                    if (bands == ProbeVolumeSHBands.SphericalHarmonicsL2)
                    {
                        Graphics.CopyTexture(source.TexL2_0, src.z + j, 0, src.x, src.y, width, kBrickProbeCountPerDim, m_Pool.TexL2_0, dst.z + j, 0, dst.x, dst.y);
                        Graphics.CopyTexture(source.TexL2_1, src.z + j, 0, src.x, src.y, width, kBrickProbeCountPerDim, m_Pool.TexL2_1, dst.z + j, 0, dst.x, dst.y);
                        Graphics.CopyTexture(source.TexL2_2, src.z + j, 0, src.x, src.y, width, kBrickProbeCountPerDim, m_Pool.TexL2_2, dst.z + j, 0, dst.x, dst.y);
                        Graphics.CopyTexture(source.TexL2_3, src.z + j, 0, src.x, src.y, width, kBrickProbeCountPerDim, m_Pool.TexL2_3, dst.z + j, 0, dst.x, dst.y);
                    }

                    if (m_ContainsProbeOcclusion)
                    {
                        Graphics.CopyTexture(source.TexProbeOcclusion, src.z + j, 0, src.x, src.y, width, kBrickProbeCountPerDim, m_Pool.TexProbeOcclusion, dst.z + j, 0, dst.x, dst.y);
                    }
                }
            }
        }

        internal void Update(CommandBuffer cmd, CellStreamingScratchBuffer dataBuffer, CellStreamingScratchBufferLayout layout,
            List<BrickChunkAlloc> dstLocations, bool updateSharedData, Texture validityTexture, ProbeVolumeSHBands bands,
            bool skyOcclusion, Texture skyOcclusionTexture, bool skyShadingDirections, Texture skyShadingDirectionsTexture, bool probeOcclusion)
        {
            using (new ProfilingScope(cmd, ProfilingSampler.Get(CoreProfileId.APVDiskStreamingUpdatePool)))
            {
                int chunkCount = dstLocations.Count;

                cmd.SetComputeTextureParam(s_DataUploadCS, s_DataUploadKernel, _Out_L0_L1Rx, m_Pool.TexL0_L1rx);
                cmd.SetComputeTextureParam(s_DataUploadCS, s_DataUploadKernel, _Out_L1G_L1Ry, m_Pool.TexL1_G_ry);
                cmd.SetComputeTextureParam(s_DataUploadCS, s_DataUploadKernel, _Out_L1B_L1Rz, m_Pool.TexL1_B_rz);

                if (updateSharedData)
                {
                    cmd.EnableKeyword(s_DataUploadCS, s_DataUpload_Shared);
                    cmd.SetComputeTextureParam(s_DataUploadCS, s_DataUploadKernel, _Out_Shared, validityTexture);

                    if (skyOcclusion)
                    {
                        cmd.EnableKeyword(s_DataUploadCS, s_DataUpload_SkyOcclusion);
                        cmd.SetComputeTextureParam(s_DataUploadCS, s_DataUploadKernel, _Out_SkyOcclusionL0L1, skyOcclusionTexture);
                        if (skyShadingDirections)
                        {
                            cmd.SetComputeTextureParam(s_DataUploadCS, s_DataUploadKernel, _Out_SkyShadingDirectionIndices, skyShadingDirectionsTexture);
                            cmd.EnableKeyword(s_DataUploadCS, s_DataUpload_SkyShadingDirection);
                        }
                        else
                            cmd.DisableKeyword(s_DataUploadCS, s_DataUpload_SkyShadingDirection);
                    }
                }
                else
                {
                    cmd.DisableKeyword(s_DataUploadCS, s_DataUpload_Shared);
                    cmd.DisableKeyword(s_DataUploadCS, s_DataUpload_SkyOcclusion);
                    cmd.DisableKeyword(s_DataUploadCS, s_DataUpload_SkyShadingDirection);
                }

                if (bands == ProbeVolumeSHBands.SphericalHarmonicsL2)
                {
                    cmd.SetComputeTextureParam(s_DataUploadL2CS, s_DataUploadL2Kernel, _Out_L2_0, m_Pool.TexL2_0);
                    cmd.SetComputeTextureParam(s_DataUploadL2CS, s_DataUploadL2Kernel, _Out_L2_1, m_Pool.TexL2_1);
                    cmd.SetComputeTextureParam(s_DataUploadL2CS, s_DataUploadL2Kernel, _Out_L2_2, m_Pool.TexL2_2);
                    cmd.SetComputeTextureParam(s_DataUploadL2CS, s_DataUploadL2Kernel, _Out_L2_3, m_Pool.TexL2_3);
                }

                if (probeOcclusion)
                {
                    cmd.EnableKeyword(s_DataUploadCS, s_DataUpload_ProbeOcclusion);
                    cmd.SetComputeTextureParam(s_DataUploadCS, s_DataUploadKernel, _Out_ProbeOcclusion, m_Pool.TexProbeOcclusion);
                }
                else
                {
                    cmd.DisableKeyword(s_DataUploadCS, s_DataUpload_ProbeOcclusion);
                }

                const int numthreads = 64;
                const int probePerThread = 4; // We can upload 4 probes per thread in the current shader.
                int threadX = DivRoundUp(kChunkSizeInBricks * kBrickProbeCountTotal / probePerThread, numthreads);

                ConstantBuffer.Push(cmd, layout, s_DataUploadCS, _ProbeVolumeScratchBufferLayout);
                cmd.SetComputeBufferParam(s_DataUploadCS, s_DataUploadKernel, _ProbeVolumeScratchBuffer, dataBuffer.buffer);
                cmd.DispatchCompute(s_DataUploadCS, s_DataUploadKernel, threadX, 1, chunkCount);

                if (bands == ProbeVolumeSHBands.SphericalHarmonicsL2)
                {
                    ConstantBuffer.Push(cmd, layout, s_DataUploadL2CS, _ProbeVolumeScratchBufferLayout);
                    cmd.SetComputeBufferParam(s_DataUploadL2CS, s_DataUploadL2Kernel, _ProbeVolumeScratchBuffer, dataBuffer.buffer);
                    cmd.DispatchCompute(s_DataUploadL2CS, s_DataUploadL2Kernel, threadX, 1, chunkCount);
                }
            }
        }

        internal void UpdateValidity(DataLocation source, List<BrickChunkAlloc> srcLocations, List<BrickChunkAlloc> dstLocations, int destStartIndex)
        {
            Debug.Assert(m_ContainsValidity);

            for (int i = 0; i < srcLocations.Count; i++)
            {
                BrickChunkAlloc src = srcLocations[i];
                BrickChunkAlloc dst = dstLocations[destStartIndex + i];

                for (int j = 0; j < kBrickProbeCountPerDim; j++)
                {
                    int width = Mathf.Min(kChunkSizeInBricks * kBrickProbeCountPerDim, source.width - src.x);
                    Graphics.CopyTexture(source.TexValidity, src.z + j, 0, src.x, src.y, width, kBrickProbeCountPerDim, m_Pool.TexValidity, dst.z + j, 0, dst.x, dst.y);
                }
            }
        }

        internal static Vector3Int ProbeCountToDataLocSize(int numProbes)
        {
            Debug.Assert(numProbes != 0);
            Debug.Assert(numProbes % kBrickProbeCountTotal == 0);

            int numBricks = numProbes / kBrickProbeCountTotal;
            int poolWidth = kMaxPoolWidth / kBrickProbeCountPerDim;

            int width, height, depth;
            depth = (numBricks + poolWidth * poolWidth - 1) / (poolWidth * poolWidth);
            if (depth > 1)
                width = height = poolWidth;
            else
            {
                height = (numBricks + poolWidth - 1) / poolWidth;
                if (height > 1)
                    width = poolWidth;
                else
                    width = numBricks;
            }

            width *= kBrickProbeCountPerDim;
            height *= kBrickProbeCountPerDim;
            depth *= kBrickProbeCountPerDim;

            return new Vector3Int(width, height, depth);
        }

        static int EstimateMemoryCost(int width, int height, int depth, GraphicsFormat format)
        {
            int elementSize = format == GraphicsFormat.R16G16B16A16_SFloat ? 8 :
                format == GraphicsFormat.R8G8B8A8_UNorm ? 4 : 1;
            return (width * height * depth) * elementSize;
        }

        // Only computes the cost of textures allocated by the blending pool
        internal static int EstimateMemoryCostForBlending(ProbeVolumeTextureMemoryBudget memoryBudget, bool compressed, ProbeVolumeSHBands bands)
        {
            if (memoryBudget == 0)
                return 0;

            DerivePoolSizeFromBudget(memoryBudget, out int width, out int height, out int depth);
            Vector3Int locSize = ProbeCountToDataLocSize(width * height * depth);
            width = locSize.x;
            height = locSize.y;
            depth = locSize.z;

            int allocatedBytes = 0;
            var L0Format = GraphicsFormat.R16G16B16A16_SFloat;
            var L1L2Format = compressed ? GraphicsFormat.RGBA_BC7_UNorm : GraphicsFormat.R8G8B8A8_UNorm;

            allocatedBytes += EstimateMemoryCost(width, height, depth, L0Format);
            allocatedBytes += EstimateMemoryCost(width, height, depth, L1L2Format) * 2;

            if (bands == ProbeVolumeSHBands.SphericalHarmonicsL2)
                allocatedBytes += EstimateMemoryCost(width, height, depth, L1L2Format) * 3;

            return allocatedBytes;
        }

        public static Texture CreateDataTexture(int width, int height, int depth, GraphicsFormat format, string name, bool allocateRendertexture, ref int allocatedBytes)
        {
            allocatedBytes += EstimateMemoryCost(width, height, depth, format);

            Texture texture;
            if (allocateRendertexture)
            {
                texture = new RenderTexture(new RenderTextureDescriptor()
                {
                    width = width,
                    height = height,
                    volumeDepth = depth,
                    graphicsFormat = format,
                    mipCount = 1,
                    enableRandomWrite = SystemInfo.supportsComputeShaders,
                    dimension = TextureDimension.Tex3D,
                    msaaSamples = 1,
                });
            }
            else
                texture = new Texture3D(width, height, depth, format, TextureCreationFlags.None, 1);

            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.name = name;

            if (allocateRendertexture)
                (texture as RenderTexture).Create();
            return texture;
        }

        public static DataLocation CreateDataLocation(int numProbes, bool compressed, ProbeVolumeSHBands bands, string name, bool allocateRendertexture,
            bool allocateValidityData, bool allocateRenderingLayers, bool allocateSkyOcclusionData, bool allocateSkyShadingDirectionData, bool allocateProbeOcclusionData, out int allocatedBytes)
        {
            Vector3Int locSize = ProbeCountToDataLocSize(numProbes);
            int width = locSize.x;
            int height = locSize.y;
            int depth = locSize.z;

            DataLocation loc;
            var L0Format = GraphicsFormat.R16G16B16A16_SFloat;
            var L1L2Format = compressed ? GraphicsFormat.RGBA_BC7_UNorm : GraphicsFormat.R8G8B8A8_UNorm;

            var ValidityFormat = allocateRenderingLayers ?
                // for 32 bits we use a float format but it's an uint
                GraphicsFormat.R32_SFloat :
                // NOTE: Platforms that do not support Sample nor LoadStore for R8_UNorm need to fallback to RGBA8_UNorm since that format should be supported for both (e.g. GLES3.x)
                SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.Sample | GraphicsFormatUsage.LoadStore) ? GraphicsFormat.R8_UNorm : GraphicsFormat.R8G8B8A8_UNorm;

            allocatedBytes = 0;
            loc.TexL0_L1rx = CreateDataTexture(width, height, depth, L0Format, $"{name}_TexL0_L1rx", allocateRendertexture, ref allocatedBytes);
            loc.TexL1_G_ry = CreateDataTexture(width, height, depth, L1L2Format, $"{name}_TexL1_G_ry", allocateRendertexture, ref allocatedBytes);
            loc.TexL1_B_rz = CreateDataTexture(width, height, depth, L1L2Format, $"{name}_TexL1_B_rz", allocateRendertexture, ref allocatedBytes);

            if (allocateValidityData)
                loc.TexValidity = CreateDataTexture(width, height, depth, ValidityFormat, $"{name}_Validity", allocateRendertexture, ref allocatedBytes);
            else
                loc.TexValidity = null;

            if (allocateSkyOcclusionData)
                loc.TexSkyOcclusion = CreateDataTexture(width, height, depth, GraphicsFormat.R16G16B16A16_SFloat, $"{name}_SkyOcclusion", allocateRendertexture, ref allocatedBytes);
            else
                loc.TexSkyOcclusion = null;

            if (allocateSkyShadingDirectionData)
                loc.TexSkyShadingDirectionIndices = CreateDataTexture(width, height, depth, GraphicsFormat.R8_UNorm, $"{name}_SkyShadingDirectionIndices", allocateRendertexture, ref allocatedBytes);
            else
                loc.TexSkyShadingDirectionIndices = null;

            if (allocateProbeOcclusionData)
                loc.TexProbeOcclusion = CreateDataTexture(width, height, depth, GraphicsFormat.R8G8B8A8_UNorm, $"{name}_ProbeOcclusion", allocateRendertexture, ref allocatedBytes);
            else
                loc.TexProbeOcclusion = null;

            if (bands == ProbeVolumeSHBands.SphericalHarmonicsL2)
            {
                loc.TexL2_0 = CreateDataTexture(width, height, depth, L1L2Format, $"{name}_TexL2_0", allocateRendertexture, ref allocatedBytes);
                loc.TexL2_1 = CreateDataTexture(width, height, depth, L1L2Format, $"{name}_TexL2_1", allocateRendertexture, ref allocatedBytes);
                loc.TexL2_2 = CreateDataTexture(width, height, depth, L1L2Format, $"{name}_TexL2_2", allocateRendertexture, ref allocatedBytes);
                loc.TexL2_3 = CreateDataTexture(width, height, depth, L1L2Format, $"{name}_TexL2_3", allocateRendertexture, ref allocatedBytes);
            }
            else
            {
                loc.TexL2_0 = null;
                loc.TexL2_1 = null;
                loc.TexL2_2 = null;
                loc.TexL2_3 = null;
            }

            loc.width = width;
            loc.height = height;
            loc.depth = depth;

            return loc;
        }

        static void DerivePoolSizeFromBudget(ProbeVolumeTextureMemoryBudget memoryBudget, out int width, out int height, out int depth)
        {
            // TODO: This is fairly simplistic for now and relies on the enum to have the value set to the desired numbers,
            // might change the heuristic later on.
            width = (int)memoryBudget;
            height = (int)memoryBudget;
            depth = kBrickProbeCountPerDim;
        }

        internal void Cleanup()
        {
            m_Pool.Cleanup();
        }
    }

    internal class ProbeBrickBlendingPool
    {
        static ComputeShader stateBlendShader;
        static int scenarioBlendingKernel = -1;

        static readonly int _PoolDim_LerpFactor = Shader.PropertyToID("_PoolDim_LerpFactor");
        static readonly int _ChunkList = Shader.PropertyToID("_ChunkList");

        static readonly int _State0_L0_L1Rx = Shader.PropertyToID("_State0_L0_L1Rx");
        static readonly int _State0_L1G_L1Ry = Shader.PropertyToID("_State0_L1G_L1Ry");
        static readonly int _State0_L1B_L1Rz = Shader.PropertyToID("_State0_L1B_L1Rz");
        static readonly int _State0_L2_0 = Shader.PropertyToID("_State0_L2_0");
        static readonly int _State0_L2_1 = Shader.PropertyToID("_State0_L2_1");
        static readonly int _State0_L2_2 = Shader.PropertyToID("_State0_L2_2");
        static readonly int _State0_L2_3 = Shader.PropertyToID("_State0_L2_3");
        static readonly int _State0_ProbeOcclusion = Shader.PropertyToID("_State0_ProbeOcclusion");

        static readonly int _State1_L0_L1Rx = Shader.PropertyToID("_State1_L0_L1Rx");
        static readonly int _State1_L1G_L1Ry = Shader.PropertyToID("_State1_L1G_L1Ry");
        static readonly int _State1_L1B_L1Rz = Shader.PropertyToID("_State1_L1B_L1Rz");
        static readonly int _State1_L2_0 = Shader.PropertyToID("_State1_L2_0");
        static readonly int _State1_L2_1 = Shader.PropertyToID("_State1_L2_1");
        static readonly int _State1_L2_2 = Shader.PropertyToID("_State1_L2_2");
        static readonly int _State1_L2_3 = Shader.PropertyToID("_State1_L2_3");
        static readonly int _State1_ProbeOcclusion = Shader.PropertyToID("_State1_ProbeOcclusion");

        internal static void Initialize()
        {
            if (SystemInfo.supportsComputeShaders)
            {
                stateBlendShader = GraphicsSettings.GetRenderPipelineSettings<ProbeVolumeRuntimeResources>()?.probeVolumeBlendStatesCS;
                scenarioBlendingKernel = stateBlendShader ? stateBlendShader.FindKernel("BlendScenarios") : -1;
            }
        }

        Vector4[] m_ChunkList;
        int m_MappedChunks;

        ProbeBrickPool m_State0, m_State1;
        ProbeVolumeTextureMemoryBudget m_MemoryBudget;
        ProbeVolumeSHBands m_ShBands;
        bool m_ProbeOcclusion;

        internal bool isAllocated => m_State0 != null;
        internal int estimatedVMemCost
        {
            get
            {
                if (!ProbeReferenceVolume.instance.supportScenarioBlending)
                    return 0;
                if (isAllocated)
                    return m_State0.estimatedVMemCost + m_State1.estimatedVMemCost;
                return ProbeBrickPool.EstimateMemoryCostForBlending(m_MemoryBudget, false, m_ShBands) * 2;
            }
        }

        internal int GetPoolWidth() { return m_State0.m_Pool.width; }
        internal int GetPoolHeight() { return m_State0.m_Pool.height; }
        internal int GetPoolDepth() { return m_State0.m_Pool.depth; }

        internal ProbeBrickBlendingPool(ProbeVolumeBlendingTextureMemoryBudget memoryBudget, ProbeVolumeSHBands shBands, bool probeOcclusion)
        {
            // Casting to other memory budget struct works cause it's casted to int in the end anyway
            m_MemoryBudget = (ProbeVolumeTextureMemoryBudget)memoryBudget;
            m_ShBands = shBands;
            m_ProbeOcclusion = probeOcclusion;
        }

        internal void AllocateResourcesIfNeeded()
        {
            if (isAllocated)
                return;

            m_State0 = new ProbeBrickPool(m_MemoryBudget, m_ShBands, allocateProbeOcclusionData: m_ProbeOcclusion);
            m_State1 = new ProbeBrickPool(m_MemoryBudget, m_ShBands, allocateProbeOcclusionData: m_ProbeOcclusion);

            int maxAvailablebrickCount = (GetPoolWidth()  / ProbeBrickPool.kChunkProbeCountPerDim)
                                       * (GetPoolHeight() / ProbeBrickPool.kBrickProbeCountPerDim)
                                       * (GetPoolDepth()  / ProbeBrickPool.kBrickProbeCountPerDim);

            m_ChunkList = new Vector4[maxAvailablebrickCount];
            m_MappedChunks = 0;
        }

        internal void Update(ProbeBrickPool.DataLocation source, List<ProbeBrickPool.BrickChunkAlloc> srcLocations, List<ProbeBrickPool.BrickChunkAlloc> dstLocations, int destStartIndex, ProbeVolumeSHBands bands, int state)
        {
            (state == 0 ? m_State0 : m_State1).Update(source, srcLocations, dstLocations, destStartIndex, bands);
        }

        internal void Update(CommandBuffer cmd, CellStreamingScratchBuffer dataBuffer, CellStreamingScratchBufferLayout layout,
            List<ProbeBrickPool.BrickChunkAlloc> dstLocations, ProbeVolumeSHBands bands, int state, Texture validityTexture,
            bool skyOcclusion, Texture skyOcclusionTexture, bool skyShadingDirections, Texture skyShadingDirectionsTexture, bool probeOcclusion)
        {
            bool updateShared = state == 0 ? true : false;

            (state == 0 ? m_State0 : m_State1).Update(cmd, dataBuffer, layout, dstLocations,
                updateShared, validityTexture, bands, updateShared && skyOcclusion, skyOcclusionTexture,
                updateShared && skyShadingDirections, skyShadingDirectionsTexture, probeOcclusion);
        }

        internal void PerformBlending(CommandBuffer cmd, float factor, ProbeBrickPool dstPool)
        {
            if (m_MappedChunks == 0)
                return;

            cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, _State0_L0_L1Rx, m_State0.m_Pool.TexL0_L1rx);
            cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, _State0_L1G_L1Ry, m_State0.m_Pool.TexL1_G_ry);
            cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, _State0_L1B_L1Rz, m_State0.m_Pool.TexL1_B_rz);

            cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, _State1_L0_L1Rx, m_State1.m_Pool.TexL0_L1rx);
            cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, _State1_L1G_L1Ry, m_State1.m_Pool.TexL1_G_ry);
            cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, _State1_L1B_L1Rz, m_State1.m_Pool.TexL1_B_rz);

            cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, ProbeBrickPool._Out_L0_L1Rx, dstPool.m_Pool.TexL0_L1rx);
            cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, ProbeBrickPool._Out_L1G_L1Ry, dstPool.m_Pool.TexL1_G_ry);
            cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, ProbeBrickPool._Out_L1B_L1Rz, dstPool.m_Pool.TexL1_B_rz);

            if (m_ShBands == ProbeVolumeSHBands.SphericalHarmonicsL2)
            {
                stateBlendShader.EnableKeyword("PROBE_VOLUMES_L2");

                cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, _State0_L2_0, m_State0.m_Pool.TexL2_0);
                cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, _State0_L2_1, m_State0.m_Pool.TexL2_1);
                cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, _State0_L2_2, m_State0.m_Pool.TexL2_2);
                cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, _State0_L2_3, m_State0.m_Pool.TexL2_3);

                cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, _State1_L2_0, m_State1.m_Pool.TexL2_0);
                cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, _State1_L2_1, m_State1.m_Pool.TexL2_1);
                cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, _State1_L2_2, m_State1.m_Pool.TexL2_2);
                cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, _State1_L2_3, m_State1.m_Pool.TexL2_3);

                cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, ProbeBrickPool._Out_L2_0, dstPool.m_Pool.TexL2_0);
                cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, ProbeBrickPool._Out_L2_1, dstPool.m_Pool.TexL2_1);
                cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, ProbeBrickPool._Out_L2_2, dstPool.m_Pool.TexL2_2);
                cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, ProbeBrickPool._Out_L2_3, dstPool.m_Pool.TexL2_3);
            }
            else
                stateBlendShader.DisableKeyword("PROBE_VOLUMES_L2");

            if (m_ProbeOcclusion)
            {
                stateBlendShader.EnableKeyword("USE_APV_PROBE_OCCLUSION");
                cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, _State0_ProbeOcclusion, m_State0.m_Pool.TexProbeOcclusion);
                cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, _State1_ProbeOcclusion, m_State1.m_Pool.TexProbeOcclusion);
                cmd.SetComputeTextureParam(stateBlendShader, scenarioBlendingKernel, ProbeBrickPool._Out_ProbeOcclusion, dstPool.m_Pool.TexProbeOcclusion);
            }
            else
                stateBlendShader.DisableKeyword("USE_APV_PROBE_OCCLUSION");

            var poolDim_LerpFactor = new Vector4(dstPool.GetPoolWidth(), dstPool.GetPoolHeight(), factor, 0.0f);

            const int numthreads = 4;
            int threadX = ProbeBrickPool.DivRoundUp(ProbeBrickPool.kChunkProbeCountPerDim, numthreads);
            int threadY = ProbeBrickPool.DivRoundUp(ProbeBrickPool.kBrickProbeCountPerDim, numthreads);
            int threadZ = ProbeBrickPool.DivRoundUp(ProbeBrickPool.kBrickProbeCountPerDim, numthreads);

            cmd.SetComputeVectorArrayParam(stateBlendShader, _ChunkList, m_ChunkList);
            cmd.SetComputeVectorParam(stateBlendShader, _PoolDim_LerpFactor, poolDim_LerpFactor);
            cmd.DispatchCompute(stateBlendShader, scenarioBlendingKernel, threadX, threadY, threadZ * m_MappedChunks);
            m_MappedChunks = 0;
        }

        internal void BlendChunks(Cell cell, ProbeBrickPool dstPool)
        {
            for (int c = 0; c < cell.blendingInfo.chunkList.Count; c++)
            {
                var chunk = cell.blendingInfo.chunkList[c];
                int dst = cell.poolInfo.chunkList[c].flattenIndex(dstPool.GetPoolWidth(), dstPool.GetPoolHeight());

                m_ChunkList[m_MappedChunks++] = new Vector4(chunk.x, chunk.y, chunk.z, dst);
            }
        }

        internal void Clear()
            => m_State0?.Clear();

        internal bool Allocate(int numberOfBrickChunks, List<ProbeBrickPool.BrickChunkAlloc> outAllocations)
        {
            AllocateResourcesIfNeeded();
            if (numberOfBrickChunks > m_State0.GetRemainingChunkCount())
                return false;

            return m_State0.Allocate(numberOfBrickChunks, outAllocations, false);
        }

        internal void Deallocate(List<ProbeBrickPool.BrickChunkAlloc> allocations)
        {
            if (allocations.Count == 0)
                return;

            m_State0.Deallocate(allocations);
        }

        internal void EnsureTextureValidity()
        {
            if (isAllocated)
            {
                m_State0.EnsureTextureValidity();
                m_State1.EnsureTextureValidity();
            }
        }

        internal void Cleanup()
        {
            if (isAllocated)
            {
                m_State0.Cleanup();
                m_State1.Cleanup();
            }
        }
    }
}

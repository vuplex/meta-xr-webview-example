using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Oculus.Avatar;
using System.Collections;

public class OvrAvatarMaterialManager : MonoBehaviour
{
    // Set up in the Prefab, and meant to be indexed by LOD
    public Texture2D[] DiffuseFallbacks;
    public Texture2D[] NormalFallbacks;

    private Renderer TargetRenderer;
    private AvatarTextureArrayProperties[] TextureArrays;

    private OvrAvatarTextureCopyManager TextureCopyManager;

    public enum TextureType
    {
        DiffuseTextures = 0,
        NormalMaps,
        RoughnessMaps,

        Count
    }
    // Material properties required to render a single component
    [System.Serializable]
    public struct AvatarComponentMaterialProperties
    {
        public ovrAvatarBodyPartType TypeIndex;
        public Color Color;
        public Texture2D[] Textures;

        [Range(0, 1)] public float DiffuseIntensity;
        [Range(0, 10)] public float RimIntensity;
        [Range(0, 1)] public float ReflectionIntensity;
    }

    // Texture arrays
    [System.Serializable]
    public struct AvatarTextureArrayProperties
    {
        public Texture2D[] Textures;
        public Texture2DArray TextureArray;
    }

    // Material property arrays that are pushed to the shader
    [System.Serializable]
    public struct AvatarMaterialPropertyBlock
    {
        public Vector4[] Colors;
        public float[] DiffuseIntensities;
        public float[] RimIntensities;
        public float[] ReflectionIntensities;
    }

    private readonly string[] TextureTypeToShaderProperties = 
    {
        "_MainTex",       // TextureType.DiffuseTextures = 0
        "_NormalMap",     // TextureType.NormalMaps
        "_RoughnessMap"   // TextureType.RoughnessMaps
    };

    public List<ReflectionProbeBlendInfo> ReflectionProbes = new List<ReflectionProbeBlendInfo>();

    // Container class for all the data relating to an avatar material description
    [System.Serializable]
    public class AvatarMaterialConfig
    {
        public AvatarComponentMaterialProperties[] ComponentMaterialProperties;
        public AvatarMaterialPropertyBlock MaterialPropertyBlock;
    }

    // Local config that this manager instance will render
    public AvatarMaterialConfig LocalAvatarConfig;
    // Default avatar config used to initially populate the locally managed avatar config
    public AvatarMaterialConfig DefaultAvatarConfig;

    // Property block for pushing to shader
    private AvatarMaterialPropertyBlock LocalAvatarMaterialPropertyBlock;

    // cache the previous shader when swapping in the loading shader.
    private Shader CombinedShader; 
    // Shader properties
    public static string AVATAR_SHADER_LOADER = "OvrAvatar/Avatar_Mobile_Loader";
    public static string AVATAR_SHADER_MAINTEX = "_MainTex";
    public static string AVATAR_SHADER_NORMALMAP = "_NormalMap";
    public static string AVATAR_SHADER_ROUGHNESSMAP = "_RoughnessMap";
    public static string AVATAR_SHADER_COLOR = "_BaseColor";
    public static string AVATAR_SHADER_DIFFUSEINTENSITY = "_DiffuseIntensity";
    public static string AVATAR_SHADER_RIMINTENSITY = "_RimIntensity";
    public static string AVATAR_SHADER_REFLECTIONINTENSITY = "_ReflectionIntensity";
    public static string AVATAR_SHADER_CUBEMAP = "_Cubemap";
    public static string AVATAR_SHADER_ALPHA = "_Alpha";
    public static string AVATAR_SHADER_LOADING_DIMMER = "_LoadingDimmer";

    public static string AVATAR_SHADER_IRIS_COLOR = "_MaskColorIris";
    public static string AVATAR_SHADER_LIP_COLOR = "_MaskColorLips";
    public static string AVATAR_SHADER_BROW_COLOR = "_MaskColorBrows";
    public static string AVATAR_SHADER_LASH_COLOR = "_MaskColorLashes";
    public static string AVATAR_SHADER_SCLERA_COLOR = "_MaskColorSclera";
    public static string AVATAR_SHADER_GUM_COLOR = "_MaskColorGums";
    public static string AVATAR_SHADER_TEETH_COLOR = "_MaskColorTeeth";
    public static string AVATAR_SHADER_LIP_SMOOTHNESS = "_LipSmoothness";

    // Loading animation
    private const float LOADING_ANIMATION_AMPLITUDE = 0.5f;
    private const float LOADING_ANIMATION_PERIOD = 0.35f;
    private const float LOADING_ANIMATION_CURVE_SCALE = 0.25f;
    private const float LOADING_ANIMATION_DIMMER_MIN = 0.3f;

    void Awake()
    {
        TextureCopyManager = gameObject.AddComponent<OvrAvatarTextureCopyManager>();
    }

    public void CreateTextureArrays()
    {
        const int componentCount = (int)ovrAvatarBodyPartType.Count;
        const int textureTypeCount = (int)TextureType.Count;

        for (int index = 0; index < componentCount; index++)
        {
            LocalAvatarConfig.ComponentMaterialProperties[index].Textures = new Texture2D[textureTypeCount];
            DefaultAvatarConfig.ComponentMaterialProperties[index].Textures = new Texture2D[textureTypeCount];
        }

        TextureArrays = new AvatarTextureArrayProperties[textureTypeCount];
    }

    public void SetRenderer(Renderer renderer)
    {
        TargetRenderer = renderer;
        TargetRenderer.GetClosestReflectionProbes(ReflectionProbes);
    }

    public void OnCombinedMeshReady()
    {
        InitTextureArrays();
        SetMaterialPropertyBlock();
        StartCoroutine(RunLoadingAnimation());
    }

    // Prepare texture arrays and copy to GPU
    public void InitTextureArrays()
    {
        var localProps = LocalAvatarConfig.ComponentMaterialProperties[0];

        for (int i = 0; i < TextureArrays.Length && i < localProps.Textures.Length; i++)
        {
            TextureArrays[i].TextureArray = new Texture2DArray(
                localProps.Textures[0].height, localProps.Textures[0].width,
                LocalAvatarConfig.ComponentMaterialProperties.Length,
                 localProps.Textures[0].format,
                true,
                QualitySettings.activeColorSpace == ColorSpace.Gamma ? false : true
            ) { filterMode = FilterMode.Trilinear };

            TextureArrays[i].Textures 
                = new Texture2D[LocalAvatarConfig.ComponentMaterialProperties.Length];

            for (int j = 0; j < LocalAvatarConfig.ComponentMaterialProperties.Length; j++)
            {
                TextureArrays[i].Textures[j] 
                    = LocalAvatarConfig.ComponentMaterialProperties[j].Textures[i];
            }

            ProcessTexturesWithMips(
                TextureArrays[i].Textures,
                localProps.Textures[i].height,
                TextureArrays[i].TextureArray);
        }
    }

    private void ProcessTexturesWithMips(
        Texture2D[] textures,
        int texArrayResolution,
        Texture2DArray texArray)
    {
        for (int i = 0; i < textures.Length; i++)
        {
            int currentMipSize = texArrayResolution;
            int correctNumberOfMips = textures[i].mipmapCount - 1;

            // Add mips to copyTexture queue in low-high order from correctNumberOfMips..0
            for (int mipLevel = correctNumberOfMips; mipLevel >= 0; mipLevel--)
            {
                int mipSize = texArrayResolution / currentMipSize;
                TextureCopyManager.CopyTexture(
                    textures[i], 
                    texArray, 
                    mipLevel, 
                    mipSize, 
                    i, 
                    true);

                currentMipSize /= 2;
            }
        }
    }

    private void SetMaterialPropertyBlock()
    {
        if (TargetRenderer != null)
        {
            for (int i = 0; i < LocalAvatarConfig.ComponentMaterialProperties.Length; i++)
            {
                LocalAvatarConfig.MaterialPropertyBlock.Colors[i] 
                    = LocalAvatarConfig.ComponentMaterialProperties[i].Color;
                LocalAvatarConfig.MaterialPropertyBlock.DiffuseIntensities[i] 
                    = LocalAvatarConfig.ComponentMaterialProperties[i].DiffuseIntensity;
                LocalAvatarConfig.MaterialPropertyBlock.RimIntensities[i] 
                    = LocalAvatarConfig.ComponentMaterialProperties[i].RimIntensity;
                LocalAvatarConfig.MaterialPropertyBlock.ReflectionIntensities[i] 
                    = LocalAvatarConfig.ComponentMaterialProperties[i].ReflectionIntensity;
            }
        }
    }

    private void ApplyMaterialPropertyBlock()
    {
        MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
        materialPropertyBlock.SetVectorArray(AVATAR_SHADER_COLOR, 
            LocalAvatarConfig.MaterialPropertyBlock.Colors);
        materialPropertyBlock.SetFloatArray(AVATAR_SHADER_DIFFUSEINTENSITY, 
            LocalAvatarConfig.MaterialPropertyBlock.DiffuseIntensities);
        materialPropertyBlock.SetFloatArray(AVATAR_SHADER_RIMINTENSITY, 
            LocalAvatarConfig.MaterialPropertyBlock.RimIntensities);
        materialPropertyBlock.SetFloatArray(AVATAR_SHADER_REFLECTIONINTENSITY, 
            LocalAvatarConfig.MaterialPropertyBlock.ReflectionIntensities);
        TargetRenderer.GetClosestReflectionProbes(ReflectionProbes);

        if (ReflectionProbes != null && ReflectionProbes.Count > 0 && ReflectionProbes[0].probe.texture != null)
        {
            materialPropertyBlock.SetTexture(AVATAR_SHADER_CUBEMAP, ReflectionProbes[0].probe.texture);
        }

        for (int i = 0; i < TextureArrays.Length; i++)
        {
            materialPropertyBlock.SetTexture(TextureTypeToShaderProperties[i],
                TextureArrays[(int)(TextureType)i].TextureArray);
        }

        TargetRenderer.SetPropertyBlock(materialPropertyBlock);
    }

    // Return a component type based on name
    public static ovrAvatarBodyPartType GetComponentType(string objectName)
    {
        if (objectName.Contains("0"))
        {
            return ovrAvatarBodyPartType.Body;
        }
        else if (objectName.Contains("1"))
        {
            return ovrAvatarBodyPartType.Clothing;
        }
        else if (objectName.Contains("2"))
        {
            return ovrAvatarBodyPartType.Eyewear;
        }
        else if (objectName.Contains("3"))
        {
            return ovrAvatarBodyPartType.Hair;
        }
        else if (objectName.Contains("4"))
        {
            return ovrAvatarBodyPartType.Beard;
        }

        return ovrAvatarBodyPartType.Count;
    }

    public void ValidateTextures()
    {
        var props = LocalAvatarConfig.ComponentMaterialProperties;

        int[] heights = new int[(int)TextureType.Count];
        TextureFormat[] formats = new TextureFormat[(int)TextureType.Count];

        for (var propIndex = 0; propIndex < props.Length; propIndex++)
        {
            for (var index = 0; index < props[propIndex].Textures.Length; index++)
            {
                if (props[propIndex].Textures[index] == null)
                {
                    throw new System.Exception(
                        props[propIndex].TypeIndex.ToString() 
                        + "Invalid: " 
                        + ((TextureType)index).ToString());
                }

                heights[index] = props[propIndex].Textures[index].height;
                formats[index] = props[propIndex].Textures[index].format;
            }
        }

        for (int textureIndex = 0; textureIndex < (int)TextureType.Count; textureIndex++)
        {
            for (var propIndex = 1; propIndex < props.Length; propIndex++)
            {
                if (props[propIndex - 1].Textures[textureIndex].height 
                    != props[propIndex].Textures[textureIndex].height)
                {
                    throw new System.Exception(
                        props[propIndex].TypeIndex.ToString() 
                        + " Mismatching Resolutions: " 
                        + ((TextureType)textureIndex).ToString()
                        + " "
                        + props[propIndex - 1].Textures[textureIndex].height 
                        + " vs " 
                        + props[propIndex].Textures[textureIndex].height
                        + " Ensure you are using ASTC texture compression on Android or turn off CombineMeshes");
                }

                if (props[propIndex - 1].Textures[textureIndex].format
                    != props[propIndex].Textures[textureIndex].format)
                {
                    throw new System.Exception(
                        props[propIndex].TypeIndex.ToString()
                        + " Mismatching Formats: "
                        + ((TextureType)textureIndex).ToString()
                        + " Ensure you are using ASTC texture compression on Android or turn off CombineMeshes");
                }
            }
        }
    }

    // Loading animation on the Dimmer properyt
    // Smooth sine lerp every 0.3 seconds between 0.25 and 0.5
    private IEnumerator RunLoadingAnimation()
    {
        // Set the material to single component while the avatar loads
        CombinedShader = TargetRenderer.sharedMaterial.shader;

        // Save shader properties
        int srcBlend = TargetRenderer.sharedMaterial.GetInt("_SrcBlend");
        int dstBlend = TargetRenderer.sharedMaterial.GetInt("_DstBlend");
        bool transparentQueue = TargetRenderer.sharedMaterial.IsKeywordEnabled("_ALPHATEST_ON");
        int renderQueue = TargetRenderer.sharedMaterial.renderQueue;
        string renderTag = TargetRenderer.sharedMaterial.GetTag("RenderType", false);

        // Swap in loading shader
        TargetRenderer.sharedMaterial.shader = Shader.Find(AVATAR_SHADER_LOADER);
        TargetRenderer.sharedMaterial.SetColor(AVATAR_SHADER_COLOR, Color.white);

        while (TextureCopyManager.GetTextureCount() > 0)
        {
            float distance = (LOADING_ANIMATION_AMPLITUDE * Mathf.Sin(Time.timeSinceLevelLoad / LOADING_ANIMATION_PERIOD) +
                LOADING_ANIMATION_AMPLITUDE) * (LOADING_ANIMATION_CURVE_SCALE) + LOADING_ANIMATION_DIMMER_MIN;
            TargetRenderer.sharedMaterial.SetFloat(AVATAR_SHADER_LOADING_DIMMER, distance);
            yield return null;
        }
        // Swap back main shader
        TargetRenderer.sharedMaterial.SetFloat(AVATAR_SHADER_LOADING_DIMMER, 1f);
        TargetRenderer.sharedMaterial.shader = CombinedShader;
        
        // Restore shader properties
        TargetRenderer.sharedMaterial.SetOverrideTag("RenderType", renderTag);
        TargetRenderer.sharedMaterial.SetInt("_SrcBlend", srcBlend);
        TargetRenderer.sharedMaterial.SetInt("_DstBlend", dstBlend);
        if (transparentQueue)
        {
            TargetRenderer.sharedMaterial.EnableKeyword("_ALPHATEST_ON");
            TargetRenderer.sharedMaterial.EnableKeyword("_ALPHABLEND_ON");
            TargetRenderer.sharedMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        else
        {
            TargetRenderer.sharedMaterial.DisableKeyword("_ALPHATEST_ON");
            TargetRenderer.sharedMaterial.DisableKeyword("_ALPHABLEND_ON");
            TargetRenderer.sharedMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        TargetRenderer.sharedMaterial.renderQueue = renderQueue;

        ApplyMaterialPropertyBlock();
    }
}

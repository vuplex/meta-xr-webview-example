using UnityEngine;
using System.Collections;
using System;
using Oculus.Avatar;

public class OvrAvatarSkinnedMeshPBSV2RenderComponent : OvrAvatarRenderComponent
{
    public OvrAvatarMaterialManager AvatarMaterialManager;
    bool PreviouslyActive = false;
    bool IsCombinedMaterial = false;

    internal void Initialize(
        IntPtr renderPart,
        ovrAvatarRenderPart_SkinnedMeshRenderPBS_V2 skinnedMeshRender,
        OvrAvatarMaterialManager materialManager,
        int thirdPersonLayer, 
        int firstPersonLayer, 
        int sortOrder,
        bool isCombinedMaterial,
        ovrAvatarAssetLevelOfDetail lod)
    {
        AvatarMaterialManager = materialManager;
        IsCombinedMaterial = isCombinedMaterial;

        mesh = CreateSkinnedMesh(
            skinnedMeshRender.meshAssetID, 
            skinnedMeshRender.visibilityMask, 
            thirdPersonLayer,
            firstPersonLayer, 
            sortOrder);

#if UNITY_ANDROID
        var singleComponentShader = "OvrAvatar/Avatar_Mobile_SingleComponent";
#else
        var singleComponentShader = "OvrAvatar/Avatar_PC_SingleComponent";
#endif

        var shader = IsCombinedMaterial
             ? Shader.Find("OvrAvatar/Avatar_Mobile_CombinedMesh")
             : Shader.Find(singleComponentShader);

       AvatarLogger.Log("Shader is: " + shader.name);

        mesh.sharedMaterial = CreateAvatarMaterial(gameObject.name + "_material", shader);
        mesh.sharedMaterial.renderQueue = OvrAvatarMaterialManager.RENDER_QUEUE;

        bones = mesh.bones;

        if (IsCombinedMaterial)
        {
            AvatarMaterialManager.SetRenderer(mesh);
            InitializeCombinedMaterial(renderPart, (int)lod - 1);
            AvatarMaterialManager.OnCombinedMeshReady();
        }
    }

    public void UpdateSkinnedMeshRender(
        OvrAvatarComponent component, 
        OvrAvatar avatar, 
        IntPtr renderPart)
    {
        ovrAvatarVisibilityFlags visibilityMask 
            = CAPI.ovrAvatarSkinnedMeshRenderPBSV2_GetVisibilityMask(renderPart);

        ovrAvatarTransform localTransform 
            = CAPI.ovrAvatarSkinnedMeshRenderPBSV2_GetTransform(renderPart);

        UpdateSkinnedMesh(avatar, bones, localTransform, visibilityMask, renderPart);

        bool isActive = gameObject.activeSelf;

        if (mesh != null && !PreviouslyActive && isActive)
        {
            if (!IsCombinedMaterial)
            {
                InitializeSingleComponentMaterial(renderPart, (int)avatar.LevelOfDetail - 1);
            }
        }

        PreviouslyActive = isActive;
    }

    private void InitializeSingleComponentMaterial(IntPtr renderPart, int lodIndex)
    {
        ovrAvatarPBSMaterialState materialState =
            CAPI.ovrAvatarSkinnedMeshRenderPBSV2_GetPBSMaterialState(renderPart);

        int componentType = (int)OvrAvatarMaterialManager.GetComponentType(gameObject.name);

        var defaultProperties = AvatarMaterialManager.DefaultAvatarConfig.ComponentMaterialProperties;

        var diffuseTexture = OvrAvatarComponent.GetLoadedTexture(materialState.albedoTextureID);
        var normalTexture = OvrAvatarComponent.GetLoadedTexture(materialState.normalTextureID);
        var metallicTexture = OvrAvatarComponent.GetLoadedTexture(materialState.metallicnessTextureID);

        if (diffuseTexture == null)
        {
            diffuseTexture = AvatarMaterialManager.DiffuseFallbacks[lodIndex];
        }
            
        if (normalTexture == null)
        {
            normalTexture = AvatarMaterialManager.NormalFallbacks[lodIndex];
        }

        if (metallicTexture == null)
        {
            metallicTexture = AvatarMaterialManager.DiffuseFallbacks[lodIndex];
        }

        mesh.sharedMaterial.SetTexture(OvrAvatarMaterialManager.AVATAR_SHADER_MAINTEX, diffuseTexture);
        mesh.sharedMaterial.SetTexture(OvrAvatarMaterialManager.AVATAR_SHADER_NORMALMAP, normalTexture);
        mesh.sharedMaterial.SetTexture(OvrAvatarMaterialManager.AVATAR_SHADER_ROUGHNESSMAP, metallicTexture);

        mesh.sharedMaterial.SetVector(OvrAvatarMaterialManager.AVATAR_SHADER_COLOR, 
            materialState.albedoMultiplier);

        mesh.sharedMaterial.SetFloat(OvrAvatarMaterialManager.AVATAR_SHADER_DIFFUSEINTENSITY,
            defaultProperties[componentType].DiffuseIntensity);

        mesh.sharedMaterial.SetFloat(OvrAvatarMaterialManager.AVATAR_SHADER_RIMINTENSITY,
            defaultProperties[componentType].RimIntensity);

        mesh.sharedMaterial.SetFloat(OvrAvatarMaterialManager.AVATAR_SHADER_BACKLIGHTINTENSITY,
            defaultProperties[componentType].BacklightIntensity);

        mesh.sharedMaterial.SetFloat(OvrAvatarMaterialManager.AVATAR_SHADER_REFLECTIONINTENSITY,
            defaultProperties[componentType].ReflectionIntensity);

        mesh.GetClosestReflectionProbes(AvatarMaterialManager.ReflectionProbes);
        if (AvatarMaterialManager.ReflectionProbes != null &&
            AvatarMaterialManager.ReflectionProbes.Count > 0)
        {
            mesh.sharedMaterial.SetTexture(OvrAvatarMaterialManager.AVATAR_SHADER_CUBEMAP,
                AvatarMaterialManager.ReflectionProbes[0].probe.texture);
        }

#if UNITY_EDITOR
        mesh.sharedMaterial.EnableKeyword("FIX_NORMAL_ON");
#endif
        mesh.sharedMaterial.EnableKeyword("PBR_LIGHTING_ON");
    }

    private void InitializeCombinedMaterial(IntPtr renderPart, int lodIndex)
    {
        ovrAvatarPBSMaterialState[] materialStates = CAPI.ovrAvatar_GetBodyPBSMaterialStates(renderPart);

        if (materialStates.Length == (int)ovrAvatarBodyPartType.Count)
        {
            AvatarMaterialManager.CreateTextureArrays();

            AvatarMaterialManager.LocalAvatarConfig = AvatarMaterialManager.DefaultAvatarConfig;
            var localProperties = AvatarMaterialManager.LocalAvatarConfig.ComponentMaterialProperties;

            AvatarLogger.Log("InitializeCombinedMaterial - Loading Material States");

            for (int i = 0; i < materialStates.Length; i++)
            {
                localProperties[i].TypeIndex = (ovrAvatarBodyPartType)i;
                localProperties[i].Color = materialStates[i].albedoMultiplier;

                var diffuse = OvrAvatarComponent.GetLoadedTexture(materialStates[i].albedoTextureID);
                var normal = OvrAvatarComponent.GetLoadedTexture(materialStates[i].normalTextureID);
                var roughness = OvrAvatarComponent.GetLoadedTexture(materialStates[i].metallicnessTextureID);

                localProperties[i].Textures[(int)OvrAvatarMaterialManager.TextureType.DiffuseTextures]
                    = diffuse == null ? AvatarMaterialManager.DiffuseFallbacks[lodIndex] : diffuse;

                localProperties[i].Textures[(int)OvrAvatarMaterialManager.TextureType.NormalMaps]
                    = normal == null ? AvatarMaterialManager.NormalFallbacks[lodIndex] : normal;

                localProperties[i].Textures[(int)OvrAvatarMaterialManager.TextureType.RoughnessMaps]
                    = roughness == null ? AvatarMaterialManager.DiffuseFallbacks[lodIndex] : roughness;

                AvatarLogger.Log(localProperties[i].TypeIndex.ToString());
                AvatarLogger.Log(AvatarLogger.Tab + "Diffuse: " + materialStates[i].albedoTextureID);
                AvatarLogger.Log(AvatarLogger.Tab + "Normal: " + materialStates[i].normalTextureID);
                AvatarLogger.Log(AvatarLogger.Tab + "Metallic: " + materialStates[i].metallicnessTextureID);
            }

            AvatarMaterialManager.ValidateTextures();
        }

#if UNITY_EDITOR
        mesh.sharedMaterial.EnableKeyword("FIX_NORMAL_ON");
#endif       
    }
}

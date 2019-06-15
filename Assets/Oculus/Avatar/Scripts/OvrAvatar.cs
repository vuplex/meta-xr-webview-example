using UnityEngine;
using System.Collections;
using System;
using Oculus.Avatar;
using System.Runtime.InteropServices;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if AVATAR_INTERNAL
using UnityEngine.Events;
#endif

[System.Serializable]
public class AvatarLayer
{
    public int layerIndex;
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(AvatarLayer))]
public class AvatarLayerPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, GUIContent.none, property);
        SerializedProperty layerIndex = property.FindPropertyRelative("layerIndex");
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
        layerIndex.intValue = EditorGUI.LayerField(position, layerIndex.intValue);
        EditorGUI.EndProperty();
    }
}
#endif

[System.Serializable]
public class PacketRecordSettings
{
    internal bool RecordingFrames = false;
    public float UpdateRate = 1f / 30f; // 30 hz update of packets
    internal float AccumulatedTime;
};

public class OvrAvatar : MonoBehaviour
{
    public OvrAvatarMaterialManager DefaultBodyMaterialManager;
    public OvrAvatarMaterialManager DefaultHandMaterialManager;

    public OvrAvatarDriver Driver;

    internal OvrAvatarBase Base = null;
    internal OvrAvatarTouchController ControllerLeft = null;
    internal OvrAvatarTouchController ControllerRight = null;
    internal OvrAvatarBody Body = null;
    internal OvrAvatarHand HandLeft = null;
    internal OvrAvatarHand HandRight = null;

    [Header("Oculus User ID")]
    public string oculusUserID;
    internal UInt64 oculusUserIDInternal;

    [Header("Capabilities")]
    public bool EnableBody = true;
    public bool EnableHands = true;
    public bool EnableBase = true;
    public bool EnableExpressive = false;

    [Header("Network")]
    public bool RecordPackets;
    public bool UseSDKPackets = true;
    public PacketRecordSettings PacketSettings = new PacketRecordSettings();

    [Header("Other")]
    public bool StartWithControllers;
    public AvatarLayer FirstPersonLayer;
    public AvatarLayer ThirdPersonLayer;
    public bool ShowFirstPerson = true;
    public bool ShowThirdPerson;
    public bool CanOwnMicrophone = true;
    internal ovrAvatarCapabilities Capabilities = ovrAvatarCapabilities.Body;

    [Header("Shaders")]
    [Tooltip("Enable this to use transparent queue, disable to use for geometry queue. Requires restart to take effect.")]
    public bool UseTransparentRenderQueue = true;
    public Shader Monochrome_SurfaceShader;
    public Shader Monochrome_SurfaceShader_SelfOccluding;
    public Shader Monochrome_SurfaceShader_PBS;
    public Shader Skinshaded_SurfaceShader_SingleComponent;
    public Shader Skinshaded_VertFrag_SingleComponent;
    public Shader Skinshaded_VertFrag_CombinedMesh;
    public Shader Skinshaded_Expressive_SurfaceShader_SingleComponent;
    public Shader Skinshaded_Expressive_VertFrag_SingleComponent;
    public Shader Skinshaded_Expressive_VertFrag_CombinedMesh;
    public Shader Loader_VertFrag_CombinedMesh;
    public Shader EyeLens;

 #if AVATAR_INTERNAL
    public AvatarControllerBlend BlendController;
    public UnityEvent AssetsDoneLoading = new UnityEvent();
#endif

    private static readonly Vector3 MOUTH_HEAD_OFFSET = new Vector3(0, -0.085f, 0.09f);
    private const string MOUTH_HELPER_NAME = "MouthAnchor";

    private OVRLipSyncMicInput micInput = null;
    private OVRLipSyncContext lipsyncContext = null;
    private OVRLipSync.Frame currentFrame = new OVRLipSync.Frame();
    private float[] visemes = new float[15];
    private AudioSource audioSource;
    private ONSPAudioSource spatializedSource;

    int renderPartCount = 0;
    bool showLeftController;
    bool showRightController;
    List<float[]> voiceUpdates = new List<float[]>();

#if UNITY_ANDROID && UNITY_5_5_OR_NEWER
    #if !UNITY_EDITOR || QA_CONFIGURATION
        bool CombineMeshes = true;
    #else
        bool CombineMeshes = false;
    #endif
#else
    bool CombineMeshes = false;
#endif

#if UNITY_EDITOR && UNITY_ANDROID
    bool ForceMobileTextureFormat = true;
#else
    bool ForceMobileTextureFormat = false;

#endif

    private bool WaitingForCombinedMesh = false;

    public IntPtr sdkAvatar = IntPtr.Zero;
    private HashSet<UInt64> assetLoadingIds = new HashSet<UInt64>();
    private Dictionary<string, OvrAvatarComponent> trackedComponents =
        new Dictionary<string, OvrAvatarComponent>();

    bool assetsFinishedLoading = false;

    static bool doneExpressiveGlobalInit = false;

    [Header("Misc")]
    public GameObject MouthAnchor;
    public Transform LeftHandCustomPose;
    public Transform RightHandCustomPose;
    Transform cachedLeftHandCustomPose;
    Transform[] cachedCustomLeftHandJoints;
    ovrAvatarTransform[] cachedLeftHandTransforms;
    Transform cachedRightHandCustomPose;
    Transform[] cachedCustomRightHandJoints;
    ovrAvatarTransform[] cachedRightHandTransforms;


    private Vector4 clothingAlphaOffset = new Vector4(0f, 0f, 0f, 1f);
    private UInt64 clothingAlphaTexture = 0;

    public class PacketEventArgs : EventArgs
    {
        public readonly OvrAvatarPacket Packet;
        public PacketEventArgs(OvrAvatarPacket packet)
        {
            Packet = packet;
        }
    }

    OvrAvatarPacket CurrentUnityPacket;

    public enum HandType
    {
        Right,
        Left,

        Max
    };

    public enum HandJoint
    {
        HandBase,
        IndexBase,
        IndexTip,
        ThumbBase,
        ThumbTip,

        Max,
    }

    private static string[,] HandJoints = new string[(int)HandType.Max, (int)HandJoint.Max]
    {
        {
            "hands:r_hand_world",
            "hands:r_hand_world/hands:b_r_hand/hands:b_r_index1",
            "hands:r_hand_world/hands:b_r_hand/hands:b_r_index1/hands:b_r_index2/hands:b_r_index3/hands:b_r_index_ignore",
            "hands:r_hand_world/hands:b_r_hand/hands:b_r_thumb1/hands:b_r_thumb2",
            "hands:r_hand_world/hands:b_r_hand/hands:b_r_thumb1/hands:b_r_thumb2/hands:b_r_thumb3/hands:b_r_thumb_ignore"
        },
        {
            "hands:l_hand_world",
            "hands:l_hand_world/hands:b_l_hand/hands:b_l_index1",
            "hands:l_hand_world/hands:b_l_hand/hands:b_l_index1/hands:b_l_index2/hands:b_l_index3/hands:b_l_index_ignore",
            "hands:l_hand_world/hands:b_l_hand/hands:b_l_thumb1/hands:b_l_thumb2",
            "hands:l_hand_world/hands:b_l_hand/hands:b_l_thumb1/hands:b_l_thumb2/hands:b_l_thumb3/hands:b_l_thumb_ignore"
        }
    };

#if UNITY_ANDROID
    internal ovrAvatarAssetLevelOfDetail LevelOfDetail = ovrAvatarAssetLevelOfDetail.Medium;
#else
    internal ovrAvatarAssetLevelOfDetail LevelOfDetail = ovrAvatarAssetLevelOfDetail.Highest;
#endif

#if QA_CONFIGURATION
    public ovrAvatarLookAndFeelVersion LookAndFeelVersion = ovrAvatarLookAndFeelVersion.Two;
    public ovrAvatarLookAndFeelVersion FallbackLookAndFeelVersion = ovrAvatarLookAndFeelVersion.One;
#else
    internal ovrAvatarLookAndFeelVersion LookAndFeelVersion = ovrAvatarLookAndFeelVersion.Two;
    internal ovrAvatarLookAndFeelVersion FallbackLookAndFeelVersion = ovrAvatarLookAndFeelVersion.One;
#endif

    private static ovrAvatarVisemes RuntimeVisemes;

    static OvrAvatar()
    {
        // This size has to match the 'MarshalAs' attribute in the ovrAvatarVisemes declaration.
        RuntimeVisemes.visemeParams = new float[32];
        RuntimeVisemes.visemeParamCount = 15;
    }

    void OnDestroy()
    {
        if (sdkAvatar != IntPtr.Zero)
        {
            CAPI.ovrAvatar_Destroy(sdkAvatar);
        }
    }

    public EventHandler<PacketEventArgs> PacketRecorded;

    public void AssetLoadedCallback(OvrAvatarAsset asset)
    {
        assetLoadingIds.Remove(asset.assetID);
    }

    public void CombinedMeshLoadedCallback(IntPtr assetPtr)
    {
        if (!WaitingForCombinedMesh)
        {
            return;
        }

        var meshIDs = CAPI.ovrAvatarAsset_GetCombinedMeshIDs(assetPtr);
        foreach (var id in meshIDs)
        {
            assetLoadingIds.Remove(id);
        }

        CAPI.ovrAvatar_GetCombinedMeshAlphaData(sdkAvatar, ref clothingAlphaTexture, ref clothingAlphaOffset);

        WaitingForCombinedMesh = false;
    }

    private void AddAvatarComponent(GameObject componentObject, ovrAvatarComponent component)
    {
        OvrAvatarComponent ovrComponent = componentObject.AddComponent<OvrAvatarComponent>();
        trackedComponents.Add(component.name, ovrComponent);

        if (ovrComponent.name == "body")
        {
            ovrComponent.ClothingAlphaOffset = clothingAlphaOffset;
            ovrComponent.ClothingAlphaTexture = clothingAlphaTexture;
        }

        AddRenderParts(ovrComponent, component, componentObject.transform);
    }

    private OvrAvatarSkinnedMeshRenderComponent AddSkinnedMeshRenderComponent(GameObject gameObject, ovrAvatarRenderPart_SkinnedMeshRender skinnedMeshRender)
    {
        OvrAvatarSkinnedMeshRenderComponent skinnedMeshRenderer = gameObject.AddComponent<OvrAvatarSkinnedMeshRenderComponent>();
        skinnedMeshRenderer.Initialize(skinnedMeshRender, Monochrome_SurfaceShader, Monochrome_SurfaceShader_SelfOccluding, ThirdPersonLayer.layerIndex, FirstPersonLayer.layerIndex, renderPartCount++);
        return skinnedMeshRenderer;
    }

    private OvrAvatarSkinnedMeshRenderPBSComponent AddSkinnedMeshRenderPBSComponent(GameObject gameObject, ovrAvatarRenderPart_SkinnedMeshRenderPBS skinnedMeshRenderPBS)
    {
        OvrAvatarSkinnedMeshRenderPBSComponent skinnedMeshRenderer = gameObject.AddComponent<OvrAvatarSkinnedMeshRenderPBSComponent>();
        skinnedMeshRenderer.Initialize(skinnedMeshRenderPBS, Monochrome_SurfaceShader_PBS, ThirdPersonLayer.layerIndex, FirstPersonLayer.layerIndex, renderPartCount++);
        return skinnedMeshRenderer;
    }

    private OvrAvatarSkinnedMeshPBSV2RenderComponent AddSkinnedMeshRenderPBSV2Component(
        IntPtr renderPart,
        GameObject go,
        ovrAvatarRenderPart_SkinnedMeshRenderPBS_V2 skinnedMeshRenderPBSV2,
        OvrAvatarMaterialManager materialManager,
        bool isBodyPartZero,
        bool isControllerModel)
    {
        OvrAvatarSkinnedMeshPBSV2RenderComponent skinnedMeshRenderer = go.AddComponent<OvrAvatarSkinnedMeshPBSV2RenderComponent>();
        skinnedMeshRenderer.Initialize(
            renderPart,
            skinnedMeshRenderPBSV2,
            materialManager,
            ThirdPersonLayer.layerIndex,
            FirstPersonLayer.layerIndex,
            renderPartCount++,
            isBodyPartZero && CombineMeshes,
            LevelOfDetail,
            isBodyPartZero && EnableExpressive,
            this,
            isControllerModel);

        return skinnedMeshRenderer;
    }

    private OvrAvatarProjectorRenderComponent AddProjectorRenderComponent(GameObject gameObject, ovrAvatarRenderPart_ProjectorRender projectorRender)
    {
        ovrAvatarComponent component = CAPI.ovrAvatarComponent_Get(sdkAvatar, projectorRender.componentIndex);
        OvrAvatarComponent ovrComponent;
        if (trackedComponents.TryGetValue(component.name, out ovrComponent))
        {
            if (projectorRender.renderPartIndex < ovrComponent.RenderParts.Count)
            {
                OvrAvatarRenderComponent targetRenderPart = ovrComponent.RenderParts[(int)projectorRender.renderPartIndex];
                OvrAvatarProjectorRenderComponent projectorComponent = gameObject.AddComponent<OvrAvatarProjectorRenderComponent>();
                projectorComponent.InitializeProjectorRender(projectorRender, Monochrome_SurfaceShader, targetRenderPart);
                return projectorComponent;
            }
        }
        return null;
    }

    static public IntPtr GetRenderPart(ovrAvatarComponent component, UInt32 renderPartIndex)
    {
        long offset = Marshal.SizeOf(typeof(IntPtr)) * renderPartIndex;
        IntPtr marshalPtr = new IntPtr(component.renderParts.ToInt64() + offset);
        return (IntPtr)Marshal.PtrToStructure(marshalPtr, typeof(IntPtr));
    }

    private void UpdateAvatarComponent(ovrAvatarComponent component)
    {
        OvrAvatarComponent ovrComponent;
        if (!trackedComponents.TryGetValue(component.name, out ovrComponent))
        {
            throw new Exception(string.Format("trackedComponents didn't have {0}", component.name));
        }

        ovrComponent.UpdateAvatar(component, this);
    }

    private static string GetRenderPartName(ovrAvatarComponent component, uint renderPartIndex)
    {
        return component.name + "_renderPart_" + (int)renderPartIndex;
    }

    internal static void ConvertTransform(ovrAvatarTransform transform, Transform target)
    {
        Vector3 position = transform.position;
        position.z = -position.z;
        Quaternion orientation = transform.orientation;
        orientation.x = -orientation.x;
        orientation.y = -orientation.y;
        target.localPosition = position;
        target.localRotation = orientation;
        target.localScale = transform.scale;
    }

    public static ovrAvatarTransform CreateOvrAvatarTransform(Vector3 position, Quaternion orientation)
    {
        return new ovrAvatarTransform
        {
            position = new Vector3(position.x, position.y, -position.z),
            orientation = new Quaternion(-orientation.x, -orientation.y, orientation.z, orientation.w),
            scale = Vector3.one
        };
    }

    private static ovrAvatarGazeTarget CreateOvrGazeTarget(uint targetId, Vector3 targetPosition, ovrAvatarGazeTargetType targetType)
    {
        return new ovrAvatarGazeTarget
        {
            id = targetId,
            // Do coordinate system switch.
            worldPosition = new Vector3(targetPosition.x, targetPosition.y, -targetPosition.z),
            type = targetType
        };
    }

    private void BuildRenderComponents()
    {
        var leftHand = CAPI.ovrAvatarPose_GetLeftHandComponent(sdkAvatar);
        var rightHand = CAPI.ovrAvatarPose_GetRightHandComponent(sdkAvatar);
        var body = CAPI.ovrAvatarPose_GetBodyComponent(sdkAvatar);
        var leftController = CAPI.ovrAvatarPose_GetLeftControllerComponent(sdkAvatar);
        var rightController = CAPI.ovrAvatarPose_GetRightControllerComponent(sdkAvatar);
        var baseComponent = CAPI.ovrAvatarPose_GetBaseComponent(sdkAvatar);

        UInt32 componentCount = CAPI.ovrAvatarComponent_Count(sdkAvatar);
        for (UInt32 i = 0; i < componentCount; i++)
        {
            IntPtr ptr = CAPI.ovrAvatarComponent_Get_Native(sdkAvatar, i);
            ovrAvatarComponent component = (ovrAvatarComponent)Marshal.PtrToStructure(ptr, typeof(ovrAvatarComponent));
            if (!trackedComponents.ContainsKey(component.name))
            {
                GameObject componentObject = new GameObject();
                componentObject.name = component.name;
                componentObject.transform.SetParent(transform);
                AddAvatarComponent(componentObject, component);

                if (leftHand.HasValue && ptr == leftHand.Value.renderComponent)
                {
                    HandLeft = componentObject.AddComponent<OvrAvatarHand>();
                }

                if (rightHand.HasValue && ptr == rightHand.Value.renderComponent)
                {
                    HandRight = componentObject.AddComponent<OvrAvatarHand>();
                }

                if (body.HasValue && ptr == body.Value.renderComponent)
                {
                    Body = componentObject.AddComponent<OvrAvatarBody>();
                }

                if (leftController.HasValue && ptr == leftController.Value.renderComponent)
                {
                    ControllerLeft = componentObject.AddComponent<OvrAvatarTouchController>();
                }

                if (rightController.HasValue && ptr == rightController.Value.renderComponent)
                {
                    ControllerRight = componentObject.AddComponent<OvrAvatarTouchController>();
                }

                if (baseComponent.HasValue && ptr == baseComponent.Value.renderComponent)
                {
                    Base = componentObject.AddComponent<OvrAvatarBase>();
                }
            }
        }
    }

    private void UpdateSDKAvatarUnityState()
    {
        UInt32 componentCount = CAPI.ovrAvatarComponent_Count(sdkAvatar);
        for (UInt32 i = 0; i < componentCount; i++)
        {
            IntPtr ptr = CAPI.ovrAvatarComponent_Get_Native(sdkAvatar, i);
            ovrAvatarComponent component = (ovrAvatarComponent)Marshal.PtrToStructure(ptr, typeof(ovrAvatarComponent));
            UpdateAvatarComponent(component);
        }

        UpdateVoiceBehavior();
    }

    void UpdateCustomPoses()
    {
        // Check to see if the pose roots changed
        if (UpdatePoseRoot(LeftHandCustomPose, ref cachedLeftHandCustomPose, ref cachedCustomLeftHandJoints, ref cachedLeftHandTransforms))
        {
            if (cachedLeftHandCustomPose == null && sdkAvatar != IntPtr.Zero)
            {
                CAPI.ovrAvatar_SetLeftHandGesture(sdkAvatar, ovrAvatarHandGesture.Default);
            }
        }
        if (UpdatePoseRoot(RightHandCustomPose, ref cachedRightHandCustomPose, ref cachedCustomRightHandJoints, ref cachedRightHandTransforms))
        {
            if (cachedRightHandCustomPose == null && sdkAvatar != IntPtr.Zero)
            {
                CAPI.ovrAvatar_SetRightHandGesture(sdkAvatar, ovrAvatarHandGesture.Default);
            }
        }

        // Check to see if the custom gestures need to be updated
        if (sdkAvatar != IntPtr.Zero)
        {
            if (cachedLeftHandCustomPose != null && UpdateTransforms(cachedCustomLeftHandJoints, cachedLeftHandTransforms))
            {
                CAPI.ovrAvatar_SetLeftHandCustomGesture(sdkAvatar, (uint)cachedLeftHandTransforms.Length, cachedLeftHandTransforms);
            }
            if (cachedRightHandCustomPose != null && UpdateTransforms(cachedCustomRightHandJoints, cachedRightHandTransforms))
            {
                CAPI.ovrAvatar_SetRightHandCustomGesture(sdkAvatar, (uint)cachedRightHandTransforms.Length, cachedRightHandTransforms);
            }
        }
    }

    static bool UpdatePoseRoot(Transform poseRoot, ref Transform cachedPoseRoot, ref Transform[] cachedPoseJoints, ref ovrAvatarTransform[] transforms)
    {
        if (poseRoot == cachedPoseRoot)
        {
            return false;
        }

        if (!poseRoot)
        {
            cachedPoseRoot = null;
            cachedPoseJoints = null;
            transforms = null;
        }
        else
        {
            List<Transform> joints = new List<Transform>();
            OrderJoints(poseRoot, joints);
            cachedPoseRoot = poseRoot;
            cachedPoseJoints = joints.ToArray();
            transforms = new ovrAvatarTransform[joints.Count];
        }
        return true;
    }

    static bool UpdateTransforms(Transform[] joints, ovrAvatarTransform[] transforms)
    {
        bool updated = false;
        for (int i = 0; i < joints.Length; ++i)
        {
            Transform joint = joints[i];
            ovrAvatarTransform transform = CreateOvrAvatarTransform(joint.localPosition, joint.localRotation);
            if (transform.position != transforms[i].position || transform.orientation != transforms[i].orientation)
            {
                transforms[i] = transform;
                updated = true;
            }
        }
        return updated;
    }


    private static void OrderJoints(Transform transform, List<Transform> joints)
    {
        joints.Add(transform);
        for (int i = 0; i < transform.childCount; ++i)
        {
            Transform child = transform.GetChild(i);
            OrderJoints(child, joints);
        }
    }

    void AvatarSpecificationCallback(IntPtr avatarSpecification)
    {
        sdkAvatar = CAPI.ovrAvatar_Create(avatarSpecification, Capabilities);
        ShowLeftController(showLeftController);
        ShowRightController(showRightController);

        //Fetch all the assets that this avatar uses.
        UInt32 assetCount = CAPI.ovrAvatar_GetReferencedAssetCount(sdkAvatar);
        for (UInt32 i = 0; i < assetCount; ++i)
        {
            UInt64 id = CAPI.ovrAvatar_GetReferencedAsset(sdkAvatar, i);
            if (OvrAvatarSDKManager.Instance.GetAsset(id) == null)
            {
                OvrAvatarSDKManager.Instance.BeginLoadingAsset(
                    id,
                    LevelOfDetail,
                    AssetLoadedCallback);

                assetLoadingIds.Add(id);
            }
        }

        if (CombineMeshes)
        {
            OvrAvatarSDKManager.Instance.RegisterCombinedMeshCallback(
                sdkAvatar,
                CombinedMeshLoadedCallback);
        }
    }

    void Start()
    {
#if !UNITY_ANDROID
        if (CombineMeshes)
        {
            CombineMeshes = false;
            AvatarLogger.Log("Combine Meshes Currently Only Supported On Android");
        }
#endif

#if !UNITY_5_5_OR_NEWER
        if (CombineMeshes)
        {
            CombineMeshes = false;
            AvatarLogger.LogWarning("Unity Version too old to use Combined Mesh Shader, required 5.5.0+");
        }
#endif

        try
        {
            oculusUserIDInternal = UInt64.Parse(oculusUserID);
        }
        catch (Exception)
        {
            oculusUserIDInternal = 0;

            AvatarLogger.LogWarning("Invalid Oculus User ID Format");
        }

        AvatarLogger.Log("Starting OvrAvatar " + gameObject.name);
        AvatarLogger.Log(AvatarLogger.Tab + "LOD: " + LevelOfDetail.ToString());
        AvatarLogger.Log(AvatarLogger.Tab + "Combine Meshes: " + CombineMeshes);
        AvatarLogger.Log(AvatarLogger.Tab + "Force Mobile Textures: " + ForceMobileTextureFormat);
        AvatarLogger.Log(AvatarLogger.Tab + "Oculus User ID: " + oculusUserIDInternal);

        Capabilities = 0;
        if (EnableBody) Capabilities |= ovrAvatarCapabilities.Body;
        if (EnableHands) Capabilities |= ovrAvatarCapabilities.Hands;
        if (EnableBase && EnableBody) Capabilities |= ovrAvatarCapabilities.Base;
        if (EnableExpressive) Capabilities |= ovrAvatarCapabilities.Expressive;

#if !UNITY_ANDROID
        Capabilities |= ovrAvatarCapabilities.BodyTilt;
#endif

        ShowLeftController(StartWithControllers);
        ShowRightController(StartWithControllers);
        OvrAvatarSDKManager.Instance.RequestAvatarSpecification(
            oculusUserIDInternal,
            this.AvatarSpecificationCallback,
            CombineMeshes,
            LevelOfDetail,
            ForceMobileTextureFormat,
            LookAndFeelVersion,
            FallbackLookAndFeelVersion,
            EnableExpressive);

        WaitingForCombinedMesh = CombineMeshes;
        if (Driver != null)
        {
            Driver.Mode = UseSDKPackets ? OvrAvatarDriver.PacketMode.SDK : OvrAvatarDriver.PacketMode.Unity;
        }
    }

    void Update()
    {
        if (sdkAvatar == IntPtr.Zero)
        {
            return;
        }

        if (Driver != null)
        {
            Driver.UpdateTransforms(sdkAvatar);

            foreach (float[] voiceUpdate in voiceUpdates)
            {
                CAPI.ovrAvatarPose_UpdateVoiceVisualization(sdkAvatar, voiceUpdate);
            }

            voiceUpdates.Clear();
#if AVATAR_INTERNAL
            if (BlendController != null)
            {
                BlendController.UpdateBlend(sdkAvatar);
            }
#endif
            CAPI.ovrAvatarPose_Finalize(sdkAvatar, Time.deltaTime);
        }

        if (RecordPackets)
        {
            RecordFrame();
        }

        if (assetLoadingIds.Count == 0)
        {
            if (!assetsFinishedLoading)
            {
                BuildRenderComponents();
#if AVATAR_INTERNAL
                AssetsDoneLoading.Invoke();
#endif
                InitPostLoad();
                assetsFinishedLoading = true;
            }

            UpdateSDKAvatarUnityState();
            UpdateCustomPoses();
            if (EnableExpressive)
            {
                UpdateExpressive();
            }
        }
    }

    public static ovrAvatarHandInputState CreateInputState(ovrAvatarTransform transform, OvrAvatarDriver.ControllerPose pose)
    {
        ovrAvatarHandInputState inputState = new ovrAvatarHandInputState();
        inputState.transform = transform;
        inputState.buttonMask = pose.buttons;
        inputState.touchMask = pose.touches;
        inputState.joystickX = pose.joystickPosition.x;
        inputState.joystickY = pose.joystickPosition.y;
        inputState.indexTrigger = pose.indexTrigger;
        inputState.handTrigger = pose.handTrigger;
        inputState.isActive = pose.isActive;
        return inputState;
    }

    public void ShowControllers(bool show)
    {
        ShowLeftController(show);
        ShowRightController(show);
    }

    public void ShowLeftController(bool show)
    {
        if (sdkAvatar != IntPtr.Zero)
        {
            CAPI.ovrAvatar_SetLeftControllerVisibility(sdkAvatar, show);
        }
        showLeftController = show;
    }

    public void ShowRightController(bool show)
    {
        if (sdkAvatar != IntPtr.Zero)
        {
            CAPI.ovrAvatar_SetRightControllerVisibility(sdkAvatar, show);
        }
        showRightController = show;
    }

    public void UpdateVoiceVisualization(float[] voiceSamples)
    {
        voiceUpdates.Add(voiceSamples);
    }

    void RecordFrame()
    {
        if(UseSDKPackets)
        {
            RecordSDKFrame();
        }
        else
        {
            RecordUnityFrame();
        }
    }

    // Meant to be used mutually exclusively with RecordSDKFrame to give user more options to optimize or tweak packet data
    private void RecordUnityFrame()
    {
        var deltaSeconds = Time.deltaTime;
        var frame = Driver.GetCurrentPose();
        // If this is our first packet, store the pose as the initial frame
        if (CurrentUnityPacket == null)
        {
            CurrentUnityPacket = new OvrAvatarPacket(frame);
            deltaSeconds = 0;
        }

        float recordedSeconds = 0;
        while (recordedSeconds < deltaSeconds)
        {
            float remainingSeconds = deltaSeconds - recordedSeconds;
            float remainingPacketSeconds = PacketSettings.UpdateRate - CurrentUnityPacket.Duration;

            // If we're not going to fill the packet, just add the frame
            if (remainingSeconds < remainingPacketSeconds)
            {
                CurrentUnityPacket.AddFrame(frame, remainingSeconds);
                recordedSeconds += remainingSeconds;
            }

            // If we're going to fill the packet, interpolate the pose, send the packet,
            // and open a new one
            else
            {
                // Interpolate between the packet's last frame and our target pose
                // to compute a pose at the end of the packet time.
                OvrAvatarDriver.PoseFrame a = CurrentUnityPacket.FinalFrame;
                OvrAvatarDriver.PoseFrame b = frame;
                float t = remainingPacketSeconds / remainingSeconds;
                OvrAvatarDriver.PoseFrame intermediatePose = OvrAvatarDriver.PoseFrame.Interpolate(a, b, t);
                CurrentUnityPacket.AddFrame(intermediatePose, remainingPacketSeconds);
                recordedSeconds += remainingPacketSeconds;

                // Broadcast the recorded packet
                if (PacketRecorded != null)
                {
                    PacketRecorded(this, new PacketEventArgs(CurrentUnityPacket));
                }

                // Open a new packet
                CurrentUnityPacket = new OvrAvatarPacket(intermediatePose);
            }
        }
    }

    private void RecordSDKFrame()
    {
        if (sdkAvatar == IntPtr.Zero)
        {
            return;
        }

        if (!PacketSettings.RecordingFrames)
        {
            CAPI.ovrAvatarPacket_BeginRecording(sdkAvatar);
            PacketSettings.AccumulatedTime = 0.0f;
            PacketSettings.RecordingFrames = true;
        }

        PacketSettings.AccumulatedTime += Time.deltaTime;

        if (PacketSettings.AccumulatedTime >= PacketSettings.UpdateRate)
        {
            PacketSettings.AccumulatedTime = 0.0f;
            var packet = CAPI.ovrAvatarPacket_EndRecording(sdkAvatar);
            CAPI.ovrAvatarPacket_BeginRecording(sdkAvatar);

            if (PacketRecorded != null)
            {
                PacketRecorded(this, new PacketEventArgs(new OvrAvatarPacket { ovrNativePacket = packet }));
            }

            CAPI.ovrAvatarPacket_Free(packet);
        }
    }

    private void AddRenderParts(
        OvrAvatarComponent ovrComponent,
        ovrAvatarComponent component,
        Transform parent)
    {
        bool isBody = ovrComponent.name == "body";
        bool isLeftController = ovrComponent.name == "controller_left";
        bool isReftController = ovrComponent.name == "controller_right";

        for (UInt32 renderPartIndex = 0; renderPartIndex < component.renderPartCount; renderPartIndex++)
        {
            GameObject renderPartObject = new GameObject();
            renderPartObject.name = GetRenderPartName(component, renderPartIndex);
            renderPartObject.transform.SetParent(parent);
            IntPtr renderPart = GetRenderPart(component, renderPartIndex);
            ovrAvatarRenderPartType type = CAPI.ovrAvatarRenderPart_GetType(renderPart);
            OvrAvatarRenderComponent ovrRenderPart;
            switch (type)
            {
                case ovrAvatarRenderPartType.SkinnedMeshRender:
                    ovrRenderPart = AddSkinnedMeshRenderComponent(renderPartObject, CAPI.ovrAvatarRenderPart_GetSkinnedMeshRender(renderPart));
                    break;
                case ovrAvatarRenderPartType.SkinnedMeshRenderPBS:
                    ovrRenderPart = AddSkinnedMeshRenderPBSComponent(renderPartObject, CAPI.ovrAvatarRenderPart_GetSkinnedMeshRenderPBS(renderPart));
                    break;
                case ovrAvatarRenderPartType.ProjectorRender:
                    ovrRenderPart = AddProjectorRenderComponent(renderPartObject, CAPI.ovrAvatarRenderPart_GetProjectorRender(renderPart));
                    break;
                case ovrAvatarRenderPartType.SkinnedMeshRenderPBS_V2:
                    {
                        ovrRenderPart = AddSkinnedMeshRenderPBSV2Component(
                            renderPart,
                            renderPartObject,
                            CAPI.ovrAvatarRenderPart_GetSkinnedMeshRenderPBSV2(renderPart),
                            isBody ? DefaultBodyMaterialManager : DefaultHandMaterialManager,
                            isBody && renderPartIndex == 0,
                            isLeftController || isReftController);
                    }
                    break;
                default:
                    throw new NotImplementedException(string.Format("Unsupported render part type: {0}", type.ToString()));
            }

            ovrComponent.RenderParts.Add(ovrRenderPart);
        }
    }

    public void RefreshBodyParts()
    {
        if (Body != null)
        {
            OvrAvatarComponent component = Body.GetComponent<OvrAvatarComponent>();

            foreach (var part in component.RenderParts)
            {
                Destroy(part.gameObject);
            }

            component.RenderParts.Clear();

            ovrAvatarBodyComponent? sdkBodyComponent = CAPI.ovrAvatarPose_GetBodyComponent(sdkAvatar);
            if (sdkBodyComponent != null)
            {
                ovrAvatarComponent sdKComponent = (ovrAvatarComponent)Marshal.PtrToStructure(sdkBodyComponent.Value.renderComponent, typeof(ovrAvatarComponent));
                AddRenderParts(component, sdKComponent, Body.gameObject.transform);
            }
            else
            {
                throw new Exception("Destroyed the body component, but didn't find a new one in the SDK");
            }
        }
    }

    public ovrAvatarBodyComponent? GetBodyComponent()
    {
        return CAPI.ovrAvatarPose_GetBodyComponent(sdkAvatar);
    }

    public Transform GetHandTransform(HandType hand, HandJoint joint)
    {
        if (hand >= HandType.Max || joint >= HandJoint.Max)
        {
            return null;
        }

        var HandObject = hand == HandType.Left ? HandLeft : HandRight;

        if (HandObject != null)
        {
            var AvatarComponent = HandObject.GetComponent<OvrAvatarComponent>();
            if (AvatarComponent != null && AvatarComponent.RenderParts.Count > 0)
            {
                var SkinnedMesh = AvatarComponent.RenderParts[0];
                return SkinnedMesh.transform.Find(HandJoints[(int)hand, (int)joint]);
            }
        }

        return null;
    }

    public void GetPointingDirection(HandType hand, ref Vector3 forward, ref Vector3 up)
    {
        Transform handBase = GetHandTransform(hand, HandJoint.HandBase);

        if (handBase != null)
        {
            forward = handBase.forward;
            up = handBase.up;
        }
    }

    public Transform GetMouthTransform()
    {
        OvrAvatarComponent component;
        if (trackedComponents.TryGetValue("voice", out component))
        {
            if (component.RenderParts.Count > 0)
            {
                return component.RenderParts[0].transform;
            }
        }

        return null;
    }

    static Vector3 MOUTH_POSITION_OFFSET = new Vector3(0, -0.018f, 0.1051f);
    static string VOICE_PROPERTY = "_Voice";
    static string MOUTH_POSITION_PROPERTY = "_MouthPosition";
    static string MOUTH_DIRECTION_PROPERTY = "_MouthDirection";
    static string MOUTH_SCALE_PROPERTY = "_MouthEffectScale";

    static float MOUTH_SCALE_GLOBAL = 0.007f;
    static float MOUTH_MAX_GLOBAL = 0.007f;
    static string NECK_JONT = "root_JNT/body_JNT/chest_JNT/neckBase_JNT/neck_JNT";

    public float VoiceAmplitude = 0f;
    public bool EnableMouthVertexAnimation = false;

    private void UpdateVoiceBehavior()
    {
        if (!EnableMouthVertexAnimation)
        {
            return;
        }

        if (Body != null)
        {
            OvrAvatarComponent component = Body.GetComponent<OvrAvatarComponent>();

            VoiceAmplitude = Mathf.Clamp(VoiceAmplitude, 0f, 1f);

            if (component.RenderParts.Count > 0)
            {
                var material = component.RenderParts[0].mesh.sharedMaterial;
                var neckJoint = component.RenderParts[0].mesh.transform.Find(NECK_JONT);
                var scaleDiff = neckJoint.TransformPoint(Vector3.up) - neckJoint.position;

                material.SetFloat(MOUTH_SCALE_PROPERTY, scaleDiff.magnitude);

                material.SetFloat(
                    VOICE_PROPERTY,
                    Mathf.Min(scaleDiff.magnitude * MOUTH_MAX_GLOBAL, scaleDiff.magnitude * VoiceAmplitude * MOUTH_SCALE_GLOBAL));

                material.SetVector(
                    MOUTH_POSITION_PROPERTY,
                    neckJoint.TransformPoint(MOUTH_POSITION_OFFSET));

                material.SetVector(MOUTH_DIRECTION_PROPERTY, neckJoint.up);
            }
        }
    }

    bool IsValidMic()
    {
        if (Microphone.devices.Length < 1)
        {
            return false;
        }

        string selectedDevice = Microphone.devices[0].ToString();

        int minFreq;
        int maxFreq;
        Microphone.GetDeviceCaps(selectedDevice, out minFreq, out maxFreq);

        if (maxFreq == 0)
        {
            maxFreq = 44100;
        }

        AudioClip clip = Microphone.Start(selectedDevice, true, 1, maxFreq);
        if (clip == null)
        {
            return false;
        }

        Microphone.End(selectedDevice);
        return true;
    }

    void InitPostLoad()
    {
        ExpressiveGlobalInit();

        ConfigureHelpers();

        if (GetComponent<OvrAvatarLocalDriver>() != null)
        {
            // Use mic.
            lipsyncContext.audioLoopback = false;
            if (CanOwnMicrophone && IsValidMic())
            {
                micInput = MouthAnchor.gameObject.AddComponent<OVRLipSyncMicInput>();
                micInput.enableMicSelectionGUI = false;
                micInput.MicFrequency = 44100;
                micInput.micControl = OVRLipSyncMicInput.micActivation.ConstantSpeak;
            }
        }
    }

	static void ExpressiveGlobalInit()
	{
		if (doneExpressiveGlobalInit)
		{
			return;
		}
		doneExpressiveGlobalInit = true;

		// Set light info. Lights are shared across all avatar instances.
		ovrAvatarLights ovrLights = new ovrAvatarLights();
		ovrLights.ambientIntensity = RenderSettings.ambientLight.grayscale * 0.5f;
		// This array size has to match the 'MarshalAs' attribute in the ovrAvatarLights declaration.
		const int maxSize = 16;
		ovrLights.lights = new ovrAvatarLight[maxSize];

		Light[] sceneLights = FindObjectsOfType(typeof(Light)) as Light[];
		int index = 0;
		for (int i = 0; i < sceneLights.Length; ++i)
		{
			Light sceneLight = sceneLights[i];
			if (sceneLight && sceneLight.enabled)
			{
				uint instanceID = (uint) sceneLight.transform.GetInstanceID();
				switch (sceneLight.type)
				{
					case LightType.Directional:
					{
						ovrLights.lights[index++] = CreateLightDirectional(instanceID, sceneLight.transform.forward, sceneLight.intensity);
						break;
					}
					case LightType.Point:
					{
						ovrLights.lights[index++] = CreateLightPoint(instanceID, sceneLight.transform.position, sceneLight.range, sceneLight.intensity);
						break;
					}
					case LightType.Spot:
					{
						ovrLights.lights[index++] = CreateLightSpot(instanceID, sceneLight.transform.position, sceneLight.transform.forward, sceneLight.spotAngle, sceneLight.range, sceneLight.intensity);
						break;
					}
				}
			}
			if (index >= maxSize)
			{
				break;
			}
		}
		ovrLights.lightCount = (uint) index;

		CAPI.ovrAvatar_UpdateLights(ovrLights);
	}

    static ovrAvatarLight CreateLightDirectional(uint id, Vector3 direction, float intensity)
    {
        ovrAvatarLight light = new ovrAvatarLight();
        light.id = id;
        light.type = ovrAvatarLightType.Direction;
        light.worldDirection = new Vector3(direction.x, direction.y, -direction.z);
        light.intensity = intensity;
        return light;
    }

    static ovrAvatarLight CreateLightPoint(uint id, Vector3 position, float range, float intensity)
    {
        ovrAvatarLight light = new ovrAvatarLight();
        light.id = id;
        light.type = ovrAvatarLightType.Point;
        light.worldPosition = new Vector3(position.x, position.y, -position.z);
        light.range = range;
        light.intensity = intensity;
        return light;
    }

    static ovrAvatarLight CreateLightSpot(uint id, Vector3 position, Vector3 direction, float spotAngleDeg, float range, float intensity)
    {
        ovrAvatarLight light = new ovrAvatarLight();
        light.id = id;
        light.type = ovrAvatarLightType.Spot;
        light.worldPosition = new Vector3(position.x, position.y, -position.z);
        light.worldDirection = new Vector3(direction.x, direction.y, -direction.z);
        light.spotAngleDeg = spotAngleDeg;
        light.range = range;
        light.intensity = intensity;
        return light;
    }

    void UpdateExpressive()
    {
        ovrAvatarTransform baseTransform = OvrAvatar.CreateOvrAvatarTransform(transform.position, transform.rotation);
        CAPI.ovrAvatar_UpdateWorldTransform(sdkAvatar, baseTransform);

        UpdateFacewave();
    }

    private void ConfigureHelpers()
    {
        Transform head =
            transform.Find("body/body_renderPart_0/root_JNT/body_JNT/chest_JNT/neckBase_JNT/neck_JNT/head_JNT");
        if (head == null)
        {
            AvatarLogger.LogError("Avatar helper config failed. Cannot find head transform. All helpers spawning on root avatar transform");
            head = transform;
        }

        if (MouthAnchor == null)
        {
            MouthAnchor = CreateHelperObject(head, MOUTH_HEAD_OFFSET, MOUTH_HELPER_NAME);
        }
        
        if (GetComponent<OvrAvatarLocalDriver>() != null)
        {
            if (audioSource == null)
            {
                audioSource = MouthAnchor.gameObject.AddComponent<AudioSource>();
            }
            spatializedSource = MouthAnchor.GetComponent<ONSPAudioSource>();

            if (spatializedSource == null)
            {
                spatializedSource = MouthAnchor.gameObject.AddComponent<ONSPAudioSource>();
            }

            spatializedSource.UseInvSqr = true;
            spatializedSource.EnableRfl = false;
            spatializedSource.EnableSpatialization = true;
            spatializedSource.Far = 100f;
            spatializedSource.Near = 0.1f;

            // Add phoneme context to the mouth anchor
            lipsyncContext = MouthAnchor.GetComponent<OVRLipSyncContext>();
            if (lipsyncContext == null)
            {
                lipsyncContext = MouthAnchor.gameObject.AddComponent<OVRLipSyncContext>();
                lipsyncContext.provider = OVRLipSync.ContextProviders.Enhanced;
                // Ignore audio callback if microphone is owned by VoIP
                lipsyncContext.skipAudioSource = !CanOwnMicrophone;
            }

            StartCoroutine(WaitForMouthAudioSource());
        }

        if (GetComponent<OvrAvatarRemoteDriver>() != null)
        {
            GazeTarget headTarget = head.gameObject.AddComponent<GazeTarget>();
            headTarget.Type = ovrAvatarGazeTargetType.AvatarHead;
            AvatarLogger.Log("Added head as gaze target");

            Transform hand = transform.Find("hand_left");
            if (hand == null)
            {
                AvatarLogger.LogWarning("Gaze target helper config failed: Cannot find left hand transform");
            }
            else
            {
                GazeTarget handTarget = hand.gameObject.AddComponent<GazeTarget>();
                handTarget.Type = ovrAvatarGazeTargetType.AvatarHand;
                AvatarLogger.Log("Added left hand as gaze target");
            }

            hand = transform.Find("hand_right");
            if (hand == null)
            {
                AvatarLogger.Log("Gaze target helper config failed: Cannot find right hand transform");
            }
            else
            {
                GazeTarget handTarget = hand.gameObject.AddComponent<GazeTarget>();
                handTarget.Type = ovrAvatarGazeTargetType.AvatarHand;
                AvatarLogger.Log("Added right hand as gaze target");
            }
        }
    }

    private IEnumerator WaitForMouthAudioSource()
    {
        while (MouthAnchor.GetComponent<AudioSource>() == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        AudioSource AS = MouthAnchor.GetComponent<AudioSource>();
        AS.minDistance = 0.3f;
        AS.maxDistance = 4f;
        AS.rolloffMode = AudioRolloffMode.Logarithmic;
        AS.loop = true;
        AS.playOnAwake = true;
        AS.spatialBlend = 1.0f;
        AS.spatialize = true;
        AS.spatializePostEffects = true;
    }

    public void DestroyHelperObjects()
    {
        if (MouthAnchor)
        {
            DestroyImmediate(MouthAnchor.gameObject);
        }
    }

    public GameObject CreateHelperObject(Transform parent, Vector3 localPositionOffset, string helperName,
        string helperTag = "")
    {
        GameObject helper = new GameObject();
        helper.name = helperName;
        if (helperTag != "")
        {
            helper.tag = helperTag;
        }
        helper.transform.SetParent(parent);
        helper.transform.localRotation = Quaternion.identity;
        helper.transform.localPosition = localPositionOffset;
        return helper;
    }

    public void UpdateVoiceData(short[] pcmData, int numChannels)
    {
      if (lipsyncContext != null && micInput == null)
      {
          lipsyncContext.ProcessAudioSamplesRaw(pcmData, numChannels);
      }
    }
    public void UpdateVoiceData(float[] pcmData, int numChannels)
    {
      if (lipsyncContext != null && micInput == null)
      {
          lipsyncContext.ProcessAudioSamplesRaw(pcmData, numChannels);
      }
    }


    private void UpdateFacewave()
    {
        if (lipsyncContext != null && (micInput != null || CanOwnMicrophone == false))
        {
            // Get the current viseme frame
            currentFrame = lipsyncContext.GetCurrentPhonemeFrame();

            // Verify length
            if (currentFrame.Visemes.Length != 15)
            {
                Debug.LogError("Unexpected number of visemes " + currentFrame.Visemes);
                return;
            }

            // Copy to viseme array
            currentFrame.Visemes.CopyTo(visemes, 0);

            // Send visemes to native implementation.
            for (int i = 0; i < 15; i++)
            {
                RuntimeVisemes.visemeParams[i] = visemes[i];
            }
            CAPI.ovrAvatar_SetVisemes(sdkAvatar, RuntimeVisemes);
        }
    }
}

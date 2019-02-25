using UnityEngine;
using System.Collections;
using System;
using System.Linq;
using Oculus.Avatar;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
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
    public OvrAvatarBase Base;
    public OvrAvatarBody Body;
    public OvrAvatarTouchController ControllerLeft;
    public OvrAvatarTouchController ControllerRight;
    public OvrAvatarHand HandLeft;
    public OvrAvatarHand HandRight;
    public bool RecordPackets;
    public bool UseSDKPackets = true;

    public bool StartWithControllers;
    public AvatarLayer FirstPersonLayer;
    public AvatarLayer ThirdPersonLayer;
    public bool ShowFirstPerson = true;
    public bool ShowThirdPerson;
    public ovrAvatarCapabilities Capabilities = ovrAvatarCapabilities.All;
    public Shader SurfaceShader;
    public Shader SurfaceShaderSelfOccluding;
    public Shader SurfaceShaderPBS;
    public Shader SurfaceShaderPBSV2Single;
    public Shader SurfaceShaderPBSV2Combined;
    public Shader SurfaceShaderPBSV2Simple;
    public Shader SurfaceShaderPBSV2Loading;

    int renderPartCount = 0;
    bool showLeftController;
    bool showRightController;
    List<float[]> voiceUpdates = new List<float[]>();

    public string oculusUserID;
    internal UInt64 oculusUserIDInternal;

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

    private UnityEvent AssetsDoneLoading = new UnityEvent();
    bool assetsFinishedLoading = false;

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

    public PacketRecordSettings PacketSettings = new PacketRecordSettings();

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
        skinnedMeshRenderer.Initialize(skinnedMeshRender, SurfaceShader, SurfaceShaderSelfOccluding, ThirdPersonLayer.layerIndex, FirstPersonLayer.layerIndex, renderPartCount++);
        return skinnedMeshRenderer;
    }

    private OvrAvatarSkinnedMeshRenderPBSComponent AddSkinnedMeshRenderPBSComponent(GameObject gameObject, ovrAvatarRenderPart_SkinnedMeshRenderPBS skinnedMeshRenderPBS)
    {
        OvrAvatarSkinnedMeshRenderPBSComponent skinnedMeshRenderer = gameObject.AddComponent<OvrAvatarSkinnedMeshRenderPBSComponent>();
        skinnedMeshRenderer.Initialize(skinnedMeshRenderPBS, SurfaceShaderPBS, ThirdPersonLayer.layerIndex, FirstPersonLayer.layerIndex, renderPartCount++);
        return skinnedMeshRenderer;
    }

    private OvrAvatarSkinnedMeshPBSV2RenderComponent AddSkinnedMeshRenderPBSV2Component(
        IntPtr renderPart,
        GameObject gameObject,
        ovrAvatarRenderPart_SkinnedMeshRenderPBS_V2 skinnedMeshRenderPBSV2,
        OvrAvatarMaterialManager materialManager)
    {
        OvrAvatarSkinnedMeshPBSV2RenderComponent skinnedMeshRenderer = gameObject.AddComponent<OvrAvatarSkinnedMeshPBSV2RenderComponent>();
        skinnedMeshRenderer.Initialize(
            renderPart,
            skinnedMeshRenderPBSV2,
            materialManager,
            ThirdPersonLayer.layerIndex,
            FirstPersonLayer.layerIndex,
            renderPartCount++,
            gameObject.name.Contains("body") && CombineMeshes,
            LevelOfDetail);

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
                projectorComponent.InitializeProjectorRender(projectorRender, SurfaceShader, targetRenderPart);
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

    private void RemoveAvatarComponent(string name)
    {
        OvrAvatarComponent componentObject;
        trackedComponents.TryGetValue(name, out componentObject);
        Destroy(componentObject.gameObject);
        trackedComponents.Remove(name);
    }

    private void UpdateSDKAvatarUnityState()
    {
        //Iterate through all the render components
        UInt32 componentCount = CAPI.ovrAvatarComponent_Count(sdkAvatar);
        HashSet<string> componentsThisRun = new HashSet<string>();
        for (UInt32 i = 0; i < componentCount; i++)
        {
            IntPtr ptr = CAPI.ovrAvatarComponent_Get_Native(sdkAvatar, i);
            ovrAvatarComponent component = (ovrAvatarComponent)Marshal.PtrToStructure(ptr, typeof(ovrAvatarComponent));
            componentsThisRun.Add(component.name);
            if (!trackedComponents.ContainsKey(component.name))
            {
                GameObject componentObject = null;
                Type specificType = null;
                if ((Capabilities & ovrAvatarCapabilities.Base) != 0)
                {
                    ovrAvatarBaseComponent? baseComponent = CAPI.ovrAvatarPose_GetBaseComponent(sdkAvatar);
                    if (baseComponent.HasValue && ptr == baseComponent.Value.renderComponent)
                    {
                        specificType = typeof(OvrAvatarBase);
                        if (Base != null)
                        {
                            componentObject = Base.gameObject;
                        }
                    }
                }

                if (specificType == null && (Capabilities & ovrAvatarCapabilities.Body) != 0)
                {
                    ovrAvatarBodyComponent? bodyComponent = CAPI.ovrAvatarPose_GetBodyComponent(sdkAvatar);
                    if (bodyComponent.HasValue && ptr == bodyComponent.Value.renderComponent)
                    {
                        specificType = typeof(OvrAvatarBody);
                        if (Body != null)
                        {
                            componentObject = Body.gameObject;
                        }
                    }
                }

                if (specificType == null && (Capabilities & ovrAvatarCapabilities.Hands) != 0)
                {
                    ovrAvatarControllerComponent? controllerComponent = CAPI.ovrAvatarPose_GetLeftControllerComponent(sdkAvatar);
                    if (specificType == null && controllerComponent.HasValue && ptr == controllerComponent.Value.renderComponent)
                    {
                        specificType = typeof(OvrAvatarTouchController);
                        if (ControllerLeft != null)
                        {
                            componentObject = ControllerLeft.gameObject;
                        }
                    }

                    controllerComponent = CAPI.ovrAvatarPose_GetRightControllerComponent(sdkAvatar);
                    if (specificType == null && controllerComponent.HasValue && ptr == controllerComponent.Value.renderComponent)
                    {
                        specificType = typeof(OvrAvatarTouchController);
                        if (ControllerRight != null)
                        {
                            componentObject = ControllerRight.gameObject;
                        }
                    }

                    ovrAvatarHandComponent? handComponent = CAPI.ovrAvatarPose_GetLeftHandComponent(sdkAvatar);
                    if (specificType == null && handComponent.HasValue && ptr == handComponent.Value.renderComponent)
                    {
                        specificType = typeof(OvrAvatarHand);
                        if (HandLeft != null)
                        {
                            componentObject = HandLeft.gameObject;
                        }
                    }

                    handComponent = CAPI.ovrAvatarPose_GetRightHandComponent(sdkAvatar);
                    if (specificType == null && handComponent.HasValue && ptr == handComponent.Value.renderComponent)
                    {
                        specificType = typeof(OvrAvatarHand);
                        if (HandRight != null)
                        {
                            componentObject = HandRight.gameObject;
                        }
                    }
                }

                // If this is an unknown type, just create an object for the rendering
                if (componentObject == null && specificType == null)
                {
                    componentObject = new GameObject();
                    componentObject.name = component.name;
                    componentObject.transform.SetParent(transform);
                }
                if (componentObject != null)
                {
                    AddAvatarComponent(componentObject, component);
                }
            }
            UpdateAvatarComponent(component);
        }
        HashSet<string> deletableNames = new HashSet<string>(trackedComponents.Keys);
        deletableNames.ExceptWith(componentsThisRun);
        //deletableNames contains the name of all components which are tracked and were
        //not present in this run
        foreach (var name in deletableNames)
        {
            RemoveAvatarComponent(name);
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
#if UNITY_ANDROID
        Capabilities &= ~ovrAvatarCapabilities.BodyTilt;
#endif
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

        ShowLeftController(StartWithControllers);
        ShowRightController(StartWithControllers);
        OvrAvatarSDKManager.Instance.RequestAvatarSpecification(
            oculusUserIDInternal,
            this.AvatarSpecificationCallback,
            CombineMeshes,
            LevelOfDetail,
            ForceMobileTextureFormat,
            LookAndFeelVersion,
            FallbackLookAndFeelVersion);

        WaitingForCombinedMesh = CombineMeshes;
        Driver.Mode = UseSDKPackets ? OvrAvatarDriver.PacketMode.SDK : OvrAvatarDriver.PacketMode.Unity;
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

            CAPI.ovrAvatarPose_Finalize(sdkAvatar, Time.deltaTime);
        }

        if (RecordPackets)
        {
            RecordFrame();
        }

        if (assetLoadingIds.Count == 0)
        {
            UpdateSDKAvatarUnityState();
            UpdateCustomPoses();

            if (!assetsFinishedLoading)
            {
                AssetsDoneLoading.Invoke();
                assetsFinishedLoading = true;
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
                        OvrAvatarMaterialManager materialManager = null;

                        if (ovrComponent.name == "body")
                        {
                            materialManager = DefaultBodyMaterialManager;
                        }
                        else if( ovrComponent.name.Contains("hand"))
                        {
                            materialManager = DefaultHandMaterialManager;
                        }

                        ovrRenderPart = AddSkinnedMeshRenderPBSV2Component(
                            renderPart,
                            renderPartObject,
                            CAPI.ovrAvatarRenderPart_GetSkinnedMeshRenderPBSV2(renderPart),
                            materialManager);
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
        OvrAvatarComponent component;
        if (trackedComponents.TryGetValue("body", out component) && Body != null)
        {
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

        OvrAvatarComponent component;
        if (trackedComponents.TryGetValue("body", out component))
        {
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
}

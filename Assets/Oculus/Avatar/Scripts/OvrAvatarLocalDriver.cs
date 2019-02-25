using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using Oculus.Avatar;

public class OvrAvatarLocalDriver : OvrAvatarDriver {

    ControllerPose GetMalibuControllerPose(OVRInput.Controller controller)
    {
        ovrAvatarButton buttons = 0;
        if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller)) buttons |= ovrAvatarButton.One;

        return new ControllerPose
        {
            buttons = buttons,
            touches = OVRInput.Get(OVRInput.Touch.PrimaryTouchpad) ? ovrAvatarTouch.One : 0,
            joystickPosition = OVRInput.Get(OVRInput.Axis2D.PrimaryTouchpad, controller),
            indexTrigger = 0f,
            handTrigger = 0f,
            isActive = (OVRInput.GetActiveController() & controller) != 0,
        };
    }

    float voiceAmplitude = 0.0f;
    ControllerPose GetControllerPose(OVRInput.Controller controller)
    {
        ovrAvatarButton buttons = 0;
        if (OVRInput.Get(OVRInput.Button.One, controller)) buttons |= ovrAvatarButton.One;
        if (OVRInput.Get(OVRInput.Button.Two, controller)) buttons |= ovrAvatarButton.Two;
        if (OVRInput.Get(OVRInput.Button.Start, controller)) buttons |= ovrAvatarButton.Three;
        if (OVRInput.Get(OVRInput.Button.PrimaryThumbstick, controller)) buttons |= ovrAvatarButton.Joystick;

        ovrAvatarTouch touches = 0;
        if (OVRInput.Get(OVRInput.Touch.One, controller)) touches |= ovrAvatarTouch.One;
        if (OVRInput.Get(OVRInput.Touch.Two, controller)) touches |= ovrAvatarTouch.Two;
        if (OVRInput.Get(OVRInput.Touch.PrimaryThumbstick, controller)) touches |= ovrAvatarTouch.Joystick;
        if (OVRInput.Get(OVRInput.Touch.PrimaryThumbRest, controller)) touches |= ovrAvatarTouch.ThumbRest;
        if (OVRInput.Get(OVRInput.Touch.PrimaryIndexTrigger, controller)) touches |= ovrAvatarTouch.Index;
        if (!OVRInput.Get(OVRInput.NearTouch.PrimaryIndexTrigger, controller)) touches |= ovrAvatarTouch.Pointing;
        if (!OVRInput.Get(OVRInput.NearTouch.PrimaryThumbButtons, controller)) touches |= ovrAvatarTouch.ThumbUp;

        return new ControllerPose
        {
            buttons = buttons,
            touches = touches,
            joystickPosition = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, controller),
            indexTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller),
            handTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, controller),
            isActive = (OVRInput.GetActiveController() & controller) != 0,
        };
    }

    private void CalculateCurrentPose()
    {
#if UNITY_2017_2_OR_NEWER
        Vector3 headPos = UnityEngine.XR.InputTracking.GetLocalPosition(UnityEngine.XR.XRNode.CenterEye);
#else
        Vector3 headPos = UnityEngine.VR.InputTracking.GetLocalPosition(UnityEngine.VR.VRNode.CenterEye);
#endif

        if (GetIsTrackedRemote())
        {
            CurrentPose = new PoseFrame
            {
                voiceAmplitude = voiceAmplitude,
                headPosition = headPos,
#if UNITY_2017_2_OR_NEWER
                headRotation = UnityEngine.XR.InputTracking.GetLocalRotation(UnityEngine.XR.XRNode.CenterEye),
#else
                headRotation = UnityEngine.VR.InputTracking.GetLocalRotation(UnityEngine.VR.VRNode.CenterEye),
#endif
                handLeftPosition = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTrackedRemote),
                handLeftRotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.LTrackedRemote),
                handRightPosition = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTrackedRemote),
                handRightRotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTrackedRemote),
                controllerLeftPose = GetMalibuControllerPose(OVRInput.Controller.LTrackedRemote),
                controllerRightPose = GetMalibuControllerPose(OVRInput.Controller.RTrackedRemote),
            };
        }
        else
        {
            CurrentPose = new PoseFrame
            {
                voiceAmplitude = voiceAmplitude,
                headPosition = headPos,
#if UNITY_2017_2_OR_NEWER
                headRotation = UnityEngine.XR.InputTracking.GetLocalRotation(UnityEngine.XR.XRNode.CenterEye),
#else
                headRotation = UnityEngine.VR.InputTracking.GetLocalRotation(UnityEngine.VR.VRNode.CenterEye),
#endif
                handLeftPosition = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch),
                handLeftRotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.LTouch),
                handRightPosition = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch),
                handRightRotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch),
                controllerLeftPose = GetControllerPose(OVRInput.Controller.LTouch),
                controllerRightPose = GetControllerPose(OVRInput.Controller.RTouch),
            };
        }

    }

    public override void UpdateTransforms(IntPtr sdkAvatar)
    {
        CalculateCurrentPose();
        UpdateTransformsFromPose(sdkAvatar);
    }
}

This Cross Platform Unity scene demonstrates how to use Oculus Avatar SDK without the Oculus Platform SDK.

Setup instructions:
1 - Create a new Unity Project
2 - Import the Oculus Avatar SDK Unity package:  https://developer.oculus.com/downloads/package/oculus-avatar-sdk/
3 - Open this CrossPlatform scene (in Assets/Oculus/Avatar/Samples/CrossPlatform)
4 - Import Oculus Utilities for Unity: 	https://developer.oculus.com/downloads/package/oculus-utilities-for-unity-5/
5 - Do *not* import the Oculus Platform SDK for Unity!
6 - Delete the imported SocialStarter folder from Assets/Oculus/Avatar/Samples (it demos Platform features, we don't need it for this)
7 - Use the Oculus Dashboard (https://dashboard.oculus.com/) to create a placeholder Rift app and copy the App ID
8 - Paste the App ID in Unity under Oculus Avatars > Edit Configuration > Oculus Rift App Id
9 - Enable OpenVR:
	Open PlayerSettings in the Inspector tab (menu Edit > Project Settings > Player)
	In PlayerSettings expand XR Settings
	Under Virtual Reality SDKs, add OpenVR
10 - Click Play


Changing Avatar customization:
1 - Note the "LocalAvatar" GameObjects in the scene. Each Avatar has distinct customization.
2 - Inspect each LocalAvatar GameObject and observe the attached Ovr Avatar Script component and the "Oculus User ID" property set on each.
3 - Create your own test accounts to customize your Avatars.
4 - Use your own User IDs in this sample scene. (You will have to leave and reenter Play mode to apply Avatar User ID changes.)



Redistribution:
When packaging a Cross Platform application using Oculus Avatars, you will need to include:
  * libovravatar.dll
  * OvrAvatarAssets.zip
On a computer with the Oculus launcher, these files can be found in "C:\Program Files\Oculus\Support\oculus-runtime" by default.

You also need to include the Oculus Avatar SDK License, found here: https://developer.oculus.com/licenses/avatar-sdk-1.0/

In your Unity project's Assets folder, add these files to a Plugins directory.

NOTE: Unity's Build will only copy DLL files in a Plugins directory over to the output Plugins directory.
You must manually copy OvrAvatarAssets.zip to the output Plugins directory.
You can automate this process with a script adding a custom build command,
see the Unity docs here: https://docs.unity3d.com/Manual/BuildPlayerPipeline.html


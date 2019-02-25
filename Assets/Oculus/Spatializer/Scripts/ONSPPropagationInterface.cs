/************************************************************************************
Filename    :   ONSPPropagationInterface.cs
Content     :   Interface into the Oculus Audio propagation functions
                Attach to a game object with meshes and material scripts to create geometry
                NOTE: ensure that Oculus Spatialization is enabled for AudioSource components
Copyright   :   Copyright (c) Facebook Technologies, LLC and its affiliates. All rights reserved.

Licensed under the Oculus SDK Version 3.5 (the "License"); 
you may not use the Oculus SDK except in compliance with the License, 
which is provided at the time of installation or download, or which 
otherwise accompanies this software in either electronic or hard copy form.

You may obtain a copy of the License at

https://developer.oculus.com/licenses/sdk-3.5/

Unless required by applicable law or agreed to in writing, the Oculus SDK 
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
************************************************************************************/
using System;
using System.Runtime.InteropServices;

namespace ONSPPropagationInterface
{
    /***********************************************************************************/
    // ENUMS and STRUCTS
    /***********************************************************************************/
    public enum FaceType : uint
    {
        TRIANGLES = 0,
        QUADS
    }

    public enum MaterialProperty : uint
    {
        ABSORPTION = 0,
        TRANSMISSION,
        SCATTERING
    }

    // Matches internal mesh layout
    [StructLayout(LayoutKind.Sequential, Pack=1)]
    public struct MeshGroup
    {
        public UIntPtr indexOffset;
        public UIntPtr faceCount;
        [MarshalAs(UnmanagedType.U4)]
        public FaceType faceType;
        public IntPtr material;
    }

    public enum ovrAudioScalarType : uint
    {
        Int8,
        UInt8,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Float16,
        Float32,
        Float64
    }

    /***********************************************************************************/
    // UNITY NATIVE
    /***********************************************************************************/
    namespace Unity_Native
    {
        public class PropIFace
        {
            static IntPtr context_ = IntPtr.Zero;
            static IntPtr context { get { if (context_ == IntPtr.Zero) { ovrAudio_GetPluginContext(out context_); } return context_; } }

            /***********************************************************************************/

            // The name used for the plugin DLL.
            public const string strOSPS = "AudioPluginOculusSpatializer";

            /***********************************************************************************/
            // Context API: Required to create internal context if it does not exist yet

            [DllImport(strOSPS)]
            public static extern int ovrAudio_GetPluginContext(out IntPtr context);

            /***********************************************************************************/
            // Settings API

            [DllImport(strOSPS)]
            public static extern int ovrAudio_SetPropagationQuality(IntPtr context, float quality);
            public static int SetPropagationQuality(float quality)
            {
                return ovrAudio_SetPropagationQuality(context, quality);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_SetPropagationThreadAffinity(IntPtr context, UInt64 cpuMask);
            public static int SetPropagationThreadAffinity(UInt64 cpuMask)
            {
                return ovrAudio_SetPropagationThreadAffinity(context, cpuMask);
            }

            /***********************************************************************************/
            // Geometry API

            [DllImport(strOSPS)]
            public static extern int ovrAudio_CreateAudioGeometry(IntPtr context, out IntPtr geometry);
            public static int CreateAudioGeometry(out IntPtr geometry)
            {
                return ovrAudio_CreateAudioGeometry(context, out geometry);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_DestroyAudioGeometry(IntPtr geometry);
            public static int DestroyAudioGeometry(IntPtr geometry)
            {
                return ovrAudio_DestroyAudioGeometry(geometry);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                                            float[] vertices, UIntPtr verticesBytesOffset, UIntPtr vertexCount, UIntPtr vertexStride, ovrAudioScalarType vertexType,
                                                                            int[] indices, UIntPtr indicesByteOffset, UIntPtr indexCount, ovrAudioScalarType indexType,
                                                                            MeshGroup[] groups, UIntPtr groupCount);

            public static int AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                            float[] vertices, int vertexCount,
                                                            int[] indices, int indexCount,
                                                            MeshGroup[] groups, int groupCount)
            {
                return ovrAudio_AudioGeometryUploadMeshArrays(geometry,
                    vertices, UIntPtr.Zero, (UIntPtr)vertexCount, UIntPtr.Zero, ovrAudioScalarType.Float32,
                    indices, UIntPtr.Zero, (UIntPtr)indexCount, ovrAudioScalarType.UInt32,
                    groups, (UIntPtr)groupCount);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioGeometrySetTransform(IntPtr geometry, float[] matrix4x4);
            public static int AudioGeometrySetTransform(IntPtr geometry, float[] matrix4x4)
            {
                return ovrAudio_AudioGeometrySetTransform(geometry, matrix4x4);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4);
            public static int AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4)
            {
                return ovrAudio_AudioGeometryGetTransform(geometry, out matrix4x4);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioGeometryWriteMeshFile(IntPtr geometry, string filePath);
            public static int AudioGeometryWriteMeshFile(IntPtr geometry, string filePath)
            {
                return ovrAudio_AudioGeometryWriteMeshFile(geometry, filePath);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioGeometryReadMeshFile(IntPtr geometry, string filePath);
            public static int AudioGeometryReadMeshFile(IntPtr geometry, string filePath)
            {
                return ovrAudio_AudioGeometryReadMeshFile(geometry, filePath);
            }

            /***********************************************************************************/
            // Material API

            [DllImport(strOSPS)]
            public static extern int ovrAudio_CreateAudioMaterial(IntPtr context, out IntPtr material);
            public static int CreateAudioMaterial(out IntPtr material)
            {
                return ovrAudio_CreateAudioMaterial(context, out material);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_DestroyAudioMaterial(IntPtr material);
            public static int DestroyAudioMaterial(IntPtr material)
            {
                return ovrAudio_DestroyAudioMaterial(material);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value);
            public static int AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value)
            {
                return ovrAudio_AudioMaterialSetFrequency(material, property, frequency, value);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value);
            public static int AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value)
            {
                return ovrAudio_AudioMaterialGetFrequency(material, property, frequency, out value);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioMaterialReset(IntPtr material, MaterialProperty property);
            public static int AudioMaterialReset(IntPtr material, MaterialProperty property)
            {
                return ovrAudio_AudioMaterialReset(material, property);
            }
        }
    }

    /***********************************************************************************/
    // WWISE
    /***********************************************************************************/
    namespace Wwise
    {
        public class PropIFace
        {
            static IntPtr context_ = IntPtr.Zero;
            static IntPtr context { get { if (context_ == IntPtr.Zero) { ovrAudio_GetPluginContext(out context_); } return context_; } }

            /***********************************************************************************/

            // The name used for the plugin DLL.
            public const string strOSPS = "OculusSpatializerWwise";

            /***********************************************************************************/
            // Context API: Required to create internal context if it does not exist yet

            [DllImport(strOSPS)]
            public static extern int ovrAudio_GetPluginContext(out IntPtr context);

            /***********************************************************************************/
            // Settings API

            [DllImport(strOSPS)]
            public static extern int ovrAudio_SetPropagationQuality(IntPtr context, float quality);
            public static int SetPropagationQuality(float quality)
            {
                return ovrAudio_SetPropagationQuality(context, quality);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_SetPropagationThreadAffinity(IntPtr context, UInt64 cpuMask);
            public static int SetPropagationThreadAffinity(UInt64 cpuMask)
            {
                return ovrAudio_SetPropagationThreadAffinity(context, cpuMask);
            }

            /***********************************************************************************/
            // Geometry API

            [DllImport(strOSPS)]
            public static extern int ovrAudio_CreateAudioGeometry(IntPtr context, out IntPtr geometry);
            public static int CreateAudioGeometry(out IntPtr geometry)
            {
                return ovrAudio_CreateAudioGeometry(context, out geometry);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_DestroyAudioGeometry(IntPtr geometry);
            public static int DestroyAudioGeometry(IntPtr geometry)
            {
                return ovrAudio_DestroyAudioGeometry(geometry);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                                            float[] vertices, UIntPtr verticesBytesOffset, UIntPtr vertexCount, UIntPtr vertexStride, ovrAudioScalarType vertexType,
                                                                            int[] indices, UIntPtr indicesByteOffset, UIntPtr indexCount, ovrAudioScalarType indexType,
                                                                            MeshGroup[] groups, UIntPtr groupCount);

            public static int AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                            float[] vertices, int vertexCount,
                                                            int[] indices, int indexCount,
                                                            MeshGroup[] groups, int groupCount)
            {
                return ovrAudio_AudioGeometryUploadMeshArrays(geometry,
                    vertices, UIntPtr.Zero, (UIntPtr)vertexCount, UIntPtr.Zero, ovrAudioScalarType.Float32,
                    indices, UIntPtr.Zero, (UIntPtr)indexCount, ovrAudioScalarType.UInt32,
                    groups, (UIntPtr)groupCount);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioGeometrySetTransform(IntPtr geometry, float[] matrix4x4);
            public static int AudioGeometrySetTransform(IntPtr geometry, float[] matrix4x4)
            {
                return ovrAudio_AudioGeometrySetTransform(geometry, matrix4x4);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4);
            public static int AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4)
            {
                return ovrAudio_AudioGeometryGetTransform(geometry, out matrix4x4);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioGeometryWriteMeshFile(IntPtr geometry, string filePath);
            public static int AudioGeometryWriteMeshFile(IntPtr geometry, string filePath)
            {
                return ovrAudio_AudioGeometryWriteMeshFile(geometry, filePath);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioGeometryReadMeshFile(IntPtr geometry, string filePath);
            public static int AudioGeometryReadMeshFile(IntPtr geometry, string filePath)
            {
                return ovrAudio_AudioGeometryReadMeshFile(geometry, filePath);
            }

            /***********************************************************************************/
            // Material API

            [DllImport(strOSPS)]
            public static extern int ovrAudio_CreateAudioMaterial(IntPtr context, out IntPtr material);
            public static int CreateAudioMaterial(out IntPtr material)
            {
                return ovrAudio_CreateAudioMaterial(context, out material);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_DestroyAudioMaterial(IntPtr material);
            public static int DestroyAudioMaterial(IntPtr material)
            {
                return ovrAudio_DestroyAudioMaterial(material);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value);
            public static int AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value)
            {
                return ovrAudio_AudioMaterialSetFrequency(material, property, frequency, value);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value);
            public static int AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value)
            {
                return ovrAudio_AudioMaterialGetFrequency(material, property, frequency, out value);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioMaterialReset(IntPtr material, MaterialProperty property);
            public static int AudioMaterialReset(IntPtr material, MaterialProperty property)
            {
                return ovrAudio_AudioMaterialReset(material, property);
            }
        }
    }

    /***********************************************************************************/
    // FMOD
    /***********************************************************************************/
    namespace FMOD
    {
        public class PropIFace
        {
            static IntPtr context_ = IntPtr.Zero;
            static IntPtr context { get { if (context_ == IntPtr.Zero) { ovrAudio_GetPluginContext(out context_); } return context_; } }

            /***********************************************************************************/

            // The name used for the plugin DLL.
            public const string strOSPS = "OculusSpatializerFMOD";

            /***********************************************************************************/
            // Context API: Required to create internal context if it does not exist yet

            [DllImport(strOSPS)]
            public static extern int ovrAudio_GetPluginContext(out IntPtr context);

            /***********************************************************************************/
            // Settings API

            [DllImport(strOSPS)]
            public static extern int ovrAudio_SetPropagationQuality(IntPtr context, float quality);
            public static int SetPropagationQuality(float quality)
            {
                return ovrAudio_SetPropagationQuality(context, quality);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_SetPropagationThreadAffinity(IntPtr context, UInt64 cpuMask);
            public static int SetPropagationThreadAffinity(UInt64 cpuMask)
            {
                return ovrAudio_SetPropagationThreadAffinity(context, cpuMask);
            }

            /***********************************************************************************/
            // Geometry API

            [DllImport(strOSPS)]
            public static extern int ovrAudio_CreateAudioGeometry(IntPtr context, out IntPtr geometry);
            public static int CreateAudioGeometry(out IntPtr geometry)
            {
                return ovrAudio_CreateAudioGeometry(context, out geometry);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_DestroyAudioGeometry(IntPtr geometry);
            public static int DestroyAudioGeometry(IntPtr geometry)
            {
                return ovrAudio_DestroyAudioGeometry(geometry);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                                            float[] vertices, UIntPtr verticesBytesOffset, UIntPtr vertexCount, UIntPtr vertexStride, ovrAudioScalarType vertexType,
                                                                            int[] indices, UIntPtr indicesByteOffset, UIntPtr indexCount, ovrAudioScalarType indexType,
                                                                            MeshGroup[] groups, UIntPtr groupCount);

            public static int AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                            float[] vertices, int vertexCount,
                                                            int[] indices, int indexCount,
                                                            MeshGroup[] groups, int groupCount)
            {
                return ovrAudio_AudioGeometryUploadMeshArrays(geometry,
                    vertices, UIntPtr.Zero, (UIntPtr)vertexCount, UIntPtr.Zero, ovrAudioScalarType.Float32,
                    indices, UIntPtr.Zero, (UIntPtr)indexCount, ovrAudioScalarType.UInt32,
                    groups, (UIntPtr)groupCount);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioGeometrySetTransform(IntPtr geometry, float[] matrix4x4);
            public static int AudioGeometrySetTransform(IntPtr geometry, float[] matrix4x4)
            {
                return ovrAudio_AudioGeometrySetTransform(geometry, matrix4x4);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4);
            public static int AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4)
            {
                return ovrAudio_AudioGeometryGetTransform(geometry, out matrix4x4);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioGeometryWriteMeshFile(IntPtr geometry, string filePath);
            public static int AudioGeometryWriteMeshFile(IntPtr geometry, string filePath)
            {
                return ovrAudio_AudioGeometryWriteMeshFile(geometry, filePath);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioGeometryReadMeshFile(IntPtr geometry, string filePath);
            public static int AudioGeometryReadMeshFile(IntPtr geometry, string filePath)
            {
                return ovrAudio_AudioGeometryReadMeshFile(geometry, filePath);
            }

            /***********************************************************************************/
            // Material API

            [DllImport(strOSPS)]
            public static extern int ovrAudio_CreateAudioMaterial(IntPtr context, out IntPtr material);
            public static int CreateAudioMaterial(out IntPtr material)
            {
                return ovrAudio_CreateAudioMaterial(context, out material);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_DestroyAudioMaterial(IntPtr material);
            public static int DestroyAudioMaterial(IntPtr material)
            {
                return ovrAudio_DestroyAudioMaterial(material);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value);
            public static int AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value)
            {
                return ovrAudio_AudioMaterialSetFrequency(material, property, frequency, value);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value);
            public static int AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value)
            {
                return ovrAudio_AudioMaterialGetFrequency(material, property, frequency, out value);
            }

            [DllImport(strOSPS)]
            public static extern int ovrAudio_AudioMaterialReset(IntPtr material, MaterialProperty property);
            public static int AudioMaterialReset(IntPtr material, MaterialProperty property)
            {
                return ovrAudio_AudioMaterialReset(material, property);
            }
        }
    }
}

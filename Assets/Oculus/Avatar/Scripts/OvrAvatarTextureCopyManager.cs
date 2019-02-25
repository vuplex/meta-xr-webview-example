using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OvrAvatarTextureCopyManager : MonoBehaviour
{
    private const int TEXTURES_TO_COPY_QUEUE_CAPACITY = 256;

    struct CopyTextureParams
    {
        public Texture Src;
        public Texture Dst;
        public int Mip;
        public int SrcSize;
        public int DstElement;

        public CopyTextureParams(
            Texture src, 
            Texture dst, 
            int mip, 
            int srcSize, 
            int dstElement)
        {
            Src = src;
            Dst = dst;
            Mip = mip;  
            SrcSize = srcSize;
            DstElement = dstElement;
        }
    }

    private Queue<CopyTextureParams> texturesToCopy;

    public OvrAvatarTextureCopyManager()
    {
        texturesToCopy = new Queue<CopyTextureParams>(TEXTURES_TO_COPY_QUEUE_CAPACITY);
    }

    public void Update()
    {
        if (texturesToCopy.Count == 0)
        {
            return;
        }

        CopyTextureParams copyTextureParams;

        lock (texturesToCopy)
        {
            copyTextureParams = texturesToCopy.Dequeue();
        }

        StartCoroutine(CopyTextureCoroutine(copyTextureParams));
    }

    public int GetTextureCount()
    {
        return texturesToCopy.Count;
    }

    public void CopyTexture(
        Texture src, 
        Texture dst, 
        int mipLevel, 
        int mipSize, 
        int dstElement, 
        bool useQueue = true)
    {
        bool queued = false;
        var copyTextureParams = new CopyTextureParams(src, dst, mipLevel, mipSize, dstElement);

        if (useQueue)
        {
            lock (texturesToCopy)
            {
                if (texturesToCopy.Count < TEXTURES_TO_COPY_QUEUE_CAPACITY)
                {
                    texturesToCopy.Enqueue(copyTextureParams);
                    queued = true;
                }
            }
        }
        else
        {
            CopyTexture(copyTextureParams);
        }

        if (!queued)
        {
            CopyTexture(copyTextureParams);
        }
    }

    IEnumerator CopyTextureCoroutine(CopyTextureParams copyTextureParams)
    {
        // Wait until frame rendering is done
        yield return new WaitForEndOfFrame();

        Graphics.CopyTexture(
            copyTextureParams.Src, 
            0, 
            copyTextureParams.Mip, 
            0, 
            0, 
            copyTextureParams.SrcSize, 
            copyTextureParams.SrcSize,
            copyTextureParams.Dst, 
            copyTextureParams.DstElement, 
            copyTextureParams.Mip, 
            0, 
            0);
    }

    private void CopyTexture(CopyTextureParams copyTextureParams)
    {
        Graphics.CopyTexture(
            copyTextureParams.Src, 
            0, 
            copyTextureParams.Mip, 
            0, 
            0, 
            copyTextureParams.SrcSize, 
            copyTextureParams.SrcSize,
            copyTextureParams.Dst, 
            copyTextureParams.DstElement, 
            copyTextureParams.Mip, 
            0, 
            0);
    }
}

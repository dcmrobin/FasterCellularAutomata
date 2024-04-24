using UnityEngine;

public class ConwayCompute : MonoBehaviour
{
    public ComputeShader computeShader;
    public int width = 300;
    public int height = 300;
    public RenderTexture renderTexture1;
    public RenderTexture renderTexture2;
    private bool useRenderTexture1 = true;

    void Start()
    {
        InitializeTextures();
        RandomInitialize();
    }

    void InitializeTextures()
    {
        renderTexture1 = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat);
        renderTexture1.enableRandomWrite = true;
        renderTexture1.Create();

        renderTexture2 = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat);
        renderTexture2.enableRandomWrite = true;
        renderTexture2.Create();
    }

    void RandomInitialize()
    {
        computeShader.SetTexture(1, "currentBuffer", useRenderTexture1 ? renderTexture1 : renderTexture2);
        computeShader.SetTexture(1, "nextBuffer", useRenderTexture1 ? renderTexture2 : renderTexture1);
        computeShader.Dispatch(1, width / 16, height / 16, 1);
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        computeShader.SetTexture(0, "currentBuffer", useRenderTexture1 ? renderTexture1 : renderTexture2);
        computeShader.SetTexture(0, "nextBuffer", useRenderTexture1 ? renderTexture2 : renderTexture1);
        computeShader.Dispatch(0, width / 16, height / 16, 1);

        // Swap render textures for the next frame
        useRenderTexture1 = !useRenderTexture1;

        // Display the result using Graphics.Blit
        Graphics.Blit(useRenderTexture1 ? renderTexture1 : renderTexture2, dest);
    }

    void OnDestroy()
    {
        renderTexture1.Release();
        renderTexture2.Release();
    }
}

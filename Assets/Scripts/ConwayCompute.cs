using UnityEngine;

public class ConwayCompute : MonoBehaviour
{
    public ComputeShader computeShader;
    public int width = 300;
    public int height = 300;
    public RenderTexture renderTexture;

    void Start()
    {
        InitializeTexture();
        RandomInitialize();
    }

    void InitializeTexture()
    {
        renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
    }

    void RandomInitialize()
    {
        computeShader.SetTexture(1, "Result", renderTexture);
        computeShader.Dispatch(1, width / 16, height / 16, 1);
    }

    void OnDestroy()
    {
        renderTexture.Release();
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(width, height, 24);
            renderTexture.enableRandomWrite = true;
            renderTexture.Create();
        }

        computeShader.SetTexture(0, "Result", renderTexture);
        computeShader.Dispatch(0, renderTexture.width / 16, renderTexture.height / 16, 1);

        Graphics.Blit(renderTexture, dest);
    }
}

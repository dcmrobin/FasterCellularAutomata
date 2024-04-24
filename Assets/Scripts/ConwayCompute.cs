using UnityEngine;

public class ConwayCompute : MonoBehaviour
{
    public ComputeShader computeShader;
    public int width = 64;
    public int height = 64;
    public RenderTexture resultTexture;

    void Start()
    {
        InitializeTexture();
        RandomInitialize();
        Compute();
    }

    void InitializeTexture()
    {
        resultTexture = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat);
        resultTexture.enableRandomWrite = true;
        resultTexture.Create();
    }

    void Compute()
    {
        computeShader.SetTexture(0, "Result", resultTexture);
        computeShader.Dispatch(0, width / 16, height / 16, 1);
    }

    void RandomInitialize()
    {
        computeShader.SetTexture(1, "Result", resultTexture);
        computeShader.Dispatch(1, width / 16, height / 16, 1);
    }

    void OnDestroy()
    {
        resultTexture.Release();
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (resultTexture == null)
        {
            resultTexture = new RenderTexture(width, height, 24);
            resultTexture.enableRandomWrite = true;
            resultTexture.Create();
        }

        computeShader.SetTexture(0, "Result", resultTexture);
        computeShader.Dispatch(0, resultTexture.width / 16, resultTexture.height / 16, 1);

        Graphics.Blit(resultTexture, dest);
    }
}

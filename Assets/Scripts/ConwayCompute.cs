using UnityEngine;

public class ConwayCompute : MonoBehaviour
{
    public ComputeShader computeShader;
    public int width = 300;
    public int height = 300;
    public RenderTexture renderTexture1;
    public RenderTexture renderTexture2;
    public MeshRenderer planeObjectRenderer;
    private ComputeBuffer clickBuffer; // Buffer to store click position
    private ComputeBuffer pauseBuffer;
    private bool useRenderTexture1 = true;
    private bool isPaused = false;

    void Start()
    {
        InitializeTextures();
        RandomInitialize();

        // Create click buffer
        clickBuffer = new ComputeBuffer(1, sizeof(int) * 2);

        pauseBuffer = new ComputeBuffer(1, sizeof(int));
        computeShader.SetBuffer(0, "pauseBuffer", pauseBuffer);
    }

    void InitializeTextures()
    {
        renderTexture1 = new RenderTexture(width, height, 0);
        renderTexture1.enableRandomWrite = true;
        renderTexture1.Create();

        planeObjectRenderer.material.mainTexture = renderTexture1;

        renderTexture2 = new RenderTexture(width, height, 0);
        renderTexture2.enableRandomWrite = true;
        renderTexture2.Create();
    }

    void RandomInitialize()
    {
        computeShader.SetTexture(1, "currentBuffer", useRenderTexture1 ? renderTexture1 : renderTexture2);
        computeShader.SetTexture(1, "nextBuffer", useRenderTexture1 ? renderTexture2 : renderTexture1);
        computeShader.Dispatch(1, width / 16, height / 16, 1);
    }

    private void Update()
    {
        computeShader.SetTexture(0, "currentBuffer", useRenderTexture1 ? renderTexture1 : renderTexture2);
        computeShader.SetTexture(0, "nextBuffer", useRenderTexture1 ? renderTexture2 : renderTexture1);
        computeShader.Dispatch(0, width / 16, height / 16, 1);

        // Swap render textures for the next frame
        useRenderTexture1 = !useRenderTexture1;

        HandleInput();
    }

    void HandleInput()
    {
        // Check for mouse click
        if (Input.GetMouseButton(0))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit))
            {
                // Convert hit point to UV coordinates on the texture
                Vector2 uv = hit.textureCoord;
                int x = Mathf.RoundToInt(uv.x * width);
                int y = Mathf.RoundToInt(uv.y * height);

                // Update click buffer with click position
                int[] clickData = new int[] { x, y };
                clickBuffer.SetData(clickData);

                // Make the cell alive at the clicked point
                MakeCellAlive();
            }
        }
        else if (Input.GetMouseButton(1))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit))
            {
                // Convert hit point to UV coordinates on the texture
                Vector2 uv = hit.textureCoord;
                int x = Mathf.RoundToInt(uv.x * width);
                int y = Mathf.RoundToInt(uv.y * height);

                // Update click buffer with click position
                int[] clickData = new int[] { x, y };
                clickBuffer.SetData(clickData);

                // Make the cell alive at the clicked point
                MakeCellDead();
            }
        }

        // Handle pause
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!isPaused)
            {
                isPaused = true;
                TogglePause(1);
            }
            else if (isPaused)
            {
                isPaused = false;
                TogglePause(0);
            }
        }
    }

    void MakeCellAlive()
    {
        int kernelHandle = computeShader.FindKernel("CSMakeCellAlive");
        computeShader.SetTexture(kernelHandle, "currentBuffer", useRenderTexture1 ? renderTexture1 : renderTexture2);
        computeShader.SetTexture(kernelHandle, "nextBuffer", useRenderTexture1 ? renderTexture2 : renderTexture1);
        computeShader.SetBuffer(kernelHandle, "clickBuffer", clickBuffer); // Set click buffer
        computeShader.Dispatch(kernelHandle, 1, 1, 1);
    }

    void MakeCellDead()
    {
        int kernelHandle = computeShader.FindKernel("CSMakeCellDead");
        computeShader.SetTexture(kernelHandle, "currentBuffer", useRenderTexture1 ? renderTexture1 : renderTexture2);
        computeShader.SetTexture(kernelHandle, "nextBuffer", useRenderTexture1 ? renderTexture2 : renderTexture1);
        computeShader.SetBuffer(kernelHandle, "clickBuffer", clickBuffer); // Set click buffer
        computeShader.Dispatch(kernelHandle, 1, 1, 1);
    }

    public void TogglePause(int pauseState)
    {
        pauseBuffer.SetData(new int[] { pauseState });
        computeShader.SetBuffer(0, "pauseBuffer", pauseBuffer);
        computeShader.Dispatch(0, 1, 1, 1);
    }

    void OnDestroy()
    {
        renderTexture1.Release();
        renderTexture2.Release();
        clickBuffer.Release(); // Release click buffer
        pauseBuffer.Release();
    }
}

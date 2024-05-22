using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Unity.VisualScripting;
using System;

[Serializable]
public class Automaton
{
    public string name;
    public Color color;
    public int s;
    public int b;
}

[Serializable]
public struct AutomatonForBuffer
{
    public Vector4 color;
    public int[] s;
    public int[] b;
}

public class ConwayCompute : MonoBehaviour
{
    public Color demo;
    public ComputeShader computeShader;
    public int width = 300;
    public int height = 300;
    public RenderTexture renderTexture1;
    public RenderTexture renderTexture2;
    public MeshRenderer planeObjectRenderer;
    public int brushRadius;
    public TMP_InputField brushSizeInputField;
    public TMP_Dropdown cellTypeDropdown;
    public Automaton[] cellularAutomata;
    private AutomatonForBuffer[] automataForBuffer;
    private ComputeBuffer clickBuffer; // Buffer to store click position
    private ComputeBuffer colorBuffer;
    private ComputeBuffer automatonBuffer;
    private bool useRenderTexture1 = true;
    private bool isPaused = false;
    private bool notDrawing = true;

    void Start()
    {
        InitializeTextures();
        //RandomInitialize();

        // Create click buffer
        clickBuffer = new ComputeBuffer(1, sizeof(int) * 2);

        colorBuffer = new ComputeBuffer(1, sizeof(float) * 4);

        InitializeRules();

        computeShader.SetBool("pauseBool", isPaused);
        computeShader.SetBool("notDrawingBool", notDrawing);
        computeShader.SetInt("width", width);
        computeShader.SetInt("height", height);
    }

    void InitializeTextures()
    {
        renderTexture1 = new RenderTexture(width, height, 0);
        renderTexture1.enableRandomWrite = true;
        renderTexture1.filterMode = FilterMode.Point;
        renderTexture1.Create();

        planeObjectRenderer.material.mainTexture = renderTexture1;

        renderTexture2 = new RenderTexture(width, height, 0);
        renderTexture2.enableRandomWrite = true;
        renderTexture2.filterMode = FilterMode.Point;
        renderTexture2.Create();
    }

    void InitializeRules()
    {

        for (int i = 0; i < cellularAutomata.Length; i++)
        {
            char[] surviveCharArray = cellularAutomata[i].s.ToString().ToCharArray();
            char[] bornCharArray = cellularAutomata[i].b.ToString().ToCharArray();

            int[] surviveIntArray = new int[surviveCharArray.Length];
            int[] bornIntArray = new int[bornCharArray.Length];

            for (int b = 0; b < bornCharArray.Length; b++)
            {
                for (int s = 0; s < surviveCharArray.Length; s++)
                {
                    if (int.TryParse(bornCharArray[b].ToString(), out int B_result) && int.TryParse(surviveCharArray[s].ToString(), out int S_result))
                    {
                        bornIntArray[b] = B_result;
                        surviveIntArray[s] = S_result;
                    }
                }
            }

            automataForBuffer[i] = new AutomatonForBuffer()
            {
                color = new Vector4(cellularAutomata[i].color.r, cellularAutomata[i].color.g, cellularAutomata[i].color.b, cellularAutomata[i].color.a),
                s = surviveIntArray,
                b = bornIntArray
            };
        }

        automatonBuffer = new ComputeBuffer(1, 1);
        automatonBuffer.SetData(automataForBuffer);
        computeShader.SetBuffer(0, "automatonBuffer", automatonBuffer);
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

        if (brushSizeInputField.text == "")
        {
            brushRadius = 3;
        }
        else
        {
            brushRadius = Convert.ToInt32(brushSizeInputField.text);
        }

        HandleInput();
    }

    void HandleInput()
    {
        // Check for mouse click
        if (Input.GetMouseButton(0))
        {
            computeShader.SetBool("notDrawingBool", false);
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit))
            {
                // Convert hit point to UV coordinates on the texture
                Vector2 uv = hit.textureCoord;
                int x = Mathf.RoundToInt(uv.x * width - 0.5f);
                int y = Mathf.RoundToInt(uv.y * height - 0.5f);

                // Update click buffer with current position
                int[] clickData = new int[] { x, y };
                clickBuffer.SetData(clickData);

                SetCellColor(CellType());

                // Make the cell alive at the current position
                MakeCellAlive();
            }
        }
        else if (Input.GetMouseButton(1))
        {
            computeShader.SetBool("notDrawingBool", false);
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit))
            {
                // Convert hit point to UV coordinates on the texture
                Vector2 uv = hit.textureCoord;
                int x = Mathf.RoundToInt(uv.x * width - 0.5f);
                int y = Mathf.RoundToInt(uv.y * height - 0.5f);

                // Update click buffer with current position
                int[] clickData = new int[] { x, y };
                clickBuffer.SetData(clickData);

                SetCellColor(CellType());

                // Make the cell alive at the current position
                MakeCellDead();
            }
        }

        if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))
        {
            computeShader.SetBool("notDrawingBool", true);
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

        // Handle camera control
        transform.position += new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), 0) / 15;
        transform.position = new Vector3(Mathf.Clamp(transform.position.x, -5.75f, 5.75f), Mathf.Clamp(transform.position.y, -5.75f, 5.75f), -10);
        GetComponent<Camera>().orthographicSize -= Mouse.current.scroll.ReadValue().normalized.y / 2;
        GetComponent<Camera>().orthographicSize = Mathf.Clamp(GetComponent<Camera>().orthographicSize, 0.1f, 6);
    }

    Vector4 CellType()
    {
        Vector4 type;
        switch (cellTypeDropdown.value)
        {
            case 0:
                type = new Vector4(1, 1, 1, 1);
                break;
            case 1:
                type = new Vector4(1, 0, 0, 1);
                break;
            case 2:
                type = new Vector4(1, 1, 0, 1);
                break;
            case 3:
                type = new Vector4(0, 1, 1, 1);
                break;
            case 4:
                type = new Vector4(1, 0, 1, 1);
                break;
            case 5:
                type = new Vector4(0, 0, 1, 1);
                break;
            case 6:
                type = new Vector4(0, 1, 0, 1);
                break;
            case 7:
                type = new Vector4(0.4f, 0, 0, 1);
                break;
            case 8:
                type = new Vector4(0.6f, 1, 0.6f, 1);
                break;
            default:
                type = new Vector4(0, 0, 0, 1);
                break;
        }

        return type;
    }

    void MakeCellAlive()
    {
        int kernelHandle = computeShader.FindKernel("CSMakeCellAlive");
        computeShader.SetTexture(kernelHandle, "currentBuffer", useRenderTexture1 ? renderTexture1 : renderTexture2);
        computeShader.SetTexture(kernelHandle, "nextBuffer", useRenderTexture1 ? renderTexture2 : renderTexture1);
        computeShader.SetBuffer(kernelHandle, "clickBuffer", clickBuffer); // Set click buffer
        computeShader.SetBuffer(kernelHandle, "colorBuffer", colorBuffer); // Set color buffer
        computeShader.SetInt("brushRadius", brushRadius);
        computeShader.Dispatch(kernelHandle, 1, 1, 1);
    }

    void MakeCellDead()
    {
        int kernelHandle = computeShader.FindKernel("CSMakeCellDead");
        computeShader.SetTexture(kernelHandle, "currentBuffer", useRenderTexture1 ? renderTexture1 : renderTexture2);
        computeShader.SetTexture(kernelHandle, "nextBuffer", useRenderTexture1 ? renderTexture2 : renderTexture1);
        computeShader.SetBuffer(kernelHandle, "clickBuffer", clickBuffer); // Set click buffer
        computeShader.SetInt("brushRadius", brushRadius);
        computeShader.Dispatch(kernelHandle, 1, 1, 1);
    }

    void SetCellColor(Vector4 color)
    {
        // Create an array to hold the color data
        Vector4[] colorData = new Vector4[] { color };

        // Set the data of the color buffer
        colorBuffer.SetData(colorData);
    }

    public void TogglePause(int pauseState)
    {
        computeShader.SetBool("pauseBool", pauseState == 1);
        computeShader.Dispatch(0, 1, 1, 1);
    }

    void OnDestroy()
    {
        renderTexture1.Release();
        renderTexture2.Release();
        clickBuffer.Release(); // Release click buffer
        colorBuffer.Release();
    }
}

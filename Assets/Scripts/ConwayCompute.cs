using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;
using TMPro;
using Unity.VisualScripting;
using System;
using System.Collections.Generic;
using System.Collections;

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
    public TMP_InputField customRuleInputField;
    public TMP_Dropdown cellTypeDropdown;
    public TMP_Text currentRuleText;
    private ComputeBuffer clickBuffer; // Buffer to store click position
    private ComputeBuffer colorBuffer;
    private ComputeBuffer customSbuffer;
    private ComputeBuffer customBbuffer;
    private bool useRenderTexture1 = true;
    private bool isPaused = false;
    private bool notDrawing = true;

    void Start()
    {
        InitializeTextures();

        // Create click buffer
        clickBuffer = new ComputeBuffer(1, sizeof(int) * 2);

        colorBuffer = new ComputeBuffer(1, sizeof(float) * 4);

        computeShader.SetBool("pauseBool", isPaused);
        computeShader.SetBool("notDrawingBool", notDrawing);
        computeShader.SetInt("width", width);
        computeShader.SetInt("height", height);
        computeShader.SetInt("wrapWidth", width - 12);
        computeShader.SetInt("wrapHeight", height - 12);

        SetCustomRule();
        SetCellColor(CellType());
        computeShader.SetBuffer(0, "colorBuffer", colorBuffer);
        computeShader.SetBuffer(0, "clickBuffer", clickBuffer);
        InitCellType();
    }

    public void InitCellType()
    {
        SetCellColor(CellType());
    }

    public void CopyRule()
    {
        GUIUtility.systemCopyBuffer = currentRuleText.text;
    }

    public void SetCustomRule()
    {
        if (customRuleInputField.text != "" && customRuleInputField.text.Contains("/"))
        {
            string[] sb = customRuleInputField.text.Split("/");

            if (sb[0] == "")
            {
                sb[0] = "9";
            }

            if (sb[1] == "")
            {
                sb[1] = "9";
            }

            char[] surviveCharArray = sb[0].ToCharArray();
            char[] bornCharArray = sb[1].ToCharArray();

            int[] surviveIntArray = new int[surviveCharArray.Length];
            int[] bornIntArray = new int[bornCharArray.Length];

            for (int s = 0; s < surviveCharArray.Length; s++)
            {
                for (int b = 0; b < bornCharArray.Length; b++)
                {
                    if (int.TryParse(surviveCharArray[s].ToString(), out int S_result) && int.TryParse(bornCharArray[b].ToString(), out int B_result))
                    {
                        surviveIntArray[s] = S_result;
                        bornIntArray[b] = B_result;
                    }
                }
            }
    
            if (customSbuffer != null && customBbuffer != null)
            {
                customSbuffer.Release();
                customBbuffer.Release();
            }
    
            customSbuffer = new ComputeBuffer(surviveIntArray.Length, sizeof(int));
            customBbuffer = new ComputeBuffer(bornIntArray.Length, sizeof(int));
    
            customSbuffer.SetData(surviveIntArray);
            customBbuffer.SetData(bornIntArray);
    
            computeShader.SetBuffer(0, "customSbuffer", customSbuffer);
            computeShader.SetBuffer(0, "customBbuffer", customBbuffer);
        }
    }

    public void ClearBoard()
    {
        computeShader.SetBool("clearing", true);
        StartCoroutine(DisableClearingFlag());
    }
    public void ClearAutomata()
    {
        computeShader.SetBool("clearingAutomata", true);
        StartCoroutine(DisableClearingFlag());
    }

    private IEnumerator DisableClearingFlag()
    {
        // Wait for one frame
        yield return null;

        // Toggle off the clearing flag
        computeShader.SetBool("clearing", false);
        computeShader.SetBool("clearingAutomata", false);
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

    public float4 CellType()
    {
        float4 type;
        switch (cellTypeDropdown.value)
        {
            case 0:
                type = new float4(1, 1, 1, 1);
                currentRuleText.text = "23/3";
                break;
            case 1:
                type = new float4(1, 0, 0, 1);
                currentRuleText.text = "23/36";
                break;
            case 2:
                type = new float4(1, 0.8f, 0, 1);
                currentRuleText.text = "/2";
                break;
            case 3:
                type = new float4(0, 1, 1, 1);
                currentRuleText.text = "34578/3678";
                break;
            case 4:
                type = new float4(1, 0, 1, 1);
                currentRuleText.text = "1358/357";
                break;
            case 5:
                type = new float4(0, 0, 1, 1);
                currentRuleText.text = "4567/345";
                break;
            case 6:
                type = new float4(0, 1, 0, 1);
                currentRuleText.text = "245/368";
                break;
            case 7:
                type = new float4(0.4f, 0, 0, 1);
                currentRuleText.text = "5678/35678";
                break;
            case 8:
                type = new float4(0.6f, 1, 0.6f, 1);
                currentRuleText.text = "12345/3";
                break;
            case 9:
                type = new float4(1, 1, 0.2f, 1);
                currentRuleText.text = "125/36";
                break;
            case 10:
                type = new float4(1, 0.2f, 0, 1);
                currentRuleText.text = "238/357";
                break;
            case 11:
                type = new float4(0.2f, 0, 0.2f, 1);
                currentRuleText.text = "34/34";
                break;
            case 12:
                type = new float4(0.2f, 0.2f, 0.2f, 1);
                currentRuleText.text = "5/345";
                break;
            case 13:
                type = new float4(0.2f, 0, 1, 1);
                currentRuleText.text = "235678/3678";
                break;
            case 14:
                type = new float4(0.6f, 0.6f, 0, 1);
                currentRuleText.text = "235678/378";
                break;
            case 15:
                type = new float4(0.2f, 0.2f, 0.8f, 1);
                currentRuleText.text = "2345/45678";
                break;
            case 16:
                type = new float4(1, 0.6f, 0, 1);
                currentRuleText.text = "1/1";
                break;
            case 17:
                type = new float4(0, 0.4f, 0.4f, 1);
                currentRuleText.text = "1357/1357";
                break;
            case 18:
                type = new float4(0, 0.4f, 0, 1);
                currentRuleText.text = "05678/3458";
                break;
            case 19:
                type = new float4(1, 0.4f, 0, 1);
                currentRuleText.text = "custom";
                break;
            case 20:
                type = new float4(0, 0, 0.8f, 0.2f);
                currentRuleText.text = "3456/278/6";
                break;
            case 21:
                type = new float4(1, 1, 0, 1);
                currentRuleText.text = "/2/3";
                break;
            case 22:
                type = new float4(1, 1, 0.4f, 1);
                currentRuleText.text = "6/246/3";
                break;
            default:
                type = new float4(0, 0, 0, 1);
                currentRuleText.text = "ERROR/ERROR";
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

    void SetCellColor(float4 color)
    {
        // Create an array to hold the color data
        float4[] colorData = new float4[] { color };

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
        customSbuffer.Release();
        customBbuffer.Release();
    }
}

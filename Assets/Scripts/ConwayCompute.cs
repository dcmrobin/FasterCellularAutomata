using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;
using TMPro;
using Unity.VisualScripting;
using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;

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
    public Slider brushSizeSlider;
    public TMP_InputField customRuleInputField;
    public TMP_Dropdown cellTypeDropdown;
    public TMP_Dropdown predefinedShapeDropdown;
    public TMP_Text currentRuleText;
    public TMP_Text brushSizeText;
    private ComputeBuffer clickBuffer; // Buffer to store click position
    private ComputeBuffer colorBuffer;
    private ComputeBuffer customSbuffer;
    private ComputeBuffer customBbuffer;
    private bool useRenderTexture1 = true;
    private bool isPaused = false;
    private bool notDrawing = true;

    void Start()
    {
        brushSizeSlider.maxValue = width;

        InitializeTextures();

        // Create click buffer
        clickBuffer = new ComputeBuffer(1, sizeof(int) * 2);

        colorBuffer = new ComputeBuffer(1, sizeof(int) * 4);

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
        customRuleInputField.text = customRuleInputField.text.Replace("	", "");
        customRuleInputField.text = customRuleInputField.text.Replace(" ", "");
        if (customRuleInputField.text != "" && customRuleInputField.text.Contains("/"))
        {
            string[] sba = customRuleInputField.text.Split("/");// sba[0] is survive, sba[1] is born, sba[2] is the count of states

            if (sba[0] == "")
            {
                sba[0] = "9";
            }

            if (sba[1] == "")
            {
                sba[1] = "9";
            }

            if (sba.Length == 3 && sba[2] == "")
            {
                sba[2] = "2";
            }

            char[] surviveCharArray = sba[0].ToCharArray();
            char[] bornCharArray = sba[1].ToCharArray();

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
            if (sba.Length == 3)
            {
                computeShader.SetInt("customA", Convert.ToInt32(sba[2]));
            }
            else
            {
                computeShader.SetInt("customA", 2);
            }
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

        brushRadius = (int)brushSizeSlider.value;
        brushSizeText.text = brushSizeSlider.value.ToString();

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

    public int4 CellType()
    {
        int4 type;
        switch (cellTypeDropdown.value)
        {
            case 0:
                type = new int4(1000, 1000, 1000, 1000);
                currentRuleText.text = "23/3";
                break;
            case 1:
                type = new int4(1000, 0, 0, 1000);
                currentRuleText.text = "23/36";
                break;
            case 2:
                type = new int4(1000, 800, 0, 100);
                currentRuleText.text = "/2";
                break;
            case 3:
                type = new int4(0, 1000, 1000, 1000);
                currentRuleText.text = "34578/3678";
                break;
            case 4:
                type = new int4(1000, 0, 1000, 1000);
                currentRuleText.text = "1358/357";
                break;
            case 5:
                type = new int4(0, 0, 1000, 1000);
                currentRuleText.text = "4567/345";
                break;
            case 6:
                type = new int4(0, 1000, 0, 1000);
                currentRuleText.text = "245/368";
                break;
            case 7:
                type = new int4(400, 0, 0, 1000);
                currentRuleText.text = "5678/35678";
                break;
            case 8:
                type = new int4(600, 1000, 600, 1000);
                currentRuleText.text = "12345/3";
                break;
            case 9:
                type = new int4(1000, 1000, 200, 100);
                currentRuleText.text = "125/36";
                break;
            case 10:
                type = new int4(1000, 200, 0, 100);
                currentRuleText.text = "238/357";
                break;
            case 11:
                type = new int4(200, 0, 200, 1000);
                currentRuleText.text = "34/34";
                break;
            case 12:
                type = new int4(200, 200, 200, 1000);
                currentRuleText.text = "5/345";
                break;
            case 13:
                type = new int4(200, 0, 1000, 1000);
                currentRuleText.text = "235678/3678";
                break;
            case 14:
                type = new int4(600, 600, 0, 1000);
                currentRuleText.text = "235678/378";
                break;
            case 15:
                type = new int4(200, 200, 800, 1000);
                currentRuleText.text = "2345/45678";
                break;
            case 16:
                type = new int4(1000, 600, 0, 1000);
                currentRuleText.text = "1/1";
                break;
            case 17:
                type = new int4(0, 400, 400, 1000);
                currentRuleText.text = "1357/1357";
                break;
            case 18:
                type = new int4(0, 400, 0, 1000);
                currentRuleText.text = "05678/3458";
                break;
            case 19:
                type = new int4(0, 0, 800, 200);
                currentRuleText.text = "3456/278/6";
                break;
            case 20:
                type = new int4(800, 0, 1000, 1000);
                currentRuleText.text = "345/2/4";
                break;
            case 21:
                type = new int4(1000, 1000, 0, 1000);
                currentRuleText.text = "/2/3";
                break;
            case 22:
                type = new int4(1000, 1000, 400, 1000);
                currentRuleText.text = "6/246/3";
                break;
            case 23:
                type = new int4(1000, 200, 200, 1000);
                currentRuleText.text = "3458/37/4";
                break;
            case 24:
                type = new int4(0, 800, 0, 400);
                currentRuleText.text = "12/34/3";
                break;
            case 25:
                type = new int4(200, 800, 200, 400);
                currentRuleText.text = "124/3/3";
                break;
            case 26:
                type = new int4(1000, 400, 0, 1000);
                currentRuleText.text = customRuleInputField.text;
                break;
            default:
                type = new int4(0, 0, 0, 1000);
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

    void SetCellColor(int4 color)
    {
        // Create an array to hold the color data
        int4[] colorData = new int4[] { color };

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

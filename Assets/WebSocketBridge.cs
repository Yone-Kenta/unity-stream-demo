using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class WebSocketBridge : MonoBehaviour
{
    [Header("Output Settings")]
    [SerializeField] string outputDirectoryName = "GeneratedText";
    [SerializeField] string fileNamePrefix = "note_";
    [SerializeField] string fileExtension = ".txt";
    [SerializeField] bool includeTimestampInName = true;

    [Header("UI")]
    [SerializeField] Text displayText;
    [SerializeField] string idleMessage = "Left click to start a new note";

    readonly StringBuilder buffer = new StringBuilder();
    string currentFilePath;
    bool isEditing;

    void Awake()
    {
        EnsureDisplayTextReference();
        UpdateDisplay();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            StartNewFile();
        }

        if (!isEditing)
        {
            return;
        }

        ProcessKeyboardInput();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            FinishEditing();
        }
    }

    void StartNewFile()
    {
        if (!EnsureDisplayTextReference())
        {
            return;
        }

        if (isEditing)
        {
            SaveBufferToFile();
        }

        buffer.Clear();
        currentFilePath = GenerateFilePath();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(currentFilePath));
            File.WriteAllText(currentFilePath, string.Empty);
            isEditing = true;
            UpdateDisplay();
            Debug.Log($"[WebSocketBridge] Created new text file at: {currentFilePath}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WebSocketBridge] Failed to create text file. {ex.Message}");
            isEditing = false;
        }
    }

    void ProcessKeyboardInput()
    {
        bool textChanged = false;
        string input = Input.inputString;

        if (!string.IsNullOrEmpty(input))
        {
            foreach (char c in input)
            {
                switch (c)
                {
                    case '\b':
                        if (buffer.Length > 0)
                        {
                            buffer.Length -= 1;
                            textChanged = true;
                        }
                        break;
                    case '\r':
                    case '\n':
                        buffer.AppendLine();
                        textChanged = true;
                        break;
                    default:
                        if (!char.IsControl(c))
                        {
                            buffer.Append(c);
                            textChanged = true;
                        }
                        break;
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            buffer.Append('\t');
            textChanged = true;
        }

        if (textChanged)
        {
            UpdateDisplay();
            SaveBufferToFile();
        }
    }

    void FinishEditing()
    {
        if (!isEditing)
        {
            return;
        }

        SaveBufferToFile();
        isEditing = false;
        UpdateDisplay();
        Debug.Log($"[WebSocketBridge] Saved text file at: {currentFilePath}");
    }

    void SaveBufferToFile()
    {
        if (string.IsNullOrEmpty(currentFilePath))
        {
            return;
        }

        try
        {
            File.WriteAllText(currentFilePath, buffer.ToString());
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WebSocketBridge] Failed to write to file. {ex.Message}");
        }
    }

    string GenerateFilePath()
    {
        string directory = Path.Combine(Application.persistentDataPath, outputDirectoryName);
        string extension = fileExtension.StartsWith(".") ? fileExtension : "." + fileExtension;

        string fileName = includeTimestampInName
            ? $"{fileNamePrefix}{DateTime.Now:yyyyMMdd_HHmmssfff}{extension}"
            : $"{fileNamePrefix}{Guid.NewGuid():N}{extension}";

        return Path.Combine(directory, fileName);
    }

    bool EnsureDisplayTextReference()
    {
        if (displayText != null)
        {
            return true;
        }

        displayText = GetComponent<Text>();
        if (displayText == null)
        {
            Debug.LogWarning("[WebSocketBridge] Text component is not assigned.");
            return false;
        }

        return true;
    }

    void UpdateDisplay()
    {
        if (displayText == null)
        {
            return;
        }

        if (buffer.Length > 0)
        {
            displayText.text = buffer.ToString();
            return;
        }

        if (isEditing)
        {
            displayText.text = string.Empty;
            return;
        }

        displayText.text = idleMessage;
    }
}

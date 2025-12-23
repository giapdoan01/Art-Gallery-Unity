using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class WebGLFileUploader : MonoBehaviour
{
    private static WebGLFileUploader _instance;
    public static WebGLFileUploader Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("WebGLFileUploader");
                _instance = go.AddComponent<WebGLFileUploader>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [DllImport("__Internal")]
    private static extern void JS_FileUploader_OpenDialog(string objectName, string callbackName, string acceptTypes);

    private Action<FileResult> currentCallback;

    public void OpenFilePicker(string acceptTypes, Action<FileResult> callback)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        currentCallback = callback;
        
        try
        {
            JS_FileUploader_OpenDialog(gameObject.name, "OnFileUploaded", acceptTypes);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WebGLFileUploader] Error: {ex.Message}");
            callback?.Invoke(new FileResult { error = ex.Message });
        }
#else
        Debug.LogWarning("[WebGLFileUploader] Only works in WebGL build");
        callback?.Invoke(new FileResult { error = "Not running in WebGL" });
#endif
    }

    public void OnFileUploaded(string jsonData)
    {
        try
        {
            FileUploadData data = JsonUtility.FromJson<FileUploadData>(jsonData);

            if (!string.IsNullOrEmpty(data.error))
            {
                currentCallback?.Invoke(new FileResult { error = data.error });
                return;
            }

            byte[] fileBytes = null;
            
            if (!string.IsNullOrEmpty(data.base64Data))
            {
                string base64String = data.base64Data;
                
                if (base64String.Contains(","))
                {
                    base64String = base64String.Substring(base64String.IndexOf(",") + 1);
                }
                
                fileBytes = Convert.FromBase64String(base64String);
            }

            FileResult result = new FileResult
            {
                fileName = data.fileName,
                fileSize = data.fileSize,
                fileType = data.fileType,
                fileBytes = fileBytes,
                success = true
            };

            currentCallback?.Invoke(result);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WebGLFileUploader] Exception: {ex.Message}");
            currentCallback?.Invoke(new FileResult { error = ex.Message });
        }
    }

    [Serializable]
    private class FileUploadData
    {
        public string fileName;
        public int fileSize;
        public string fileType;
        public string base64Data;
        public string error;
    }

    public class FileResult
    {
        public string fileName;
        public int fileSize;
        public string fileType;
        public byte[] fileBytes;
        public bool success;
        public string error;
    }
}

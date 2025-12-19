using UnityEngine;

[System.Serializable]
public class ArtFrameData
{
    public int frameId;
    public string frameName;
    public Sprite sprite;
    public Material material;
    public string imageUrl;
    public float loadedTime;

    public ArtFrameData(int id, string name)
    {
        frameId = id;
        frameName = name;
        loadedTime = Time.time;
    }

    public bool IsLoaded()
    {
        return sprite != null && material != null;
    }
}
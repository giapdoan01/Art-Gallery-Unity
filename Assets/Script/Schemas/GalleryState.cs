using Colyseus.Schema;

public class Player : Schema
{
    [Type(0, "string")]
    public string sessionId = default(string);

    [Type(1, "string")]
    public string username = default(string);

    [Type(2, "string")]
    public string avatarURL = default(string);

    [Type(3, "number")]
    public float x = default(float);

    [Type(4, "number")]
    public float y = default(float);

    [Type(5, "number")]
    public float z = default(float);

    [Type(6, "number")]
    public float rotationY = default(float);

    [Type(7, "string")]
    public string animationState = default(string);

    [Type(8, "boolean")]
    public bool isMoving = default(bool);
}

public class GalleryState : Schema
{
    [Type(0, "map", typeof(MapSchema<Player>))]
    public MapSchema<Player> players = new MapSchema<Player>();

    [Type(1, "number")]
    public float serverTime = default(float);
}

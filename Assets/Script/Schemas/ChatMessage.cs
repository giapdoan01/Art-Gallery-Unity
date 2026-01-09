using Colyseus.Schema;

public partial class ChatMessage : Schema
{
    [Type(0, "string")]
    public string id = default(string);

    [Type(1, "string")]
    public string sessionId = default(string);

    [Type(2, "string")]
    public string username = default(string);

    [Type(3, "string")]
    public string message = default(string);

    [Type(4, "number")]
    public float timestamp = default(float);
}

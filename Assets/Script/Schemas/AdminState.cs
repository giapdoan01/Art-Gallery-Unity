using Colyseus.Schema;
using System;
using System.Collections.Generic;

[Serializable]
public class AdminPlayer : Player
{
    [Colyseus.Schema.Type(0, "boolean")]
    public bool isAdmin = true;

    [Colyseus.Schema.Type(1, "object")]
    public Dictionary<string, object> permissions = new Dictionary<string, object>();
}

[Serializable]
public class AdminState : Schema
{
    [Colyseus.Schema.Type(0, "map", typeof(MapSchema<AdminPlayer>))]
    public MapSchema<AdminPlayer> admins = new MapSchema<AdminPlayer>();

    [Colyseus.Schema.Type(1, "number")]
    public float serverTime = 0;

    [Colyseus.Schema.Type(2, "object")]
    public Dictionary<string, object> serverStats = new Dictionary<string, object>();
}
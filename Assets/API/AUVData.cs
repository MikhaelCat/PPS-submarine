using System;

[Serializable]
public class AUVCommand
{
    public int auv_id;
    public string command;
    public float value;
}

[Serializable]
public class AUVTelemetry
{
    public int auv_id;
    public double lat;
    public double lon;
    public float alt;
    public float depth;
    public float pitch;
    public float roll;
    public float yaw;
    public float velocity;
}
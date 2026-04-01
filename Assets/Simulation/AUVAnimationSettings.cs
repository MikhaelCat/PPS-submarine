using UnityEngine;

public class AUVAnimationSettings : MonoBehaviour
{
    // === Motor ids for animation ===
    [Header("Motor Ids")]
    public int TopFrontMotorId = 3;
    public int TopRearMotorId = 4;
    public int SideLeftMotorId = 1;
    public int SideRightMotorId = 2;

    [Header("Rotation Vectors")]
    public Vector3 TopFrontRotationVector = new Vector3(0, 1f, 0f);
    public Vector3 TopRearRotationVector = new Vector3(0, 1f, 0f);
    public Vector3 SideLeftRotationVector = new Vector3(1f, 0f, 0f);
    public Vector3 SideRightRotationVector = new Vector3(1f, 0f, 0f);
}

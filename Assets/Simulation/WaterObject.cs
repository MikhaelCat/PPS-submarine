using UnityEngine;

public class WaterObject :  MonoBehaviour
{
    Vector3 COM = new Vector3(0, 0, 0);
    float bouncy = 1;
    Vector3? COB = null;
    public void Init (Vector3 init_COM, float init_bouncy, Vector3? init_COB=null)
    {
        COM = init_COM;
        bouncy = init_bouncy;
        COB = init_COB;
    }
    public void Start()
    {
        
    }

}
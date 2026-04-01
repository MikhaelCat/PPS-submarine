using UnityEngine;

public class mycomponent1 : MonoBehaviour
{

    public GameObject ject;
    public mycomponent comp;
    void Start()
    {
        comp.ject = ject;

    }

    void Update()
    {
        comp.transform.position = new Vector3(10, comp.transform.position.y, comp.transform.position.z);
    }
}

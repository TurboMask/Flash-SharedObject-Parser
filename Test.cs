using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    void Start()
    {
        string so_path = "/data/user/0/" + Application.identifier + "/" + Application.identifier + "/Local Store/#SharedObjects/saved_data.sol";
        SharedObject so = SharedObjectParser.Parse(so_path);
        Debug.Log("Parameter int_param: " + so.Get("int_param").int_val);
    }
}

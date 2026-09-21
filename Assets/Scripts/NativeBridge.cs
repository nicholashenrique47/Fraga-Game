using UnityEngine;
using System.Runtime.InteropServices;

public class NativeBridge : MonoBehaviour
{
    [DllImport("FragaNative")]
    public static extern int InitFragaEngine();

    void Start()
    {
        int ano = InitFragaEngine();
        Debug.Log($"[SISTEMA HÍBRIDO] O motor C++ respondeu com sucesso. Ano atual: {ano}");
    }
}
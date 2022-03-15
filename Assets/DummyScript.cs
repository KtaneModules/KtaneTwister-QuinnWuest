using UnityEngine;

public class DummyScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMSelectable Button;
    private void Start() { Button.OnInteract += delegate() { Module.HandlePass(); return false; } ; }
}

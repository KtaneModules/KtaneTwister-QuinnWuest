using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DummyScript : MonoBehaviour
{

    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMSelectable Button;
    public GameObject Cylinder;
    public Material SolveMat;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        Button.OnInteract += Press;
    }

    private bool Press()
    {
        if (!_moduleSolved)
        {
            _moduleSolved = true;
            Module.HandlePass();
        }
        return false;
    }
}

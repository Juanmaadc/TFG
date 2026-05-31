using UnityEngine;

public class ForceResolution1920x1080 : MonoBehaviour
{
    [SerializeField] private int width = 1920;
    [SerializeField] private int height = 1080;
    [SerializeField] private FullScreenMode screenMode = FullScreenMode.Windowed;

    private void Awake()
    {
        Screen.SetResolution(width, height, screenMode);
    }
}

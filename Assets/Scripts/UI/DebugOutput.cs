using UnityEngine;
using TMPro;

public class DebugOutput : MonoBehaviour
{
    public static DebugOutput Instance { get; private set; }

    [Header("UI Text")]
    [SerializeField] TextMeshProUGUI text;
    string[] output = new string[100];

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Output(string str, int line)
    {
        if (line >= 0 && line < output.Length)
            output[line] = str;
    }

    void Update()
    {
        var outputString = "";
        foreach(var s in output)
        {
            outputString += s + "\n";
        }
        text.text = outputString;
    }
}

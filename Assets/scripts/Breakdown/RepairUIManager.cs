using UnityEngine;
using Mirror;

public class RepairUIManager : MonoBehaviour
{
    public static RepairUIManager Instance { get; private set; }

    [SerializeField] private GameObject minigamePanel;
    [SerializeField] private int requiredMatches = 3; // E.g., 3 white squares to match
    
    private int currentMatches = 0;
    private BreakdownManager targetCar;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        minigamePanel.SetActive(false);
    }

    public void OpenMinigame(BreakdownManager carToFix)
    {
        targetCar = carToFix;
        currentMatches = 0;
        
        
        minigamePanel.SetActive(true);
    }

    private void Update()
    {
        if (minigamePanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            CancelMinigame();
        }
    }

    public void ReportSquareMatched()
    {
        currentMatches++;
        if (currentMatches >= requiredMatches)
        {
            CompleteRepair();
        }
    }

    private void CompleteRepair()
    {
        minigamePanel.SetActive(false);

        
        if (NetworkClient.localPlayer.TryGetComponent(out PlayerEntity localPlayer))
        {
            localPlayer.CmdFixBreakdown(targetCar.netIdentity);
        }
    }

    public void CancelMinigame()
    {
        minigamePanel.SetActive(false);
        targetCar = null;
    }
}
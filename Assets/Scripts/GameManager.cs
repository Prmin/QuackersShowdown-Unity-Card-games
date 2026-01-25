using Mirror;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GameManager : NetworkBehaviour
{
    public static GameManager instance;

    [Header("Turn Settings")]
    public float turnDuration = 30f; // เวลาต่อเทิร์น 30 วิ

    [Header("Game State")]
    [SyncVar] public float currentTurnTime;
    [SyncVar] public int currentTurnIndex = -1;
    [SyncVar] public bool isGameActive = false;

    public List<PlayerManager> allPlayers = new List<PlayerManager>();

    private void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    // --- ฟังก์ชันที่ Error แจ้งว่าหาไม่เจอ ---
    public void RegisterPlayer(PlayerManager player)
    {
        if (!allPlayers.Contains(player))
        {
            allPlayers.Add(player);
            Debug.Log($"Player registered: {player.netId}");
        }
    }

    public void UnregisterPlayer(PlayerManager player)
    {
        allPlayers.Remove(player);
    }

    // --- เริ่มเกม ---
    [Server]
    public void StartGame()
    {
        if (allPlayers.Count < 2) return;
        isGameActive = true;
        currentTurnIndex = 0;
        StartTurnFor(currentTurnIndex);
    }

    // --- ระบบเทิร์น ---
    [Server]
    void StartTurnFor(int index)
    {
        currentTurnTime = turnDuration;

        for (int i = 0; i < allPlayers.Count; i++)
        {
            bool isMyTurn = (i == index);
            allPlayers[i].isMyTurn = isMyTurn;
            allPlayers[i].RpcOnTurnChanged(isMyTurn);
        }
        
        Debug.Log($"Server: Start Turn Player {index}");
    }

    [ServerCallback]
    void Update()
    {
        if (!isGameActive || allPlayers.Count < 2) return;

        if (currentTurnTime > 0)
        {
            currentTurnTime -= Time.deltaTime;

            if (currentTurnTime <= 0)
            {
                Debug.Log("Server: Timeout! Executing Penalty...");
                
                // ลงโทษผู้เล่นและข้ามเทิร์น
                ApplyPenalty(allPlayers[currentTurnIndex]);
                NextTurn();
            }
        }
    }

    // --- ฟังก์ชันข้ามเทิร์น ---
    [Server]
    public void NextTurn()
    {
        if (allPlayers.Count == 0) return;
        currentTurnIndex = (currentTurnIndex + 1) % allPlayers.Count;
        StartTurnFor(currentTurnIndex);
    }

    // --- ระบบลงโทษ (เรียกใช้ PlayerDeckManager และ DropZone) ---
    [Server]
    void ApplyPenalty(PlayerManager player)
    {
        // ต้องมี PlayerDeckManager แปะอยู่ที่ตัวผู้เล่นด้วยนะ
        PlayerDeckManager deckManager = player.GetComponent<PlayerDeckManager>();
        
        if (deckManager == null) return;

        // 1. เช็คในกอง (Deck)
        if (deckManager.duckDeck.Count > 0)
        {
            GameObject duckCard = deckManager.duckDeck[0]; 
            deckManager.duckDeck.RemoveAt(0);
            
            if (duckCard != null) NetworkServer.Destroy(duckCard);

            Debug.Log($"Penalty: Destroyed 1 Duck from Deck of {player.netId}");
            player.RpcNotifyPenalty("Timeout! Duck destroyed from Deck!");
        }
        // 2. เช็คในสนาม (DropZone)
        else
        {
            // ค้นหา DropZone ที่มี currentCard ไม่เป็น null
            DropZone zoneWithDuck = deckManager.myDuckZones.Find(z => z.currentCard != null);

            if (zoneWithDuck != null)
            {
                GameObject duckToKill = zoneWithDuck.currentCard;
                
                zoneWithDuck.currentCard = null; // เคลียร์สถานะในโซน
                NetworkServer.Destroy(duckToKill); // ทำลายการ์ด

                Debug.Log($"Penalty: Destroyed 1 Duck from Zone of {player.netId}");
                player.RpcNotifyPenalty("Timeout! Duck destroyed from Zone!");
            }
        }
    }
}
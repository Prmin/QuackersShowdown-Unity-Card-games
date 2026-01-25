using Mirror;
using UnityEngine;
using System.Collections.Generic;

public class PlayerDeckManager : NetworkBehaviour
{
    [Header("Deck & Hand Data")]
    public List<GameObject> duckDeck = new List<GameObject>(); 
    public List<DropZone> myDuckZones = new List<DropZone>();

    public List<GameObject> handActionCards = new List<GameObject>();

    public DropZone GetEmptyZone()
    {
        foreach(var zone in myDuckZones)
        {
            // เดี๋ยวเราต้องไปแก้ DropZone ให้ currentCard เป็น public ก่อนนะ
            if (zone.currentCard == null) return zone;
        }
        return null;
    }
}
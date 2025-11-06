using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ZoneKind : byte
{
    None = 0,
    DuckZone = 1,
    DropZone = 2,
    PlayerArea = 3,
    TargetZone = 4,
    // ถ้าจะใช้โซนอื่นเพิ่ม ค่อยเติมภายหลังได้
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardFlipper : MonoBehaviour
{ 
    public Sprite CardFront;
    public Sprite CardBack;

    public void Flip()
    {
        Image cardImage = gameObject.GetComponent<Image>(); 
        Sprite currentSprite = cardImage.sprite;

        if (currentSprite == CardFront)
        {
            cardImage.sprite = CardBack;
        }
        else
        {
            cardImage.sprite = CardFront;
        }
    }
}
